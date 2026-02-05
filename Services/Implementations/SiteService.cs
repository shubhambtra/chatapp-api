using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Services.Implementations;

public class SiteService : ISiteService
{
    private readonly ApplicationDbContext _context;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IEmailService _emailService;

    public SiteService(ApplicationDbContext context, ISubscriptionService subscriptionService, IEmailService emailService)
    {
        _context = context;
        _subscriptionService = subscriptionService;
        _emailService = emailService;
    }

    public async Task<SiteDto> CreateSiteAsync(string userId, CreateSiteRequest request)
    {
        var site = new Site
        {
            Name = request.Name,
            Domain = request.Domain,
            ApiKey = GenerateApiKey(),
            OwnerUserId = userId,
            CompanyName = request.CompanyName,
            CompanyWebsite = request.CompanyWebsite,
            CompanySize = request.CompanySize,
            Industry = request.Industry,
            Timezone = request.Timezone ?? "UTC",
            PaymentReference = request.PaymentReference
        };

        _context.Sites.Add(site);

        // Add owner as admin
        var userSite = new UserSite
        {
            UserId = userId,
            SiteId = site.Id,
            CanView = true,
            CanRespond = true,
            CanCloseConversations = true,
            CanManageSettings = true,
            AssignedBy = userId
        };

        _context.UserSites.Add(userSite);

        // Update user role to site_admin (site owners get admin role)
        var user = await _context.Users.FindAsync(userId);
        if (user != null && user.Role != "site_admin")
        {
            user.Role = "site_admin";
        }

        await _context.SaveChangesAsync();

        // Assign subscription plan to the new site
        if (!string.IsNullOrEmpty(request.PlanId))
        {
            // Paid plan selected during registration - assign it directly
            var plan = await _context.SubscriptionPlans.FindAsync(request.PlanId);
            if (plan != null)
            {
                var now = DateTime.UtcNow;
                var billingCycle = request.BillingCycle ?? "monthly";
                var periodEnd = billingCycle == "yearly" || billingCycle == "annual"
                    ? now.AddYears(1)
                    : now.AddMonths(1);

                var subscription = new Subscription
                {
                    SiteId = site.Id,
                    PlanId = request.PlanId,
                    Status = "active",
                    BillingCycle = billingCycle,
                    CurrentPeriodStart = now,
                    CurrentPeriodEnd = periodEnd
                };

                _context.Subscriptions.Add(subscription);
                _context.SubscriptionHistories.Add(new SubscriptionHistory
                {
                    SubscriptionId = subscription.Id,
                    Action = "created",
                    ToPlanId = request.PlanId,
                    Reason = $"Assigned {plan.Name} plan on site creation with payment"
                });

                await _context.SaveChangesAsync();
            }
            else
            {
                // Plan not found, fall back to Free plan
                await _subscriptionService.AssignFreePlanToSiteAsync(site.Id);
            }
        }
        else
        {
            // No plan specified - assign Free plan
            await _subscriptionService.AssignFreePlanToSiteAsync(site.Id);
        }

        return await MapToDto(site);
    }

    public async Task<SiteDto?> GetSiteAsync(string siteId)
    {
        var site = await _context.Sites
            .Include(s => s.Subscriptions.Where(sub => sub.Status == "active"))
            .ThenInclude(sub => sub.Plan)
            .FirstOrDefaultAsync(s => s.Id == siteId);

        return site != null ? await MapToDto(site) : null;
    }

    public async Task<PagedResponse<SiteDto>> GetUserSitesAsync(string userId, int page, int pageSize)
    {
        var query = _context.Sites
            .Where(s => s.UserSites.Any(us => us.UserId == userId));

        var totalItems = await query.CountAsync();
        var sites = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var siteDtos = new List<SiteDto>();
        foreach (var site in sites)
        {
            siteDtos.Add(await MapToDto(site));
        }

        return new PagedResponse<SiteDto>(
            siteDtos,
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)
        );
    }

    public async Task<PagedResponse<SiteDto>> GetAllSitesAsync(int page, int pageSize)
    {
        var query = _context.Sites
            .Include(s => s.OwnerUser)
            .AsQueryable();

        var totalItems = await query.CountAsync();
        var sites = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var siteDtos = new List<SiteDto>();
        foreach (var site in sites)
        {
            var dto = await MapToDto(site);
            // Add owner info for admin dashboard
            dto.OwnerId = site.OwnerUserId;
            if (site.OwnerUser != null)
            {
                dto.OwnerEmail = site.OwnerUser.Email;
                dto.OwnerUsername = site.OwnerUser.Username;
            }
            siteDtos.Add(dto);
        }

        return new PagedResponse<SiteDto>(
            siteDtos,
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)
        );
    }

    public async Task<SiteDto> UpdateSiteAsync(string siteId, UpdateSiteRequest request)
    {
        var site = await _context.Sites.FindAsync(siteId);
        if (site == null) throw new KeyNotFoundException("Site not found");

        if (request.Name != null) site.Name = request.Name;
        if (request.Domain != null) site.Domain = request.Domain;
        if (request.CompanyName != null) site.CompanyName = request.CompanyName;
        if (request.CompanyWebsite != null) site.CompanyWebsite = request.CompanyWebsite;
        if (request.CompanySize != null) site.CompanySize = request.CompanySize;
        if (request.Industry != null) site.Industry = request.Industry;
        if (request.Timezone != null) site.Timezone = request.Timezone;
        if (request.BusinessHours != null) site.BusinessHours = request.BusinessHours;
        if (request.AiEnabled.HasValue) site.AiEnabled = request.AiEnabled.Value;
        if (request.AiModel != null) site.AiModel = request.AiModel;
        if (request.MaxFileSizeMb.HasValue) site.MaxFileSizeMb = request.MaxFileSizeMb.Value;
        if (request.AllowedFileTypes != null) site.AllowedFileTypes = request.AllowedFileTypes;
        if (request.AutoReplyEnabled.HasValue) site.AutoReplyEnabled = request.AutoReplyEnabled.Value;
        if (request.AnalysisEnabled.HasValue) site.AnalysisEnabled = request.AnalysisEnabled.Value;

        await _context.SaveChangesAsync();

        return await MapToDto(site);
    }

    public async Task DeleteSiteAsync(string siteId)
    {
        var site = await _context.Sites.FindAsync(siteId);
        if (site == null) throw new KeyNotFoundException("Site not found");

        _context.Sites.Remove(site);
        await _context.SaveChangesAsync();
    }

    public async Task<string> RegenerateApiKeyAsync(string siteId)
    {
        var site = await _context.Sites.FindAsync(siteId);
        if (site == null) throw new KeyNotFoundException("Site not found");

        site.ApiKey = GenerateApiKey();
        await _context.SaveChangesAsync();

        return site.ApiKey;
    }

    public async Task<bool> ValidateApiKeyAsync(string siteId, string apiKey)
    {
        if (string.IsNullOrEmpty(siteId) || string.IsNullOrEmpty(apiKey))
            return false;

        var site = await _context.Sites.FindAsync(siteId);
        if (site == null) return false;

        return site.ApiKey == apiKey;
    }

    public async Task<WidgetConfigDto?> GetWidgetConfigAsync(string siteId)
    {
        var site = await _context.Sites.FindAsync(siteId);
        if (site == null) return null;

        return ParseWidgetConfig(site.WidgetConfig);
    }

    public async Task<WidgetConfigDto> UpdateWidgetConfigAsync(string siteId, UpdateWidgetConfigRequest request)
    {
        var site = await _context.Sites.FindAsync(siteId);
        if (site == null) throw new KeyNotFoundException("Site not found");

        var config = ParseWidgetConfig(site.WidgetConfig) ?? new WidgetConfigDto(null, null, null, null, null, null, null, null, null, null);

        var newConfig = new WidgetConfigDto(
            request.PrimaryColor ?? config.PrimaryColor,
            request.SecondaryColor ?? config.SecondaryColor,
            request.Position ?? config.Position,
            request.WelcomeMessage ?? config.WelcomeMessage,
            request.OfflineMessage ?? config.OfflineMessage,
            request.ShowAgentAvatar ?? config.ShowAgentAvatar,
            request.ShowAgentName ?? config.ShowAgentName,
            request.EnableEmoji ?? config.EnableEmoji,
            request.EnableFileUpload ?? config.EnableFileUpload,
            request.EnableSoundNotifications ?? config.EnableSoundNotifications
        );

        site.WidgetConfig = JsonSerializer.Serialize(newConfig);
        await _context.SaveChangesAsync();

        return newConfig;
    }

    public async Task<List<SiteAgentDto>> GetSiteAgentsAsync(string siteId)
    {
        // Get the site to find the owner
        var site = await _context.Sites.FindAsync(siteId);
        if (site == null) throw new KeyNotFoundException("Site not found");

        // Get all agents except the site owner
        var userSites = await _context.UserSites
            .Include(us => us.User)
            .Where(us => us.SiteId == siteId && us.UserId != site.OwnerUserId)
            .ToListAsync();

        return userSites.Select(us => new SiteAgentDto(
            us.UserId,
            us.User.Username,
            us.User.Email,
            string.IsNullOrEmpty(us.User.FirstName) ? us.User.Username : $"{us.User.FirstName} {us.User.LastName}".Trim(),
            us.User.FirstName,
            us.User.LastName,
            us.User.AvatarUrl,
            us.User.Role,
            us.User.Status,
            us.CanView,
            us.CanRespond,
            us.CanCloseConversations,
            us.CanManageSettings,
            us.AssignedAt
        )).ToList();
    }

    public async Task<SiteAgentDto> AddAgentToSiteAsync(string siteId, AddAgentToSiteRequest request)
    {
        // Check agent limit
        var (allowed, reason, limit, current) = await _subscriptionService.CheckLimitAsync(siteId, "agents");
        if (!allowed)
        {
            throw new InvalidOperationException(reason ?? $"Agent limit reached ({current}/{limit})");
        }

        // Get site info for email
        var site = await _context.Sites.FindAsync(siteId);
        if (site == null) throw new KeyNotFoundException("Site not found");

        // Find user by UserId or Email
        User? user = null;
        bool isNewUser = false;
        string? plainPassword = null;

        if (!string.IsNullOrEmpty(request.UserId))
        {
            user = await _context.Users.FindAsync(request.UserId);
        }
        else if (!string.IsNullOrEmpty(request.Email))
        {
            user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            // If user not found and password is provided, create new user
            if (user == null && !string.IsNullOrEmpty(request.Password))
            {
                // Generate username from email (part before @)
                var username = request.Email.Split('@')[0];

                // Check if username exists, append number if so
                var baseUsername = username;
                var counter = 1;
                while (await _context.Users.AnyAsync(u => u.Username == username))
                {
                    username = $"{baseUsername}{counter}";
                    counter++;
                }

                user = new User
                {
                    Email = request.Email,
                    Username = username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    Role = "support_agent",
                    Status = "offline"
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                isNewUser = true;
                plainPassword = request.Password;
            }
        }

        if (user == null)
        {
            throw new KeyNotFoundException("User not found. Please provide email and password to create a new agent account.");
        }

        var existing = await _context.UserSites
            .FirstOrDefaultAsync(us => us.SiteId == siteId && us.UserId == user.Id);
        if (existing != null) throw new InvalidOperationException("User already assigned to site");

        var userSite = new UserSite
        {
            UserId = user.Id,
            SiteId = siteId,
            CanView = request.CanView,
            CanRespond = request.CanRespond,
            CanCloseConversations = request.CanCloseConversations,
            CanManageSettings = request.CanManageSettings
        };

        _context.UserSites.Add(userSite);
        await _context.SaveChangesAsync();

        // Send credentials email to new user
        if (isNewUser && !string.IsNullOrEmpty(plainPassword))
        {
            try
            {
                await _emailService.SendAgentCredentialsEmailAsync(
                    user.Email,
                    user.Username,
                    plainPassword,
                    site.Name
                );
            }
            catch (Exception)
            {
                // Log but don't fail if email fails
            }
        }

        return new SiteAgentDto(
            user.Id,
            user.Username,
            user.Email,
            string.IsNullOrEmpty(user.FirstName) ? user.Username : $"{user.FirstName} {user.LastName}".Trim(),
            user.FirstName,
            user.LastName,
            user.AvatarUrl,
            user.Role,
            user.Status,
            userSite.CanView,
            userSite.CanRespond,
            userSite.CanCloseConversations,
            userSite.CanManageSettings,
            userSite.AssignedAt
        );
    }

    public async Task<SiteAgentDto> UpdateAgentPermissionsAsync(string siteId, string userId, UpdateAgentPermissionsRequest request)
    {
        var userSite = await _context.UserSites
            .Include(us => us.User)
            .FirstOrDefaultAsync(us => us.SiteId == siteId && us.UserId == userId);
        if (userSite == null) throw new KeyNotFoundException("User not assigned to site");

        if (request.CanView.HasValue) userSite.CanView = request.CanView.Value;
        if (request.CanRespond.HasValue) userSite.CanRespond = request.CanRespond.Value;
        if (request.CanCloseConversations.HasValue) userSite.CanCloseConversations = request.CanCloseConversations.Value;
        if (request.CanManageSettings.HasValue) userSite.CanManageSettings = request.CanManageSettings.Value;

        await _context.SaveChangesAsync();

        return new SiteAgentDto(
            userSite.User.Id,
            userSite.User.Username,
            userSite.User.Email,
            string.IsNullOrEmpty(userSite.User.FirstName) ? userSite.User.Username : $"{userSite.User.FirstName} {userSite.User.LastName}".Trim(),
            userSite.User.FirstName,
            userSite.User.LastName,
            userSite.User.AvatarUrl,
            userSite.User.Role,
            userSite.User.Status,
            userSite.CanView,
            userSite.CanRespond,
            userSite.CanCloseConversations,
            userSite.CanManageSettings,
            userSite.AssignedAt
        );
    }

    public async Task RemoveAgentFromSiteAsync(string siteId, string userId)
    {
        var userSite = await _context.UserSites
            .FirstOrDefaultAsync(us => us.SiteId == siteId && us.UserId == userId);
        if (userSite == null) throw new KeyNotFoundException("User not assigned to site");

        _context.UserSites.Remove(userSite);
        await _context.SaveChangesAsync();
    }

    public async Task<SiteBillingDto?> GetBillingInfoAsync(string siteId)
    {
        var site = await _context.Sites.FindAsync(siteId);
        if (site == null) return null;

        return new SiteBillingDto(
            site.BillingEmail,
            site.BillingName,
            site.BillingPhone,
            site.BillingAddressLine1,
            site.BillingAddressLine2,
            site.BillingCity,
            site.BillingState,
            site.BillingPostalCode,
            site.BillingCountry,
            site.TaxId,
            site.TaxExempt
        );
    }

    public async Task<SiteBillingDto> UpdateBillingInfoAsync(string siteId, UpdateBillingInfoRequest request)
    {
        var site = await _context.Sites.FindAsync(siteId);
        if (site == null) throw new KeyNotFoundException("Site not found");

        if (request.BillingEmail != null) site.BillingEmail = request.BillingEmail;
        if (request.BillingName != null) site.BillingName = request.BillingName;
        if (request.BillingPhone != null) site.BillingPhone = request.BillingPhone;
        if (request.BillingAddressLine1 != null) site.BillingAddressLine1 = request.BillingAddressLine1;
        if (request.BillingAddressLine2 != null) site.BillingAddressLine2 = request.BillingAddressLine2;
        if (request.BillingCity != null) site.BillingCity = request.BillingCity;
        if (request.BillingState != null) site.BillingState = request.BillingState;
        if (request.BillingPostalCode != null) site.BillingPostalCode = request.BillingPostalCode;
        if (request.BillingCountry != null) site.BillingCountry = request.BillingCountry;
        if (request.TaxId != null) site.TaxId = request.TaxId;
        if (request.TaxExempt.HasValue) site.TaxExempt = request.TaxExempt.Value;

        await _context.SaveChangesAsync();

        return new SiteBillingDto(
            site.BillingEmail,
            site.BillingName,
            site.BillingPhone,
            site.BillingAddressLine1,
            site.BillingAddressLine2,
            site.BillingCity,
            site.BillingState,
            site.BillingPostalCode,
            site.BillingCountry,
            site.TaxId,
            site.TaxExempt
        );
    }

    private static string GenerateApiKey()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return $"sk_{Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")[..40]}";
    }

    private static WidgetConfigDto? ParseWidgetConfig(string json)
    {
        if (string.IsNullOrEmpty(json) || json == "{}") return null;
        try
        {
            return JsonSerializer.Deserialize<WidgetConfigDto>(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task<SiteDto> MapToDto(Site site)
    {
        var subscription = await _context.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.SiteId == site.Id && (s.Status == "active" || s.Status == "trialing"));

        var owner = site.OwnerUserId != null
            ? await _context.Users.FindAsync(site.OwnerUserId)
            : null;

        var dto = new SiteDto(
            site.Id,
            site.Name,
            site.Domain,
            site.ApiKey,
            site.CompanyName,
            site.CompanyWebsite,
            site.Status,
            site.Timezone,
            site.AiEnabled,
            site.AiModel,
            site.AutoReplyEnabled,
            site.AnalysisEnabled,
            ParseWidgetConfig(site.WidgetConfig),
            subscription != null ? new SubscriptionDto(
                subscription.Id,
                subscription.SiteId,
                subscription.PlanId,
                subscription.Plan.Name,
                subscription.Status,
                subscription.BillingCycle,
                subscription.CurrentPeriodStart,
                subscription.CurrentPeriodEnd,
                subscription.TrialStart,
                subscription.TrialEnd,
                subscription.CanceledAt,
                subscription.CancelAt,
                subscription.CancelAtPeriodEnd,
                subscription.AutoPayEnabled,
                subscription.PreferredPaymentGateway,
                subscription.DefaultPaymentMethodId,
                subscription.CreatedAt
            ) : null,
            site.CreatedAt
        )
        {
            Plan = subscription?.Plan?.Name ?? "Free",
            OwnerId = site.OwnerUserId,
            OwnerEmail = owner?.Email,
            OwnerUsername = owner?.Username,
            IsActive = site.Status == "active"
        };

        return dto;
    }

    // Welcome Messages
    public async Task<List<WelcomeMessageDto>> GetWelcomeMessagesAsync(string siteId)
    {
        var messages = await _context.WelcomeMessages
            .Where(m => m.SiteId == siteId)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync();

        return messages.Select(m => new WelcomeMessageDto(
            m.Id,
            m.Message,
            m.DisplayOrder,
            m.IsActive,
            m.DelayMs,
            m.CreatedAt
        )).ToList();
    }

    public async Task<WelcomeMessageDto> CreateWelcomeMessageAsync(string siteId, CreateWelcomeMessageRequest request)
    {
        var site = await _context.Sites.FindAsync(siteId);
        if (site == null) throw new KeyNotFoundException("Site not found");

        // Get the highest display order
        var maxOrder = await _context.WelcomeMessages
            .Where(m => m.SiteId == siteId)
            .MaxAsync(m => (int?)m.DisplayOrder) ?? -1;

        var message = new WelcomeMessage
        {
            SiteId = siteId,
            Message = request.Message,
            DisplayOrder = request.DisplayOrder ?? (maxOrder + 1),
            IsActive = true
        };

        _context.WelcomeMessages.Add(message);
        await _context.SaveChangesAsync();

        return new WelcomeMessageDto(
            message.Id,
            message.Message,
            message.DisplayOrder,
            message.IsActive,
            message.DelayMs,
            message.CreatedAt
        );
    }

    public async Task<WelcomeMessageDto> UpdateWelcomeMessageAsync(string siteId, string messageId, UpdateWelcomeMessageRequest request)
    {
        var message = await _context.WelcomeMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.SiteId == siteId);
        if (message == null) throw new KeyNotFoundException("Welcome message not found");

        if (request.Message != null) message.Message = request.Message;
        if (request.DisplayOrder.HasValue) message.DisplayOrder = request.DisplayOrder.Value;
        if (request.IsActive.HasValue) message.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        return new WelcomeMessageDto(
            message.Id,
            message.Message,
            message.DisplayOrder,
            message.IsActive,
            message.DelayMs,
            message.CreatedAt
        );
    }

    public async Task DeleteWelcomeMessageAsync(string siteId, string messageId)
    {
        var message = await _context.WelcomeMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.SiteId == siteId);
        if (message == null) throw new KeyNotFoundException("Welcome message not found");

        _context.WelcomeMessages.Remove(message);
        await _context.SaveChangesAsync();
    }

    public async Task ReorderWelcomeMessagesAsync(string siteId, ReorderWelcomeMessagesRequest request)
    {
        var messages = await _context.WelcomeMessages
            .Where(m => m.SiteId == siteId && request.MessageIds.Contains(m.Id))
            .ToListAsync();

        for (int i = 0; i < request.MessageIds.Count; i++)
        {
            var message = messages.FirstOrDefault(m => m.Id == request.MessageIds[i]);
            if (message != null)
            {
                message.DisplayOrder = i;
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<SupervisorOverviewDto> GetSupervisorOverviewAsync(string siteId)
    {
        var site = await _context.Sites.FindAsync(siteId);
        if (site == null) throw new KeyNotFoundException("Site not found");

        // Get all agents for this site
        var userSites = await _context.UserSites
            .Where(us => us.SiteId == siteId)
            .Include(us => us.User)
            .ToListAsync();

        var agents = userSites.Select(us => new SupervisorAgentDto(
            us.UserId,
            us.User?.Username ?? "Unknown",
            us.User?.Email,
            "offline", // Status would come from WebSocket/real-time tracking
            0, // Active conversations count - simplified for now
            null
        )).ToList();

        // Get active conversations for this site
        var conversations = await _context.Conversations
            .Where(c => c.SiteId == siteId && c.Status != "closed")
            .Include(c => c.Visitor)
            .Include(c => c.AssignedUser)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Take(100)
            .Select(c => new SupervisorConversationDto(
                c.Id,
                c.VisitorId,
                c.Visitor != null ? c.Visitor.Name : null,
                c.AssignedUserId,
                c.AssignedUser != null ? c.AssignedUser.Username : null,
                c.Status,
                c.MessageCount,
                c.LastMessageAt,
                c.CreatedAt
            ))
            .ToListAsync();

        // Calculate stats
        var totalActiveConversations = conversations.Count;
        var unassignedConversations = conversations.Count(c => string.IsNullOrEmpty(c.AssignedAgentId));

        var stats = new SupervisorStatsDto(
            TotalAgents: agents.Count,
            OnlineAgents: 0, // Would come from real-time tracking
            TotalActiveConversations: totalActiveConversations,
            UnassignedConversations: unassignedConversations,
            AvgResponseTimeSeconds: 0 // Would need analytics calculation
        );

        return new SupervisorOverviewDto(agents, conversations, stats);
    }
}
