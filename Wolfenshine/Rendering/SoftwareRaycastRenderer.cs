// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Wolfenshine.Rendering;

/// <summary>
/// Converts wall-column raycast results into an RGBA software framebuffer.
/// </summary>
/// <remarks>
/// This first renderer uses flat colors while preserving the same column data needed for indexed textures later.
/// </remarks>
public static class SoftwareRaycastRenderer
{
    private static readonly RgbColor s_ceilingColor = new(45, 48, 55);
    private static readonly RgbColor s_floorColor = new(61, 57, 53);
    private static readonly RgbColor s_doorColor = new(188, 142, 70);
    private static readonly RgbColor[] s_wallColors =
    [
        new(145, 63, 57),
        new(74, 111, 148),
        new(132, 126, 103),
        new(83, 126, 91),
        new(137, 91, 139),
        new(151, 124, 66)
    ];

    public static void Render(IReadOnlyList<WallColumn> columns, int height, Span<byte> pixels)
    {
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Count == 0)
            throw new ArgumentException("At least one wall column is required.", nameof(columns));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var width = columns.Count;
        var requiredPixelBytes = checked(width * height * 4);
        if (pixels.Length != requiredPixelBytes)
        {
            throw new ArgumentException(
                $"The pixel buffer must contain exactly {requiredPixelBytes} bytes.",
                nameof(pixels));
        }

        // Establish the ceiling and floor first so wall drawing only needs to overwrite its vertical span.
        for (var y = 0; y < height; y++)
        {
            var background = y < height / 2 ? s_ceilingColor : s_floorColor;
            for (var x = 0; x < width; x++)
                WritePixel(pixels, width, x, y, background);
        }

        // Project each unit-height wall from its perpendicular distance and shade its grid orientation.
        for (var x = 0; x < width; x++)
        {
            var column = columns[x];
            var wallHeight = Math.Min(height, (int)Math.Round(height / Math.Max(column.Distance, 0.0001)));
            var top = Math.Max(0, (height - wallHeight) / 2);
            var bottom = Math.Min(height, top + wallHeight);
            var color = column.Tile is >= 90 and <= 101
                ? s_doorColor
                : s_wallColors[column.Tile % s_wallColors.Length];
            if (column.Side == WallSide.Horizontal)
                color = color.Scale(0.68);
            for (var y = top; y < bottom; y++)
                WritePixel(pixels, width, x, y, color);
        }

    }

    private static void WritePixel(Span<byte> pixels, int width, int x, int y, RgbColor color)
    {
        var offset = ((y * width) + x) * 4;
        pixels[offset] = color.Red;
        pixels[offset + 1] = color.Green;
        pixels[offset + 2] = color.Blue;
        pixels[offset + 3] = byte.MaxValue;
    }

    private readonly record struct RgbColor(byte Red, byte Green, byte Blue)
    {
        public RgbColor Scale(double amount) => new(
            (byte)(Red * amount),
            (byte)(Green * amount),
            (byte)(Blue * amount));
    }
}
