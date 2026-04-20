using ApiTuneScore.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApiTuneScore.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HealthController : ControllerBase
{
    private readonly TuneScoreContext _context;
    private readonly HealthCheckService _healthChecks;

    public HealthController(TuneScoreContext context, HealthCheckService healthChecks)
    {
        _context = context;
        _healthChecks = healthChecks;
    }

    [HttpGet]
    [AllowAnonymous]
    [EndpointDescription("Runs all registered health checks and returns overall service health.")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var report = await _healthChecks.CheckHealthAsync(_ => true, ct);
        return ToHealthResponse(report);
    }

    [HttpGet("ready")]
    [AllowAnonymous]
    [EndpointDescription("Runs readiness checks only (currently the database check tagged as ready).")]
    public async Task<IActionResult> GetReady(CancellationToken ct)
    {
        var report = await _healthChecks.CheckHealthAsync(r => r.Tags.Contains("ready"), ct);
        return ToHealthResponse(report);
    }

    [HttpGet("wake")]
    [AllowAnonymous]
    [EndpointDescription("Checks database connectivity and helps wake up auto-paused Azure SQL instances.")]
    public async Task<IActionResult> Wake(CancellationToken ct)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(ct);
            return canConnect
                ? Ok(new { status = "ready", message = "Database is ready." })
                : StatusCode(503, new { status = "unavailable", message = "Cannot connect to database." });
        }
        catch (Exception ex) when (IsAzurePaused(ex))
        {
            return StatusCode(202, new { status = "waking", message = "Database is resuming from auto-pause. Retry in a few seconds." });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(503, new { status = "unavailable", message = "Database connection timed out." });
        }
        catch
        {
            return StatusCode(503, new { status = "unavailable", message = "Database is unavailable." });
        }
    }

    private static IActionResult ToHealthResponse(HealthReport report)
    {
        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        };

        var statusCode = report.Status == HealthStatus.Healthy ? 200 : 503;
        return new ObjectResult(payload) { StatusCode = statusCode };
    }

    private static bool IsAzurePaused(Exception ex)
    {
        var msg = ex.ToString();
        return msg.Contains("40613") ||
               msg.Contains("not currently available") ||
               msg.Contains("auto-paused");
    }
}
