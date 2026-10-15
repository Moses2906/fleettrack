using System.Text.Json;
using FleetTrack.Core.Entities;
using FleetTrack.Core.Rules;
using FleetTrack.Core.Security;
using FleetTrack.Web.Security;
using FleetTrack.Web.Services;

namespace FleetTrack.Web.History;

public static class HistoryEndpoints
{
    /// <summary>GET /history/{vehicleId}/pdf?years=5 downloads the vehicle's history as a PDF.</summary>
    public static void MapHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/history/{vehicleId:int}/pdf", async (int vehicleId, int? years, HttpContext context,
            HistoryService history, CurrentUserService currentUser, AuditService audit, TimeProvider clock) =>
        {
            var report = await history.GetAsync(vehicleId, years ?? HistoryService.DefaultYears);
            if (report is null)
                return Results.NotFound();

            var user = await currentUser.GetPrincipalAsync();
            var now = SouthAfricanTime.ToLocal(clock.GetUtcNow().UtcDateTime);
            var pdf = HistoryPdf.Generate(report, user.DisplayName(), now);

            // The report names the licence holder and driver, so exporting it is recorded like viewing a document.
            await audit.LogAsync(AuditAction.Export, nameof(Vehicle), vehicleId.ToString(),
                JsonSerializer.Serialize(new { Report = "VehicleHistory", Years = years ?? HistoryService.DefaultYears }));

            context.Response.Headers.CacheControl = "no-store, private";
            var fileName = $"FleetTrack-history-{RegistrationNumbers.ToKey(report.Vehicle.Row.RegistrationNumber)}-{now:yyyy-MM-dd}.pdf";
            return Results.File(pdf, "application/pdf", fileName);
        })
        .RequireAuthorization(nameof(Permission.ViewRecords));
    }
}
