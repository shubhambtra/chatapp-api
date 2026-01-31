using System.ComponentModel.DataAnnotations.Schema;

namespace ChatApp.API.Models.Entities;

/// <summary>
/// Tracks all website visits (page views) for analytics
/// </summary>
public class WebsiteVisit : BaseEntity
{
    [Column("ip_address")]
    public string IpAddress { get; set; } = string.Empty;

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("page_url")]
    public string? PageUrl { get; set; }

    [Column("referrer_url")]
    public string? ReferrerUrl { get; set; }

    // Location (from IP geolocation)
    [Column("country")]
    public string? Country { get; set; }

    [Column("country_code")]
    public string? CountryCode { get; set; }

    [Column("region")]
    public string? Region { get; set; }

    [Column("city")]
    public string? City { get; set; }

    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    [Column("timezone")]
    public string? Timezone { get; set; }

    [Column("isp")]
    public string? Isp { get; set; }

    // Device info (parsed from user agent)
    [Column("browser")]
    public string? Browser { get; set; }

    [Column("browser_version")]
    public string? BrowserVersion { get; set; }

    [Column("os")]
    public string? Os { get; set; }

    [Column("device_type")]
    public string? DeviceType { get; set; }

    // Visit metadata
    [Column("visit_source")]
    public string? VisitSource { get; set; } // "landing", "login", "support", "widget", etc.

    [Column("session_id")]
    public string? SessionId { get; set; } // To group visits in same session

    [Column("visited_at")]
    public DateTime VisitedAt { get; set; } = DateTime.UtcNow;
}
