using MudBlazor;

namespace FleetTrack.Web.Components.Layout;

/// <summary>Colours, font and corner rounding for every MudBlazor component.</summary>
public static class FleetTrackTheme
{
    private static readonly string[] Font = ["Inter", "system-ui", "-apple-system", "Segoe UI", "Roboto", "sans-serif"];

    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#2563EB",
            Secondary = "#64748B",
            Success = "#16A34A",
            Warning = "#D97706",
            Error = "#DC2626",
            Background = "#F5F7FA",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#111827",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#374151",
            DrawerIcon = "#6B7280",
            TextPrimary = "#111827",
            TextSecondary = "#6B7280",
            LinesDefault = "#E5E7EB",
            TableLines = "#EEF0F3",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#60A5FA",
            Secondary = "#94A3B8",
            Success = "#4ADE80",
            Warning = "#FBBF24",
            Error = "#F87171",
            Background = "#0F1115",
            Surface = "#171A21",
            AppbarBackground = "#171A21",
            AppbarText = "#E5E7EB",
            DrawerBackground = "#171A21",
            DrawerText = "#D1D5DB",
            DrawerIcon = "#9CA3AF",
            TextPrimary = "#E5E7EB",
            TextSecondary = "#9CA3AF",
            LinesDefault = "#262A33",
            TableLines = "#232730",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = Font, LetterSpacing = "normal" },
            H4 = new H4Typography { FontFamily = Font, FontWeight = "600", LetterSpacing = "-0.02em" },
            H5 = new H5Typography { FontFamily = Font, FontWeight = "600", LetterSpacing = "-0.01em" },
            H6 = new H6Typography { FontFamily = Font, FontWeight = "600", LetterSpacing = "-0.01em" },
            Button = new ButtonTypography { FontFamily = Font, FontWeight = "500", TextTransform = "none", LetterSpacing = "normal" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "248px",
        },
    };
}
