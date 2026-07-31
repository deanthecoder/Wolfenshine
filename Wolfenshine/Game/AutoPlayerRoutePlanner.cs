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
        return NavigationRouteSmoother.SmoothCorners(
            route,
            (x, y) => IsPassable(session, x, y, allowClosedDoors: false),
            (x, y) => session.Doors.Get(x, y) != null);
    }

    /// <summary>
    /// Finds the furthest currently visible waypoint, including a closed door at the end of a visible run.
    /// </summary>
    public static int FindVisibleLookAhead(
        GameSession session,
        IReadOnlyList<NavigationRoutePoint> route,
        int routeIndex)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(route);
        if (routeIndex <= 0 || routeIndex >= route.Count)
            return Math.Clamp(routeIndex, 0, Math.Max(0, route.Count - 1));
        var lookAhead = routeIndex;
        for (var index = routeIndex; index < route.Count; index++)
        {
            var candidate = route[index];
            var door = session.Doors.Get(candidate.X, candidate.Y);
            if (CanSeeWaypoint(session, candidate, door != null))
                lookAhead = index;
            if (door is { IsFullyOpen: false })
                break;
        }
        return lookAhead;
    }

    private static bool CanSeeWaypoint(
        GameSession session,
        NavigationRoutePoint waypoint,
        bool stopBeforeWaypoint)
    {
        var deltaX = waypoint.X + 0.5 - session.Camera.X;
        var deltaY = waypoint.Y + 0.5 - session.Camera.Y;
        var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (!stopBeforeWaypoint || distance <= 0.55)
            return session.HasLineOfSightTo(waypoint.X + 0.5, waypoint.Y + 0.5);
        var scale = (distance - 0.55) / distance;
        return session.HasLineOfSightTo(
            session.Camera.X + (deltaX * scale),
            session.Camera.Y + (deltaY * scale));
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
