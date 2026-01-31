using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;
using ChatApp.API.Models.Entities;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "super_admin")]
public class SmtpSettingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SmtpSettingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get SMTP settings
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<SmtpSettingsDto>>> GetSettings()
    {
        var settings = await _context.SmtpSettings.FirstOrDefaultAsync();

        if (settings == null)
        {
            // Return default settings if none exist
            return Ok(ApiResponse<SmtpSettingsDto>.Ok(new SmtpSettingsDto
            {
                SmtpHost = "smtp.gmail.com",
                SmtpPort = 587,
                SmtpUsername = "",
                SmtpPassword = "",
                FromEmail = "noreply@chatapp.com",
                FromName = "ChatApp",
                EnableSsl = true,
                IsActive = false
            }));
        }

        return Ok(ApiResponse<SmtpSettingsDto>.Ok(MapToDto(settings)));
    }

    /// <summary>
    /// Update SMTP settings
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<ApiResponse<SmtpSettingsDto>>> UpdateSettings([FromBody] UpdateSmtpSettingsRequest request)
    {
        var settings = await _context.SmtpSettings.FirstOrDefaultAsync();

        if (settings == null)
        {
            // Create new settings if none exist
            settings = new SmtpSettings();
            _context.SmtpSettings.Add(settings);
        }

        // Update SMTP settings
        if (request.SmtpHost != null) settings.SmtpHost = request.SmtpHost;
        if (request.SmtpPort.HasValue) settings.SmtpPort = request.SmtpPort.Value;
        if (request.SmtpUsername != null) settings.SmtpUsername = request.SmtpUsername;
        if (request.SmtpPassword != null) settings.SmtpPassword = request.SmtpPassword;
        if (request.FromEmail != null) settings.FromEmail = request.FromEmail;
        if (request.FromName != null) settings.FromName = request.FromName;
        if (request.EnableSsl.HasValue) settings.EnableSsl = request.EnableSsl.Value;
        if (request.IsActive.HasValue) settings.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();

        return Ok(ApiResponse<SmtpSettingsDto>.Ok(MapToDto(settings), "SMTP settings updated successfully"));
    }

    /// <summary>
    /// Test SMTP connection
    /// </summary>
    [HttpPost("test")]
    public async Task<ActionResult<ApiResponse<string>>> TestConnection([FromBody] TestSmtpRequest request)
    {
        try
        {
            var settings = await _context.SmtpSettings.FirstOrDefaultAsync();

            string smtpHost, smtpUsername, smtpPassword, fromEmail, fromName;
            int smtpPort;
            bool enableSsl;

            if (settings != null && settings.IsActive)
            {
                smtpHost = settings.SmtpHost;
                smtpPort = settings.SmtpPort;
                smtpUsername = settings.SmtpUsername;
                smtpPassword = settings.SmtpPassword;
                fromEmail = settings.FromEmail;
                fromName = settings.FromName;
                enableSsl = settings.EnableSsl;
            }
            else
            {
                return BadRequest(ApiResponse<string>.Fail("SMTP settings are not configured or not active"));
            }

            // Bypass SSL certificate validation for testing
            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                (sender, certificate, chain, sslPolicyErrors) => true;

            using var client = new System.Net.Mail.SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new System.Net.NetworkCredential(smtpUsername, smtpPassword),
                EnableSsl = enableSsl
            };

            var message = new System.Net.Mail.MailMessage
            {
                From = new System.Net.Mail.MailAddress(fromEmail, fromName),
                Subject = "ChatApp SMTP Test",
                IsBodyHtml = true,
                Body = "<h2>SMTP Test Successful</h2><p>Your SMTP settings are configured correctly.</p>"
            };

            message.To.Add(request.TestEmail);

            await client.SendMailAsync(message);

            return Ok(ApiResponse<string>.Ok($"Test email sent successfully to {request.TestEmail}"));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.Fail($"SMTP test failed: {ex.Message}"));
        }
    }

    private static SmtpSettingsDto MapToDto(SmtpSettings settings)
    {
        return new SmtpSettingsDto
        {
            Id = settings.Id,
            SmtpHost = settings.SmtpHost,
            SmtpPort = settings.SmtpPort,
            SmtpUsername = settings.SmtpUsername,
            SmtpPassword = settings.SmtpPassword,
            FromEmail = settings.FromEmail,
            FromName = settings.FromName,
            EnableSsl = settings.EnableSsl,
            IsActive = settings.IsActive,
            UpdatedAt = settings.UpdatedAt
        };
    }
}

// DTOs
public class SmtpSettingsDto
{
    public string? Id { get; set; }
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@chatapp.com";
    public string FromName { get; set; } = "ChatApp";
    public bool EnableSsl { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime? UpdatedAt { get; set; }
}

public class UpdateSmtpSettingsRequest
{
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
    public bool? EnableSsl { get; set; }
    public bool? IsActive { get; set; }
}

public class TestSmtpRequest
{
    public string TestEmail { get; set; } = string.Empty;
}
