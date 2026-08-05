using HiddenIcons.Core;

namespace HiddenIcons.Service;

public sealed class Worker(ConfigStore store, ProcessSupervisor supervisor, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Hidden Icons service started at {Time}", DateTimeOffset.Now);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try { supervisor.Reconcile(store.Load().Profiles); }
            catch (Exception ex) { logger.LogError(ex, "Profile reconciliation failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        supervisor.StopOwnedProcesses();
        return base.StopAsync(cancellationToken);
    }
}
