using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;

namespace PaymentService.HealthChecks
{
    internal class MongoConnectionHealthCheck : IHealthCheck
    {
        private readonly string _connectionString;
        public MongoConnectionHealthCheck(string connectionString) => _connectionString = connectionString;

        public async System.Threading.Tasks.Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                var client = new MongoClient(_connectionString);
                using var cursor = await client.ListDatabaseNamesAsync(cancellationToken: cancellationToken);
                await cursor.MoveNextAsync(cancellationToken);
                return HealthCheckResult.Healthy("MongoDB reachable");
            }
            catch (System.Exception ex)
            {
                return HealthCheckResult.Unhealthy($"MongoDB unreachable: {ex.Message}");
            }
        }
    }
}
