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
/// Defines a position, facing direction, and projection plane for grid raycasting.
/// </summary>
/// <remarks>
/// The initial camera is derived from the original player-start marker in map plane one.
/// </remarks>
public sealed class RaycastCamera
{
    private const double ProjectionPlaneLength = 0.66;

    public RaycastCamera(
        double x,
        double y,
        double directionX,
        double directionY,
        double planeX,
        double planeY)
    {
        X = x;
        Y = y;
        DirectionX = directionX;
        DirectionY = directionY;
        PlaneX = planeX;
        PlaneY = planeY;
    }

    public double X { get; }
    public double Y { get; }
    public double DirectionX { get; }
    public double DirectionY { get; }
    public double PlaneX { get; }
    public double PlaneY { get; }

    public static RaycastCamera FromPlayerStart(WolfensteinMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var marker = map.GetObject(x, y);
                if (marker is < 19 or > 22)
                    continue;

                var (directionX, directionY) = marker switch
                {
                    19 => (0.0, -1.0),
                    20 => (1.0, 0.0),
                    21 => (0.0, 1.0),
                    22 => (-1.0, 0.0),
                    _ => throw new InvalidOperationException()
                };
                return new RaycastCamera(
                    x + 0.5,
                    y + 0.5,
                    directionX,
                    directionY,
                    -directionY * ProjectionPlaneLength,
                    directionX * ProjectionPlaneLength);
            }
        }

        throw new InvalidDataException($"{map.Name} does not contain a player-start marker.");
    }
}
