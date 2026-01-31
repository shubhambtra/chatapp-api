using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Services.Interfaces;

public interface IConversationService
{
    Task<ConversationDto> CreateConversationAsync(CreateConversationRequest request);
    Task<ConversationDto?> GetConversationAsync(string conversationId);
    Task<PagedResponse<ConversationListDto>> GetConversationsAsync(string siteId, ConversationListRequest request);
    Task<ConversationDto> UpdateConversationAsync(string conversationId, UpdateConversationRequest request);
    Task<ConversationDto> AssignConversationAsync(string conversationId, AssignConversationRequest request);
    Task<ConversationDto> CloseConversationAsync(string conversationId, CloseConversationRequest request);
    Task<ConversationDto> SubmitCsatAsync(string conversationId, SubmitCsatRequest request);
    Task<ConversationDto> ReopenConversationAsync(string conversationId);
    Task DeleteConversationAsync(string conversationId);

    // Get active conversations for an agent
    Task<List<ConversationListDto>> GetAgentConversationsAsync(string userId, string? siteId);

    // Get conversations for a visitor
    Task<List<ConversationListDto>> GetVisitorConversationsAsync(string visitorId);

    // Get or create an active conversation for a visitor
    Task<ConversationDto> GetOrCreateConversationAsync(string siteId, string visitorId);
}
