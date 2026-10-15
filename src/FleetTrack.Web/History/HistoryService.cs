using FleetTrack.Core.Rules;
using FleetTrack.Core.Security;
using FleetTrack.Web.Security;
using FleetTrack.Web.Services;

namespace FleetTrack.Web.History;

public enum HistoryEntryKind
{
    Renewal,
    Service
}

/// <summary>One event on a timeline: a licence renewal or a service/repair.</summary>
public record HistoryEntry(
    int VehicleId,
    string RegistrationNumber,
    DateOnly Date,
    HistoryEntryKind Kind,
    string Title,
    string Summary,
    decimal Amount,
    int? OdometerKm,
    string? Notes,
    IReadOnlyList<string> FollowUps);

/// <summary>Totals shared by the vehicle and monthly histories.</summary>
public abstract record HistoryBase(DateOnly From, DateOnly To, IReadOnlyList<HistoryEntry> Entries)
{
    public decimal RenewalTotal => Entries.Where(e => e.Kind == HistoryEntryKind.Renewal).Sum(e => e.Amount);
    public decimal ServiceTotal => Entries.Where(e => e.Kind == HistoryEntryKind.Service).Sum(e => e.Amount);
    public int RenewalCount => Entries.Count(e => e.Kind == HistoryEntryKind.Renewal);
    public int ServiceCount => Entries.Count(e => e.Kind == HistoryEntryKind.Service);
}

/// <summary>One vehicle's renewals and services over a number of years.</summary>
public record VehicleHistory(VehicleDetails Vehicle, DateOnly From, DateOnly To, IReadOnlyList<HistoryEntry> Entries)
    : HistoryBase(From, To, Entries);

/// <summary>Every vehicle's renewals and services in one calendar month.</summary>
public record MonthlyHistory(YearMonth Month, IReadOnlyList<HistoryEntry> Entries)
    : HistoryBase(Month.FirstDay, Month.LastDay, Entries)
{
    public int VehicleCount => Entries.Select(e => e.VehicleId).Distinct().Count();
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
        var entries = await EntriesAsync(vehicleId, from, to);
        return new VehicleHistory(vehicle, from, to, entries);
    }

    /// <summary>Every renewal and service across the fleet in one month, newest first.</summary>
    public async Task<MonthlyHistory> GetMonthAsync(YearMonth month)
    {
        await permissions.DemandAsync(Permission.ViewRecords);
        return new MonthlyHistory(month, await EntriesAsync(vehicleId: null, month.FirstDay, month.LastDay));
    }

    /// <param name="vehicleId">One vehicle, or the whole fleet when null.</param>
    private async Task<List<HistoryEntry>> EntriesAsync(int? vehicleId, DateOnly from, DateOnly to)
    {
        var renewalEntries = (await renewals.ListAsync(vehicleId))
            .Where(r => r.DatePurchased >= from && r.DatePurchased <= to)
            .Select(r => new HistoryEntry(
                r.VehicleId,
                r.RegistrationNumber,
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
            .Where(s => s.ServiceDate >= from && s.ServiceDate <= to)
            .Select(s => new HistoryEntry(
                s.VehicleId,
                s.RegistrationNumber,
                s.ServiceDate,
                HistoryEntryKind.Service,
                DisplayNames.Of(s.WorkType),
                $"{s.Description} · {s.Workshop}",
                s.Cost,
                s.OdometerKm,
                s.Notes,
                s.NextServiceDueDate is { } next ? [$"Next service due {next:dd MMM yyyy}"] : []));

        return renewalEntries.Concat(serviceEntries)
            .OrderByDescending(e => e.Date)
            .ThenBy(e => e.Kind)
            .ThenBy(e => e.RegistrationNumber)
            .ToList();
    }
}
