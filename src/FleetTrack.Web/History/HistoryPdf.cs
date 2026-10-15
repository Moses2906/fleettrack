using FleetTrack.Core.Entities;
using FleetTrack.Core.Rules;
using FleetTrack.Web.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FleetTrack.Web.History;

/// <summary>Turns a <see cref="VehicleHistory"/> into an A4 PDF report.</summary>
public static class HistoryPdf
{
    private const string Primary = "#2563EB";
    private const string Ink = "#111827";
    private const string Muted = "#6B7280";
    private const string Line = "#E5E7EB";
    private const string Stripe = "#F9FAFB";

    static HistoryPdf()
    {
        // QuestPDF is free under its Community licence for organisations with less than USD 1M annual revenue.
        // See README: a larger company needs a commercial licence.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(VehicleHistory history, string generatedBy, DateTime generatedAtLocal)
    {
        var v = history.Vehicle.Row;
        var status = LicenceStatusRules.GetStatus(v.LicenceExpiryDate, history.To);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.6f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Ink).LineHeight(1.35f));

                page.Header().Element(header => ComposeHeader(header, history, generatedBy, generatedAtLocal));
                page.Content().PaddingVertical(14).Column(column =>
                {
                    column.Spacing(16);
                    column.Item().Element(c => ComposeVehicleSummary(c, history, status));
                    column.Item().Element(c => ComposeTotals(c, history));
                    column.Item().Element(c => ComposeTimeline(c, history));
                });
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Confidential: contains personal information. Handle in line with POPIA.")
                        .FontSize(7.5f).FontColor(Muted);
                    row.AutoItem().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Muted));
                        t.Span("Page ");
                        t.CurrentPageNumber();
                        t.Span(" of ");
                        t.TotalPages();
                    });
                });
            });
        })
        .WithMetadata(new DocumentMetadata
        {
            Title = $"Vehicle history {v.RegistrationNumber}",
            Author = "FleetTrack",
            Subject = $"Licence renewals and services {history.From:dd MMM yyyy} to {history.To:dd MMM yyyy}",
        })
        .GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, VehicleHistory history, string generatedBy, DateTime generatedAtLocal)
    {
        container.BorderBottom(1).BorderColor(Line).PaddingBottom(10).Row(row =>
        {
            row.RelativeItem().Row(brand =>
            {
                brand.ConstantItem(26).Height(26).Background(Primary).CornerRadius(6)
                    .AlignCenter().AlignMiddle().Text("F").FontColor(Colors.White).Bold().FontSize(13);
                brand.ConstantItem(8);
                brand.RelativeItem().AlignMiddle().Column(c =>
                {
                    c.Item().Text("FleetTrack").Bold().FontSize(12);
                    c.Item().Text("Vehicle history report").FontColor(Muted).FontSize(8.5f);
                });
            });
            row.AutoItem().AlignRight().Column(c =>
            {
                c.Item().AlignRight().Text(history.Vehicle.Row.RegistrationNumber).Bold().FontSize(16);
                c.Item().AlignRight().Text($"Generated {generatedAtLocal:dd MMM yyyy HH:mm} by {generatedBy}").FontColor(Muted).FontSize(7.5f);
            });
        });
    }

    private static void ComposeVehicleSummary(IContainer container, VehicleHistory history, LicenceStatus status)
    {
        var details = history.Vehicle;
        var v = details.Row;
        var (statusText, statusColour) = status switch
        {
            LicenceStatus.Expired => ("Expired", "#DC2626"),
            LicenceStatus.DueSoon => ("Due within 30 days", "#D97706"),
            _ => ("Valid", "#16A34A"),
        };
        var documents = new[] { ("RC1", v.HasRc1), ("BRNC", v.HasBrnc), ("Proxy letter", v.HasProxyLetter), ("Proxy ID", v.HasProxyId) };

        container.Border(1).BorderColor(Line).CornerRadius(8).Padding(12).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(95);
                columns.RelativeColumn();
                columns.ConstantColumn(95);
                columns.RelativeColumn();
            });

            void Fact(string label, string value)
            {
                table.Cell().PaddingVertical(3).Text(label).FontColor(Muted);
                table.Cell().PaddingVertical(3).PaddingRight(8).Text(value);
            }

            Fact("Make & model", v.MakeAndModel + (v.Year is null ? "" : $" ({v.Year})"));
            Fact("Category", v.Category);
            Fact("VIN", details.Vin ?? "—");
            Fact("Current driver", v.Driver ?? "Unassigned");
            // Only the masked ID number goes into the report.
            Fact("Licensed to", v.LicenceHolder);
            Fact(details.HolderType == HolderType.Company ? "Company reg. no." : "ID number", details.HolderIdentifierForDisplay);

            table.Cell().PaddingVertical(3).Text("Disk expires").FontColor(Muted);
            table.Cell().PaddingVertical(3).Text(t =>
            {
                t.Span($"{v.LicenceExpiryDate:dd MMM yyyy}  ");
                t.Span(statusText).FontColor(statusColour).SemiBold();
            });
            table.Cell().PaddingVertical(3).Text("Documents").FontColor(Muted);
            // Small coloured labels; missing ones also say "missing", so the meaning doesn't rely on colour alone.
            // Inlined wraps the labels onto a second line when they don't fit, like words in a sentence.
            table.Cell().PaddingVertical(2).Inlined(labels =>
            {
                labels.Spacing(4);
                foreach (var (name, held) in documents)
                {
                    labels.Item().Background(held ? "#DCFCE7" : "#FEE2E2").CornerRadius(4)
                        .PaddingHorizontal(5).PaddingVertical(1)
                        .Text(held ? name : $"{name} missing").FontSize(8).FontColor(held ? "#15803D" : "#B91C1C");
                }
            });
        });
    }

    private static void ComposeTotals(IContainer container, VehicleHistory history)
    {
        container.Row(row =>
        {
            row.Spacing(10);
            Stat(row.RelativeItem(), "Period", $"{history.From:dd MMM yyyy} – {history.To:dd MMM yyyy}");
            Stat(row.RelativeItem(), $"Licence renewals ({history.RenewalCount})", DisplayNames.Money(history.RenewalTotal));
            Stat(row.RelativeItem(), $"Services & repairs ({history.ServiceCount})", DisplayNames.Money(history.ServiceTotal));
        });

        static void Stat(IContainer c, string label, string value) =>
            c.Background(Stripe).CornerRadius(8).Padding(10).Column(col =>
            {
                col.Item().Text(label).FontColor(Muted).FontSize(8);
                col.Item().Text(value).SemiBold().FontSize(11);
            });
    }

    private static void ComposeTimeline(IContainer container, VehicleHistory history)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(6).Text("Timeline (newest first)").Bold().FontSize(11);

            if (history.Entries.Count == 0)
            {
                column.Item().Text("No licence renewals or services were recorded in this period.").FontColor(Muted);
                return;
            }

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(62);   // date
                    columns.ConstantColumn(58);   // type
                    columns.RelativeColumn();     // details
                    columns.ConstantColumn(62);   // odometer
                    columns.ConstantColumn(68);   // amount
                });

                table.Header(header =>
                {
                    foreach (var (text, right) in new[] { ("Date", false), ("Type", false), ("Details", false), ("Odometer", true), ("Amount", true) })
                    {
                        var cell = header.Cell().BorderBottom(1).BorderColor(Line).PaddingVertical(5);
                        (right ? cell.AlignRight() : cell).Text(text).FontColor(Muted).SemiBold().FontSize(8.5f);
                    }
                });

                var index = 0;
                foreach (var entry in history.Entries)
                {
                    var background = index++ % 2 == 1 ? Stripe : "#FFFFFF";
                    IContainer Cell() => table.Cell().Background(background).BorderBottom(0.5f).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(3);

                    Cell().Text(entry.Date.ToString("dd MMM yyyy"));
                    Cell().Text(entry.Kind == HistoryEntryKind.Renewal ? "Renewal" : "Service")
                        .FontColor(entry.Kind == HistoryEntryKind.Renewal ? Primary : "#0F766E").SemiBold();
                    Cell().Column(details =>
                    {
                        details.Item().Text(entry.Title).SemiBold();
                        details.Item().Text(entry.Summary).FontColor(Muted);
                        foreach (var followUp in entry.FollowUps)
                            details.Item().Text($"• {followUp}").FontSize(8.5f).FontColor(Muted);
                        if (entry.Notes is not null)
                            details.Item().Text(entry.Notes).Italic().FontSize(8.5f).FontColor(Muted);
                    });
                    Cell().AlignRight().Text(entry.OdometerKm is { } km ? $"{km:#,##0} km" : "");
                    Cell().AlignRight().Text(DisplayNames.Money(entry.Amount));
                }
            });
        });
    }
}
