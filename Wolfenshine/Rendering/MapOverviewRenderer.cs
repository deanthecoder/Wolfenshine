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
using Wolfenshine.Maps;

namespace Wolfenshine.Rendering;

/// <summary>
/// Builds a compact, textured overview of a Wolfenstein level.
/// </summary>
/// <remarks>
/// The generated map is static so the debug window can draw the changing player marker as a cheap overlay.
/// </remarks>
public static class MapOverviewRenderer
{
    public const int TileSize = 8;
    private const ushort VerticalElevatorDoorTile = 100;
    private const ushort HorizontalElevatorDoorTile = 101;
    private const ushort PushableMarker = 98;
    private const ushort VictoryMarker = 99;
    private static readonly RgbaColor s_secretColor = new(255, 64, 192);
    private static readonly RgbaColor s_exitColor = new(64, 240, 96);

    public static void Render(
        WolfensteinMap map,
        WolfensteinWallTextures wallTextures,
        WolfensteinPalette palette,
        Span<byte> pixels)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(wallTextures);
        ArgumentNullException.ThrowIfNull(palette);
        var width = map.Width * TileSize;
        var height = map.Height * TileSize;
        if (pixels.Length != width * height * 4)
            throw new ArgumentException("The map buffer must contain exactly width x height RGBA pixels.", nameof(pixels));
        pixels.Clear();
        for (var alpha = 3; alpha < pixels.Length; alpha += 4)
            pixels[alpha] = byte.MaxValue;

        for (var tileY = 0; tileY < map.Height; tileY++)
        {
            for (var tileX = 0; tileX < map.Width; tileX++)
                DrawTile(map, wallTextures, palette, pixels, width, tileX, tileY);
        }
    }

    private static void DrawTile(
        WolfensteinMap map,
        WolfensteinWallTextures wallTextures,
        WolfensteinPalette palette,
        Span<byte> pixels,
        int width,
        int tileX,
        int tileY)
    {
        var tile = map.GetWall(tileX, tileY);
        if (map.IsSolid(tileX, tileY) && BordersWalkableCell(map, tileX, tileY))
        {
            var texture = wallTextures.GetTexture(new WallColumn(0.0, 0.0, tile, WallSide.Horizontal));
            for (var y = 0; y < TileSize; y++)
            {
                for (var x = 0; x < TileSize; x++)
                {
                    var color = palette.GetColor(texture.GetIndex((x * 8) + 4, (y * 8) + 4));
                    SetPixel(pixels, width, (tileX * TileSize) + x, (tileY * TileSize) + y, color);
                }
            }
        }

        var marker = map.GetObject(tileX, tileY);
        if (tile is VerticalElevatorDoorTile or HorizontalElevatorDoorTile)
            DrawBorder(pixels, width, tileX, tileY, s_exitColor);
        else if (marker == VictoryMarker)
            DrawBorder(pixels, width, tileX, tileY, s_exitColor);
        else if (marker == PushableMarker)
            DrawBorder(pixels, width, tileX, tileY, s_secretColor);
    }

    private static bool BordersWalkableCell(WolfensteinMap map, int tileX, int tileY)
    {
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                    continue;
                var x = tileX + offsetX;
                var y = tileY + offsetY;
                if (x >= 0 && x < map.Width && y >= 0 && y < map.Height && !map.IsSolid(x, y))
                    return true;
            }
        }
        return false;
    }

    private static void DrawBorder(
        Span<byte> pixels,
        int width,
        int tileX,
        int tileY,
        RgbaColor color)
    {
        var left = tileX * TileSize;
        var top = tileY * TileSize;
        for (var offset = 0; offset < TileSize; offset++)
        {
            SetPixel(pixels, width, left + offset, top, color);
            SetPixel(pixels, width, left + offset, top + TileSize - 1, color);
            SetPixel(pixels, width, left, top + offset, color);
            SetPixel(pixels, width, left + TileSize - 1, top + offset, color);
        }
    }

    private static void SetPixel(Span<byte> pixels, int width, int x, int y, RgbaColor color)
    {
        var offset = ((y * width) + x) * 4;
        pixels[offset] = color.Red;
        pixels[offset + 1] = color.Green;
        pixels[offset + 2] = color.Blue;
        pixels[offset + 3] = color.Alpha;
    }
}
