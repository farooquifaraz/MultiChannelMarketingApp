namespace MarketingApp.Application.Interfaces.Inbox;

/// <summary>
/// Hangfire recurring job entry point — polls every SmtpGroup with EnableInboxPolling=true.
/// Cron expression comes from SystemSettings.InboxPollingCron so admin can pause/resume without redeploy.
/// </summary>
public interface IInboxPollingService
{
    Task PollAllAsync(CancellationToken ct);
}
