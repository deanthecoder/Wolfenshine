// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Wolfenshine.Graphics;

namespace Wolfenshine.Rendering;

/// <summary>
/// Converts wall-column raycast results into an RGBA software framebuffer.
/// </summary>
/// <remarks>
/// Indexed textures resolve through a palette at draw time, while flat colors remain available for diagnostics.
/// </remarks>
public static class SoftwareRaycastRenderer
{
    private static readonly RgbaColor s_ceilingColor = new(45, 48, 55);
    private static readonly RgbaColor s_floorColor = new(61, 57, 53);
    private static readonly RgbaColor s_doorColor = new(188, 142, 70);
    private static readonly RgbaColor[] s_wallColors =
    [
        new(145, 63, 57),
        new(74, 111, 148),
        new(132, 126, 103),
        new(83, 126, 91),
        new(137, 91, 139),
        new(151, 124, 66)
    ];

    public static void Render(IReadOnlyList<WallColumn> columns, int height, Span<byte> pixels)
        => Render(columns, height, pixels, null, null);

    public static void Render(
        IReadOnlyList<WallColumn> columns,
        int height,
        Span<byte> pixels,
        WolfensteinWallTextures wallTextures,
        WolfensteinPalette palette)
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

        var useTextures = wallTextures != null && palette != null;
        // Project each unit-height wall and either sample its indexed page or apply a diagnostic flat color.
        for (var x = 0; x < width; x++)
        {
            var column = columns[x];
            var projectedHeight = height / Math.Max(column.Distance, 0.0001);
            var wallTop = (height - projectedHeight) * 0.5;
            var top = Math.Max(0, (int)Math.Floor(wallTop));
            var bottom = Math.Min(height, (int)Math.Ceiling(wallTop + projectedHeight));
            if (useTextures)
            {
                var texture = wallTextures.GetTexture(column);
                var textureX = Math.Clamp(
                    (int)(column.TextureU * WolfensteinWallTexture.Size),
                    0,
                    WolfensteinWallTexture.Size - 1);
                for (var y = top; y < bottom; y++)
                {
                    var textureV = (y - wallTop) / projectedHeight;
                    var textureY = Math.Clamp(
                        (int)(textureV * WolfensteinWallTexture.Size),
                        0,
                        WolfensteinWallTexture.Size - 1);
                    WritePixel(pixels, width, x, y, palette.GetColor(texture.GetIndex(textureX, textureY)));
                }
                continue;
            }

            var color = GetFlatColor(column);
            for (var y = top; y < bottom; y++)
                WritePixel(pixels, width, x, y, color);
        }
    }

    public static void DrawSprite(
        WolfensteinSprite sprite,
        WolfensteinPalette palette,
        int centerX,
        int bottomY,
        int renderedSize,
        Span<byte> pixels,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(renderedSize);
        var expectedPixelBytes = checked(width * height * 4);
        if (pixels.Length != expectedPixelBytes)
            throw new ArgumentException($"The pixel buffer must contain exactly {expectedPixelBytes} bytes.", nameof(pixels));

        // Scale the sprite's virtual 64 x 64 canvas, clipping transparent posts and off-screen edges.
        var left = centerX - (renderedSize / 2);
        var top = bottomY - renderedSize;
        var firstX = Math.Max(0, left);
        var lastX = Math.Min(width, left + renderedSize);
        var firstY = Math.Max(0, top);
        var lastY = Math.Min(height, top + renderedSize);
        for (var y = firstY; y < lastY; y++)
        {
            var sourceY = ((y - top) * WolfensteinSprite.Size) / renderedSize;
            for (var x = firstX; x < lastX; x++)
            {
                var sourceX = ((x - left) * WolfensteinSprite.Size) / renderedSize;
                if (sprite.TryGetIndex(sourceX, sourceY, out var index))
                    WritePixel(pixels, width, x, y, palette.GetColor(index));
            }
        }
    }

    private static RgbaColor GetFlatColor(WallColumn column)
    {
        var color = column.Tile is >= 90 and <= 101
            ? s_doorColor
            : s_wallColors[column.Tile % s_wallColors.Length];
        return column.Side == WallSide.Horizontal ? color.Scale(0.68) : color;
    }

    private static void WritePixel(Span<byte> pixels, int width, int x, int y, RgbaColor color)
    {
        var offset = ((y * width) + x) * 4;
        pixels[offset] = color.Red;
        pixels[offset + 1] = color.Green;
        pixels[offset + 2] = color.Blue;
        pixels[offset + 3] = color.Alpha;
    }
}
