using FleetTrack.Core.Entities;
using FleetTrack.Core.Rules;
using FleetTrack.Core.Security;
using FleetTrack.Web.Data;
using FleetTrack.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Web.Services;

/// <summary>One row of the vehicles table.</summary>
public record VehicleRow(
    int Id,
    string RegistrationNumber,
    string Make,
    string Model,
    int? Year,
    int CategoryId,
    string Category,
    DateOnly LicenceExpiryDate,
    int LicenceHolderId,
    string LicenceHolder,
    int? DriverId,
    string? Driver,
    bool HasRc1,
    bool HasBrnc,
    bool HasProxyLetter,
    bool HasProxyId)
{
    public string MakeAndModel => $"{Make} {Model}";
    public int MissingDocumentCount => (HasRc1 ? 0 : 1) + (HasBrnc ? 0 : 1) + (HasProxyLetter ? 0 : 1) + (HasProxyId ? 0 : 1);
    public bool HasAllDocuments => MissingDocumentCount == 0;
    public LicenceStatus StatusOn(DateOnly today) => LicenceStatusRules.GetStatus(LicenceExpiryDate, today);
}

public record VehicleDetails(
    VehicleRow Row,
    string? Vin,
    HolderType HolderType,
    string HolderIdentifierForDisplay,
    string? DriverPhone,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record DashboardSummary(
    int TotalVehicles,
    int Expired,
    int DueSoon,
    int MissingDocuments,
    IReadOnlyList<VehicleRow> LicenceAttention,
    IReadOnlyList<VehicleRow> DocumentAttention);

public class VehicleService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    PermissionService permissions,
    TimeProvider clock)
{
    public DateOnly Today => SouthAfricanTime.Today(clock);

    public async Task<List<VehicleRow>> ListAsync()
    {
        await permissions.DemandAsync(Permission.ViewRecords);
        await using var db = await dbFactory.CreateDbContextAsync();

        // Select only the columns the table needs. EF Core turns this into one SQL query with JOINs.
        return await db.Vehicles
            .AsNoTracking()
            .OrderBy(v => v.RegistrationNumber)
            .Select(ToRow)
            .ToListAsync();
    }

    public async Task<VehicleDetails?> GetAsync(int id)
    {
        await permissions.DemandAsync(Permission.ViewRecords);
        await using var db = await dbFactory.CreateDbContextAsync();

        var row = await db.Vehicles.AsNoTracking().Where(v => v.Id == id).Select(ToRow).SingleOrDefaultAsync();
        if (row is null)
            return null;

        var extra = await db.Vehicles.AsNoTracking().Where(v => v.Id == id)
            .Select(v => new
            {
                v.Vin, v.Notes, v.CreatedAtUtc, v.UpdatedAtUtc,
                v.LicenceHolder.HolderType, v.LicenceHolder.Identifier,
                DriverPhone = v.CurrentDriver == null ? null : v.CurrentDriver.Phone,
            })
            .SingleAsync();

        // Company numbers are public record; only a person's ID number is masked.
        var identifier = extra.HolderType == HolderType.Individual ? IdentityNumbers.Mask(extra.Identifier) : extra.Identifier;
        return new VehicleDetails(row, extra.Vin, extra.HolderType, identifier,
            extra.DriverPhone, extra.Notes, extra.CreatedAtUtc, extra.UpdatedAtUtc);
    }

    public async Task<VehicleForm?> GetFormAsync(int id)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var v = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id);
        return v is null ? null : new VehicleForm
        {
            RegistrationNumber = v.RegistrationNumber,
            Make = v.Make,
            Model = v.Model,
            Year = v.Year,
            Vin = v.Vin,
            CategoryId = v.CategoryId,
            LicenceHolderId = v.LicenceHolderId,
            CurrentDriverId = v.CurrentDriverId,
            LicenceExpiryDate = v.LicenceExpiryDate.ToDateTime(TimeOnly.MinValue),
            HasRc1 = v.HasRc1,
            HasBrnc = v.HasBrnc,
            HasProxyLetter = v.HasProxyLetter,
            HasProxyId = v.HasProxyId,
            Notes = v.Notes,
            ConcurrencyStamp = v.ConcurrencyStamp,
        };
    }

    public async Task<OperationResult> CreateAsync(VehicleForm form)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        if (FormValidator.Validate(form) is { } error)
            return OperationResult.Fail(error);

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await CheckReferencesAsync(db, form, isNew: true) is { } referenceError)
            return OperationResult.Fail(referenceError);
        if (await RegistrationTakenAsync(db, form.RegistrationNumber, exceptId: null))
            return OperationResult.Fail($"A vehicle with registration {RegistrationNumbers.Normalise(form.RegistrationNumber)} already exists.");

        var vehicle = new Vehicle();
        Apply(form, vehicle);
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        return OperationResult.Created(vehicle.Id);
    }

    public async Task<OperationResult> UpdateAsync(int id, VehicleForm form)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        if (FormValidator.Validate(form) is { } error)
            return OperationResult.Fail(error);

        await using var db = await dbFactory.CreateDbContextAsync();
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == id);
        if (vehicle is null)
            return OperationResult.Fail("This vehicle has been deleted.");
        if (await CheckReferencesAsync(db, form, isNew: false) is { } referenceError)
            return OperationResult.Fail(referenceError);
        if (await RegistrationTakenAsync(db, form.RegistrationNumber, exceptId: id))
            return OperationResult.Fail($"Another vehicle already has registration {RegistrationNumbers.Normalise(form.RegistrationNumber)}.");

        // Tell EF which version the user started editing. If the row has changed since, the UPDATE
        // matches no rows and EF throws DbUpdateConcurrencyException instead of overwriting.
        db.Entry(vehicle).Property(v => v.ConcurrencyStamp).OriginalValue = form.ConcurrencyStamp ?? "";
        Apply(form, vehicle);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult.Fail("Someone else changed this vehicle while you were editing. Close this form, then open it again to see their changes.");
        }
        return OperationResult.Ok;
    }

    /// <summary>Ticks or unticks one checklist item.</summary>
    public async Task<OperationResult> SetDocumentHeldAsync(int id, DocumentType type, bool held)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == id);
        if (vehicle is null)
            return OperationResult.Fail("This vehicle has been deleted.");

        vehicle.SetDocumentHeld(type, held);
        await db.SaveChangesAsync();
        return OperationResult.Ok;
    }

    /// <summary>
    /// Permanently deletes the vehicle and, through the database's cascade rules, its documents,
    /// services and renewals. Used for POPIA deletion requests; the audit log keeps the fact of deletion.
    /// </summary>
    public async Task<OperationResult> DeleteAsync(int id)
    {
        await permissions.DemandAsync(Permission.DeleteRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(v => v.Id == id);
        if (vehicle is null)
            return OperationResult.Ok;

        db.Vehicles.Remove(vehicle);
        await db.SaveChangesAsync();
        return OperationResult.Ok;
    }

    public async Task<DashboardSummary> GetDashboardAsync()
    {
        await permissions.DemandAsync(Permission.ViewRecords);
        var today = Today;
        var dueLimit = today.AddDays(LicenceStatusRules.DueSoonDays);

        await using var db = await dbFactory.CreateDbContextAsync();
        var vehicles = db.Vehicles.AsNoTracking();

        // Each count is a single SELECT COUNT(*) ... WHERE in Oracle. Dates are compared as parameters,
        // so the same LicenceStatusRules boundaries apply in SQL and in C#.
        var total = await vehicles.CountAsync();
        var expired = await vehicles.CountAsync(v => v.LicenceExpiryDate < today);
        var dueSoon = await vehicles.CountAsync(v => v.LicenceExpiryDate >= today && v.LicenceExpiryDate <= dueLimit);
        var missingDocs = await vehicles.CountAsync(v => !v.HasRc1 || !v.HasBrnc || !v.HasProxyLetter || !v.HasProxyId);

        var licenceAttention = await vehicles
            .Where(v => v.LicenceExpiryDate <= dueLimit)
            .OrderBy(v => v.LicenceExpiryDate)
            .Select(ToRow)
            .Take(10)
            .ToListAsync();

        var documentAttention = await vehicles
            .Where(v => !v.HasRc1 || !v.HasBrnc || !v.HasProxyLetter || !v.HasProxyId)
            .OrderBy(v => v.RegistrationNumber)
            .Select(ToRow)
            .Take(10)
            .ToListAsync();

        return new DashboardSummary(total, expired, dueSoon, missingDocs, licenceAttention, documentAttention);
    }

    // An expression (not a method) so EF Core can translate it into the SQL SELECT list.
    private static readonly System.Linq.Expressions.Expression<Func<Vehicle, VehicleRow>> ToRow = v => new VehicleRow(
        v.Id, v.RegistrationNumber, v.Make, v.Model, v.Year,
        v.CategoryId, v.Category.Name,
        v.LicenceExpiryDate,
        v.LicenceHolderId, v.LicenceHolder.Name,
        v.CurrentDriverId, v.CurrentDriver == null ? null : v.CurrentDriver.FullName,
        v.HasRc1, v.HasBrnc, v.HasProxyLetter, v.HasProxyId);

    private static void Apply(VehicleForm form, Vehicle vehicle)
    {
        vehicle.RegistrationNumber = form.RegistrationNumber; // normalised by the entity's setter
        vehicle.Make = form.Make.Trim();
        vehicle.Model = form.Model.Trim();
        vehicle.Year = form.Year;
        vehicle.Vin = string.IsNullOrWhiteSpace(form.Vin) ? null : form.Vin.Trim().ToUpperInvariant();
        vehicle.CategoryId = form.CategoryId!.Value;
        vehicle.LicenceHolderId = form.LicenceHolderId!.Value;
        vehicle.CurrentDriverId = form.CurrentDriverId;
        vehicle.LicenceExpiryDate = DateOnly.FromDateTime(form.LicenceExpiryDate!.Value);
        vehicle.HasRc1 = form.HasRc1;
        vehicle.HasBrnc = form.HasBrnc;
        vehicle.HasProxyLetter = form.HasProxyLetter;
        vehicle.HasProxyId = form.HasProxyId;
        vehicle.Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim();
    }

    private static Task<bool> RegistrationTakenAsync(ApplicationDbContext db, string registration, int? exceptId)
    {
        var key = RegistrationNumbers.ToKey(registration);
        return db.Vehicles.AnyAsync(v => v.RegistrationKey == key && v.Id != exceptId);
    }

    /// <summary>The IDs come from the browser, so check they point at real rows before saving.</summary>
    private static async Task<string?> CheckReferencesAsync(ApplicationDbContext db, VehicleForm form, bool isNew)
    {
        var category = await db.VehicleCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == form.CategoryId);
        if (category is null || (isNew && !category.IsActive))
            return "Choose an active category.";
        if (!await db.LicenceHolders.AnyAsync(h => h.Id == form.LicenceHolderId))
            return "That licence holder no longer exists.";
        if (form.CurrentDriverId is { } driverId && !await db.Drivers.AnyAsync(d => d.Id == driverId))
            return "That driver no longer exists.";
        return null;
    }
}
