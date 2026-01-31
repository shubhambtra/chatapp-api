using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Services.Interfaces;

public interface IVisitorService
{
    Task<VisitorDto> CreateVisitorAsync(CreateVisitorRequest request);
    Task<VisitorDto?> GetVisitorAsync(string visitorId);
    Task<VisitorDto?> GetVisitorByExternalIdAsync(string siteId, string externalId);
    Task<VisitorDto> GetOrCreateVisitorAsync(string siteId, string externalVisitorId, string? name, string? email);
    Task<PagedResponse<VisitorDto>> GetVisitorsAsync(string siteId, VisitorListRequest request);
    Task<ActiveVisitorsResponse> GetActiveVisitorsAsync(string siteId);
    Task<VisitorDto> UpdateVisitorAsync(string visitorId, UpdateVisitorRequest request);
    Task BlockVisitorAsync(string visitorId);
    Task UnblockVisitorAsync(string visitorId);

    // Sessions
    Task<VisitorSessionDto> StartSessionAsync(string visitorId, string? ipAddress, string? userAgent, string? currentPage, string? referrerUrl);
    Task UpdateSessionActivityAsync(string sessionId, string? currentPage);
    Task EndSessionAsync(string sessionId);
    Task<List<VisitorSessionDto>> GetVisitorSessionsAsync(string visitorId);
}
