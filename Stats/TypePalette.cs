using System.Windows.Media;

namespace Pomidoro.Stats;

// A color is bound to the type's index in the list, so it stays with the type when the period
// changes or buttons are toggled. "No type" is always gray.
internal static class TypePalette
{
    private static readonly Brush[] Palette =
    [
        Frozen(0xE5, 0x48, 0x4D),
        Frozen(0x3D, 0xBE, 0x8B),
        Frozen(0x4C, 0x8D, 0xF6),
        Frozen(0xE8, 0xA3, 0x3D),
        Frozen(0xA7, 0x74, 0xE8),
        Frozen(0x2F, 0xB6, 0xC4),
        Frozen(0xD6, 0x5D, 0xB1),
        Frozen(0x7A, 0xC7, 0x4F)
    ];

    private static readonly Brush NoTypeBrush = Frozen(0x6B, 0x70, 0x80);

    public static Brush For(string canonicalType, int index) => canonicalType.Length == 0
        ? NoTypeBrush
        : Palette[index % Palette.Length];

    private static SolidColorBrush Frozen(byte red, byte green, byte blue)
    {
        SolidColorBrush brush = new(Color.FromRgb(red, green, blue));
        brush.Freeze();

        return brush;
    }
}
