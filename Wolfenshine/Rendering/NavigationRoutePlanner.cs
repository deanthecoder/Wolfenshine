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
        bool hasSilverKey,
        int preferredDirectionX = 0,
        int preferredDirectionY = 0)
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
        var target = keyTarget ?? FindNearestExit(map, distances);
        if (target == null)
            return NavigationRoute.Empty;

        if (Math.Abs(preferredDirectionX) + Math.Abs(preferredDirectionY) == 1)
        {
            var launchPoints = new List<NavigationRoutePoint>(2);
            for (var distance = 1; distance <= 2; distance++)
            {
                var preferredX = startX + (preferredDirectionX * distance);
                var preferredY = startY + (preferredDirectionY * distance);
                if (!IsOpenSpace(
                        map,
                        doors,
                        pushWalls,
                        preferredX,
                        preferredY,
                        hasGoldKey,
                        hasSilverKey))
                {
                    break;
                }
                launchPoints.Add(new NavigationRoutePoint(preferredX, preferredY));
            }
            if (launchPoints.Count > 0)
            {
                var launchPoint = launchPoints[^1];
                var preferredParents = Enumerable.Repeat(-1, parents.Length).ToArray();
                var preferredDistances = Enumerable.Repeat(-1, distances.Length).ToArray();
                BuildSearch(
                    map,
                    doors,
                    pushWalls,
                    launchPoint.X,
                    launchPoint.Y,
                    hasGoldKey,
                    hasSilverKey,
                    preferredParents,
                    preferredDistances);
                if (preferredDistances[ToIndex(map, target.Value.X, target.Value.Y)] >= 0)
                {
                    var route = BuildRoute(
                        map,
                        preferredParents,
                        launchPoint.X,
                        launchPoint.Y,
                        target.Value,
                        preferredDirectionX,
                        preferredDirectionY);
                    if (launchPoints.Count == 1)
                        return route;
                    return new NavigationRoute(
                        launchPoints.Take(launchPoints.Count - 1).Concat(route.Points).ToArray(),
                        route.TargetType,
                        route.InitialDirectionX,
                        route.InitialDirectionY);
                }
            }
        }

        return BuildRoute(map, parents, startX, startY, target.Value);
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

    private static RouteTarget? FindNearestKey(
        WolfensteinMap map,
        IReadOnlyList<WorldSprite> staticObjects,
        int[] distances)
    {
        var bestDistance = int.MaxValue;
        RouteTarget? best = null;
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
            best = new RouteTarget(x, y, targetType);
        }
        return best;
    }

    private static RouteTarget? FindNearestExit(WolfensteinMap map, int[] distances)
    {
        var bestDistance = int.MaxValue;
        RouteTarget? best = null;
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
            best = new RouteTarget(x, y, NavigationTargetType.Exit);
        }
    }

    private static NavigationRoute BuildRoute(
        WolfensteinMap map,
        int[] parents,
        int startX,
        int startY,
        RouteTarget target,
        int initialDirectionX = 0,
        int initialDirectionY = 0)
    {
        var start = ToIndex(map, startX, startY);
        var current = ToIndex(map, target.X, target.Y);
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
        return new NavigationRoute(reversed, target.TargetType, initialDirectionX, initialDirectionY);
    }

    private static bool IsOpenSpace(
        WolfensteinMap map,
        WolfensteinDoors doors,
        WolfensteinPushWalls pushWalls,
        int x,
        int y,
        bool hasGoldKey,
        bool hasSilverKey)
    {
        var door = doors.Get(x, y);
        return (door == null || door.IsFullyOpen) &&
               IsPassable(map, doors, pushWalls, x, y, hasGoldKey, hasSilverKey);
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

    private readonly record struct RouteTarget(int X, int Y, NavigationTargetType TargetType);
}
