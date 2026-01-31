using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Services.Interfaces;

public interface IMessageService
{
    Task<MessageDto> SendMessageAsync(string senderType, string? senderId, SendMessageRequest request);
    Task<MessageDto?> GetMessageAsync(string messageId);
    Task<PagedResponse<MessageDto>> GetMessagesAsync(string conversationId, MessageListRequest request);
    Task<MessageDto> UpdateMessageAsync(string messageId, UpdateMessageRequest request);
    Task DeleteMessageAsync(string messageId);
    Task MarkMessagesReadAsync(string readerType, string readerId, MarkMessagesReadRequest request);
    Task<int> GetUnreadCountAsync(string conversationId, string readerType, string readerId);
}
