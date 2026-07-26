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
/// Provides map-coordinate lookup and updates for a level's sliding doors.
/// </summary>
/// <remarks>
/// A dense lookup avoids dictionary work while hundreds of rays traverse the map each frame.
/// </remarks>
public sealed class WolfensteinDoors
{
    private readonly WolfensteinDoor[] m_doorsByTile;

    private WolfensteinDoors(WolfensteinMap map, IReadOnlyList<WolfensteinDoor> doors)
    {
        Map = map;
        Items = doors;
        m_doorsByTile = new WolfensteinDoor[map.Width * map.Height];
        foreach (var door in doors)
            m_doorsByTile[(door.Y * map.Width) + door.X] = door;
    }

    public WolfensteinMap Map { get; }
    public IReadOnlyList<WolfensteinDoor> Items { get; }

    public static WolfensteinDoors FromMap(WolfensteinMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var doors = new List<WolfensteinDoor>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var tile = map.GetWall(x, y);
                if (tile is >= 90 and <= 101)
                    doors.Add(new WolfensteinDoor(x, y, tile));
            }
        }

        return new WolfensteinDoors(map, doors);
    }

    public WolfensteinDoor Get(int x, int y) =>
        x < 0 || x >= Map.Width || y < 0 || y >= Map.Height
            ? null
            : m_doorsByTile[(y * Map.Width) + x];

    public bool Update(double elapsedSeconds) => Update(elapsedSeconds, _ => true);

    public bool Update(double elapsedSeconds, Func<WolfensteinDoor, bool> canClose)
    {
        ArgumentNullException.ThrowIfNull(canClose);
        var changed = false;
        foreach (var door in Items)
            changed |= door.Update(elapsedSeconds, canClose(door));
        return changed;
    }
}
