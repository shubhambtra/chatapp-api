using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Hubs;

public class ChatHub : Hub
{
    private readonly IMessageService _messageService;
    private readonly IVisitorService _visitorService;
    private readonly INotificationService _notificationService;

    public ChatHub(
        IMessageService messageService,
        IVisitorService visitorService,
        INotificationService notificationService)
    {
        _messageService = messageService;
        _visitorService = visitorService;
        _notificationService = notificationService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var siteId = Context.GetHttpContext()?.Request.Query["siteId"].ToString();
        var visitorId = Context.GetHttpContext()?.Request.Query["visitorId"].ToString();

        if (!string.IsNullOrEmpty(siteId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"site_{siteId}");
        }

        if (!string.IsNullOrEmpty(userId))
        {
            // Agent connected
            await Groups.AddToGroupAsync(Context.ConnectionId, $"agent_{userId}");
            await Clients.Group($"site_{siteId}").SendAsync("AgentOnline", userId);
        }
        else if (!string.IsNullOrEmpty(visitorId))
        {
            // Visitor connected
            await Groups.AddToGroupAsync(Context.ConnectionId, $"visitor_{visitorId}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var siteId = Context.GetHttpContext()?.Request.Query["siteId"].ToString();

        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(siteId))
        {
            await Clients.Group($"site_{siteId}").SendAsync("AgentOffline", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // Join a conversation room
    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
    }

    // Leave a conversation room
    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
    }

    // Send a message (for agents)
    [Authorize]
    public async Task SendMessage(string conversationId, string content, string messageType = "text", string? fileId = null)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return;

        var request = new SendMessageRequest(conversationId, content, messageType, fileId, null);
        var message = await _messageService.SendMessageAsync("agent", userId, request);

        await Clients.Group($"conversation_{conversationId}").SendAsync("NewMessage", message);
    }

    // Send a message (for visitors)
    public async Task SendVisitorMessage(string conversationId, string visitorId, string content, string messageType = "text", string? fileId = null)
    {
        var request = new SendMessageRequest(conversationId, content, messageType, fileId, null);
        var message = await _messageService.SendMessageAsync("visitor", visitorId, request);

        await Clients.Group($"conversation_{conversationId}").SendAsync("NewMessage", message);
    }

    // Typing indicator
    public async Task StartTyping(string conversationId, string senderType, string senderId)
    {
        await Clients.OthersInGroup($"conversation_{conversationId}")
            .SendAsync("UserTyping", new { conversationId, senderType, senderId });
    }

    public async Task StopTyping(string conversationId, string senderType, string senderId)
    {
        await Clients.OthersInGroup($"conversation_{conversationId}")
            .SendAsync("UserStoppedTyping", new { conversationId, senderType, senderId });
    }

    // Mark messages as read
    public async Task MarkAsRead(string conversationId, List<string> messageIds, string readerType, string readerId)
    {
        await _messageService.MarkMessagesReadAsync(readerType, readerId, new MarkMessagesReadRequest(messageIds));

        await Clients.OthersInGroup($"conversation_{conversationId}")
            .SendAsync("MessagesRead", new { conversationId, messageIds, readerType, readerId });
    }

    // Visitor page update
    public async Task UpdateVisitorPage(string visitorId, string currentPage)
    {
        var siteId = Context.GetHttpContext()?.Request.Query["siteId"].ToString();

        await Clients.Group($"site_{siteId}")
            .SendAsync("VisitorPageChanged", new { visitorId, currentPage });
    }

    // Agent status update
    [Authorize]
    public async Task UpdateAgentStatus(string status)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var siteId = Context.GetHttpContext()?.Request.Query["siteId"].ToString();

        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(siteId))
        {
            await Clients.Group($"site_{siteId}")
                .SendAsync("AgentStatusChanged", new { userId, status });
        }
    }
}

// Extension methods for sending hub notifications from services
public static class ChatHubExtensions
{
    public static async Task NotifyNewConversation(this IHubContext<ChatHub> hubContext, string siteId, ConversationDto conversation)
    {
        await hubContext.Clients.Group($"site_{siteId}")
            .SendAsync("NewConversation", conversation);
    }

    public static async Task NotifyConversationUpdated(this IHubContext<ChatHub> hubContext, string conversationId, ConversationDto conversation)
    {
        await hubContext.Clients.Group($"conversation_{conversationId}")
            .SendAsync("ConversationUpdated", conversation);
    }

    public static async Task NotifyConversationAssigned(this IHubContext<ChatHub> hubContext, string userId, ConversationDto conversation)
    {
        await hubContext.Clients.Group($"agent_{userId}")
            .SendAsync("ConversationAssigned", conversation);
    }

    public static async Task NotifyNewVisitor(this IHubContext<ChatHub> hubContext, string siteId, VisitorDto visitor)
    {
        await hubContext.Clients.Group($"site_{siteId}")
            .SendAsync("NewVisitor", visitor);
    }

    public static async Task NotifyVisitorOnline(this IHubContext<ChatHub> hubContext, string siteId, string visitorId)
    {
        await hubContext.Clients.Group($"site_{siteId}")
            .SendAsync("VisitorOnline", visitorId);
    }

    public static async Task NotifyVisitorOffline(this IHubContext<ChatHub> hubContext, string siteId, string visitorId)
    {
        await hubContext.Clients.Group($"site_{siteId}")
            .SendAsync("VisitorOffline", visitorId);
    }
}
