using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Services.Implementations;

public class VisitorService : IVisitorService
{
    private readonly ApplicationDbContext _context;

    public VisitorService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VisitorDto> CreateVisitorAsync(CreateVisitorRequest request)
    {
        var visitor = new Visitor
        {
            SiteId = request.SiteId,
            ExternalId = request.ExternalId,
            Email = request.Email,
            Name = request.Name,
            Phone = request.Phone,
            UserAgent = request.UserAgent,
            IpAddress = request.IpAddress,
            ReferrerUrl = request.ReferrerUrl,
            LandingPage = request.LandingPage,
            CustomData = request.CustomData != null ? JsonSerializer.Serialize(request.CustomData) : "{}"
        };

        // Parse user agent for browser/os info
        if (!string.IsNullOrEmpty(request.UserAgent))
        {
            ParseUserAgent(visitor, request.UserAgent);
        }

        _context.Visitors.Add(visitor);
        await _context.SaveChangesAsync();

        return MapToDto(visitor);
    }

    public async Task<VisitorDto?> GetVisitorAsync(string visitorId)
    {
        var visitor = await _context.Visitors.FindAsync(visitorId);
        return visitor != null ? MapToDto(visitor) : null;
    }

    public async Task<VisitorDto?> GetVisitorByExternalIdAsync(string siteId, string externalId)
    {
        var visitor = await _context.Visitors
            .FirstOrDefaultAsync(v => v.SiteId == siteId && v.ExternalId == externalId);
        return visitor != null ? MapToDto(visitor) : null;
    }

    public async Task<VisitorDto> GetOrCreateVisitorAsync(string siteId, string externalVisitorId, string? name, string? email)
    {
        // Try to find existing visitor
        var visitor = await _context.Visitors
            .FirstOrDefaultAsync(v => v.SiteId == siteId && v.ExternalId == externalVisitorId);

        if (visitor != null)
        {
            // Update name if provided and visitor doesn't have one
            if (!string.IsNullOrEmpty(name) && string.IsNullOrEmpty(visitor.Name))
            {
                visitor.Name = name;
            }
            if (!string.IsNullOrEmpty(email) && string.IsNullOrEmpty(visitor.Email))
            {
                visitor.Email = email;
            }
            visitor.LastSeenAt = DateTime.UtcNow;
            visitor.IsOnline = true;
            await _context.SaveChangesAsync();
            return MapToDto(visitor);
        }

        // Create new visitor
        visitor = new Visitor
        {
            SiteId = siteId,
            ExternalId = externalVisitorId,
            Name = name,
            Email = email,
            IsOnline = true,
            LastSeenAt = DateTime.UtcNow
        };

        _context.Visitors.Add(visitor);
        await _context.SaveChangesAsync();

        return MapToDto(visitor);
    }

    public async Task<PagedResponse<VisitorDto>> GetVisitorsAsync(string siteId, VisitorListRequest request)
    {
        var query = _context.Visitors.Where(v => v.SiteId == siteId);

        if (!string.IsNullOrEmpty(request.Status))
        {
            query = request.Status == "online"
                ? query.Where(v => v.IsOnline)
                : query.Where(v => !v.IsOnline);
        }

        if (!string.IsNullOrEmpty(request.Search))
        {
            query = query.Where(v =>
                (v.Name != null && v.Name.Contains(request.Search)) ||
                (v.Email != null && v.Email.Contains(request.Search)));
        }

        if (request.From.HasValue)
        {
            query = query.Where(v => v.CreatedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(v => v.CreatedAt <= request.To.Value);
        }

        var totalItems = await query.CountAsync();
        var visitors = await query
            .OrderByDescending(v => v.LastSeenAt ?? v.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new PagedResponse<VisitorDto>(
            visitors.Select(MapToDto).ToList(),
            request.Page,
            request.PageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)request.PageSize)
        );
    }

    public async Task<ActiveVisitorsResponse> GetActiveVisitorsAsync(string siteId)
    {
        var activeVisitors = await _context.Visitors
            .Where(v => v.SiteId == siteId && v.IsOnline)
            .OrderByDescending(v => v.LastSeenAt)
            .Select(v => new
            {
                v.Id,
                v.Name,
                v.IsOnline,
                v.CurrentPage,
                LastMessage = v.Conversations
                    .SelectMany(c => c.Messages)
                    .OrderByDescending(m => m.CreatedAt)
                    .FirstOrDefault(),
                UnreadCount = v.Conversations
                    .SelectMany(c => c.Messages)
                    .Count(m => m.SenderType == "visitor")
            })
            .ToListAsync();

        var visitors = activeVisitors.Select(v => new ActiveVisitorDto(
            v.Id,
            v.Name,
            v.IsOnline ? "online" : "offline",
            v.CurrentPage,
            v.UnreadCount,
            v.LastMessage?.CreatedAt
        )).ToList();

        return new ActiveVisitorsResponse(visitors, visitors.Count);
    }

    public async Task<VisitorDto> UpdateVisitorAsync(string visitorId, UpdateVisitorRequest request)
    {
        var visitor = await _context.Visitors.FindAsync(visitorId);
        if (visitor == null) throw new KeyNotFoundException("Visitor not found");

        if (request.Email != null) visitor.Email = request.Email;
        if (request.Name != null) visitor.Name = request.Name;
        if (request.Phone != null) visitor.Phone = request.Phone;
        if (request.ExternalId != null) visitor.ExternalId = request.ExternalId;
        if (request.Tags != null) visitor.Tags = JsonSerializer.Serialize(request.Tags);
        if (request.CustomData != null) visitor.CustomData = JsonSerializer.Serialize(request.CustomData);

        await _context.SaveChangesAsync();

        return MapToDto(visitor);
    }

    public async Task BlockVisitorAsync(string visitorId)
    {
        var visitor = await _context.Visitors.FindAsync(visitorId);
        if (visitor == null) throw new KeyNotFoundException("Visitor not found");

        visitor.IsBlocked = true;
        await _context.SaveChangesAsync();
    }

    public async Task UnblockVisitorAsync(string visitorId)
    {
        var visitor = await _context.Visitors.FindAsync(visitorId);
        if (visitor == null) throw new KeyNotFoundException("Visitor not found");

        visitor.IsBlocked = false;
        await _context.SaveChangesAsync();
    }

    public async Task<VisitorSessionDto> StartSessionAsync(string visitorId, string? ipAddress, string? userAgent, string? currentPage, string? referrerUrl)
    {
        var visitor = await _context.Visitors.FindAsync(visitorId);
        if (visitor == null) throw new KeyNotFoundException("Visitor not found");

        var session = new VisitorSession
        {
            VisitorId = visitorId,
            SiteId = visitor.SiteId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CurrentPage = currentPage,
            ReferrerUrl = referrerUrl
        };

        visitor.IsOnline = true;
        visitor.LastSeenAt = DateTime.UtcNow;
        visitor.TotalVisits++;
        visitor.CurrentPage = currentPage;

        _context.VisitorSessions.Add(session);
        await _context.SaveChangesAsync();

        return MapToSessionDto(session);
    }

    public async Task UpdateSessionActivityAsync(string sessionId, string? currentPage)
    {
        var session = await _context.VisitorSessions
            .Include(s => s.Visitor)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null) throw new KeyNotFoundException("Session not found");

        session.LastActivityAt = DateTime.UtcNow;
        session.PageViews++;
        if (currentPage != null) session.CurrentPage = currentPage;

        session.Visitor.LastSeenAt = DateTime.UtcNow;
        session.Visitor.PageViews++;
        if (currentPage != null) session.Visitor.CurrentPage = currentPage;

        await _context.SaveChangesAsync();
    }

    public async Task EndSessionAsync(string sessionId)
    {
        var session = await _context.VisitorSessions
            .Include(s => s.Visitor)
            .FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null) throw new KeyNotFoundException("Session not found");

        session.IsActive = false;
        session.EndedAt = DateTime.UtcNow;

        // Check if visitor has any other active sessions
        var hasOtherSessions = await _context.VisitorSessions
            .AnyAsync(s => s.VisitorId == session.VisitorId && s.Id != sessionId && s.IsActive);

        if (!hasOtherSessions)
        {
            session.Visitor.IsOnline = false;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<VisitorSessionDto>> GetVisitorSessionsAsync(string visitorId)
    {
        var sessions = await _context.VisitorSessions
            .Where(s => s.VisitorId == visitorId)
            .OrderByDescending(s => s.StartedAt)
            .Take(50)
            .ToListAsync();

        return sessions.Select(MapToSessionDto).ToList();
    }

    private static void ParseUserAgent(Visitor visitor, string userAgent)
    {
        // Simple UA parsing - in production use a proper library
        if (userAgent.Contains("Chrome")) visitor.Browser = "Chrome";
        else if (userAgent.Contains("Firefox")) visitor.Browser = "Firefox";
        else if (userAgent.Contains("Safari")) visitor.Browser = "Safari";
        else if (userAgent.Contains("Edge")) visitor.Browser = "Edge";

        if (userAgent.Contains("Windows")) visitor.Os = "Windows";
        else if (userAgent.Contains("Mac OS")) visitor.Os = "macOS";
        else if (userAgent.Contains("Linux")) visitor.Os = "Linux";
        else if (userAgent.Contains("Android")) visitor.Os = "Android";
        else if (userAgent.Contains("iOS")) visitor.Os = "iOS";

        if (userAgent.Contains("Mobile")) visitor.DeviceType = "mobile";
        else if (userAgent.Contains("Tablet")) visitor.DeviceType = "tablet";
        else visitor.DeviceType = "desktop";
    }

    private static VisitorDto MapToDto(Visitor visitor)
    {
        List<string>? tags = null;
        Dictionary<string, object>? customData = null;

        try
        {
            if (!string.IsNullOrEmpty(visitor.Tags) && visitor.Tags != "[]")
                tags = JsonSerializer.Deserialize<List<string>>(visitor.Tags);
            if (!string.IsNullOrEmpty(visitor.CustomData) && visitor.CustomData != "{}")
                customData = JsonSerializer.Deserialize<Dictionary<string, object>>(visitor.CustomData);
        }
        catch { }

        return new VisitorDto(
            visitor.Id,
            visitor.SiteId,
            visitor.ExternalId,
            visitor.Email,
            visitor.Name,
            visitor.Phone,
            visitor.AvatarUrl,
            visitor.Browser,
            visitor.Os,
            visitor.DeviceType,
            visitor.Country,
            visitor.City,
            visitor.CurrentPage,
            visitor.PageViews,
            visitor.TotalVisits,
            tags,
            customData,
            visitor.LastSeenAt,
            visitor.IsOnline,
            visitor.IsBlocked,
            visitor.CreatedAt
        );
    }

    private static VisitorSessionDto MapToSessionDto(VisitorSession session) => new(
        session.Id,
        session.VisitorId,
        session.CurrentPage,
        session.ReferrerUrl,
        session.IsActive,
        session.StartedAt,
        session.LastActivityAt,
        session.EndedAt,
        session.PageViews
    );
}
