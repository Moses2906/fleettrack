using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace FleetTrack.Web.Services;

/// <summary>
/// The result of a create/update/delete: success (with the new record's ID if one was created),
/// or a message that is safe to show the user. Expected problems such as "registration already exists"
/// come back as results rather than exceptions, so pages can simply display them.
/// </summary>
public record OperationResult(bool Succeeded, string? Error = null, int? Id = null)
{
    public static readonly OperationResult Ok = new(true);
    public static OperationResult Created(int id) => new(true, Id: id);
    public static OperationResult Fail(string error) => new(false, error);
    public static OperationResult From(IdentityResult result) =>
        result.Succeeded ? Ok : Fail(string.Join(" ", result.Errors.Select(e => e.Description)));
}

public static class FormValidator
{
    /// <summary>
    /// Runs the same [Required], [StringLength] etc. rules the form shows in the browser, but on the server.
    /// Returns null when valid, otherwise the error messages.
    /// </summary>
    public static string? Validate(object form)
    {
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true)
            ? null
            : string.Join(" ", results.Select(r => r.ErrorMessage));
    }
}
