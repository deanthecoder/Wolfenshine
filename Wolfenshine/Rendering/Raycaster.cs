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
/// Casts one grid-traversal ray for each output column.
/// </summary>
/// <remarks>
/// Raycasting remains independent of presentation so its results can feed interchangeable renderers.
/// </remarks>
public static class Raycaster
{
    public static void Cast(
        WolfensteinMap map,
        WolfensteinDoors doors,
        RaycastCamera camera,
        Span<WallColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(doors);
        ArgumentNullException.ThrowIfNull(camera);
        if (!ReferenceEquals(map, doors.Map))
            throw new ArgumentException("The door collection belongs to a different map.", nameof(doors));
        if (columns.IsEmpty)
            throw new ArgumentException("At least one output column is required.", nameof(columns));

        // Map each pixel center onto the camera plane, then cast its world-space ray independently.
        for (var column = 0; column < columns.Length; column++)
        {
            var cameraX = (2.0 * (column + 0.5) / columns.Length) - 1.0;
            var rayDirectionX = camera.DirectionX + (camera.PlaneX * cameraX);
            var rayDirectionY = camera.DirectionY + (camera.PlaneY * cameraX);
            columns[column] = CastRay(map, doors, camera, rayDirectionX, rayDirectionY);
        }

    }

    private static WallColumn CastRay(
        WolfensteinMap map,
        WolfensteinDoors doors,
        RaycastCamera camera,
        double rayDirectionX,
        double rayDirectionY)
    {
        var mapX = (int)camera.X;
        var mapY = (int)camera.Y;
        // Precalculate the distance and direction needed to cross each successive map-grid boundary.
        var deltaDistanceX = rayDirectionX == 0.0 ? double.PositiveInfinity : Math.Abs(1.0 / rayDirectionX);
        var deltaDistanceY = rayDirectionY == 0.0 ? double.PositiveInfinity : Math.Abs(1.0 / rayDirectionY);
        var stepX = rayDirectionX < 0.0 ? -1 : 1;
        var stepY = rayDirectionY < 0.0 ? -1 : 1;
        var sideDistanceX = rayDirectionX < 0.0
            ? (camera.X - mapX) * deltaDistanceX
            : (mapX + 1.0 - camera.X) * deltaDistanceX;
        var sideDistanceY = rayDirectionY < 0.0
            ? (camera.Y - mapY) * deltaDistanceY
            : (mapY + 1.0 - camera.Y) * deltaDistanceY;

        WallSide side;
        ushort tile;
        // Advance through whichever grid boundary is nearest until the ray enters a solid wall tile.
        while (true)
        {
            if (sideDistanceX < sideDistanceY)
            {
                sideDistanceX += deltaDistanceX;
                mapX += stepX;
                side = WallSide.Vertical;
            }
            else
            {
                sideDistanceY += deltaDistanceY;
                mapY += stepY;
                side = WallSide.Horizontal;
            }

            if (mapX < 0 || mapX >= map.Width || mapY < 0 || mapY >= map.Height)
                throw new InvalidDataException("A ray left the map without hitting an enclosing wall.");
            var door = doors.Get(mapX, mapY);
            if (door != null)
            {
                if (TryHitDoor(door, camera, rayDirectionX, rayDirectionY, out var doorColumn))
                    return doorColumn;
                continue;
            }
            if (map.IsSolid(mapX, mapY))
            {
                tile = map.GetWall(mapX, mapY);
                break;
            }
        }

        // This perpendicular distance projects walls without fish-eye distortion.
        var distance = side == WallSide.Vertical
            ? sideDistanceX - deltaDistanceX
            : sideDistanceY - deltaDistanceY;
        // Retain the fractional hit position now so textured renderers need no additional map traversal later.
        var wallPosition = side == WallSide.Vertical
            ? camera.Y + (distance * rayDirectionY)
            : camera.X + (distance * rayDirectionX);
        var textureU = wallPosition - Math.Floor(wallPosition);
        // Match the original renderer's face orientation. Subtracting from the largest value below one
        // mirrors discrete texels exactly: column zero becomes 63 rather than overflowing to column 64.
        if (side == WallSide.Vertical && rayDirectionX < 0.0 ||
            side == WallSide.Horizontal && rayDirectionY > 0.0)
        {
            textureU = double.BitDecrement(1.0) - textureU;
        }
        textureU = Math.Clamp(textureU, 0.0, double.BitDecrement(1.0));
        return new WallColumn(distance, textureU, tile, side);
    }

    private static bool TryHitDoor(
        WolfensteinDoor door,
        RaycastCamera camera,
        double rayDirectionX,
        double rayDirectionY,
        out WallColumn column)
    {
        column = default;
        if (door.IsFullyOpen)
            return false;

        double distance;
        double slideCoordinate;
        WallSide side;
        if (door.Orientation == DoorOrientation.Vertical)
        {
            if (rayDirectionX == 0.0)
                return false;
            distance = (door.X + 0.5 - camera.X) / rayDirectionX;
            var hitY = camera.Y + (distance * rayDirectionY);
            if (distance <= 0.0 || (int)Math.Floor(hitY) != door.Y)
                return false;
            slideCoordinate = hitY - Math.Floor(hitY);
            side = WallSide.Vertical;
        }
        else
        {
            if (rayDirectionY == 0.0)
                return false;
            distance = (door.Y + 0.5 - camera.Y) / rayDirectionY;
            var hitX = camera.X + (distance * rayDirectionX);
            if (distance <= 0.0 || (int)Math.Floor(hitX) != door.X)
                return false;
            slideCoordinate = hitX - Math.Floor(hitX);
            side = WallSide.Horizontal;
        }

        // The panel retracts toward increasing texture coordinates, exposing more of the doorway as it moves.
        if (slideCoordinate < door.OpenAmount)
            return false;
        column = new WallColumn(distance, slideCoordinate - door.OpenAmount, door.Tile, side);
        return true;
    }
}
