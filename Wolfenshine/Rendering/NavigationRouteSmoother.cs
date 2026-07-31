// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Wolfenshine.Rendering;

/// <summary>
/// Replaces safe square route bends with direct diagonal steps.
/// </summary>
public static class NavigationRouteSmoother
{
    public static IReadOnlyList<NavigationRoutePoint> SmoothCorners(
        IReadOnlyList<NavigationRoutePoint> route,
        Func<int, int, bool> isPassable,
        Func<int, int, bool> isDoor)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(isPassable);
        ArgumentNullException.ThrowIfNull(isDoor);
        if (route.Count < 3)
            return route;

        var smoothed = new List<NavigationRoutePoint> { route[0] };
        for (var index = 1; index < route.Count - 1; index++)
        {
            var previous = smoothed[^1];
            var corner = route[index];
            var next = route[index + 1];
            var isAdjacentDiagonal = Math.Abs(next.X - previous.X) == 1 &&
                                     Math.Abs(next.Y - previous.Y) == 1;
            var insideCornerX = previous.X + next.X - corner.X;
            var insideCornerY = previous.Y + next.Y - corner.Y;
            if (isAdjacentDiagonal && !isDoor(corner.X, corner.Y) &&
                !isDoor(insideCornerX, insideCornerY) && isPassable(insideCornerX, insideCornerY))
            {
                continue;
            }
            smoothed.Add(corner);
        }
        smoothed.Add(route[^1]);
        return smoothed;
    }
}
