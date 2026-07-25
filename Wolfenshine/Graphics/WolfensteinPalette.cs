// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Wolfenshine.Graphics;

/// <summary>
/// Resolves the original 8-bit image indices into renderer-independent colors.
/// </summary>
/// <remarks>
/// Keeping palette lookup behind this API permits later packed, shifted, or GPU-backed implementations.
/// </remarks>
public sealed class WolfensteinPalette
{
    public const int ColorCount = 256;
    public const int VgaDataLength = ColorCount * 3;
    private readonly RgbaColor[] m_colors;

    private WolfensteinPalette(RgbaColor[] colors) => m_colors = colors;

    public RgbaColor GetColor(byte index) => m_colors[index];

    public static WolfensteinPalette FromVgaDac(ReadOnlySpan<byte> data)
    {
        if (data.Length != VgaDataLength)
        {
            throw new ArgumentException(
                $"A VGA palette must contain exactly {VgaDataLength} bytes.",
                nameof(data));
        }

        var colors = new RgbaColor[ColorCount];
        for (var index = 0; index < colors.Length; index++)
        {
            var offset = index * 3;
            colors[index] = new RgbaColor(
                ExpandVgaChannel(data[offset]),
                ExpandVgaChannel(data[offset + 1]),
                ExpandVgaChannel(data[offset + 2]));
        }

        return new WolfensteinPalette(colors);
    }

    private static byte ExpandVgaChannel(byte value)
    {
        if (value > 63)
            throw new InvalidDataException($"VGA palette channel {value} exceeds the six-bit range.");
        return (byte)((value << 2) | (value >> 4));
    }
}
