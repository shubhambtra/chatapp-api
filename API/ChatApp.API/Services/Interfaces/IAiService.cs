using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Services.Interfaces;

public interface IAiService
{
    Task<AnalyzeConversationResponse> AnalyzeConversationAsync(string conversationId);
    Task<AnalyzeMessageResponse> AnalyzeMessageAsync(AnalyzeMessageRequest request);
    Task<GenerateResponseResponse> GenerateResponseAsync(GenerateResponseRequest request);
    Task<SummarizeConversationResponse> SummarizeConversationAsync(string conversationId);
}
