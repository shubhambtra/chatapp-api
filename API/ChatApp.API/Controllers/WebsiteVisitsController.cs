using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebsiteVisitsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebsiteVisitsController> _logger;

    public WebsiteVisitsController(
        ApplicationDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<WebsiteVisitsController> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Track a website visit (called from any page)
    /// </summary>
    [HttpPost("track")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<WebsiteVisitDto>>> TrackVisit([FromBody] TrackVisitRequest request)
    {
        var ipAddress = GetClientIpAddress();

        var visit = new WebsiteVisit
        {
            IpAddress = ipAddress ?? "unknown",
            UserAgent = request.UserAgent ?? Request.Headers["User-Agent"].FirstOrDefault(),
            PageUrl = request.PageUrl,
            ReferrerUrl = request.ReferrerUrl,
            VisitSource = request.VisitSource,
            SessionId = request.SessionId,
            VisitedAt = DateTime.UtcNow
        };

        // Parse user agent
        if (!string.IsNullOrEmpty(visit.UserAgent))
        {
            ParseUserAgent(visit);
        }

        // Get geolocation from IP (async, don't block the response)
        if (!string.IsNullOrEmpty(ipAddress) && ipAddress != "unknown" && !IsLocalIp(ipAddress))
        {
            try
            {
                await GetGeoLocationAsync(visit, ipAddress);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get geolocation for IP {IpAddress}", ipAddress);
            }
        }

        _context.WebsiteVisits.Add(visit);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<WebsiteVisitDto>.Ok(MapToDto(visit), "Visit tracked"));
    }

    /// <summary>
    /// Get website visit statistics (admin only)
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<WebsiteVisitStatsDto>>> GetStats(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        // Fetch all data for the period (limit to reasonable amount for performance)
        var visits = await _context.WebsiteVisits
            .Where(v => v.VisitedAt >= fromDate && v.VisitedAt <= toDate)
            .OrderByDescending(v => v.VisitedAt)
            .Take(10000)
            .ToListAsync();

        var totalVisits = visits.Count;
        var uniqueVisitors = visits.Select(v => v.IpAddress).Distinct().Count();

        // Visits by day (in memory)
        var visitsByDay = visits
            .GroupBy(v => v.VisitedAt.Date)
            .Select(g => new DailyVisitDto(g.Key, g.Count(), g.Select(v => v.IpAddress).Distinct().Count()))
            .OrderBy(d => d.Date)
            .ToList();

        // Visits by country (in memory)
        var visitsByCountry = visits
            .Where(v => v.Country != null)
            .GroupBy(v => new { v.Country, v.CountryCode })
            .Select(g => new CountryVisitDto(g.Key.Country!, g.Key.CountryCode, g.Count()))
            .OrderByDescending(c => c.Count)
            .Take(10)
            .ToList();

        // Visits by page (in memory)
        var visitsByPage = visits
            .Where(v => v.PageUrl != null)
            .GroupBy(v => v.PageUrl)
            .Select(g => new PageVisitDto(g.Key!, g.Count()))
            .OrderByDescending(p => p.Count)
            .Take(10)
            .ToList();

        // Visits by device type (in memory)
        var visitsByDevice = visits
            .Where(v => v.DeviceType != null)
            .GroupBy(v => v.DeviceType)
            .Select(g => new DeviceVisitDto(g.Key!, g.Count()))
            .OrderByDescending(d => d.Count)
            .ToList();

        // Visits by browser (in memory)
        var visitsByBrowser = visits
            .Where(v => v.Browser != null)
            .GroupBy(v => v.Browser)
            .Select(g => new BrowserVisitDto(g.Key!, g.Count()))
            .OrderByDescending(b => b.Count)
            .Take(5)
            .ToList();

        // Recent visitors (already sorted)
        var recentVisitors = visits
            .Take(20)
            .Select(v => MapToDto(v))
            .ToList();

        var stats = new WebsiteVisitStatsDto(
            totalVisits,
            uniqueVisitors,
            visitsByDay,
            visitsByCountry,
            visitsByPage,
            visitsByDevice,
            visitsByBrowser,
            recentVisitors
        );

        return Ok(ApiResponse<WebsiteVisitStatsDto>.Ok(stats));
    }

    /// <summary>
    /// Get unique visitor count for dashboard widget
    /// </summary>
    [HttpGet("unique-count")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<UniqueVisitorCountDto>>> GetUniqueVisitorCount(
        [FromQuery] string period = "today")
    {
        DateTime fromDate = period switch
        {
            "today" => DateTime.UtcNow.Date,
            "week" => DateTime.UtcNow.AddDays(-7),
            "month" => DateTime.UtcNow.AddDays(-30),
            "year" => DateTime.UtcNow.AddDays(-365),
            _ => DateTime.UtcNow.Date
        };

        var uniqueCount = await _context.WebsiteVisits
            .Where(v => v.VisitedAt >= fromDate)
            .Select(v => v.IpAddress)
            .Distinct()
            .CountAsync();

        var totalVisits = await _context.WebsiteVisits
            .Where(v => v.VisitedAt >= fromDate)
            .CountAsync();

        // Get previous period for comparison
        var previousFromDate = period switch
        {
            "today" => DateTime.UtcNow.Date.AddDays(-1),
            "week" => DateTime.UtcNow.AddDays(-14),
            "month" => DateTime.UtcNow.AddDays(-60),
            "year" => DateTime.UtcNow.AddDays(-730),
            _ => DateTime.UtcNow.Date.AddDays(-1)
        };

        var previousUniqueCount = await _context.WebsiteVisits
            .Where(v => v.VisitedAt >= previousFromDate && v.VisitedAt < fromDate)
            .Select(v => v.IpAddress)
            .Distinct()
            .CountAsync();

        var percentageChange = previousUniqueCount > 0
            ? Math.Round(((double)(uniqueCount - previousUniqueCount) / previousUniqueCount) * 100, 1)
            : 0;

        return Ok(ApiResponse<UniqueVisitorCountDto>.Ok(new UniqueVisitorCountDto(
            uniqueCount,
            totalVisits,
            percentageChange,
            period
        )));
    }

    /// <summary>
    /// Get all visits with pagination (admin only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<PagedResponse<WebsiteVisitDto>>>> GetVisits(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _context.WebsiteVisits.AsQueryable();

        if (from.HasValue)
            query = query.Where(v => v.VisitedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(v => v.VisitedAt <= to.Value);

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(v =>
                v.IpAddress.Contains(search) ||
                (v.Country != null && v.Country.Contains(search)) ||
                (v.City != null && v.City.Contains(search)) ||
                (v.PageUrl != null && v.PageUrl.Contains(search)));
        }

        var totalItems = await query.CountAsync();
        var visits = await query
            .OrderByDescending(v => v.VisitedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new PagedResponse<WebsiteVisitDto>(
            visits.Select(MapToDto).ToList(),
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)
        );

        return Ok(ApiResponse<PagedResponse<WebsiteVisitDto>>.Ok(result));
    }

    private string? GetClientIpAddress()
    {
        var forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static bool IsLocalIp(string ip)
    {
        return ip == "127.0.0.1" || ip == "::1" || ip.StartsWith("192.168.") || ip.StartsWith("10.") || ip.StartsWith("172.");
    }

    private async Task GetGeoLocationAsync(WebsiteVisit visit, string ipAddress)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        // Using ip-api.com (free, no API key required, 45 requests/minute)
        var response = await client.GetAsync($"http://ip-api.com/json/{ipAddress}?fields=status,country,countryCode,region,regionName,city,lat,lon,timezone,isp");

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var geoData = JsonSerializer.Deserialize<IpApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (geoData?.Status == "success")
            {
                visit.Country = geoData.Country;
                visit.CountryCode = geoData.CountryCode;
                visit.Region = geoData.RegionName;
                visit.City = geoData.City;
                visit.Latitude = geoData.Lat;
                visit.Longitude = geoData.Lon;
                visit.Timezone = geoData.Timezone;
                visit.Isp = geoData.Isp;
            }
        }
    }

    private static void ParseUserAgent(WebsiteVisit visit)
    {
        var ua = visit.UserAgent ?? "";

        // Browser detection
        if (ua.Contains("Edg/")) visit.Browser = "Edge";
        else if (ua.Contains("Chrome/")) visit.Browser = "Chrome";
        else if (ua.Contains("Firefox/")) visit.Browser = "Firefox";
        else if (ua.Contains("Safari/") && !ua.Contains("Chrome")) visit.Browser = "Safari";
        else if (ua.Contains("MSIE") || ua.Contains("Trident/")) visit.Browser = "IE";
        else visit.Browser = "Other";

        // OS detection
        if (ua.Contains("Windows")) visit.Os = "Windows";
        else if (ua.Contains("Mac OS")) visit.Os = "macOS";
        else if (ua.Contains("Linux") && !ua.Contains("Android")) visit.Os = "Linux";
        else if (ua.Contains("Android")) visit.Os = "Android";
        else if (ua.Contains("iPhone") || ua.Contains("iPad")) visit.Os = "iOS";
        else visit.Os = "Other";

        // Device type
        if (ua.Contains("Mobile") || ua.Contains("Android") || ua.Contains("iPhone"))
            visit.DeviceType = "Mobile";
        else if (ua.Contains("Tablet") || ua.Contains("iPad"))
            visit.DeviceType = "Tablet";
        else
            visit.DeviceType = "Desktop";
    }

    private static WebsiteVisitDto MapToDto(WebsiteVisit visit) => new(
        visit.Id,
        visit.IpAddress,
        visit.PageUrl,
        visit.ReferrerUrl,
        visit.Country,
        visit.CountryCode,
        visit.City,
        visit.Browser,
        visit.Os,
        visit.DeviceType,
        visit.VisitSource,
        visit.VisitedAt
    );
}

// Request/Response DTOs
public record TrackVisitRequest(
    string? PageUrl,
    string? ReferrerUrl,
    string? UserAgent,
    string? VisitSource,
    string? SessionId
);

public record WebsiteVisitDto(
    string Id,
    string IpAddress,
    string? PageUrl,
    string? ReferrerUrl,
    string? Country,
    string? CountryCode,
    string? City,
    string? Browser,
    string? Os,
    string? DeviceType,
    string? VisitSource,
    DateTime VisitedAt
);

public record WebsiteVisitStatsDto(
    int TotalVisits,
    int UniqueVisitors,
    List<DailyVisitDto> VisitsByDay,
    List<CountryVisitDto> VisitsByCountry,
    List<PageVisitDto> VisitsByPage,
    List<DeviceVisitDto> VisitsByDevice,
    List<BrowserVisitDto> VisitsByBrowser,
    List<WebsiteVisitDto> RecentVisitors
);

public record DailyVisitDto(DateTime Date, int TotalVisits, int UniqueVisitors);
public record CountryVisitDto(string Country, string? CountryCode, int Count);
public record PageVisitDto(string PageUrl, int Count);
public record DeviceVisitDto(string DeviceType, int Count);
public record BrowserVisitDto(string Browser, int Count);
public record UniqueVisitorCountDto(int UniqueVisitors, int TotalVisits, double PercentageChange, string Period);

// IP-API response model
internal class IpApiResponse
{
    public string? Status { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public string? Region { get; set; }
    public string? RegionName { get; set; }
    public string? City { get; set; }
    public double? Lat { get; set; }
    public double? Lon { get; set; }
    public string? Timezone { get; set; }
    public string? Isp { get; set; }
}
