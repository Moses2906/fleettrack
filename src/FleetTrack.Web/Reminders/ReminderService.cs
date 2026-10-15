using System.Text;
using System.Text.Encodings.Web;
using FleetTrack.Core.Entities;
using FleetTrack.Core.Rules;
using FleetTrack.Core.Security;
using FleetTrack.Web.Data;
using FleetTrack.Web.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FleetTrack.Web.Reminders;

/// <summary>One line in the reminder email.</summary>
public record ReminderItem(ReminderKind Kind, int VehicleId, string RegistrationNumber, string MakeAndModel, DateOnly DueDate, string Milestone, string Description);

public record ReminderRecipient(string FullName, string Email);

public enum ReminderRunOutcome
{
    Sent,
    NothingDue,
    NoRecipients,
    NotConfigured,
    Failed
}

public record ReminderRunResult(ReminderRunOutcome Outcome, int ItemCount = 0, int RecipientCount = 0, string? Error = null);

public record ReminderHistoryRow(DateTime SentAtUtc, ReminderKind Kind, string RegistrationNumber, DateOnly DueDate, string Milestone, int RecipientCount);

/// <summary>
/// Builds and sends the morning reminder email. A reminder for each milestone (see <see cref="ReminderRules"/>)
/// is sent once and recorded in REMINDERS_SENT, so running this several times a day never sends duplicates.
/// </summary>
public class ReminderService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IMailSender mail,
    IOptions<EmailOptions> options,
    TimeProvider clock,
    IServiceProvider services,
    ILogger<ReminderService> logger)
{
    private EmailOptions Settings => options.Value;
    public DateOnly Today => SouthAfricanTime.Today(clock);
    public bool IsConfigured => Settings.IsConfigured;

    /// <summary>Reminders that have reached a new milestone and haven't been emailed yet.</summary>
    public async Task<List<ReminderItem>> PendingAsync(CancellationToken cancellationToken = default)
    {
        var today = Today;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var vehicles = await db.Vehicles.AsNoTracking()
            .Select(v => new { v.Id, v.RegistrationNumber, v.Make, v.Model, v.LicenceExpiryDate })
            .ToListAsync(cancellationToken);

        // The next service date comes from each vehicle's latest service (same rule as the Services page).
        var services = await db.ServiceRecords.AsNoTracking()
            .Select(s => new { s.Id, s.VehicleId, s.ServiceDate, s.NextServiceDueDate })
            .ToListAsync(cancellationToken);
        var nextService = services
            .GroupBy(s => s.VehicleId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.ServiceDate).ThenByDescending(s => s.Id).First().NextServiceDueDate);

        var alreadySent = (await db.RemindersSent.AsNoTracking()
                .Select(r => new { r.Kind, r.VehicleId, r.DueDate, r.Milestone })
                .ToListAsync(cancellationToken))
            .Select(r => (r.Kind, r.VehicleId, r.DueDate, r.Milestone))
            .ToHashSet();

        var items = new List<ReminderItem>();
        foreach (var v in vehicles)
        {
            Consider(ReminderKind.LicenceDisk, v.Id, v.RegistrationNumber, $"{v.Make} {v.Model}", v.LicenceExpiryDate);
            if (nextService.TryGetValue(v.Id, out var due) && due is { } serviceDue)
                Consider(ReminderKind.Service, v.Id, v.RegistrationNumber, $"{v.Make} {v.Model}", serviceDue);
        }

        return items.OrderBy(i => i.DueDate).ThenBy(i => i.RegistrationNumber).ToList();

        void Consider(ReminderKind kind, int vehicleId, string registration, string makeAndModel, DateOnly dueDate)
        {
            var milestone = ReminderRules.CurrentMilestone(kind, dueDate, today);
            if (milestone is not null && !alreadySent.Contains((kind, vehicleId, dueDate, milestone)))
                items.Add(new ReminderItem(kind, vehicleId, registration, makeAndModel, dueDate, milestone, ReminderRules.Describe(kind, dueDate, today)));
        }
    }

    public async Task<List<ReminderRecipient>> RecipientsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users.AsNoTracking()
            .Where(u => u.ReceivesReminderEmails && u.IsActive && u.EmailConfirmed && u.Email != null)
            .OrderBy(u => u.FullName)
            .Select(u => new ReminderRecipient(u.FullName, u.Email!))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Sends the reminder email if anything new is due. Called every morning by <see cref="ReminderBackgroundService"/>.</summary>
    public async Task<ReminderRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!Settings.IsConfigured)
            return new(ReminderRunOutcome.NotConfigured);

        var items = await PendingAsync(cancellationToken);
        if (items.Count == 0)
            return new(ReminderRunOutcome.NothingDue);

        var recipients = await RecipientsAsync(cancellationToken);
        if (recipients.Count == 0)
            return new(ReminderRunOutcome.NoRecipients, items.Count);

        var message = Compose(items);
        var delivered = 0;
        string? lastError = null;
        foreach (var recipient in recipients)
        {
            try
            {
                await mail.SendAsync(message with { To = recipient.Email }, cancellationToken);
                delivered++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex.Message;
                logger.LogError(ex, "Couldn't send the reminder email to {Recipient}.", recipient.Email);
            }
        }

        // Only remember reminders that reached at least one person; otherwise they are retried next time.
        if (delivered == 0)
            return new(ReminderRunOutcome.Failed, items.Count, 0, lastError);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        db.RemindersSent.AddRange(items.Select(i => new ReminderSent
        {
            Kind = i.Kind, VehicleId = i.VehicleId, DueDate = i.DueDate, Milestone = i.Milestone, SentAtUtc = now, RecipientCount = delivered,
        }));
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Sent the reminder email about {Count} item(s) to {Recipients} recipient(s).", items.Count, delivered);
        return new(ReminderRunOutcome.Sent, items.Count, delivered, lastError);
    }

    /// <summary>"Send now" from the Reminders page. Admin only.</summary>
    public async Task<ReminderRunResult> RunNowAsync()
    {
        await RequireAdminAsync();
        return await RunAsync();
    }

    /// <summary>Sends a short test email, so an admin can check the mail settings work.</summary>
    public async Task<ReminderRunResult> SendTestAsync(string to)
    {
        await RequireAdminAsync();
        if (!Settings.IsConfigured)
            return new(ReminderRunOutcome.NotConfigured);
        try
        {
            await mail.SendAsync(new EmailMessage(to, "FleetTrack test email",
                "<p style=\"font-family:Arial,sans-serif\">This is a test email from FleetTrack. Reminder emails will arrive like this one.</p>",
                "This is a test email from FleetTrack. Reminder emails will arrive like this one."));
            return new(ReminderRunOutcome.Sent, 0, 1);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test email to {Recipient} failed.", to);
            return new(ReminderRunOutcome.Failed, Error: ex.Message);
        }
    }

    public async Task<List<ReminderHistoryRow>> HistoryAsync(int take = 30)
    {
        await RequireAdminAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.RemindersSent.AsNoTracking()
            .OrderByDescending(r => r.SentAtUtc).ThenBy(r => r.Vehicle.RegistrationNumber)
            .Take(take)
            .Select(r => new ReminderHistoryRow(r.SentAtUtc, r.Kind, r.Vehicle.RegistrationNumber, r.DueDate, r.Milestone, r.RecipientCount))
            .ToListAsync();
    }

    // The morning job runs without a signed-in user, so the permission check is only applied to calls from pages.
    private Task RequireAdminAsync() => services.GetRequiredService<PermissionService>().DemandAsync(Permission.ManageUsers);

    public EmailMessage Compose(IReadOnlyList<ReminderItem> items)
    {
        var html = HtmlEncoder.Default;
        var licence = items.Where(i => i.Kind == ReminderKind.LicenceDisk).ToList();
        var service = items.Where(i => i.Kind == ReminderKind.Service).ToList();
        var baseUrl = Settings.AppBaseUrl?.TrimEnd('/');

        var subject = (licence.Count, service.Count) switch
        {
            ( > 0, > 0) => $"FleetTrack: {licence.Count} licence disk(s) and {service.Count} service(s) need attention",
            ( > 0, _) => $"FleetTrack: {licence.Count} licence disk(s) need attention",
            _ => $"FleetTrack: {service.Count} service(s) need attention",
        };

        var h = new StringBuilder();
        var t = new StringBuilder();
        h.Append("<div style=\"font-family:Arial,Helvetica,sans-serif;color:#111827;max-width:600px\">");
        h.Append("<h2 style=\"margin:0 0 4px\">Good morning</h2>");
        h.Append($"<p style=\"color:#6B7280;margin:0 0 20px\">Here's what needs attention in the fleet today, {Today:dddd d MMMM yyyy}.</p>");
        t.AppendLine($"Here's what needs attention in the fleet today, {Today:dddd d MMMM yyyy}.").AppendLine();

        Section("Licence disks", licence);
        Section("Services", service);

        if (baseUrl is not null)
        {
            h.Append($"<p><a href=\"{html.Encode(baseUrl)}\" style=\"background:#2563EB;color:#fff;padding:10px 16px;border-radius:8px;text-decoration:none\">Open FleetTrack</a></p>");
            t.AppendLine($"Open FleetTrack: {baseUrl}");
        }
        h.Append("<p style=\"color:#9CA3AF;font-size:12px;margin-top:24px\">You get this email because an administrator turned on reminders for your FleetTrack account. Each reminder is sent once per milestone (30 and 7 days before, then weekly while overdue).</p></div>");

        return new EmailMessage("", subject, h.ToString(), t.ToString());

        void Section(string title, List<ReminderItem> sectionItems)
        {
            if (sectionItems.Count == 0)
                return;
            h.Append($"<h3 style=\"margin:20px 0 8px\">{title}</h3><table style=\"border-collapse:collapse;width:100%\">");
            t.AppendLine(title.ToUpperInvariant());
            foreach (var i in sectionItems)
            {
                var overdue = i.DueDate < Today;
                var colour = overdue ? "#DC2626" : "#B45309";
                // Every value is HTML-encoded, so nothing typed into FleetTrack can inject markup into the email.
                var registration = html.Encode(i.RegistrationNumber);
                var link = baseUrl is null ? registration : $"<a href=\"{html.Encode($"{baseUrl}/vehicles/{i.VehicleId}")}\" style=\"color:#2563EB\">{registration}</a>";
                h.Append("<tr style=\"border-bottom:1px solid #E5E7EB\">")
                 .Append($"<td style=\"padding:8px 0;font-weight:bold;white-space:nowrap\">{link}</td>")
                 .Append($"<td style=\"padding:8px\">{html.Encode(i.MakeAndModel)}</td>")
                 .Append($"<td style=\"padding:8px 0;color:{colour};text-align:right;white-space:nowrap\">{html.Encode(i.Description)}<br><span style=\"color:#6B7280;font-size:12px\">{i.DueDate:dd MMM yyyy}</span></td>")
                 .Append("</tr>");
                t.AppendLine($"- {i.RegistrationNumber} ({i.MakeAndModel}): {i.Description} ({i.DueDate:dd MMM yyyy})");
            }
            h.Append("</table>");
            t.AppendLine();
        }
    }
}
