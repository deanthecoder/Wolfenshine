// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Wolfenshine.Game;

/// <summary>
/// Describes one activated secret wall and its continuous world position.
/// </summary>
/// <remarks>
/// Activated walls remain available to collision and rendering after their one-way movement finishes.
/// </remarks>
public sealed class WolfensteinPushWall
{
    internal WolfensteinPushWall(int originX, int originY, ushort tile, int directionX, int directionY)
    {
        OriginX = originX;
        OriginY = originY;
        Tile = tile;
        DirectionX = directionX;
        DirectionY = directionY;
    }

    public int OriginX { get; }
    public int OriginY { get; }
    public ushort Tile { get; }
    public int DirectionX { get; }
    public int DirectionY { get; }
    public double Distance { get; internal set; }
    public double X => OriginX + 0.5 + (DirectionX * Distance);
    public double Y => OriginY + 0.5 + (DirectionY * Distance);
    public bool IsMoving { get; internal set; } = true;
}
