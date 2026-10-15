using FleetTrack.Core.Security;
using FleetTrack.Web.Security;
using FleetTrack.Web.Services;

namespace FleetTrack.Web.History;

public enum HistoryEntryKind
{
    Renewal,
    Service
}

/// <summary>One event on a vehicle's timeline: a licence renewal or a service/repair.</summary>
public record HistoryEntry(
    DateOnly Date,
    HistoryEntryKind Kind,
    string Title,
    string Summary,
    decimal Amount,
    int? OdometerKm,
    string? Notes,
    IReadOnlyList<string> FollowUps);

public record VehicleHistory(VehicleDetails Vehicle, DateOnly From, DateOnly To, IReadOnlyList<HistoryEntry> Entries)
{
    public decimal RenewalTotal => Entries.Where(e => e.Kind == HistoryEntryKind.Renewal).Sum(e => e.Amount);
    public decimal ServiceTotal => Entries.Where(e => e.Kind == HistoryEntryKind.Service).Sum(e => e.Amount);
    public int RenewalCount => Entries.Count(e => e.Kind == HistoryEntryKind.Renewal);
    public int ServiceCount => Entries.Count(e => e.Kind == HistoryEntryKind.Service);
}

/// <summary>
/// Builds a vehicle's timeline of renewals and services, newest first.
/// It reuses the same services as the Renewals and Services tabs, so the history can never disagree with them
/// (and their permission checks apply here too).
/// </summary>
public class HistoryService(VehicleService vehicles, RenewalService renewals, ServiceLogService services, PermissionService permissions)
{
    public const int DefaultYears = 5;

    public async Task<VehicleHistory?> GetAsync(int vehicleId, int years = DefaultYears)
    {
        await permissions.DemandAsync(Permission.ViewRecords);
        years = Math.Clamp(years, 1, 20);

        var vehicle = await vehicles.GetAsync(vehicleId);
        if (vehicle is null)
            return null;

        var to = vehicles.Today;
        var from = to.AddYears(-years);

        var renewalEntries = (await renewals.ListAsync(vehicleId))
            .Where(r => r.DatePurchased >= from)
            .Select(r => new HistoryEntry(
                r.DatePurchased,
                HistoryEntryKind.Renewal,
                "Licence disk renewed",
                $"Valid until {r.NewExpiryDate:dd MMM yyyy}"
                    + (r.ReferenceNumber is null ? "" : $" · Ref {r.ReferenceNumber}")
                    + $" · processed by {r.ProcessedBy}",
                r.AmountPaid,
                null,
                r.Notes,
                r.Steps.Select(s => $"{DisplayNames.Of(s.StepType)} on {s.StepDate:dd MMM yyyy}" + (s.Notes is null ? "" : $": {s.Notes}")).ToList()));

        var serviceEntries = (await services.ListAsync(vehicleId))
            .Where(s => s.ServiceDate >= from)
            .Select(s => new HistoryEntry(
                s.ServiceDate,
                HistoryEntryKind.Service,
                DisplayNames.Of(s.WorkType),
                $"{s.Description} · {s.Workshop}",
                s.Cost,
                s.OdometerKm,
                s.Notes,
                s.NextServiceDueDate is { } next ? [$"Next service due {next:dd MMM yyyy}"] : []));

        var entries = renewalEntries.Concat(serviceEntries)
            .OrderByDescending(e => e.Date)
            .ThenBy(e => e.Kind)
            .ToList();

        return new VehicleHistory(vehicle, from, to, entries);
    }
}
