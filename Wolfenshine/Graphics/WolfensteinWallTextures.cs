// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Wolfenshine.Rendering;

namespace Wolfenshine.Graphics;

/// <summary>
/// Maps raycast wall hits onto their corresponding VSWAP texture pages.
/// </summary>
/// <remarks>
/// Centralizing the original page rules keeps the renderer independent of VSWAP numbering details.
/// </remarks>
public sealed class WolfensteinWallTextures
{
    private const int DoorPageCount = 8;

    public WolfensteinWallTextures(IReadOnlyList<WolfensteinWallTexture> pages, int spriteStart)
    {
        ArgumentNullException.ThrowIfNull(pages);
        if (pages.Count != spriteStart)
            throw new ArgumentException("The wall-page count must match the VSWAP sprite start.", nameof(pages));
        if (spriteStart < DoorPageCount)
            throw new ArgumentOutOfRangeException(nameof(spriteStart));
        Pages = pages;
        SpriteStart = spriteStart;
    }

    public IReadOnlyList<WolfensteinWallTexture> Pages { get; }
    public int SpriteStart { get; }

    public WolfensteinWallTexture GetTexture(WallColumn column)
    {
        var page = column.IsDoorJamb
            ? GetDoorJambPage(column.Side)
            : column.Tile is >= 90 and <= 101
                ? GetDoorPage(column)
                : ((column.Tile - 1) * 2) + (column.Side == WallSide.Vertical ? 1 : 0);
        if (page < 0 || page >= Pages.Count)
            throw new InvalidDataException($"Wall tile {column.Tile} resolves to invalid VSWAP page {page}.");
        return Pages[page];
    }

    private int GetDoorJambPage(WallSide side) =>
        SpriteStart - DoorPageCount + (side == WallSide.Vertical ? 3 : 2);

    private int GetDoorPage(WallColumn column)
    {
        var doorBasePage = SpriteStart - DoorPageCount;
        var typeOffset = column.Tile switch
        {
            90 or 91 => 0,
            100 or 101 => 4,
            _ => 6
        };
        var orientationOffset = column.Side == WallSide.Vertical ? 1 : 0;
        return doorBasePage + typeOffset + orientationOffset;
    }
}
