namespace Planora.Shared.Constants;

public static class PlanoraColors
{
    public const string DefaultBoardColor = "#17182B";
    public const string DefaultSurfaceColor = "#FFFFFF";
    public const string DefaultCardColor = "#F5F3FF";
    public const string DefaultColumnColor = "#F5F3FF";
    public const string DefaultLabelColor = "#6D28D9";

    public const string SurfaceTextColor = "#1A1A2E";
    public const string LightTextColor = "#FFFFFF";
    public const double MinimumTextContrast = 4.5;

    public static readonly string[] BoardBackgroundColors =
    [
        "#17182B",
        "#241F47",
        "#2E1065",
        "#4C1D95",
        "#6D28D9",
        "#0E7490",
        "#334155",
        "#475569"
    ];

    public static readonly string[] SurfaceBackgroundColors =
    [
        "#FFFFFF",
        "#F8FAFC",
        "#F5F3FF",
        "#EDE9FE",
        "#EEF2F7",
        "#E2E8F0",
        "#ECFEFF",
        "#CFFAFE"
    ];

    public static readonly string[] LabelBackgroundColors =
    [
        "#6D28D9",
        "#4C1D95",
        "#0E7490",
        "#0891B2",
        "#334155",
        "#475569",
        "#E11D48",
        "#D97706",
        "#16A34A"
    ];

    public static bool TryNormalizeHex(string? color, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(color)) return false;

        var trimmed = color.Trim();
        if (trimmed.Length != 7 || trimmed[0] != '#') return false;

        for (var i = 1; i < trimmed.Length; i++)
        {
            if (!Uri.IsHexDigit(trimmed[i])) return false;
        }

        normalized = trimmed.ToUpperInvariant();
        return true;
    }

    public static bool TryNormalizeSafeBoardBackground(string? color, out string normalized) =>
        TryNormalizeHex(color, out normalized)
        && ContrastRatio(normalized, LightTextColor) >= MinimumTextContrast;

    public static bool TryNormalizeSafeSurfaceBackground(string? color, out string normalized) =>
        TryNormalizeHex(color, out normalized)
        && ContrastRatio(normalized, SurfaceTextColor) >= MinimumTextContrast;

    public static string? SafeBoardBackgroundOrNull(string? color) =>
        TryNormalizeSafeBoardBackground(color, out var normalized) ? normalized : null;

    public static string? SafeSurfaceBackgroundOrNull(string? color) =>
        TryNormalizeSafeSurfaceBackground(color, out var normalized) ? normalized : null;

    public static string NormalizeHexOrDefault(string? color, string fallback) =>
        TryNormalizeHex(color, out var normalized) ? normalized : fallback;

    public static string TextColorFor(string? backgroundColor)
    {
        if (!TryNormalizeHex(backgroundColor, out var normalized))
            return SurfaceTextColor;

        return ContrastRatio(normalized, SurfaceTextColor) >= ContrastRatio(normalized, LightTextColor)
            ? SurfaceTextColor
            : LightTextColor;
    }

    public static double ContrastRatio(string firstHex, string secondHex)
    {
        var first = RelativeLuminance(firstHex) + 0.05;
        var second = RelativeLuminance(secondHex) + 0.05;
        return Math.Max(first, second) / Math.Min(first, second);
    }

    private static double RelativeLuminance(string hex)
    {
        var r = ToLinear(Convert.ToByte(hex.Substring(1, 2), 16));
        var g = ToLinear(Convert.ToByte(hex.Substring(3, 2), 16));
        var b = ToLinear(Convert.ToByte(hex.Substring(5, 2), 16));
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double ToLinear(byte value)
    {
        var channel = value / 255.0;
        return channel <= 0.03928
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }
}
