using FleetTrack.Core.Entities;
using FleetTrack.Core.Rules;
using FleetTrack.Core.Security;
using FleetTrack.Web.Data;
using FleetTrack.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace FleetTrack.Web.Services;

public record CategoryRow(int Id, string Name, string? Description, bool IsActive, int SortOrder, int VehicleCount);
public record DriverRow(int Id, string FullName, string? Phone, string? Email, string? DriverLicenceNumber, bool IsActive, IReadOnlyList<string> Vehicles)
{
    public int VehicleCount => Vehicles.Count;
}
public record LicenceHolderRow(int Id, HolderType HolderType, string Name, string IdentifierMasked, int VehicleCount);
public record Option(int Id, string Label, bool IsActive = true);

/// <summary>The lists a vehicle refers to: categories, drivers and licence holders.</summary>
public class ReferenceDataService(IDbContextFactory<ApplicationDbContext> dbFactory, PermissionService permissions)
{
    // ----- Options for drop-downs -----

    public async Task<List<Option>> CategoryOptionsAsync()
    {
        await permissions.DemandAsync(Permission.ViewRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.VehicleCategories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new Option(c.Id, c.Name, c.IsActive)).ToListAsync();
    }

    public async Task<List<Option>> DriverOptionsAsync()
    {
        await permissions.DemandAsync(Permission.ViewRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Drivers.AsNoTracking().OrderBy(d => d.FullName)
            .Select(d => new Option(d.Id, d.FullName, d.IsActive)).ToListAsync();
    }

    public async Task<List<Option>> LicenceHolderOptionsAsync()
    {
        await permissions.DemandAsync(Permission.ViewRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.LicenceHolders.AsNoTracking().OrderBy(h => h.Name)
            .Select(h => new Option(h.Id, h.Name, true)).ToListAsync();
    }

    // ----- Categories -----

    public async Task<List<CategoryRow>> ListCategoriesAsync()
    {
        await permissions.DemandAsync(Permission.ViewRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.VehicleCategories.AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new CategoryRow(c.Id, c.Name, c.Description, c.IsActive, c.SortOrder, c.Vehicles.Count))
            .ToListAsync();
    }

    public async Task<OperationResult> SaveCategoryAsync(int? id, CategoryForm form)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        if (FormValidator.Validate(form) is { } error)
            return OperationResult.Fail(error);

        await using var db = await dbFactory.CreateDbContextAsync();
        var name = form.Name.Trim();
        if (await db.VehicleCategories.AnyAsync(c => c.Name.ToUpper() == name.ToUpper() && c.Id != id))
            return OperationResult.Fail($"There is already a category called \"{name}\".");

        var category = id is null ? new VehicleCategory() : await db.VehicleCategories.FindAsync(id.Value);
        if (category is null)
            return OperationResult.Fail("This category has been deleted.");
        category.Name = name;
        category.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
        category.IsActive = form.IsActive;
        category.SortOrder = form.SortOrder;
        if (id is null)
            db.VehicleCategories.Add(category);
        await db.SaveChangesAsync();
        return OperationResult.Created(category.Id);
    }

    public async Task<OperationResult> DeleteCategoryAsync(int id)
    {
        await permissions.DemandAsync(Permission.DeleteRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.Vehicles.AnyAsync(v => v.CategoryId == id))
            return OperationResult.Fail("Vehicles still use this category. Mark it inactive instead, or move those vehicles first.");

        var category = await db.VehicleCategories.FindAsync(id);
        if (category is not null)
        {
            db.VehicleCategories.Remove(category);
            await db.SaveChangesAsync();
        }
        return OperationResult.Ok;
    }

    // ----- Drivers -----

    public async Task<List<DriverRow>> ListDriversAsync()
    {
        await permissions.DemandAsync(Permission.ViewRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Drivers.AsNoTracking().OrderBy(d => d.FullName)
            .Select(d => new DriverRow(d.Id, d.FullName, d.Phone, d.Email, d.DriverLicenceNumber, d.IsActive,
                d.AssignedVehicles.OrderBy(v => v.RegistrationNumber).Select(v => v.RegistrationNumber).ToList()))
            .ToListAsync();
    }

    public async Task<DriverForm?> GetDriverFormAsync(int id)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var d = await db.Drivers.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id);
        return d is null ? null : new DriverForm
        {
            FullName = d.FullName, Phone = d.Phone, Email = d.Email, DriverLicenceNumber = d.DriverLicenceNumber, IsActive = d.IsActive,
        };
    }

    public async Task<OperationResult> SaveDriverAsync(int? id, DriverForm form)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        if (FormValidator.Validate(form) is { } error)
            return OperationResult.Fail(error);

        await using var db = await dbFactory.CreateDbContextAsync();
        var driver = id is null ? new Driver() : await db.Drivers.FindAsync(id.Value);
        if (driver is null)
            return OperationResult.Fail("This driver has been deleted.");
        driver.FullName = form.FullName.Trim();
        driver.Phone = Blank(form.Phone);
        driver.Email = Blank(form.Email);
        driver.DriverLicenceNumber = Blank(form.DriverLicenceNumber)?.ToUpperInvariant();
        driver.IsActive = form.IsActive;
        if (id is null)
            db.Drivers.Add(driver);
        await db.SaveChangesAsync();
        return OperationResult.Created(driver.Id);
    }

    /// <summary>Deleting a driver unassigns them from their vehicles (the database sets CURRENT_DRIVER_ID to NULL).</summary>
    public async Task<OperationResult> DeleteDriverAsync(int id)
    {
        await permissions.DemandAsync(Permission.DeleteRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var driver = await db.Drivers.Include(d => d.AssignedVehicles).SingleOrDefaultAsync(d => d.Id == id);
        if (driver is null)
            return OperationResult.Ok;

        db.Drivers.Remove(driver);
        await db.SaveChangesAsync();
        return OperationResult.Ok;
    }

    // ----- Licence holders -----

    public async Task<List<LicenceHolderRow>> ListLicenceHoldersAsync()
    {
        await permissions.DemandAsync(Permission.ViewRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var holders = await db.LicenceHolders.AsNoTracking().OrderBy(h => h.Name)
            .Select(h => new { h.Id, h.HolderType, h.Name, h.Identifier, Count = h.Vehicles.Count })
            .ToListAsync();

        // ID numbers are masked in lists; company numbers are public record, so they're shown in full.
        return holders.Select(h => new LicenceHolderRow(h.Id, h.HolderType, h.Name,
            h.HolderType == HolderType.Individual ? IdentityNumbers.Mask(h.Identifier) : h.Identifier, h.Count)).ToList();
    }

    public async Task<LicenceHolderForm?> GetLicenceHolderFormAsync(int id)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var h = await db.LicenceHolders.AsNoTracking().SingleOrDefaultAsync(h => h.Id == id);
        return h is null ? null : new LicenceHolderForm { HolderType = h.HolderType, Name = h.Name, Identifier = h.Identifier };
    }

    public async Task<OperationResult> SaveLicenceHolderAsync(int? id, LicenceHolderForm form)
    {
        await permissions.DemandAsync(Permission.EditRecords);
        if (FormValidator.Validate(form) is { } error)
            return OperationResult.Fail(error);

        var identifier = IdentityNumbers.Clean(form.Identifier);
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.LicenceHolders.AnyAsync(h => h.HolderType == form.HolderType && h.Identifier == identifier && h.Id != id))
            return OperationResult.Fail("A licence holder with that number already exists.");

        var holder = id is null ? new LicenceHolder() : await db.LicenceHolders.FindAsync(id.Value);
        if (holder is null)
            return OperationResult.Fail("This licence holder has been deleted.");
        holder.HolderType = form.HolderType;
        holder.Name = form.Name.Trim();
        holder.Identifier = identifier;
        if (id is null)
            db.LicenceHolders.Add(holder);
        await db.SaveChangesAsync();
        return OperationResult.Created(holder.Id);
    }

    /// <summary>For POPIA requests: a person's details can be deleted once no vehicle is licensed to them.</summary>
    public async Task<OperationResult> DeleteLicenceHolderAsync(int id)
    {
        await permissions.DemandAsync(Permission.DeleteRecords);
        await using var db = await dbFactory.CreateDbContextAsync();
        var holder = await db.LicenceHolders.SingleOrDefaultAsync(h => h.Id == id);
        if (holder is null)
            return OperationResult.Ok;
        if (await db.Vehicles.AnyAsync(v => v.LicenceHolderId == id))
            return OperationResult.Fail("Vehicles are still licensed to this holder. Change or delete those vehicles first.");

        db.LicenceHolders.Remove(holder);
        await db.SaveChangesAsync();
        return OperationResult.Ok;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
