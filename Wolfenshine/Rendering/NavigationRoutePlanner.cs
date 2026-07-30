// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Wolfenshine.Game;
using Wolfenshine.Maps;

namespace Wolfenshine.Rendering;

/// <summary>
/// Finds a playable route to an obtainable key or the level exit.
/// </summary>
/// <remarks>
/// The guide may pass through ordinary doors but never reveals unopened secret walls or assumes an unowned key.
/// </remarks>
public static class NavigationRoutePlanner
{
    private const ushort ElevatorSwitchTile = 21;

    public static NavigationRoute Find(
        WolfensteinMap map,
        WolfensteinDoors doors,
        WolfensteinPushWalls pushWalls,
        int startX,
        int startY,
        IReadOnlyList<WorldSprite> staticObjects,
        bool hasGoldKey,
        bool hasSilverKey)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(doors);
        ArgumentNullException.ThrowIfNull(pushWalls);
        ArgumentNullException.ThrowIfNull(staticObjects);
        if (!ReferenceEquals(map, doors.Map) || !ReferenceEquals(map, pushWalls.Map))
            throw new ArgumentException("Doors and pushwalls must belong to the supplied map.");
        if (!IsInMap(map, startX, startY))
            return NavigationRoute.Empty;

        var parents = Enumerable.Repeat(-1, map.Width * map.Height).ToArray();
        var distances = Enumerable.Repeat(-1, parents.Length).ToArray();
        BuildSearch(map, doors, pushWalls, startX, startY, hasGoldKey, hasSilverKey, parents, distances);

        var keyTarget = FindNearestKey(map, staticObjects, distances);
        if (keyTarget is { } key)
            return BuildRoute(map, parents, startX, startY, key.X, key.Y, key.TargetType);

        var exitTarget = FindNearestExit(map, distances);
        return exitTarget is { } exit
            ? BuildRoute(map, parents, startX, startY, exit.X, exit.Y, NavigationTargetType.Exit)
            : NavigationRoute.Empty;
    }

    private static void BuildSearch(
        WolfensteinMap map,
        WolfensteinDoors doors,
        WolfensteinPushWalls pushWalls,
        int startX,
        int startY,
        bool hasGoldKey,
        bool hasSilverKey,
        int[] parents,
        int[] distances)
    {
        var start = ToIndex(map, startX, startY);
        var pending = new Queue<int>();
        distances[start] = 0;
        pending.Enqueue(start);
        while (pending.TryDequeue(out var current))
        {
            var x = current % map.Width;
            var y = current / map.Width;
            TryQueue(x - 1, y);
            TryQueue(x + 1, y);
            TryQueue(x, y - 1);
            TryQueue(x, y + 1);

            void TryQueue(int nextX, int nextY)
            {
                if (!IsPassable(map, doors, pushWalls, nextX, nextY, hasGoldKey, hasSilverKey))
                    return;
                var next = ToIndex(map, nextX, nextY);
                if (distances[next] >= 0)
                    return;
                parents[next] = current;
                distances[next] = distances[current] + 1;
                pending.Enqueue(next);
            }
        }
    }

    private static (int X, int Y, NavigationTargetType TargetType)? FindNearestKey(
        WolfensteinMap map,
        IReadOnlyList<WorldSprite> staticObjects,
        int[] distances)
    {
        var bestDistance = int.MaxValue;
        (int X, int Y, NavigationTargetType TargetType)? best = null;
        foreach (var sprite in staticObjects)
        {
            var targetType = WolfensteinStaticObjects.GetPickupType(sprite.SpriteNumber) switch
            {
                WolfensteinPickupType.GoldKey => NavigationTargetType.GoldKey,
                WolfensteinPickupType.SilverKey => NavigationTargetType.SilverKey,
                _ => NavigationTargetType.None
            };
            if (targetType == NavigationTargetType.None)
                continue;
            var x = (int)Math.Floor(sprite.X);
            var y = (int)Math.Floor(sprite.Y);
            if (!IsInMap(map, x, y))
                continue;
            var distance = distances[ToIndex(map, x, y)];
            if (distance < 0 || distance >= bestDistance)
                continue;
            bestDistance = distance;
            best = (x, y, targetType);
        }
        return best;
    }

    private static (int X, int Y)? FindNearestExit(WolfensteinMap map, int[] distances)
    {
        var bestDistance = int.MaxValue;
        (int X, int Y)? best = null;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map.GetWall(x, y) != ElevatorSwitchTile)
                    continue;
                Consider(x - 1, y);
                Consider(x + 1, y);
            }
        }
        return best;

        void Consider(int x, int y)
        {
            if (!IsInMap(map, x, y))
                return;
            var distance = distances[ToIndex(map, x, y)];
            if (distance < 0 || distance >= bestDistance)
                return;
            bestDistance = distance;
            best = (x, y);
        }
    }

    private static NavigationRoute BuildRoute(
        WolfensteinMap map,
        int[] parents,
        int startX,
        int startY,
        int targetX,
        int targetY,
        NavigationTargetType targetType)
    {
        var start = ToIndex(map, startX, startY);
        var current = ToIndex(map, targetX, targetY);
        var reversed = new List<NavigationRoutePoint>();
        while (true)
        {
            reversed.Add(new NavigationRoutePoint(current % map.Width, current / map.Width));
            if (current == start)
                break;
            current = parents[current];
            if (current < 0)
                return NavigationRoute.Empty;
        }
        reversed.Reverse();
        return new NavigationRoute(reversed, targetType);
    }

    private static bool IsPassable(
        WolfensteinMap map,
        WolfensteinDoors doors,
        WolfensteinPushWalls pushWalls,
        int x,
        int y,
        bool hasGoldKey,
        bool hasSilverKey)
    {
        if (!IsInMap(map, x, y) || pushWalls.IsTileReserved(x, y))
            return false;
        var door = doors.Get(x, y);
        if (door?.RequiredKeyIndex is { } keyIndex)
        {
            if (keyIndex == 0 && !hasGoldKey || keyIndex == 1 && !hasSilverKey || keyIndex > 1)
                return false;
        }
        if (door != null)
            return true;
        if (!pushWalls.IsOriginalWallSuppressed(x, y) && map.IsSolid(x, y))
            return false;
        return !WolfensteinStaticObjects.BlocksMovement(map.GetObject(x, y));
    }

    private static bool IsInMap(WolfensteinMap map, int x, int y) =>
        x >= 0 && x < map.Width && y >= 0 && y < map.Height;

    private static int ToIndex(WolfensteinMap map, int x, int y) => (y * map.Width) + x;
}
