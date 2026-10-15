using FleetTrack.Core.Rules;
using Microsoft.Extensions.Options;

namespace FleetTrack.Web.Reminders;

/// <summary>
/// Runs inside the web app and sends the reminder email once a day, after the configured hour (07:00 SA time
/// by default). It checks every 15 minutes; because each reminder is only sent once per milestone, an extra
/// check (or a restart) never causes duplicates.
/// </summary>
public class ReminderBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailOptions> options,
    TimeProvider clock,
    ILogger<ReminderBackgroundService> logger) : BackgroundService
{
    private DateOnly? lastRun;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsConfigured)
        {
            logger.LogInformation("Email isn't configured (Email__Host / Email__FromAddress), so reminder emails are off.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15), clock);
        do
        {
            var now = SouthAfricanTime.ToLocal(clock.GetUtcNow().UtcDateTime);
            var today = DateOnly.FromDateTime(now);
            if (now.Hour >= options.Value.SendHour && lastRun != today)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<ReminderService>().RunAsync(stoppingToken);
                    // A failed send isn't marked as done, so it is retried at the next check.
                    if (result.Outcome != ReminderRunOutcome.Failed)
                        lastRun = today;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "The reminder email run failed; it will be retried in 15 minutes.");
                }
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
