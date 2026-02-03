using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Services.Implementations;

public class ConversationService : IConversationService
{
    private readonly ApplicationDbContext _context;
    private readonly IMessageService _messageService;
    private readonly ISubscriptionService _subscriptionService;

    public ConversationService(ApplicationDbContext context, IMessageService messageService, ISubscriptionService subscriptionService)
    {
        _context = context;
        _messageService = messageService;
        _subscriptionService = subscriptionService;
    }

    public async Task<ConversationDto> CreateConversationAsync(CreateConversationRequest request)
    {
        // Check conversation limit
        var (allowed, reason, limit, current) = await _subscriptionService.CheckLimitAsync(request.SiteId, "conversations");
        if (!allowed)
        {
            throw new InvalidOperationException(reason ?? $"Conversation limit reached ({current}/{limit})");
        }

        var conversation = new Conversation
        {
            SiteId = request.SiteId,
            VisitorId = request.VisitorId,
            Subject = request.Subject,
            Channel = request.Channel,
            Status = "active"
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        // Record usage
        await _subscriptionService.RecordUsageAsync(request.SiteId, "conversations", 1);

        // Send initial message if provided
        if (!string.IsNullOrEmpty(request.InitialMessage))
        {
            await _messageService.SendMessageAsync("visitor", request.VisitorId, new SendMessageRequest(
                conversation.Id,
                request.InitialMessage
            ));
        }

        return await GetConversationDtoAsync(conversation.Id);
    }

    public async Task<ConversationDto?> GetConversationAsync(string conversationId)
    {
        return await GetConversationDtoAsync(conversationId);
    }

    public async Task<PagedResponse<ConversationListDto>> GetConversationsAsync(string siteId, ConversationListRequest request)
    {
        var query = _context.Conversations
            .Include(c => c.Visitor)
            .Include(c => c.AssignedUser)
            .Include(c => c.Analysis)
            .Where(c => c.SiteId == siteId);

        if (!string.IsNullOrEmpty(request.Status))
        {
            var statuses = request.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (statuses.Count == 1)
                query = query.Where(c => c.Status == statuses[0]);
            else
                query = query.Where(c => statuses.Contains(c.Status));
        }

        if (!string.IsNullOrEmpty(request.Priority))
        {
            var priorities = request.Priority.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (priorities.Count == 1)
                query = query.Where(c => c.Priority == priorities[0]);
            else
                query = query.Where(c => priorities.Contains(c.Priority));
        }

        if (!string.IsNullOrEmpty(request.AssignedUserId))
            query = query.Where(c => c.AssignedUserId == request.AssignedUserId);

        if (!string.IsNullOrEmpty(request.VisitorId))
            query = query.Where(c => c.VisitorId == request.VisitorId);

        if (request.From.HasValue)
            query = query.Where(c => c.CreatedAt >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(c => c.CreatedAt <= request.To.Value);

        if (!string.IsNullOrEmpty(request.Search))
        {
            query = query.Where(c =>
                (c.Subject != null && c.Subject.Contains(request.Search)) ||
                (c.Visitor.Name != null && c.Visitor.Name.Contains(request.Search)) ||
                (c.Visitor.Email != null && c.Visitor.Email.Contains(request.Search)));
        }

        // Advanced filters
        if (!string.IsNullOrEmpty(request.Tags))
        {
            var tagList = request.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var tag in tagList)
                query = query.Where(c => c.Tags.Contains(tag));
        }

        if (request.RatingMin.HasValue)
            query = query.Where(c => c.Rating >= request.RatingMin.Value);

        if (request.RatingMax.HasValue)
            query = query.Where(c => c.Rating <= request.RatingMax.Value);

        if (!string.IsNullOrEmpty(request.ResolutionStatus))
        {
            var resStatuses = request.ResolutionStatus.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (resStatuses.Count == 1)
                query = query.Where(c => c.ResolutionStatus == resStatuses[0]);
            else
                query = query.Where(c => resStatuses.Contains(c.ResolutionStatus));
        }

        // AI filters
        if (!string.IsNullOrEmpty(request.Sentiment))
        {
            var sentiments = request.Sentiment.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (sentiments.Count == 1)
                query = query.Where(c => c.Analysis != null && c.Analysis.Sentiment == sentiments[0]);
            else
                query = query.Where(c => c.Analysis != null && sentiments.Contains(c.Analysis.Sentiment));
        }

        if (!string.IsNullOrEmpty(request.Intent))
            query = query.Where(c => c.Analysis != null && c.Analysis.Intent == request.Intent);

        if (request.UrgencyScoreMin.HasValue)
            query = query.Where(c => c.Analysis != null && c.Analysis.UrgencyScore >= request.UrgencyScoreMin.Value);

        if (request.UrgencyScoreMax.HasValue)
            query = query.Where(c => c.Analysis != null && c.Analysis.UrgencyScore <= request.UrgencyScoreMax.Value);

        var totalItems = await query.CountAsync();
        var conversations = await query
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var dtos = new List<ConversationListDto>();
        foreach (var conv in conversations)
        {
            var lastMessage = await _context.Messages
                .Where(m => m.ConversationId == conv.Id && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.Content)
                .FirstOrDefaultAsync();

            List<string>? convTags = null;
            try
            {
                if (!string.IsNullOrEmpty(conv.Tags) && conv.Tags != "[]")
                    convTags = JsonSerializer.Deserialize<List<string>>(conv.Tags);
            }
            catch { }

            dtos.Add(new ConversationListDto
            {
                Id = conv.Id,
                VisitorId = conv.Visitor.ExternalId ?? conv.VisitorId,
                VisitorName = conv.Visitor.Name,
                VisitorEmail = conv.Visitor.Email,
                AssignedUserId = conv.AssignedUserId,
                AssignedUserName = conv.AssignedUser?.FullName,
                Status = conv.Status,
                Priority = conv.Priority,
                Subject = conv.Subject,
                LastMessagePreview = lastMessage != null && lastMessage.Length > 100 ? lastMessage[..100] + "..." : lastMessage,
                MessageCount = conv.MessageCount,
                UnreadCount = 0,
                LastMessageAt = conv.LastMessageAt,
                CreatedAt = conv.CreatedAt,
                SiteId = conv.SiteId,
                // Advanced fields
                Tags = convTags,
                Rating = conv.Rating,
                ResolutionStatus = conv.ResolutionStatus,
                // AI fields
                Sentiment = conv.Analysis?.Sentiment,
                SentimentScore = conv.Analysis?.SentimentScore,
                Intent = conv.Analysis?.Intent,
                UrgencyScore = conv.Analysis?.UrgencyScore
            });
        }

        return new PagedResponse<ConversationListDto>(
            dtos,
            request.Page,
            request.PageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)request.PageSize)
        );
    }

    public async Task<ConversationDto> UpdateConversationAsync(string conversationId, UpdateConversationRequest request)
    {
        var conversation = await _context.Conversations.FindAsync(conversationId);
        if (conversation == null) throw new KeyNotFoundException("Conversation not found");

        if (request.Status != null) conversation.Status = request.Status;
        if (request.Priority != null) conversation.Priority = request.Priority;
        if (request.Subject != null) conversation.Subject = request.Subject;
        if (request.Tags != null) conversation.Tags = JsonSerializer.Serialize(request.Tags);
        if (request.AssignedUserId != null) conversation.AssignedUserId = request.AssignedUserId;

        await _context.SaveChangesAsync();

        return await GetConversationDtoAsync(conversationId);
    }

    public async Task<ConversationDto> AssignConversationAsync(string conversationId, AssignConversationRequest request)
    {
        var conversation = await _context.Conversations.FindAsync(conversationId);
        if (conversation == null) throw new KeyNotFoundException("Conversation not found");

        conversation.AssignedUserId = request.UserId;
        await _context.SaveChangesAsync();

        return await GetConversationDtoAsync(conversationId);
    }

    public async Task<ConversationDto> CloseConversationAsync(string conversationId, CloseConversationRequest request)
    {
        var conversation = await _context.Conversations.FindAsync(conversationId);
        if (conversation == null) throw new KeyNotFoundException("Conversation not found");

        conversation.Status = "closed";
        conversation.ClosedAt = DateTime.UtcNow;
        conversation.Rating = request.Rating;
        conversation.Feedback = request.Feedback;
        conversation.ResolutionStatus = request.ResolutionStatus ?? "resolved";
        conversation.ClosingNote = request.Note;

        if (conversation.ResolvedAt == null)
        {
            conversation.ResolvedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return await GetConversationDtoAsync(conversationId);
    }

    public async Task<ConversationDto> SubmitCsatAsync(string conversationId, SubmitCsatRequest request)
    {
        var conversation = await _context.Conversations.FindAsync(conversationId);
        if (conversation == null) throw new KeyNotFoundException("Conversation not found");

        conversation.Rating = request.Rating;
        conversation.Feedback = request.Feedback;

        await _context.SaveChangesAsync();

        return await GetConversationDtoAsync(conversationId);
    }

    public async Task<ConversationDto> ReopenConversationAsync(string conversationId)
    {
        var conversation = await _context.Conversations.FindAsync(conversationId);
        if (conversation == null) throw new KeyNotFoundException("Conversation not found");

        conversation.Status = "active";
        conversation.ClosedAt = null;
        await _context.SaveChangesAsync();

        return await GetConversationDtoAsync(conversationId);
    }

    public async Task DeleteConversationAsync(string conversationId)
    {
        var conversation = await _context.Conversations.FindAsync(conversationId);
        if (conversation == null) throw new KeyNotFoundException("Conversation not found");

        _context.Conversations.Remove(conversation);
        await _context.SaveChangesAsync();
    }

    public async Task<List<ConversationListDto>> GetAgentConversationsAsync(string userId, string? siteId)
    {
        var query = _context.Conversations
            .Include(c => c.Visitor)
            .Where(c => c.AssignedUserId == userId && c.Status == "active");

        if (!string.IsNullOrEmpty(siteId))
            query = query.Where(c => c.SiteId == siteId);

        var conversations = await query
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ToListAsync();

        var dtos = new List<ConversationListDto>();
        foreach (var conv in conversations)
        {
            var lastMessage = await _context.Messages
                .Where(m => m.ConversationId == conv.Id && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.Content)
                .FirstOrDefaultAsync();

            dtos.Add(new ConversationListDto
            {
                Id = conv.Id,
                VisitorId = conv.Visitor.ExternalId ?? conv.VisitorId,
                VisitorName = conv.Visitor.Name,
                VisitorEmail = conv.Visitor.Email,
                AssignedUserId = conv.AssignedUserId,
                AssignedUserName = null,
                Status = conv.Status,
                Priority = conv.Priority,
                Subject = conv.Subject,
                LastMessagePreview = lastMessage != null && lastMessage.Length > 100 ? lastMessage[..100] + "..." : lastMessage,
                MessageCount = conv.MessageCount,
                UnreadCount = 0,
                LastMessageAt = conv.LastMessageAt,
                CreatedAt = conv.CreatedAt,
                SiteId = conv.SiteId
            });
        }

        return dtos;
    }

    public async Task<List<ConversationListDto>> GetVisitorConversationsAsync(string visitorId)
    {
        var conversations = await _context.Conversations
            .Include(c => c.Visitor)
            .Include(c => c.AssignedUser)
            .Where(c => c.VisitorId == visitorId)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ToListAsync();

        var dtos = new List<ConversationListDto>();
        foreach (var conv in conversations)
        {
            var lastMessage = await _context.Messages
                .Where(m => m.ConversationId == conv.Id && !m.IsDeleted)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.Content)
                .FirstOrDefaultAsync();

            dtos.Add(new ConversationListDto
            {
                Id = conv.Id,
                VisitorId = conv.Visitor.ExternalId ?? conv.VisitorId,
                VisitorName = conv.Visitor.Name,
                VisitorEmail = conv.Visitor.Email,
                AssignedUserId = conv.AssignedUserId,
                AssignedUserName = conv.AssignedUser?.FullName,
                Status = conv.Status,
                Priority = conv.Priority,
                Subject = conv.Subject,
                LastMessagePreview = lastMessage != null && lastMessage.Length > 100 ? lastMessage[..100] + "..." : lastMessage,
                MessageCount = conv.MessageCount,
                UnreadCount = 0,
                LastMessageAt = conv.LastMessageAt,
                CreatedAt = conv.CreatedAt,
                SiteId = conv.SiteId
            });
        }

        return dtos;
    }

    public async Task<ConversationDto> GetOrCreateConversationAsync(string siteId, string visitorId)
    {
        // Try to find an existing active conversation
        var existingConversation = await _context.Conversations
            .Where(c => c.SiteId == siteId && c.VisitorId == visitorId && c.Status == "active")
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (existingConversation != null)
        {
            return await GetConversationDtoAsync(existingConversation.Id);
        }

        // Check conversation limit before creating new one
        var (allowed, reason, limit, current) = await _subscriptionService.CheckLimitAsync(siteId, "conversations");
        if (!allowed)
        {
            throw new InvalidOperationException(reason ?? $"Conversation limit reached ({current}/{limit})");
        }

        // Create a new conversation
        var conversation = new Conversation
        {
            SiteId = siteId,
            VisitorId = visitorId,
            Status = "active",
            Channel = "widget"
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        // Record usage
        await _subscriptionService.RecordUsageAsync(siteId, "conversations", 1);

        return await GetConversationDtoAsync(conversation.Id);
    }

    private async Task<ConversationDto> GetConversationDtoAsync(string conversationId)
    {
        var conversation = await _context.Conversations
            .Include(c => c.Visitor)
            .Include(c => c.AssignedUser)
            .Include(c => c.Analysis)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null) throw new KeyNotFoundException("Conversation not found");

        List<string>? tags = null;
        try
        {
            if (!string.IsNullOrEmpty(conversation.Tags) && conversation.Tags != "[]")
                tags = JsonSerializer.Deserialize<List<string>>(conversation.Tags);
        }
        catch { }

        return new ConversationDto(
            conversation.Id,
            conversation.SiteId,
            conversation.VisitorId,
            MapVisitorToDto(conversation.Visitor),
            conversation.AssignedUserId,
            conversation.AssignedUser != null ? MapUserToDto(conversation.AssignedUser) : null,
            conversation.Status,
            conversation.Priority,
            conversation.Channel,
            conversation.Subject,
            tags,
            conversation.MessageCount,
            conversation.FirstResponseAt,
            conversation.LastMessageAt,
            conversation.ResolvedAt,
            conversation.ClosedAt,
            conversation.Rating,
            conversation.Feedback,
            conversation.ResolutionStatus,
            conversation.ClosingNote,
            conversation.Analysis != null ? MapAnalysisToDto(conversation.Analysis) : null,
            conversation.CreatedAt
        );
    }

    private static VisitorDto MapVisitorToDto(Visitor visitor) => new(
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
        null, null,
        visitor.LastSeenAt,
        visitor.IsOnline,
        visitor.IsBlocked,
        visitor.CreatedAt
    );

    private static UserDto MapUserToDto(User user) => new(
        user.Id,
        user.Username,
        user.Email,
        user.FirstName,
        user.LastName,
        user.AvatarUrl,
        user.Role,
        user.Status,
        user.IsActive,
        user.EmailVerified,
        user.LastSeenAt,
        user.CreatedAt
    );

    private static ConversationAnalysisDto MapAnalysisToDto(ConversationAnalysis analysis)
    {
        List<string>? topics = null;
        List<string>? suggestedResponses = null;
        List<string>? keyPhrases = null;

        try
        {
            if (!string.IsNullOrEmpty(analysis.Topics))
                topics = JsonSerializer.Deserialize<List<string>>(analysis.Topics);
            if (!string.IsNullOrEmpty(analysis.SuggestedResponses))
                suggestedResponses = JsonSerializer.Deserialize<List<string>>(analysis.SuggestedResponses);
            if (!string.IsNullOrEmpty(analysis.KeyPhrases))
                keyPhrases = JsonSerializer.Deserialize<List<string>>(analysis.KeyPhrases);
        }
        catch { }

        return new ConversationAnalysisDto(
            analysis.Summary,
            analysis.Sentiment,
            analysis.SentimentScore,
            topics,
            analysis.Intent,
            analysis.Language,
            analysis.UrgencyScore,
            suggestedResponses,
            keyPhrases,
            analysis.AnalyzedAt
        );
    }
}
