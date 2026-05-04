using ApiTuneScore.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApiTuneScore.HealthChecks;

public sealed class DatabaseHealthCheck(TuneScoreContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("Database is reachable.")
                : HealthCheckResult.Unhealthy(
                    "Database is not reachable (CanConnect returned false). Check server name, database name, credentials, and Azure SQL firewall rules for this App Service.");
        }
        catch (Exception ex)
        {
            var detail = ex.Message.Length > 400 ? ex.Message[..400] + "…" : ex.Message;
            return HealthCheckResult.Unhealthy($"Database health check failed: {ex.GetType().Name}: {detail}", ex);
        }
    }
}
