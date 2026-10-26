using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Case2.Api.Infrastructure;

public sealed class DatabaseHealthCheck(Database database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await database.Open(cancellationToken);
            await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT 1;", commandTimeout: 3, cancellationToken: cancellationToken));
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("SQL Server bağlantısı kurulamadı.", exception);
        }
    }
}
