// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Wolfenshine.Maps;

namespace Wolfenshine.Game;

/// <summary>
/// Activates and advances the level's one-way secret pushwalls.
/// </summary>
/// <remarks>
/// The original permits only one moving wall at a time and retains each wall at its final position.
/// </remarks>
public sealed class WolfensteinPushWalls
{
    private const ushort PushableMarker = 98;
    private const double MovementSpeed = 70.0 / 128.0;
    private readonly WolfensteinMap m_map;
    private readonly List<WolfensteinPushWall> m_items = [];
    private readonly HashSet<(int X, int Y)> m_activatedOrigins = [];

    public WolfensteinPushWalls(WolfensteinMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        m_map = map;
    }

    public IReadOnlyList<WolfensteinPushWall> Items => m_items;
    public WolfensteinPushWall MovingWall => m_items.FirstOrDefault(item => item.IsMoving);

    public bool TryPush(int x, int y, int directionX, int directionY, Func<int, int, bool> canEnterTile)
    {
        ArgumentNullException.ThrowIfNull(canEnterTile);
        if (x < 0 || x >= m_map.Width || y < 0 || y >= m_map.Height ||
            MovingWall != null || m_activatedOrigins.Contains((x, y)) ||
            m_map.GetObject(x, y) != PushableMarker || !m_map.IsSolid(x, y) ||
            Math.Abs(directionX) + Math.Abs(directionY) != 1 ||
            !canEnterTile(x + directionX, y + directionY))
        {
            return false;
        }

        m_items.Add(new WolfensteinPushWall(x, y, m_map.GetWall(x, y), directionX, directionY));
        m_activatedOrigins.Add((x, y));
        return true;
    }

    public bool Update(double elapsedSeconds, Func<int, int, bool> canEnterTile)
    {
        ArgumentNullException.ThrowIfNull(canEnterTile);
        var wall = MovingWall;
        if (wall == null || elapsedSeconds <= 0.0)
            return false;

        var nextDistance = wall.Distance + (MovementSpeed * elapsedSeconds);
        if (wall.Distance < 1.0 && nextDistance >= 1.0)
        {
            var secondX = wall.OriginX + (wall.DirectionX * 2);
            var secondY = wall.OriginY + (wall.DirectionY * 2);
            if (!canEnterTile(secondX, secondY))
            {
                wall.Distance = 1.0;
                wall.IsMoving = false;
                return true;
            }
        }

        wall.Distance = Math.Min(2.0, nextDistance);
        if (wall.Distance >= 2.0)
            wall.IsMoving = false;
        return true;
    }

    public bool IsOriginalWallSuppressed(int x, int y) => m_activatedOrigins.Contains((x, y));

    public bool IsTileReserved(int x, int y)
    {
        foreach (var wall in m_items)
        {
            var currentTile = wall.IsMoving
                ? Math.Min(1, (int)Math.Floor(wall.Distance))
                : (int)Math.Round(wall.Distance);
            var currentX = wall.OriginX + (wall.DirectionX * currentTile);
            var currentY = wall.OriginY + (wall.DirectionY * currentTile);
            if (x == currentX && y == currentY)
                return true;
            if (!wall.IsMoving)
                continue;
            if (x == currentX + wall.DirectionX && y == currentY + wall.DirectionY)
                return true;
        }
        return false;
    }
}
