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

namespace Wolfenshine.Game;

/// <summary>
/// Finds a shortest player route to any supplied map tile.
/// </summary>
/// <remarks>
/// Attract mode can restrict a search to the currently open area or permit usable doors for long-term objectives.
/// </remarks>
public static class AutoPlayerRoutePlanner
{
    public static IReadOnlyList<NavigationRoutePoint> FindNearest(
        GameSession session,
        IReadOnlyCollection<NavigationRoutePoint> targets,
        bool allowClosedDoors)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
            return [];

        var map = session.Map;
        var startX = (int)Math.Floor(session.Camera.X);
        var startY = (int)Math.Floor(session.Camera.Y);
        var targetSet = targets.ToHashSet();
        var parents = Enumerable.Repeat(-1, map.Width * map.Height).ToArray();
        var visited = new bool[parents.Length];
        var pending = new Queue<int>();
        var start = ToIndex(map.Width, startX, startY);
        visited[start] = true;
        pending.Enqueue(start);
        while (pending.TryDequeue(out var current))
        {
            var x = current % map.Width;
            var y = current / map.Width;
            if (targetSet.Contains(new NavigationRoutePoint(x, y)))
                return BuildRoute(map.Width, parents, start, current);
            TryQueue(x - 1, y);
            TryQueue(x + 1, y);
            TryQueue(x, y - 1);
            TryQueue(x, y + 1);

            void TryQueue(int nextX, int nextY)
            {
                if (!IsPassable(session, nextX, nextY, allowClosedDoors))
                    return;
                var next = ToIndex(map.Width, nextX, nextY);
                if (visited[next])
                    return;
                visited[next] = true;
                parents[next] = current;
                pending.Enqueue(next);
            }
        }
        return [];
    }

    /// <summary>
    /// Removes square waypoint bends when both tiles bordering the resulting diagonal are unobstructed.
    /// </summary>
    public static IReadOnlyList<NavigationRoutePoint> SmoothCorners(
        GameSession session,
        IReadOnlyList<NavigationRoutePoint> route)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(route);
        if (route.Count < 3)
            return route;

        var smoothed = new List<NavigationRoutePoint> { route[0] };
        for (var index = 1; index < route.Count - 1; index++)
        {
            var previous = smoothed[^1];
            var corner = route[index];
            var next = route[index + 1];
            var isRightAngle = Math.Abs(next.X - previous.X) == 1 &&
                               Math.Abs(next.Y - previous.Y) == 1;
            var inner = new NavigationRoutePoint(
                previous.X + next.X - corner.X,
                previous.Y + next.Y - corner.Y);
            if (isRightAngle && session.Doors.Get(corner.X, corner.Y) == null &&
                session.Doors.Get(inner.X, inner.Y) == null &&
                IsPassable(session, inner.X, inner.Y, allowClosedDoors: false))
            {
                continue;
            }
            smoothed.Add(corner);
        }
        smoothed.Add(route[^1]);
        return smoothed;
    }

    /// <summary>
    /// Finds the furthest waypoint in the current straight run, including a door at its end.
    /// </summary>
    public static int FindStraightLookAhead(
        GameSession session,
        IReadOnlyList<NavigationRoutePoint> route,
        int routeIndex)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(route);
        if (routeIndex <= 0 || routeIndex >= route.Count)
            return Math.Clamp(routeIndex, 0, Math.Max(0, route.Count - 1));
        var previous = route[routeIndex - 1];
        var current = route[routeIndex];
        if (session.Doors.Get(current.X, current.Y) != null)
            return routeIndex;
        var directionX = current.X - previous.X;
        var directionY = current.Y - previous.Y;
        var lookAhead = routeIndex;
        for (var index = routeIndex + 1; index < route.Count; index++)
        {
            var next = route[index];
            if (next.X - current.X != directionX || next.Y - current.Y != directionY)
                break;
            if (!IsPassable(session, next.X, next.Y, allowClosedDoors: true))
                break;
            lookAhead = index;
            if (session.Doors.Get(next.X, next.Y) != null)
                break;
            current = next;
        }
        return lookAhead;
    }

    private static IReadOnlyList<NavigationRoutePoint> BuildRoute(
        int mapWidth,
        IReadOnlyList<int> parents,
        int start,
        int target)
    {
        var route = new List<NavigationRoutePoint>();
        for (var current = target; current >= 0; current = parents[current])
        {
            route.Add(new NavigationRoutePoint(current % mapWidth, current / mapWidth));
            if (current == start)
                break;
        }
        route.Reverse();
        return route;
    }

    private static bool IsPassable(GameSession session, int x, int y, bool allowClosedDoors)
    {
        var map = session.Map;
        if (x < 0 || x >= map.Width || y < 0 || y >= map.Height ||
            session.PushWalls.IsTileReserved(x, y))
        {
            return false;
        }
        var door = session.Doors.Get(x, y);
        if (door != null)
        {
            if (door.RequiredKeyIndex is { } keyIndex &&
                (keyIndex == 0 && !session.HasGoldKey || keyIndex == 1 && !session.HasSilverKey || keyIndex > 1))
            {
                return false;
            }
            return allowClosedDoors || door.IsFullyOpen;
        }
        if (!session.PushWalls.IsOriginalWallSuppressed(x, y) && map.IsSolid(x, y) ||
            WolfensteinStaticObjects.BlocksMovement(map.GetObject(x, y)))
        {
            return false;
        }
        return session.Actors.All(actor => actor.IsDead ||
            (int)Math.Floor(actor.X) != x || (int)Math.Floor(actor.Y) != y);
    }

    private static int ToIndex(int width, int x, int y) => (y * width) + x;
}
