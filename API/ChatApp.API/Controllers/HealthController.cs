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
}
