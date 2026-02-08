using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Services.Implementations;

public class MessageService : IMessageService
{
    private readonly ApplicationDbContext _context;
    private readonly ISubscriptionService _subscriptionService;

    public MessageService(ApplicationDbContext context, ISubscriptionService subscriptionService)
    {
        _context = context;
        _subscriptionService = subscriptionService;
    }

    public async Task<MessageDto> SendMessageAsync(string senderType, string? senderId, SendMessageRequest request)
    {
        var conversation = await _context.Conversations.FindAsync(request.ConversationId);
        if (conversation == null) throw new KeyNotFoundException("Conversation not found");

        // Check message limit
        var (allowed, reason, limit, current) = await _subscriptionService.CheckLimitAsync(conversation.SiteId, "messages");
        if (!allowed)
        {
            throw new InvalidOperationException(reason ?? $"Message limit reached ({current}/{limit})");
        }

        var message = new Message
        {
            ConversationId = request.ConversationId,
            SenderType = senderType,
            SenderId = senderId,
            Content = request.Content,
            MessageType = request.MessageType,
            FileId = request.FileId,
            Metadata = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null
        };

        _context.Messages.Add(message);

        // Update conversation stats
        conversation.MessageCount++;
        conversation.LastMessageAt = DateTime.UtcNow;

        // Track first response time for agents and auto-assign conversation
        // Handle both "agent" and "support" sender types
        if ((senderType == "agent" || senderType == "support") && !string.IsNullOrEmpty(senderId))
        {
            if (conversation.FirstResponseAt == null)
            {
                conversation.FirstResponseAt = DateTime.UtcNow;
            }

            // Auto-assign conversation to the agent if not already assigned
            if (string.IsNullOrEmpty(conversation.AssignedUserId))
            {
                conversation.AssignedUserId = senderId;
            }
        }

        await _context.SaveChangesAsync();

        // Record usage
        await _subscriptionService.RecordUsageAsync(conversation.SiteId, "messages", 1);

        return await GetMessageDtoAsync(message.Id);
    }

    public async Task<MessageDto?> GetMessageAsync(string messageId)
    {
        return await GetMessageDtoAsync(messageId);
    }

    public async Task<PagedResponse<MessageDto>> GetMessagesAsync(string conversationId, MessageListRequest request)
    {
        var query = _context.Messages
            .Include(m => m.File)
            .Include(m => m.ReadReceipts)
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted);

        if (!string.IsNullOrEmpty(request.Before))
        {
            var beforeMsg = await _context.Messages.FindAsync(request.Before);
            if (beforeMsg != null)
                query = query.Where(m => m.CreatedAt < beforeMsg.CreatedAt);
        }

        if (!string.IsNullOrEmpty(request.After))
        {
            var afterMsg = await _context.Messages.FindAsync(request.After);
            if (afterMsg != null)
                query = query.Where(m => m.CreatedAt > afterMsg.CreatedAt);
        }

        var totalItems = await query.CountAsync();
        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        // Reverse to get chronological order
        messages.Reverse();

        var dtos = new List<MessageDto>();
        foreach (var msg in messages)
        {
            dtos.Add(await MapToDtoAsync(msg));
        }

        return new PagedResponse<MessageDto>(
            dtos,
            request.Page,
            request.PageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)request.PageSize)
        );
    }

    public async Task<MessageDto> UpdateMessageAsync(string messageId, UpdateMessageRequest request)
    {
        var message = await _context.Messages.FindAsync(messageId);
        if (message == null) throw new KeyNotFoundException("Message not found");

        message.Content = request.Content;
        message.IsEdited = true;
        message.EditedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetMessageDtoAsync(messageId);
    }

    public async Task DeleteMessageAsync(string messageId)
    {
        var message = await _context.Messages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.Id == messageId);
        if (message == null) throw new KeyNotFoundException("Message not found");

        message.IsDeleted = true;
        message.DeletedAt = DateTime.UtcNow;

        // Update conversation message count
        message.Conversation.MessageCount--;

        await _context.SaveChangesAsync();
    }

    public async Task MarkMessagesReadAsync(string readerType, string readerId, MarkMessagesReadRequest request)
    {
        foreach (var messageId in request.MessageIds)
        {
            var exists = await _context.MessageReads
                .AnyAsync(mr => mr.MessageId == messageId && mr.ReaderId == readerId);

            if (!exists)
            {
                _context.MessageReads.Add(new MessageRead
                {
                    MessageId = messageId,
                    ReaderType = readerType,
                    ReaderId = readerId
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync(string conversationId, string readerType, string readerId)
    {
        var excludeSenderType = readerType == "agent" ? "agent" : "visitor";

        return await _context.Messages
            .Where(m => m.ConversationId == conversationId &&
                       !m.IsDeleted &&
                       m.SenderType != excludeSenderType &&
                       !m.ReadReceipts.Any(r => r.ReaderId == readerId))
            .CountAsync();
    }

    private async Task<MessageDto> GetMessageDtoAsync(string messageId)
    {
        var message = await _context.Messages
            .Include(m => m.File)
            .Include(m => m.ReadReceipts)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        if (message == null) throw new KeyNotFoundException("Message not found");

        return await MapToDtoAsync(message);
    }

    private async Task<MessageDto> MapToDtoAsync(Message message)
    {
        string? senderName = null;
        string? senderAvatar = null;

        if (message.SenderType == "agent" && !string.IsNullOrEmpty(message.SenderId))
        {
            var user = await _context.Users.FindAsync(message.SenderId);
            if (user != null)
            {
                senderName = user.FullName;
                senderAvatar = user.AvatarUrl;
            }
        }
        else if (message.SenderType == "visitor" && !string.IsNullOrEmpty(message.SenderId))
        {
            var visitor = await _context.Visitors.FindAsync(message.SenderId);
            if (visitor != null)
            {
                senderName = visitor.Name;
                senderAvatar = visitor.AvatarUrl;
            }
        }

        Dictionary<string, object>? metadata = null;
        try
        {
            if (!string.IsNullOrEmpty(message.Metadata))
                metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(message.Metadata);
        }
        catch { }

        // Determine read status from the opposite party's read receipts
        // For agent messages: look for visitor reads. For visitor messages: look for agent reads.
        var oppositeReaderType = message.SenderType == "visitor" ? "agent" : "visitor";
        DateTime? readAt = message.ReadReceipts?
            .Where(r => r.ReaderType == oppositeReaderType)
            .Select(r => (DateTime?)r.ReadAt)
            .Min();

        return new MessageDto(
            message.Id,
            message.ConversationId,
            message.SenderType,
            message.SenderId,
            senderName,
            senderAvatar,
            message.Content,
            message.MessageType,
            message.File != null ? new FileDto(
                message.File.Id,
                message.File.OriginalName,
                message.File.MimeType,
                message.File.FileSize,
                $"/api/files/{message.File.Id}",
                message.File.ThumbnailPath != null ? $"/api/files/{message.File.Id}/thumbnail" : null,
                message.File.Width,
                message.File.Height,
                message.File.CreatedAt
            ) : null,
            metadata,
            message.IsEdited,
            message.EditedAt,
            message.CreatedAt,
            message.DeliveredAt,
            readAt
        );
    }
}
