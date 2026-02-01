using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Data;
using ChatApp.API.Models.DTOs;

namespace ChatApp.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public HealthController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<HealthCheckResponse>> GetHealth()
    {
        var services = new Dictionary<string, string>();

        // Check database connection
        try
        {
            await _context.Database.CanConnectAsync();
            services["Database"] = "Healthy";
        }
        catch
        {
            services["Database"] = "Unhealthy";
        }

        services["API"] = "Healthy";
        services["SignalR"] = "Healthy";

        var allHealthy = services.Values.All(v => v == "Healthy");

        return Ok(new HealthCheckResponse(
            allHealthy ? "Healthy" : "Degraded",
            DateTime.UtcNow,
            services
        ));
    }

    [HttpGet("ready")]
    public async Task<IActionResult> GetReadiness()
    {
        try
        {
            await _context.Database.CanConnectAsync();
            return Ok(new { Status = "Ready" });
        }
        catch
        {
            return StatusCode(503, new { Status = "Not Ready" });
        }
    }

    [HttpGet("live")]
    public IActionResult GetLiveness()
    {
        return Ok(new { Status = "Alive" });
    }

    [HttpGet("diagnose")]
    public async Task<ActionResult> Diagnose()
    {
        var results = new Dictionary<string, object>();
        var connectionString = _context.Database.GetConnectionString();
        results["ConnectionString"] = connectionString != null
            ? connectionString.Substring(0, Math.Min(connectionString.IndexOf("Password=", StringComparison.OrdinalIgnoreCase) is int idx and >= 0 ? idx : connectionString.Length, connectionString.Length)) + "***"
            : "NULL";
        results["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "not set";

        // DB connect
        try
        {
            await _context.Database.CanConnectAsync();
            results["DbConnect"] = "OK";
        }
        catch (Exception ex)
        {
            results["DbConnect"] = $"FAIL: {ex.GetType().Name}: {ex.Message}";
        }

        // Raw SQL test
        try
        {
            await _context.Database.ExecuteSqlRawAsync("SELECT 1");
            results["DbQuery"] = "OK";
        }
        catch (Exception ex)
        {
            results["DbQuery"] = $"FAIL: {ex.GetType().Name}: {ex.Message}";
        }

        // Table checks
        var tables = new[] { "users", "sites", "site_settings", "smtp_settings", "app_settings", "subscriptions", "conversations" };
        foreach (var table in tables)
        {
            try
            {
                var count = await _context.Database.ExecuteSqlRawAsync($"SELECT TOP 1 * FROM dbo.[{table}]");
                results[$"Table:{table}"] = "OK";
            }
            catch (Exception ex)
            {
                results[$"Table:{table}"] = $"FAIL: {ex.Message}";
            }
        }

        // Test SiteSettings query (the one that 500s)
        try
        {
            var settings = await _context.SiteSettings.FirstOrDefaultAsync();
            results["SiteSettingsQuery"] = settings != null ? $"OK (id={settings.Id})" : "OK (null)";
        }
        catch (Exception ex)
        {
            results["SiteSettingsQuery"] = $"FAIL: {ex.GetType().Name}: {ex.Message}";
            if (ex.InnerException != null)
                results["SiteSettingsQuery_Inner"] = $"{ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
        }

        // Test Auth query
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync();
            results["UsersQuery"] = user != null ? $"OK (found user)" : "OK (no users)";
        }
        catch (Exception ex)
        {
            results["UsersQuery"] = $"FAIL: {ex.GetType().Name}: {ex.Message}";
            if (ex.InnerException != null)
                results["UsersQuery_Inner"] = $"{ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
        }

        return Ok(results);
    }
}
