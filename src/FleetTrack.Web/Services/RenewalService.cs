using FleetTrack.Core.Entities;
using FleetTrack.Core.Rules;
using FleetTrack.Core.Security;
using FleetTrack.Web.Data;
using FleetTrack.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Web.Services;

public record RenewalStepRow(int Id, RenewalStepType StepType, DateOnly StepDate, string? Notes, string RecordedBy);

public record RenewalRow(
    int Id,
    int VehicleId,
    string RegistrationNumber,
    DateOnly DatePurchased,
    DateOnly? PreviousExpiryDate,
    DateOnly NewExpiryDate,
    decimal AmountPaid,
    string? ReferenceNumber,
    string ProcessedBy,
    string? Notes,
    IReadOnlyList<RenewalStepRow> Steps)
{
    /// <summary>The usual follow-up steps that haven't been recorded yet.</summary>
    public IEnumerable<RenewalStepType> OutstandingSteps =>
        new[] { RenewalStepType.DiskHandedToDriver, RenewalStepType.DiskFittedToVehicle, RenewalStepType.CopyFiled }
            .Where(t => Steps.All(s => s.StepType != t));
}

/// <summary>Licence disk renewals and what happened after each new disk was bought.</summary>
public class RenewalService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    PermissionService permissions,
    CurrentUserService currentUser,
    TimeProvider clock)
{
    public DateOnly Today => SouthAfricanTime.Today(clock);

    /// <param name="vehicleId">Only this vehicle's renewals, or all vehicles when null.</param>
    public async Task<List<RenewalRow>> ListAsync(int? vehicleId = null)
    {
        await permissions.DemandAsync(Permission.ViewRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.LicenceRenewals.AsNoTracking();
        if (vehicleId is not null)
            query = query.Where(r => r.VehicleId == vehicleId);

        return await query
            .OrderByDescending(r => r.DatePurchased).ThenByDescending(r => r.Id)
            .Select(r => new RenewalRow(r.Id, r.VehicleId, r.Vehicle.RegistrationNumber, r.DatePurchased, r.PreviousExpiryDate,
                r.NewExpiryDate, r.AmountPaid, r.ReferenceNumber, r.ProcessedBy, r.Notes,
                r.Steps.OrderBy(s => s.StepDate).ThenBy(s => s.Id)
                    .Select(s => new RenewalStepRow(s.Id, s.StepType, s.StepDate, s.Notes, s.RecordedBy)).ToList()))
            .ToListAsync();
    }

    /// <summary>A pre-filled form: bought today, valid for a year after the current disk expires, processed by me.</summary>
    public async Task<RenewalForm?> NewFormAsync(int vehicleId)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(v => v.Id == vehicleId);
        if (vehicle is null)
            return null;

        var user = await currentUser.GetPrincipalAsync();
        return new RenewalForm
        {
            DatePurchased = Today.ToDateTime(TimeOnly.MinValue),
            NewExpiryDate = RenewalRules.SuggestNewExpiry(vehicle.LicenceExpiryDate).ToDateTime(TimeOnly.MinValue),
            ProcessedBy = user.DisplayName(),
        };
    }

    /// <summary>
    /// Records a renewal and, in the same save, moves the vehicle's expiry date forward.
    /// Both changes are written in one transaction, so they can never get out of step.
    /// </summary>
    /// <param name="stepsDone">Follow-up steps already done (handed over, fitted, filed), saved with the renewal.</param>
    public async Task<OperationResult> CreateAsync(int vehicleId, RenewalForm form, IReadOnlyList<RenewalStepForm>? stepsDone = null)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        if (FormValidator.Validate(form) is { } error)
            return OperationResult.Fail(error);

        var purchased = DateOnly.FromDateTime(form.DatePurchased!.Value);
        var newExpiry = DateOnly.FromDateTime(form.NewExpiryDate!.Value);
        if (RenewalRules.Validate(purchased, newExpiry, Today) is { } dateError)
            return OperationResult.Fail(dateError);

        await using var db = await dbFactory.CreateDbContextAsync();
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == vehicleId);
        if (vehicle is null)
            return OperationResult.Fail("This vehicle has been deleted.");

        var renewal = new LicenceRenewal
        {
            VehicleId = vehicleId,
            DatePurchased = purchased,
            NewExpiryDate = newExpiry,
            AmountPaid = Math.Round(form.AmountPaid!.Value, 2),
            ReferenceNumber = Blank(form.ReferenceNumber),
            ProcessedBy = form.ProcessedBy.Trim(),
            Notes = Blank(form.Notes),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = (await currentUser.GetActorAsync()).UserName ?? "unknown",
        };
        var recordedBy = renewal.CreatedBy;
        foreach (var step in stepsDone ?? [])
        {
            if (FormValidator.Validate(step) is { } stepError)
                return OperationResult.Fail(stepError);
            var stepDate = DateOnly.FromDateTime(step.StepDate!.Value);
            if (stepDate > Today || stepDate < purchased)
                return OperationResult.Fail($"\"{DisplayNames.Of(step.StepType)}\" must be dated between the purchase date and today.");
            renewal.Steps.Add(new RenewalStep { StepType = step.StepType, StepDate = stepDate, Notes = Blank(step.Notes), RecordedBy = recordedBy });
        }

        RenewalRules.Apply(vehicle, renewal);
        db.LicenceRenewals.Add(renewal);
        await db.SaveChangesAsync();
        return OperationResult.Created(renewal.Id);
    }

    /// <summary>
    /// Corrects the amount, reference, processor or notes. The dates can't be edited, because they drive the
    /// vehicle's expiry date: to fix a date, delete the renewal (which restores the old expiry) and record it again.
    /// </summary>
    public async Task<OperationResult> UpdateDetailsAsync(int id, RenewalForm form)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        if (FormValidator.Validate(form) is { } error)
            return OperationResult.Fail(error);

        await using var db = await dbFactory.CreateDbContextAsync();
        var renewal = await db.LicenceRenewals.FindAsync(id);
        if (renewal is null)
            return OperationResult.Fail("This renewal has been deleted.");

        renewal.AmountPaid = Math.Round(form.AmountPaid!.Value, 2);
        renewal.ReferenceNumber = Blank(form.ReferenceNumber);
        renewal.ProcessedBy = form.ProcessedBy.Trim();
        renewal.Notes = Blank(form.Notes);
        await db.SaveChangesAsync();
        return OperationResult.Ok;
    }

    public async Task<RenewalForm?> GetFormAsync(int id)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var r = await db.LicenceRenewals.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id);
        return r is null ? null : new RenewalForm
        {
            DatePurchased = r.DatePurchased.ToDateTime(TimeOnly.MinValue),
            NewExpiryDate = r.NewExpiryDate.ToDateTime(TimeOnly.MinValue),
            AmountPaid = r.AmountPaid,
            ReferenceNumber = r.ReferenceNumber,
            ProcessedBy = r.ProcessedBy,
            Notes = r.Notes,
        };
    }

    /// <summary>Deletes a renewal (and its steps). If it set the current expiry date, the previous date is restored.</summary>
    public async Task<OperationResult> DeleteAsync(int id)
    {
        await permissions.DemandAsync(Permission.DeleteRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var renewal = await db.LicenceRenewals.Include(r => r.Vehicle).Include(r => r.Steps).SingleOrDefaultAsync(r => r.Id == id);
        if (renewal is null)
            return OperationResult.Ok;

        RenewalRules.Revert(renewal.Vehicle, renewal);
        db.LicenceRenewals.Remove(renewal);
        await db.SaveChangesAsync();
        return OperationResult.Ok;
    }

    public async Task<OperationResult> AddStepAsync(int renewalId, RenewalStepForm form)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        if (FormValidator.Validate(form) is { } error)
            return OperationResult.Fail(error);

        await using var db = await dbFactory.CreateDbContextAsync();
        var renewal = await db.LicenceRenewals.AsNoTracking().SingleOrDefaultAsync(r => r.Id == renewalId);
        if (renewal is null)
            return OperationResult.Fail("This renewal has been deleted.");

        var stepDate = DateOnly.FromDateTime(form.StepDate!.Value);
        if (stepDate > Today)
            return OperationResult.Fail("The date can't be in the future.");
        if (stepDate < renewal.DatePurchased)
            return OperationResult.Fail($"This can't have happened before the disk was bought on {renewal.DatePurchased:dd MMM yyyy}.");

        var step = new RenewalStep
        {
            LicenceRenewalId = renewalId,
            StepType = form.StepType,
            StepDate = stepDate,
            Notes = Blank(form.Notes),
            RecordedBy = (await currentUser.GetActorAsync()).UserName ?? "unknown",
        };
        db.RenewalSteps.Add(step);
        await db.SaveChangesAsync();
        return OperationResult.Created(step.Id);
    }

    public async Task<OperationResult> DeleteStepAsync(int stepId)
    {
        await permissions.DemandAsync(Permission.DeleteRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var step = await db.RenewalSteps.FindAsync(stepId);
        if (step is not null)
        {
            db.RenewalSteps.Remove(step);
            await db.SaveChangesAsync();
        }
        return OperationResult.Ok;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
