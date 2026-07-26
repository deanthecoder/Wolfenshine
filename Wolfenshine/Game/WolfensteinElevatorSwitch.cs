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
/// Records the elevator switch changed by the player for the remainder of the level.
/// </summary>
/// <remarks>
/// The source map remains immutable; renderers use this state to substitute the original switched texture.
/// </remarks>
public sealed class WolfensteinElevatorSwitch
{
    private const ushort UnflippedTile = 21;
    private const ushort FlippedTile = 22;

    public WolfensteinElevatorSwitch(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }
    public int Y { get; }

    public ushort ResolveTile(int x, int y, ushort sourceTile) =>
        x == X && y == Y && sourceTile == UnflippedTile ? FlippedTile : sourceTile;
}
