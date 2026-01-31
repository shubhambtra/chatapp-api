using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Services.Interfaces;

public interface ISiteService
{
    Task<SiteDto> CreateSiteAsync(string userId, CreateSiteRequest request);
    Task<SiteDto?> GetSiteAsync(string siteId);
    Task<PagedResponse<SiteDto>> GetUserSitesAsync(string userId, int page, int pageSize);
    Task<PagedResponse<SiteDto>> GetAllSitesAsync(int page, int pageSize);
    Task<SiteDto> UpdateSiteAsync(string siteId, UpdateSiteRequest request);
    Task DeleteSiteAsync(string siteId);
    Task<string> RegenerateApiKeyAsync(string siteId);
    Task<bool> ValidateApiKeyAsync(string siteId, string apiKey);

    // Widget config
    Task<WidgetConfigDto?> GetWidgetConfigAsync(string siteId);
    Task<WidgetConfigDto> UpdateWidgetConfigAsync(string siteId, UpdateWidgetConfigRequest request);

    // Agents
    Task<List<SiteAgentDto>> GetSiteAgentsAsync(string siteId);
    Task<SiteAgentDto> AddAgentToSiteAsync(string siteId, AddAgentToSiteRequest request);
    Task<SiteAgentDto> UpdateAgentPermissionsAsync(string siteId, string userId, UpdateAgentPermissionsRequest request);
    Task RemoveAgentFromSiteAsync(string siteId, string userId);

    // Billing
    Task<SiteBillingDto?> GetBillingInfoAsync(string siteId);
    Task<SiteBillingDto> UpdateBillingInfoAsync(string siteId, UpdateBillingInfoRequest request);

    // Welcome Messages
    Task<List<WelcomeMessageDto>> GetWelcomeMessagesAsync(string siteId);
    Task<WelcomeMessageDto> CreateWelcomeMessageAsync(string siteId, CreateWelcomeMessageRequest request);
    Task<WelcomeMessageDto> UpdateWelcomeMessageAsync(string siteId, string messageId, UpdateWelcomeMessageRequest request);
    Task DeleteWelcomeMessageAsync(string siteId, string messageId);
    Task ReorderWelcomeMessagesAsync(string siteId, ReorderWelcomeMessagesRequest request);

    // Supervisor
    Task<SupervisorOverviewDto> GetSupervisorOverviewAsync(string siteId);
}
