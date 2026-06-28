using AgenticFactory.Application;
using AgenticFactory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgenticFactory.Runtime.WindowsService;

public sealed class RuntimeWorker(
    ILogger<RuntimeWorker> logger,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var runtime = scope.ServiceProvider.GetRequiredService<IAgentRuntime>();
                await runtime.TickAsync(stoppingToken);

                // Lightweight local health marker in DB.
                var db = scope.ServiceProvider.GetRequiredService<AgenticFactoryDbContext>();
                var heartbeat = await db.RuntimeHeartbeats.FirstOrDefaultAsync(x => x.NodeName == Environment.MachineName, stoppingToken);
                if (heartbeat is not null)
                {
                    heartbeat.Status = "Healthy";
                    heartbeat.LastSeenUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Runtime tick failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }
}
