using Hangfire;

namespace OpsForge.Worker;

public sealed class GitHubSyncScheduler(
    IBackgroundJobClient jobs,
    IConfiguration configuration,
    ILogger<GitHubSyncScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Max(2, configuration.GetValue("GitHubSync:IntervalSeconds", 2));
        var interval = TimeSpan.FromSeconds(intervalSeconds);

        logger.LogInformation("GitHub sync scheduler started with {IntervalSeconds}s interval.", intervalSeconds);

        using var timer = new PeriodicTimer(interval);
        EnqueueSync();

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            EnqueueSync();
        }
    }

    private void EnqueueSync()
    {
        if (HasActiveSyncJob())
        {
            logger.LogDebug("GitHub sync enqueue skipped because a sync job is already active.");
            return;
        }

        jobs.Enqueue<GitHubAccountSyncJob>(job => job.RunAsync(CancellationToken.None));
    }

    private static bool HasActiveSyncJob()
    {
        var monitor = JobStorage.Current.GetMonitoringApi();
        return monitor.ProcessingJobs(0, 100).Any(IsSyncJob)
            || monitor.EnqueuedJobs("default", 0, 100).Any(IsSyncJob)
            || monitor.ScheduledJobs(0, 100).Any(IsSyncJob);
    }

    private static bool IsSyncJob(KeyValuePair<string, Hangfire.Storage.Monitoring.ProcessingJobDto> item) =>
        item.Value.Job?.Type == typeof(GitHubAccountSyncJob);

    private static bool IsSyncJob(KeyValuePair<string, Hangfire.Storage.Monitoring.EnqueuedJobDto> item) =>
        item.Value.Job?.Type == typeof(GitHubAccountSyncJob);

    private static bool IsSyncJob(KeyValuePair<string, Hangfire.Storage.Monitoring.ScheduledJobDto> item) =>
        item.Value.Job?.Type == typeof(GitHubAccountSyncJob);
}
