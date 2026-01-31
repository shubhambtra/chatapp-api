namespace ChatApp.API.Models.Entities;

public class Visitor : BaseEntity
{
    public string SiteId { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    // Identity
    public string? ExternalId { get; set; }
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }

    // Device/Browser info
    public string? UserAgent { get; set; }
    public string? Browser { get; set; }
    public string? BrowserVersion { get; set; }
    public string? Os { get; set; }
    public string? OsVersion { get; set; }
    public string? DeviceType { get; set; }

    // Location
    public string? IpAddress { get; set; }
    public string? Country { get; set; }
    public string? Region { get; set; }
    public string? City { get; set; }
    public string? Timezone { get; set; }

    // Tracking
    public string? ReferrerUrl { get; set; }
    public string? LandingPage { get; set; }
    public string? CurrentPage { get; set; }
    public int PageViews { get; set; }
    public int TotalVisits { get; set; } = 1;

    // Tags and custom data
    public string Tags { get; set; } = "[]";
    public string CustomData { get; set; } = "{}";

    // Status
    public DateTime? LastSeenAt { get; set; }
    public bool IsOnline { get; set; }
    public bool IsBlocked { get; set; }

    // Navigation properties
    public ICollection<VisitorSession> Sessions { get; set; } = new List<VisitorSession>();
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
}

public class VisitorSession : BaseEntity
{
    public string VisitorId { get; set; } = string.Empty;
    public Visitor Visitor { get; set; } = null!;

    public string SiteId { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    public string? WebsocketId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CurrentPage { get; set; }
    public string? ReferrerUrl { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public int PageViews { get; set; }
}
