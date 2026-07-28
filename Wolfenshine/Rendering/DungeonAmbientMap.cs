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

namespace Wolfenshine.Rendering;

/// <summary>
/// Locates blue-stone jail areas and provides smoothly fading dungeon darkness at a world position.
/// </summary>
/// <remarks>
/// Retail maps consistently use wall tile 7 for jail bars with at least two cardinal tile-8/9 blue-stone neighbors.
/// </remarks>
public sealed class DungeonAmbientMap
{
    private const ushort JailWallTile = 7;
    private const ushort FirstBlueWallTile = 8;
    private const ushort SecondBlueWallTile = 9;
    private const int RequiredBlueNeighborCount = 2;
    private const double FullDarknessRadius = 4.0;
    private const double FadeEndRadius = 10.0;
    private readonly (double X, double Y)[] m_jailWalls;

    private DungeonAmbientMap((double X, double Y)[] jailWalls) => m_jailWalls = jailWalls;

    /// <summary>
    /// Detects jail-wall clusters from the original map tiles.
    /// </summary>
    public static DungeonAmbientMap FromMap(WolfensteinMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var jailWalls = new List<(double X, double Y)>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map.GetWall(x, y) == JailWallTile && CountBlueNeighbors(map, x, y) >= RequiredBlueNeighborCount)
                    jailWalls.Add((x + 0.5, y + 0.5));
            }
        }
        return new DungeonAmbientMap(jailWalls.ToArray());
    }

    /// <summary>
    /// Returns dungeon darkness from zero in ordinary areas to one near confirmed jail walls.
    /// </summary>
    public double GetDarkness(double x, double y)
    {
        if (!double.IsFinite(x))
            throw new ArgumentOutOfRangeException(nameof(x));
        if (!double.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(y));
        var nearestDistanceSquared = double.PositiveInfinity;
        foreach (var jailWall in m_jailWalls)
        {
            var deltaX = x - jailWall.X;
            var deltaY = y - jailWall.Y;
            nearestDistanceSquared = Math.Min(nearestDistanceSquared, (deltaX * deltaX) + (deltaY * deltaY));
        }
        if (!double.IsFinite(nearestDistanceSquared))
            return 0.0;

        var distance = Math.Sqrt(nearestDistanceSquared);
        var fadePosition = Math.Clamp(
            (distance - FullDarknessRadius) / (FadeEndRadius - FullDarknessRadius),
            0.0,
            1.0);
        var smoothFade = fadePosition * fadePosition * (3.0 - (2.0 * fadePosition));
        return 1.0 - smoothFade;
    }

    private static int CountBlueNeighbors(WolfensteinMap map, int x, int y)
    {
        var count = 0;
        if (IsBlueWall(map, x - 1, y))
            count++;
        if (IsBlueWall(map, x + 1, y))
            count++;
        if (IsBlueWall(map, x, y - 1))
            count++;
        if (IsBlueWall(map, x, y + 1))
            count++;
        return count;
    }

    private static bool IsBlueWall(WolfensteinMap map, int x, int y) =>
        x >= 0 && x < map.Width && y >= 0 && y < map.Height &&
        map.GetWall(x, y) is FirstBlueWallTile or SecondBlueWallTile;
}
