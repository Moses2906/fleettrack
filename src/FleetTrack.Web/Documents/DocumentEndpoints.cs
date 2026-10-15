using FleetTrack.Core.Security;
using Microsoft.Net.Http.Headers;

namespace FleetTrack.Web.Documents;

public static class DocumentEndpoints
{
    /// <summary>
    /// GET /documents/{id}            opens the file in the browser (to read or print)
    /// GET /documents/{id}?download=1 saves it as a file
    ///
    /// Files are only ever served through here, after the ViewDocuments permission check,
    /// and every view or download is written to the audit log.
    /// </summary>
    public static void MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/documents/{id:int}", async (int id, bool? download, HttpContext context, DocumentService documents) =>
        {
            var file = await documents.OpenAsync(id, download == true, context.RequestAborted);
            if (file is null)
                return Results.NotFound();

            var headers = context.Response.Headers;
            // ID copies must not be kept in the browser's or a proxy's cache.
            headers.CacheControl = "no-store, private";
            // A locked-down policy just for the file. It still allows the browser's built-in PDF viewer.
            headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; object-src 'self'; frame-ancestors 'self'";

            if (download == true)
                return Results.File(file.Content, file.ContentType, fileDownloadName: file.FileName); // "attachment": save

            // "inline": show in the browser tab (to read or print), still under the original file name.
            SetInlineFileName(context.Response, file.FileName);
            return Results.File(file.Content, file.ContentType);
        })
        .RequireAuthorization(nameof(Permission.ViewDocuments));

        // GET /invoices/{serviceId}  (and ?download=true): a service's invoice, for anyone who can see the service log.
        endpoints.MapGet("/invoices/{serviceId:int}", async (int serviceId, bool? download, HttpContext context, FleetTrack.Web.Services.ServiceLogService services) =>
        {
            var file = await services.OpenInvoiceAsync(serviceId, download == true);
            if (file is null)
                return Results.NotFound();

            context.Response.Headers.CacheControl = "no-store, private";
            context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; object-src 'self'; frame-ancestors 'self'";
            if (download == true)
                return Results.File(file.Content, file.ContentType, fileDownloadName: file.FileName);
            SetInlineFileName(context.Response, file.FileName);
            return Results.File(file.Content, file.ContentType);
        })
        .RequireAuthorization(nameof(Permission.ViewRecords));
    }

    /// <summary>
    /// Names the file without forcing a download. The header class encodes the name safely,
    /// so unusual characters in an uploaded file name can't break the response.
    /// </summary>
    private static void SetInlineFileName(HttpResponse response, string fileName) =>
        response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline") { FileNameStar = fileName }.ToString();
}
