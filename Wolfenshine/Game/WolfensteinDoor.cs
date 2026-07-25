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
/// Tracks the location and opening position of one map door.
/// </summary>
/// <remarks>
/// Door position is normalized from zero when closed to one when fully retracted.
/// </remarks>
public sealed class WolfensteinDoor
{
    private const double OpeningSpeed = 1.0;

    public WolfensteinDoor(int x, int y, ushort tile)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        if (tile is < 90 or > 101)
            throw new ArgumentOutOfRangeException(nameof(tile), "Door tiles must be in the range 90 to 101.");
        X = x;
        Y = y;
        Tile = tile;
        Orientation = (tile & 1) == 0 ? DoorOrientation.Vertical : DoorOrientation.Horizontal;
    }

    public int X { get; }
    public int Y { get; }
    public ushort Tile { get; }
    public DoorOrientation Orientation { get; }
    public double OpenAmount { get; private set; }
    public bool IsFullyOpen => OpenAmount >= 1.0;
    public bool IsLocked => Tile is not 90 and not 91;
    public bool IsOpening { get; private set; }

    public bool Open()
    {
        if (IsLocked || IsOpening || IsFullyOpen)
            return false;
        IsOpening = true;
        return true;
    }

    public bool Update(double elapsedSeconds)
    {
        if (!IsOpening)
            return false;
        OpenAmount = Math.Min(1.0, OpenAmount + (OpeningSpeed * elapsedSeconds));
        if (IsFullyOpen)
            IsOpening = false;
        return true;
    }
}
