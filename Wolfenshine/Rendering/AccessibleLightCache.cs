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
/// Caches static lights that can be reached from the player's current map region without crossing a closed door.
/// </summary>
/// <remarks>
/// A door joins its neighboring regions as soon as it starts opening and keeps them joined until it fully closes.
/// Activated pushwalls follow the same collision topology as the game, exposing vacated tiles while retaining the
/// moving wall as an obstruction. This avoids changing the shader's candidate lights merely because the player moves
/// near an unrelated room.
/// </remarks>
public sealed class AccessibleLightCache
{
    public const int ShaderLightCapacity = 32;
    private WolfensteinMap m_map;
    private WolfensteinDoors m_doors;
    private WolfensteinPushWalls m_pushWalls;
    private bool[] m_doorConnections = [];
    private int[] m_pushWallStates = [];
    private bool[] m_accessibleTiles = [];
    private int m_staticObjectCount = -1;

    public IReadOnlyList<WorldSprite> Lights { get; private set; } = [];

    /// <summary>
    /// Rebuilds the accessible light list when map topology or the static-object collection has changed.
    /// </summary>
    /// <returns><c>true</c> when the cached light list was rebuilt.</returns>
    public bool Refresh(
        WolfensteinMap map,
        WolfensteinDoors doors,
        WolfensteinPushWalls pushWalls,
        RaycastCamera camera,
        IReadOnlyList<WorldSprite> staticObjects,
        bool force = false)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(doors);
        ArgumentNullException.ThrowIfNull(pushWalls);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(staticObjects);
        if (!ReferenceEquals(map, doors.Map))
            throw new ArgumentException("The door collection belongs to a different map.", nameof(doors));
        if (!ReferenceEquals(map, pushWalls.Map))
            throw new ArgumentException("The pushwall collection belongs to a different map.", nameof(pushWalls));

        var playerX = (int)Math.Floor(camera.X);
        var playerY = (int)Math.Floor(camera.Y);
        var topologyChanged = !ReferenceEquals(m_map, map) ||
                              !ReferenceEquals(m_doors, doors) ||
                              !ReferenceEquals(m_pushWalls, pushWalls) ||
                              HaveDoorConnectionsChanged(doors) ||
                              HavePushWallTopologyChanged(pushWalls);
        var playerLeftCachedArea = !IsCachedAccessible(map, playerX, playerY);
        if (!force && !topologyChanged && !playerLeftCachedArea &&
            m_staticObjectCount == staticObjects.Count)
        {
            return false;
        }

        m_map = map;
        m_doors = doors;
        m_pushWalls = pushWalls;
        CaptureDoorConnections(doors);
        CapturePushWallTopology(pushWalls);
        BuildAccessibleTiles(map, doors, pushWalls, playerX, playerY);
        Lights = staticObjects.Where(IsAccessibleLight).ToArray();
        m_staticObjectCount = staticObjects.Count;
        return true;
    }

    private bool IsAccessibleLight(WorldSprite sprite)
    {
        var (upward, downward) = WolfensteinStaticObjects.GetLightBrightness(sprite.SpriteNumber);
        if (upward <= 0.0f && downward <= 0.0f)
            return false;
        var x = (int)Math.Floor(sprite.X);
        var y = (int)Math.Floor(sprite.Y);
        return IsCachedAccessible(m_map, x, y);
    }

    private bool HaveDoorConnectionsChanged(WolfensteinDoors doors)
    {
        if (!ReferenceEquals(m_doors, doors) || m_doorConnections.Length != doors.Items.Count)
            return true;
        for (var index = 0; index < doors.Items.Count; index++)
        {
            if (m_doorConnections[index] != ConnectsAreas(doors.Items[index]))
                return true;
        }
        return false;
    }

    private void CaptureDoorConnections(WolfensteinDoors doors)
    {
        if (m_doorConnections.Length != doors.Items.Count)
            m_doorConnections = new bool[doors.Items.Count];
        for (var index = 0; index < doors.Items.Count; index++)
            m_doorConnections[index] = ConnectsAreas(doors.Items[index]);
    }

    private bool HavePushWallTopologyChanged(WolfensteinPushWalls pushWalls)
    {
        if (!ReferenceEquals(m_pushWalls, pushWalls) || m_pushWallStates.Length != pushWalls.Items.Count)
            return true;
        for (var index = 0; index < pushWalls.Items.Count; index++)
        {
            if (m_pushWallStates[index] != GetTopologyState(pushWalls.Items[index]))
                return true;
        }
        return false;
    }

    private void CapturePushWallTopology(WolfensteinPushWalls pushWalls)
    {
        if (m_pushWallStates.Length != pushWalls.Items.Count)
            m_pushWallStates = new int[pushWalls.Items.Count];
        for (var index = 0; index < pushWalls.Items.Count; index++)
            m_pushWallStates[index] = GetTopologyState(pushWalls.Items[index]);
    }

    private void BuildAccessibleTiles(
        WolfensteinMap map,
        WolfensteinDoors doors,
        WolfensteinPushWalls pushWalls,
        int playerX,
        int playerY)
    {
        var tileCount = map.Width * map.Height;
        if (m_accessibleTiles.Length != tileCount)
            m_accessibleTiles = new bool[tileCount];
        else
            Array.Clear(m_accessibleTiles);
        if (!IsPassable(map, doors, pushWalls, playerX, playerY))
            return;

        var pending = new Queue<(int X, int Y)>();
        m_accessibleTiles[(playerY * map.Width) + playerX] = true;
        pending.Enqueue((playerX, playerY));
        while (pending.TryDequeue(out var tile))
        {
            TryQueue(map, doors, pushWalls, pending, tile.X - 1, tile.Y);
            TryQueue(map, doors, pushWalls, pending, tile.X + 1, tile.Y);
            TryQueue(map, doors, pushWalls, pending, tile.X, tile.Y - 1);
            TryQueue(map, doors, pushWalls, pending, tile.X, tile.Y + 1);
        }
    }

    private void TryQueue(
        WolfensteinMap map,
        WolfensteinDoors doors,
        WolfensteinPushWalls pushWalls,
        Queue<(int X, int Y)> pending,
        int x,
        int y)
    {
        if (!IsPassable(map, doors, pushWalls, x, y) || IsCachedAccessible(map, x, y))
            return;
        m_accessibleTiles[(y * map.Width) + x] = true;
        pending.Enqueue((x, y));
    }

    private static bool IsPassable(
        WolfensteinMap map,
        WolfensteinDoors doors,
        WolfensteinPushWalls pushWalls,
        int x,
        int y)
    {
        if (x < 0 || x >= map.Width || y < 0 || y >= map.Height)
            return false;
        if (pushWalls.IsTileReserved(x, y))
            return false;
        var door = doors.Get(x, y);
        return door != null
            ? ConnectsAreas(door)
            : pushWalls.IsOriginalWallSuppressed(x, y) || !map.IsSolid(x, y);
    }

    private bool IsCachedAccessible(WolfensteinMap map, int x, int y) =>
        ReferenceEquals(m_map, map) &&
        x >= 0 && x < map.Width && y >= 0 && y < map.Height &&
        m_accessibleTiles.Length == map.Width * map.Height &&
        m_accessibleTiles[(y * map.Width) + x];

    private static bool ConnectsAreas(WolfensteinDoor door) => door.IsOpening || door.OpenAmount > 0.0;

    private static int GetTopologyState(WolfensteinPushWall wall)
    {
        var currentTile = wall.IsMoving
            ? Math.Min(1, (int)Math.Floor(wall.Distance))
            : (int)Math.Round(wall.Distance);
        return (currentTile * 2) + (wall.IsMoving ? 1 : 0);
    }
}
