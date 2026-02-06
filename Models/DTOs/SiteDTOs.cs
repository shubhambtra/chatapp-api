namespace ChatApp.API.Models.DTOs;

public record SiteDto(
    string Id,
    string Name,
    string Domain,
    string ApiKey,
    string? CompanyName,
    string? CompanyWebsite,
    string Status,
    string Timezone,
    bool AiEnabled,
    string AiModel,
    bool AutoReplyEnabled,
    bool AnalysisEnabled,
    WidgetConfigDto? WidgetConfig,
    string? OnboardingState,
    SubscriptionDto? CurrentSubscription,
    DateTime CreatedAt
)
{
    public string? OwnerEmail { get; set; }
    public string? OwnerUsername { get; set; }
    public string? OwnerId { get; set; }
    public string? Plan { get; set; }
    public bool IsActive { get; set; } = true;
}

public record CreateSiteRequest(
    string Name,
    string Domain,
    string? CompanyName,
    string? CompanyWebsite,
    string? CompanySize,
    string? Industry,
    string? Timezone,
    string? PaymentReference = null,
    string? PlanId = null,
    string? BillingCycle = null
);

public record UpdateSiteRequest(
    string? Name,
    string? Domain,
    string? CompanyName,
    string? CompanyWebsite,
    string? CompanySize,
    string? Industry,
    string? Timezone,
    string? BusinessHours,
    bool? AiEnabled,
    string? AiModel,
    int? MaxFileSizeMb,
    string? AllowedFileTypes,
    bool? AutoReplyEnabled,
    bool? AnalysisEnabled,
    string? OnboardingState
);

public record WidgetConfigDto(
    string? PrimaryColor,
    string? SecondaryColor,
    string? Position,
    string? WelcomeMessage,
    string? OfflineMessage,
    bool? ShowAgentAvatar,
    bool? ShowAgentName,
    bool? EnableEmoji,
    bool? EnableFileUpload,
    bool? EnableSoundNotifications
);

public record UpdateWidgetConfigRequest(
    string? PrimaryColor,
    string? SecondaryColor,
    string? Position,
    string? WelcomeMessage,
    string? OfflineMessage,
    bool? ShowAgentAvatar,
    bool? ShowAgentName,
    bool? EnableEmoji,
    bool? EnableFileUpload,
    bool? EnableSoundNotifications
);

public record SiteAgentDto(
    string UserId,
    string Username,
    string? Email,
    string? Name,
    string? FirstName,
    string? LastName,
    string? AvatarUrl,
    string Role,
    string Status,
    bool CanView,
    bool CanRespond,
    bool CanCloseConversations,
    bool CanManageSettings,
    DateTime AssignedAt
);

public record AddAgentToSiteRequest(
    string? UserId = null,
    string? Email = null,
    string? Password = null,
    bool CanView = true,
    bool CanRespond = true,
    bool CanCloseConversations = true,
    bool CanManageSettings = false
);

public record UpdateAgentPermissionsRequest(
    bool? CanView,
    bool? CanRespond,
    bool? CanCloseConversations,
    bool? CanManageSettings
);

public record SiteBillingDto(
    string? BillingEmail,
    string? BillingName,
    string? BillingPhone,
    string? BillingAddressLine1,
    string? BillingAddressLine2,
    string? BillingCity,
    string? BillingState,
    string? BillingPostalCode,
    string? BillingCountry,
    string? TaxId,
    bool TaxExempt
);

public record UpdateBillingInfoRequest(
    string? BillingEmail,
    string? BillingName,
    string? BillingPhone,
    string? BillingAddressLine1,
    string? BillingAddressLine2,
    string? BillingCity,
    string? BillingState,
    string? BillingPostalCode,
    string? BillingCountry,
    string? TaxId,
    bool? TaxExempt
);

// Welcome Messages
public record WelcomeMessageDto(
    string Id,
    string Message,
    int DisplayOrder,
    bool IsActive,
    int DelayMs,
    DateTime CreatedAt
);

public record CreateWelcomeMessageRequest(
    string Message,
    int? DisplayOrder = null
);

public record UpdateWelcomeMessageRequest(
    string? Message,
    int? DisplayOrder,
    bool? IsActive
);

public record ReorderWelcomeMessagesRequest(
    List<string> MessageIds
);

public record ValidateApiKeyRequest(
    string ApiKey
);

// Supervisor Overview
public record SupervisorOverviewDto(
    List<SupervisorAgentDto> Agents,
    List<SupervisorConversationDto> Conversations,
    SupervisorStatsDto Stats
);

public record SupervisorAgentDto(
    string UserId,
    string Username,
    string? Email,
    string Status,
    int ActiveConversations,
    DateTime? LastActivity
);

public record SupervisorConversationDto(
    string Id,
    string VisitorId,
    string? VisitorName,
    string? AssignedAgentId,
    string? AssignedAgentName,
    string Status,
    int MessageCount,
    DateTime? LastMessageAt,
    DateTime CreatedAt
);

public record SupervisorStatsDto(
    int TotalAgents,
    int OnlineAgents,
    int TotalActiveConversations,
    int UnassignedConversations,
    double AvgResponseTimeSeconds
);
