namespace ChatApp.API.Models.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    // Profile
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AvatarUrl { get; set; }

    // Role and status
    public string Role { get; set; } = "support_agent";
    public string Status { get; set; } = "offline";

    // Settings
    public string NotificationPreferences { get; set; } = "{}";

    // Tracking
    public DateTime? LastSeenAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int LoginCount { get; set; }

    // Account status
    public bool IsActive { get; set; } = true;
    public bool EmailVerified { get; set; }

    // Password reset
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    // Navigation properties
    public ICollection<UserSite> UserSites { get; set; } = new List<UserSite>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<AgentSession> AgentSessions { get; set; } = new List<AgentSession>();
    public ICollection<Conversation> AssignedConversations { get; set; } = new List<Conversation>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    // Computed
    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Initials => $"{FirstName?.FirstOrDefault()}{LastName?.FirstOrDefault()}".ToUpper();
}

public class UserSite : BaseEntityWithIntId
{
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;

    public string SiteId { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    // Permissions
    public bool CanView { get; set; } = true;
    public bool CanRespond { get; set; } = true;
    public bool CanCloseConversations { get; set; } = true;
    public bool CanManageSettings { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public string? AssignedBy { get; set; }
}

public class RefreshToken : BaseEntityWithIntId
{
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;

    public string Token { get; set; } = string.Empty;
    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }

    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public class AgentSession : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = null!;

    public string SiteId { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    public string Token { get; set; } = string.Empty;
    public string? WebsocketId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public DateTime? DisconnectedAt { get; set; }
}
