using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Services.Interfaces;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SitesController : ControllerBase
{
    private readonly ISiteService _siteService;
    private readonly IEmailService _emailService;
    private readonly ApplicationDbContext _context;

    public SitesController(ISiteService siteService, IEmailService emailService, ApplicationDbContext context)
    {
        _siteService = siteService;
        _emailService = emailService;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResponse<SiteDto>>>> GetSites(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse<PagedResponse<SiteDto>>.Fail("User not found"));
        }

        var result = await _siteService.GetUserSitesAsync(userId, page, pageSize);
        return Ok(ApiResponse<PagedResponse<SiteDto>>.Ok(result));
    }

    [HttpGet("all")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<ApiResponse<PagedResponse<SiteDto>>>> GetAllSites(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _siteService.GetAllSitesAsync(page, pageSize);
        return Ok(ApiResponse<PagedResponse<SiteDto>>.Ok(result));
    }

    [HttpGet("{siteId}")]
    public async Task<ActionResult<ApiResponse<SiteDto>>> GetSite(string siteId)
    {
        var site = await _siteService.GetSiteAsync(siteId);
        if (site == null)
        {
            return NotFound(ApiResponse<SiteDto>.Fail("Site not found"));
        }

        return Ok(ApiResponse<SiteDto>.Ok(site));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SiteDto>>> CreateSite([FromBody] CreateSiteRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse<SiteDto>.Fail("User not found"));
        }

        var site = await _siteService.CreateSiteAsync(userId, request);
        return Ok(ApiResponse<SiteDto>.Ok(site, "Site created successfully"));
    }

    [HttpPut("{siteId}")]
    public async Task<ActionResult<ApiResponse<SiteDto>>> UpdateSite(string siteId, [FromBody] UpdateSiteRequest request)
    {
        try
        {
            var site = await _siteService.UpdateSiteAsync(siteId, request);
            return Ok(ApiResponse<SiteDto>.Ok(site));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<SiteDto>.Fail(ex.Message));
        }
    }

    [HttpDelete("{siteId}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteSite(string siteId)
    {
        try
        {
            await _siteService.DeleteSiteAsync(siteId);
            return Ok(ApiResponse<object>.Ok(null, "Site deactivated successfully"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("{siteId}/regenerate-api-key")]
    public async Task<ActionResult<ApiResponse<object>>> RegenerateApiKey(string siteId)
    {
        try
        {
            var newKey = await _siteService.RegenerateApiKeyAsync(siteId);
            return Ok(ApiResponse<object>.Ok(new { apiKey = newKey }, "API key regenerated"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("{siteId}/validate-api-key")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> ValidateApiKey(string siteId, [FromBody] ValidateApiKeyRequest request)
    {
        var isValid = await _siteService.ValidateApiKeyAsync(siteId, request.ApiKey);
        return Ok(ApiResponse<object>.Ok(new { valid = isValid }, isValid ? "API key is valid" : "Invalid API key"));
    }

    // Widget Config
    [HttpGet("{siteId}/widget-config")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<WidgetConfigDto>>> GetWidgetConfig(string siteId)
    {
        var config = await _siteService.GetWidgetConfigAsync(siteId);
        return Ok(ApiResponse<WidgetConfigDto>.Ok(config));
    }

    [HttpPut("{siteId}/widget-config")]
    public async Task<ActionResult<ApiResponse<WidgetConfigDto>>> UpdateWidgetConfig(
        string siteId,
        [FromBody] UpdateWidgetConfigRequest request)
    {
        try
        {
            var config = await _siteService.UpdateWidgetConfigAsync(siteId, request);
            return Ok(ApiResponse<WidgetConfigDto>.Ok(config));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<WidgetConfigDto>.Fail(ex.Message));
        }
    }

    // Agents
    [HttpGet("{siteId}/agents")]
    public async Task<ActionResult<ApiResponse<List<SiteAgentDto>>>> GetAgents(string siteId)
    {
        var agents = await _siteService.GetSiteAgentsAsync(siteId);
        return Ok(ApiResponse<List<SiteAgentDto>>.Ok(agents));
    }

    [HttpPost("{siteId}/agents")]
    public async Task<ActionResult<ApiResponse<SiteAgentDto>>> AddAgent(
        string siteId,
        [FromBody] AddAgentToSiteRequest request)
    {
        try
        {
            var agent = await _siteService.AddAgentToSiteAsync(siteId, request);
            return Ok(ApiResponse<SiteAgentDto>.Ok(agent, "Agent added successfully"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<SiteAgentDto>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SiteAgentDto>.Fail(ex.Message));
        }
    }

    [HttpPut("{siteId}/agents/{userId}")]
    public async Task<ActionResult<ApiResponse<SiteAgentDto>>> UpdateAgentPermissions(
        string siteId,
        string userId,
        [FromBody] UpdateAgentPermissionsRequest request)
    {
        try
        {
            var agent = await _siteService.UpdateAgentPermissionsAsync(siteId, userId, request);
            return Ok(ApiResponse<SiteAgentDto>.Ok(agent));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<SiteAgentDto>.Fail(ex.Message));
        }
    }

    [HttpDelete("{siteId}/agents/{userId}")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveAgent(string siteId, string userId)
    {
        try
        {
            await _siteService.RemoveAgentFromSiteAsync(siteId, userId);
            return Ok(ApiResponse<object>.Ok(null, "Agent removed successfully"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // Billing
    [HttpGet("{siteId}/billing")]
    public async Task<ActionResult<ApiResponse<SiteBillingDto>>> GetBillingInfo(string siteId)
    {
        var billing = await _siteService.GetBillingInfoAsync(siteId);
        if (billing == null)
        {
            return NotFound(ApiResponse<SiteBillingDto>.Fail("Site not found"));
        }

        return Ok(ApiResponse<SiteBillingDto>.Ok(billing));
    }

    [HttpPut("{siteId}/billing")]
    public async Task<ActionResult<ApiResponse<SiteBillingDto>>> UpdateBillingInfo(
        string siteId,
        [FromBody] UpdateBillingInfoRequest request)
    {
        try
        {
            var billing = await _siteService.UpdateBillingInfoAsync(siteId, request);
            return Ok(ApiResponse<SiteBillingDto>.Ok(billing));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<SiteBillingDto>.Fail(ex.Message));
        }
    }

    // Welcome Messages
    [HttpGet("{siteId}/welcome-messages")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<WelcomeMessageDto>>>> GetWelcomeMessages(string siteId)
    {
        var messages = await _siteService.GetWelcomeMessagesAsync(siteId);
        return Ok(ApiResponse<List<WelcomeMessageDto>>.Ok(messages));
    }

    [HttpPost("{siteId}/welcome-messages")]
    public async Task<ActionResult<ApiResponse<WelcomeMessageDto>>> CreateWelcomeMessage(
        string siteId,
        [FromBody] CreateWelcomeMessageRequest request)
    {
        try
        {
            var message = await _siteService.CreateWelcomeMessageAsync(siteId, request);
            return Ok(ApiResponse<WelcomeMessageDto>.Ok(message, "Welcome message created"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<WelcomeMessageDto>.Fail(ex.Message));
        }
    }

    [HttpPut("{siteId}/welcome-messages/{messageId}")]
    public async Task<ActionResult<ApiResponse<WelcomeMessageDto>>> UpdateWelcomeMessage(
        string siteId,
        string messageId,
        [FromBody] UpdateWelcomeMessageRequest request)
    {
        try
        {
            var message = await _siteService.UpdateWelcomeMessageAsync(siteId, messageId, request);
            return Ok(ApiResponse<WelcomeMessageDto>.Ok(message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<WelcomeMessageDto>.Fail(ex.Message));
        }
    }

    [HttpDelete("{siteId}/welcome-messages/{messageId}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteWelcomeMessage(string siteId, string messageId)
    {
        try
        {
            await _siteService.DeleteWelcomeMessageAsync(siteId, messageId);
            return Ok(ApiResponse<object>.Ok(null, "Welcome message deleted"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{siteId}/welcome-messages/reorder")]
    public async Task<ActionResult<ApiResponse<object>>> ReorderWelcomeMessages(
        string siteId,
        [FromBody] ReorderWelcomeMessagesRequest request)
    {
        await _siteService.ReorderWelcomeMessagesAsync(siteId, request);
        return Ok(ApiResponse<object>.Ok(null, "Messages reordered"));
    }

    [HttpPost("{siteId}/send-email")]
    public async Task<ActionResult<ApiResponse<string>>> SendEmail(string siteId, [FromBody] SendSiteEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.To))
            return BadRequest(ApiResponse<string>.Fail("Recipient email is required"));
        if (string.IsNullOrWhiteSpace(request.Subject))
            return BadRequest(ApiResponse<string>.Fail("Subject is required"));
        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(ApiResponse<string>.Fail("Message body is required"));

        var htmlBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                <div style='background: linear-gradient(135deg, #0ea5e9, #2563eb); padding: 24px; border-radius: 12px 12px 0 0;'>
                    <h2 style='color: white; margin: 0;'>{System.Net.WebUtility.HtmlEncode(request.Subject)}</h2>
                </div>
                <div style='padding: 24px; background: #ffffff; border: 1px solid #e5e7eb; border-top: none; border-radius: 0 0 12px 12px;'>
                    <p style='color: #374151; line-height: 1.6; white-space: pre-wrap;'>{System.Net.WebUtility.HtmlEncode(request.Body)}</p>
                </div>
            </div>";

        await _emailService.SendEmailAsync(request.To, request.Subject, htmlBody, request.Body);
        return Ok(ApiResponse<string>.Ok("Email sent successfully"));
    }

    // Supervisor Overview
    [HttpGet("{siteId}/supervisor/overview")]
    public async Task<ActionResult<ApiResponse<SupervisorOverviewDto>>> GetSupervisorOverview(string siteId)
    {
        try
        {
            var overview = await _siteService.GetSupervisorOverviewAsync(siteId);
            return Ok(ApiResponse<SupervisorOverviewDto>.Ok(overview));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<SupervisorOverviewDto>.Fail(ex.Message));
        }
    }
    // Onboarding
    [HttpGet("{siteId}/onboarding")]
    public async Task<ActionResult<ApiResponse<object>>> GetOnboarding(string siteId)
    {
        var site = await _context.Sites.FindAsync(siteId);
        if (site == null)
            return NotFound(ApiResponse<object>.Fail("Site not found"));

        object? state = null;
        if (!string.IsNullOrEmpty(site.OnboardingState))
        {
            state = JsonSerializer.Deserialize<object>(site.OnboardingState);
        }

        return Ok(ApiResponse<object>.Ok(state));
    }

    [HttpPut("{siteId}/onboarding")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateOnboarding(
        string siteId,
        [FromBody] JsonElement body)
    {
        var site = await _context.Sites.FindAsync(siteId);
        if (site == null)
            return NotFound(ApiResponse<object>.Fail("Site not found"));

        site.OnboardingState = body.GetRawText();
        site.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok(null, "Onboarding state updated"));
    }
}

public record SendSiteEmailRequest(string To, string Subject, string Body, string? VisitorId = null);
