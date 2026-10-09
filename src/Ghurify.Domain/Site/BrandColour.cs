using System.Globalization;

namespace Ghurify.Domain.Site;

/// <summary>
/// A colour the site is drawn with, as <c>#rrggbb</c>.
///
/// Only six-digit hex is accepted. Not because three-digit or <c>rgb()</c> could not be parsed,
/// but because these values are written into a stylesheet at runtime: the narrowest possible
/// shape is the one that cannot carry anything else into the page.
/// </summary>
public readonly record struct BrandColour
{
    private BrandColour(byte red, byte green, byte blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }

    public byte Red { get; }

    public byte Green { get; }

    public byte Blue { get; }

    /// <summary>Lower-case <c>#rrggbb</c>, the one form everything stores and compares.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"#{Red:x2}{Green:x2}{Blue:x2}");

    public static bool TryParse(string? text, out BrandColour colour)
    {
        colour = default;

        if (text is null)
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length != 7 || trimmed[0] != '#')
        {
            return false;
        }

        var digits = trimmed.AsSpan(1);
        foreach (var character in digits)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        colour = new BrandColour(
            byte.Parse(digits[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(digits[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(digits[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));

        return true;
    }

    public static BrandColour White => new(255, 255, 255);

    /// <summary>
    /// Relative luminance as WCAG 2.1 defines it: each channel scaled to 0-1, un-gamma'd, then
    /// weighted for how bright the eye finds that channel.
    /// </summary>
    public double Luminance()
    {
        return (0.2126 * Channel(Red)) + (0.7152 * Channel(Green)) + (0.0722 * Channel(Blue));

        static double Channel(byte value)
        {
            var scaled = value / 255.0;
            return scaled <= 0.03928 ? scaled / 12.92 : Math.Pow((scaled + 0.055) / 1.055, 2.4);
        }
    }

    /// <summary>
    /// The WCAG contrast ratio between two colours, from 1 (identical) to 21 (black on white).
    /// AA wants 4.5 for ordinary text and 3 for large text and for the edges of controls.
    /// </summary>
    public static double Contrast(BrandColour one, BrandColour other)
    {
        var first = one.Luminance();
        var second = other.Luminance();
        var lighter = Math.Max(first, second);
        var darker = Math.Min(first, second);

        return (lighter + 0.05) / (darker + 0.05);
    }
}
