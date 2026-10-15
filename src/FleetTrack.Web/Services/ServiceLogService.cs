using FleetTrack.Core.Entities;
using FleetTrack.Core.Rules;
using FleetTrack.Core.Security;
using FleetTrack.Web.Data;
using FleetTrack.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Web.Services;

public record ServiceRow(
    int Id,
    int VehicleId,
    string RegistrationNumber,
    DateOnly ServiceDate,
    int OdometerKm,
    WorkType WorkType,
    string Description,
    string Workshop,
    decimal Cost,
    string? Notes,
    DateOnly? NextServiceDueDate,
    string CreatedBy);

public record ServiceReminderRow(
    int VehicleId,
    string RegistrationNumber,
    string MakeAndModel,
    DateOnly LastServiceDate,
    int LastOdometerKm,
    DateOnly NextServiceDue)
{
    public ServiceReminder ReminderOn(DateOnly today) => ServiceRules.GetReminder(NextServiceDue, today);
}

/// <summary>The service and repair log, and next-service reminders.</summary>
public class ServiceLogService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    PermissionService permissions,
    CurrentUserService currentUser,
    TimeProvider clock)
{
    public DateOnly Today => SouthAfricanTime.Today(clock);

    /// <param name="vehicleId">Only this vehicle's services, or all vehicles when null.</param>
    public async Task<List<ServiceRow>> ListAsync(int? vehicleId = null)
    {
        await permissions.DemandAsync(Permission.ViewRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.ServiceRecords.AsNoTracking();
        if (vehicleId is not null)
            query = query.Where(s => s.VehicleId == vehicleId);

        return await query
            .OrderByDescending(s => s.ServiceDate).ThenByDescending(s => s.Id)
            .Select(s => new ServiceRow(s.Id, s.VehicleId, s.Vehicle.RegistrationNumber, s.ServiceDate, s.OdometerKm, s.WorkType,
                s.Description, s.Workshop, s.Cost, s.Notes, s.NextServiceDueDate, s.CreatedBy))
            .ToListAsync();
    }

    /// <summary>Every vehicle whose most recent service set a next-service date, soonest first.</summary>
    public async Task<List<ServiceReminderRow>> RemindersAsync()
    {
        await permissions.DemandAsync(Permission.ViewRecords);
        await using var db = await dbFactory.CreateDbContextAsync();

        // A small fleet has a few hundred service rows, so the "latest service per vehicle" step is done
        // in C# with the same ServiceRules method the tests cover, rather than a complex SQL query.
        var services = await db.ServiceRecords.AsNoTracking()
            .Select(s => new { s.Id, s.VehicleId, s.ServiceDate, s.OdometerKm, s.NextServiceDueDate, s.Vehicle.RegistrationNumber, s.Vehicle.Make, s.Vehicle.Model })
            .ToListAsync();

        return services
            .GroupBy(s => s.VehicleId)
            .Select(g =>
            {
                var latest = g.OrderByDescending(s => s.ServiceDate).ThenByDescending(s => s.Id).First();
                return latest.NextServiceDueDate is { } due
                    ? new ServiceReminderRow(g.Key, latest.RegistrationNumber, $"{latest.Make} {latest.Model}", latest.ServiceDate, latest.OdometerKm, due)
                    : null;
            })
            .OfType<ServiceReminderRow>()
            .OrderBy(r => r.NextServiceDue)
            .ToList();
    }

    public async Task<ServiceForm?> GetFormAsync(int id)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.ServiceRecords.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
        return s is null ? null : new ServiceForm
        {
            VehicleId = s.VehicleId,
            ServiceDate = s.ServiceDate.ToDateTime(TimeOnly.MinValue),
            OdometerKm = s.OdometerKm,
            WorkType = s.WorkType,
            Description = s.Description,
            Workshop = s.Workshop,
            Cost = s.Cost,
            Notes = s.Notes,
            NextServiceDueDate = s.NextServiceDueDate?.ToDateTime(TimeOnly.MinValue),
        };
    }

    /// <summary>Logs a service or repair. Staff may do this too.</summary>
    public async Task<OperationResult> CreateAsync(ServiceForm form)
    {
        await permissions.DemandAsync(Permission.AddServiceRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await ValidateAsync(db, form, exceptId: null) is { } error)
            return OperationResult.Fail(error);

        var record = new ServiceRecord
        {
            VehicleId = form.VehicleId!.Value,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = (await currentUser.GetActorAsync()).UserName ?? "unknown",
        };
        Apply(form, record);
        db.ServiceRecords.Add(record);
        await db.SaveChangesAsync();
        return OperationResult.Created(record.Id);
    }

    /// <summary>Correcting a logged service is an admin task.</summary>
    public async Task<OperationResult> UpdateAsync(int id, ServiceForm form)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var record = await db.ServiceRecords.SingleOrDefaultAsync(s => s.Id == id);
        if (record is null)
            return OperationResult.Fail("This service record has been deleted.");

        form.VehicleId = record.VehicleId; // a service can't be moved to another vehicle
        if (await ValidateAsync(db, form, exceptId: id) is { } error)
            return OperationResult.Fail(error);

        Apply(form, record);
        await db.SaveChangesAsync();
        return OperationResult.Ok;
    }

    public async Task<OperationResult> DeleteAsync(int id)
    {
        await permissions.DemandAsync(Permission.DeleteRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var record = await db.ServiceRecords.FindAsync(id);
        if (record is not null)
        {
            db.ServiceRecords.Remove(record);
            await db.SaveChangesAsync();
        }
        return OperationResult.Ok;
    }

    private async Task<string?> ValidateAsync(ApplicationDbContext db, ServiceForm form, int? exceptId)
    {
        if (FormValidator.Validate(form) is { } error)
            return error;
        if (!await db.Vehicles.AnyAsync(v => v.Id == form.VehicleId))
            return "That vehicle no longer exists.";

        var others = await db.ServiceRecords.AsNoTracking()
            .Where(s => s.VehicleId == form.VehicleId && s.Id != exceptId)
            .ToListAsync();
        return ServiceRules.CheckOdometer(form.OdometerKm!.Value, DateOnly.FromDateTime(form.ServiceDate!.Value), others);
    }

    private static void Apply(ServiceForm form, ServiceRecord record)
    {
        record.ServiceDate = DateOnly.FromDateTime(form.ServiceDate!.Value);
        record.OdometerKm = form.OdometerKm!.Value;
        record.WorkType = form.WorkType;
        record.Description = form.Description.Trim();
        record.Workshop = form.Workshop.Trim();
        record.Cost = Math.Round(form.Cost!.Value, 2);
        record.Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim();
        record.NextServiceDueDate = form.NextServiceDueDate is { } next ? DateOnly.FromDateTime(next) : null;
    }
}
