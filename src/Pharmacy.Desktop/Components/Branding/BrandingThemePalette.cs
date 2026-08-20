using System.Globalization;

namespace Pharmacy.Desktop.Components.Branding;

public sealed record BrandingThemePalette(
    string Primary,
    string PrimaryDark,
    string PrimaryActive,
    string PrimarySoft,
    string PrimaryBorder,
    string OnPrimary,
    string Focus)
{
    private const string DarkText = "#111827";
    private const string LightText = "#FFFFFF";

    public static BrandingThemePalette FromPrimary(string primary)
    {
        var color = RgbColor.Parse(primary);
        var primaryDark = color.Mix(RgbColor.Black, 0.20);
        var primaryActive = color.Mix(RgbColor.Black, 0.34);
        var primarySoft = color.Mix(RgbColor.White, 0.92);
        var primaryBorder = color.Mix(RgbColor.White, 0.55);
        var onPrimary = Contrast(color, RgbColor.Parse(LightText)) >= Contrast(color, RgbColor.Parse(DarkText))
            ? LightText
            : DarkText;
        var focus = color;
        while (Contrast(focus, RgbColor.White) < 3 && !focus.Equals(RgbColor.Black))
            focus = focus.Mix(RgbColor.Black, 0.10);

        return new BrandingThemePalette(
            color.ToHex(),
            primaryDark.ToHex(),
            primaryActive.ToHex(),
            primarySoft.ToHex(),
            primaryBorder.ToHex(),
            onPrimary,
            focus.ToHex());
    }

    private static double Contrast(RgbColor left, RgbColor right)
    {
        var brighter = Math.Max(left.Luminance, right.Luminance);
        var darker = Math.Min(left.Luminance, right.Luminance);
        return (brighter + 0.05) / (darker + 0.05);
    }

    private readonly record struct RgbColor(byte Red, byte Green, byte Blue)
    {
        public static RgbColor Black { get; } = new(0, 0, 0);
        public static RgbColor White { get; } = new(255, 255, 255);

        public double Luminance => 0.2126 * Linear(Red) + 0.7152 * Linear(Green) + 0.0722 * Linear(Blue);

        public static RgbColor Parse(string value) => new(
            byte.Parse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));

        public RgbColor Mix(RgbColor other, double otherWeight) => new(
            MixChannel(Red, other.Red, otherWeight),
            MixChannel(Green, other.Green, otherWeight),
            MixChannel(Blue, other.Blue, otherWeight));

        public string ToHex() => $"#{Red:X2}{Green:X2}{Blue:X2}";

        private static byte MixChannel(byte first, byte second, double secondWeight) =>
            (byte)Math.Round(first * (1 - secondWeight) + second * secondWeight, MidpointRounding.AwayFromZero);

        private static double Linear(byte channel)
        {
            var normalized = channel / 255d;
            return normalized <= 0.04045
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }
    }
}
