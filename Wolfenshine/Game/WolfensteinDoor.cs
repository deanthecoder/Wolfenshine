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
    private const double OriginalTicksPerSecond = 70.0;
    private const double OpenDuration = 300.0 / OriginalTicksPerSecond;
    private DoorState m_state;
    private double m_openTime;

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
    public bool IsLocked => Tile is >= 92 and <= 99;
    public bool IsOpening => m_state == DoorState.Opening;
    public bool IsClosing => m_state == DoorState.Closing;

    public bool Open()
    {
        if (IsLocked || IsOpening)
            return false;
        if (m_state == DoorState.Open)
        {
            m_openTime = 0.0;
            return true;
        }
        m_state = DoorState.Opening;
        return true;
    }

    public bool Operate(bool canClose)
    {
        if (IsLocked)
            return false;
        return m_state switch
        {
            DoorState.Closed or DoorState.Closing => Open(),
            DoorState.Open => Close(canClose),
            _ => false
        };
    }

    public bool Update(double elapsedSeconds, bool canClose = true)
    {
        switch (m_state)
        {
            case DoorState.Opening:
                OpenAmount = Math.Min(1.0, OpenAmount + (OpeningSpeed * elapsedSeconds));
                if (IsFullyOpen)
                {
                    m_state = DoorState.Open;
                    m_openTime = 0.0;
                }
                return true;
            case DoorState.Open:
                m_openTime += elapsedSeconds;
                return m_openTime >= OpenDuration && Close(canClose);
            case DoorState.Closing when !canClose:
                m_state = DoorState.Opening;
                return true;
            case DoorState.Closing:
                OpenAmount = Math.Max(0.0, OpenAmount - (OpeningSpeed * elapsedSeconds));
                if (OpenAmount == 0.0)
                    m_state = DoorState.Closed;
                return true;
            default:
                return false;
        }
    }

    private bool Close(bool canClose)
    {
        if (!canClose)
            return false;
        m_state = DoorState.Closing;
        return true;
    }

    private enum DoorState
    {
        Closed,
        Opening,
        Open,
        Closing
    }
}
