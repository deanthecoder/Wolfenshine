// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Wolfenshine.Maps;

/// <summary>
/// Contains the two tile planes used by a Wolfenstein 3D level.
/// </summary>
/// <remarks>
/// Plane zero describes walls and areas, while plane one places actors, objects, and other level information.
/// </remarks>
public sealed class WolfensteinMap
{
    public WolfensteinMap(
        int slot,
        string name,
        int width,
        int height,
        IReadOnlyList<ushort> walls,
        IReadOnlyList<ushort> objects)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(walls);
        ArgumentNullException.ThrowIfNull(objects);

        var tileCount = checked(width * height);
        if (walls.Count != tileCount)
            throw new ArgumentException($"The wall plane must contain {tileCount} tiles.", nameof(walls));
        if (objects.Count != tileCount)
            throw new ArgumentException($"The object plane must contain {tileCount} tiles.", nameof(objects));

        Slot = slot;
        Name = name;
        Width = width;
        Height = height;
        Walls = walls;
        Objects = objects;
    }

    public int Slot { get; }
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<ushort> Walls { get; }
    public IReadOnlyList<ushort> Objects { get; }

    public ushort GetWall(int x, int y) => GetTile(Walls, x, y);

    public ushort GetObject(int x, int y) => GetTile(Objects, x, y);

    private ushort GetTile(IReadOnlyList<ushort> plane, int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);
        return plane[(y * Width) + x];
    }
}
