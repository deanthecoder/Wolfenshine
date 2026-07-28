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
/// Derives architectural lighting zones from floor geometry and blends their ambience across doors.
/// </summary>
/// <remarks>
/// Doors divide rooms even when the original map reuses one sound-area tile on both sides. Secret pushwalls do the
/// opposite: their adjoining floor regions are merged so lighting cannot disclose the hidden opening.
/// </remarks>
public sealed class AreaAmbientMap
{
    public const double MinimumAmbientScale = 0.42;
    public const double MaximumAmbientScale = 1.25;
    public const double DoorBlendRadius = 0.75;
    public const double DoorBlendHalfWidth = 0.55;
    private const ushort AmbushTile = 106;
    private const ushort FirstAreaTile = 107;
    private const ushort PushwallMarker = 98;
    private const int SmallRoomTileCount = 16;
    private const double SmallRoomInheritance = 0.75;
    private const double ChandelierAmbientBoost = MaximumAmbientScale - 1.0;
    private const double FullChandelierStrength = 3.0;
    private const double FullLightingCoverage = 0.38;
    private const double FullFixtureStrength = 2.4;
    private readonly WolfensteinMap m_map;
    private readonly int[] m_zoneByTile;
    private readonly IReadOnlyList<double> m_zoneAmbientScales;
    private readonly IReadOnlyList<DoorTransition> m_doorTransitions;

    private AreaAmbientMap(
        WolfensteinMap map,
        int[] zoneByTile,
        IReadOnlyList<double> zoneAmbientScales,
        IReadOnlyList<DoorTransition> doorTransitions)
    {
        m_map = map;
        m_zoneByTile = zoneByTile;
        m_zoneAmbientScales = zoneAmbientScales;
        m_doorTransitions = doorTransitions;
    }

    /// <summary>
    /// Finds rooms and corridors separated by ordinary doors, then measures their broad ambient-light coverage.
    /// </summary>
    public static AreaAmbientMap FromMap(WolfensteinMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var layout = BuildZoneLayout(map);
        var doorTransitions = FindDoorTransitions(map, layout.ZoneByTile);
        var zoneLightCoverage = new double[layout.TileCounts.Count];
        var zoneFixtureStrength = new double[layout.TileCounts.Count];
        var zoneChandelierStrength = new double[layout.TileCounts.Count];
        var zoneFixtureCounts = new int[layout.TileCounts.Count];
        foreach (var worldSprite in WolfensteinStaticObjects.FromMap(map))
        {
            if (!ContributesToRoomAmbient(worldSprite.SpriteNumber))
                continue;
            var (_, downwardBrightness) = WolfensteinStaticObjects.GetLightBrightness(worldSprite.SpriteNumber);
            var (_, downwardRadius) = WolfensteinStaticObjects.GetLightRadii(worldSprite.SpriteNumber);
            if (downwardBrightness <= 0.0f || downwardRadius <= 0.0f)
                continue;
            var zone = GetZone(layout.ZoneByTile, map, (int)worldSprite.X, (int)worldSprite.Y);
            if (zone < 0)
                continue;
            zoneLightCoverage[zone] += Math.PI * downwardRadius * downwardRadius * downwardBrightness;
            zoneFixtureStrength[zone] += downwardBrightness;
            if (worldSprite.SpriteNumber == 6)
                zoneChandelierStrength[zone] += downwardBrightness;
            zoneFixtureCounts[zone]++;
        }

        var zoneAmbientScales = new double[layout.TileCounts.Count];
        for (var zone = 0; zone < zoneAmbientScales.Length; zone++)
        {
            zoneAmbientScales[zone] = CalculateAmbientScale(
                zoneLightCoverage[zone],
                zoneFixtureStrength[zone],
                zoneChandelierStrength[zone],
                layout.TileCounts[zone]);
        }
        InheritSmallRoomAmbient(
            zoneAmbientScales,
            zoneFixtureCounts,
            layout.TileCounts,
            doorTransitions);
        return new AreaAmbientMap(map, layout.ZoneByTile, zoneAmbientScales, doorTransitions);
    }

    /// <summary>
    /// Returns the static ambient scale at a world position without doorway transition effects.
    /// </summary>
    public double GetAmbientScale(double x, double y)
    {
        ValidatePosition(x, y);
        return GetZoneAmbientScale(FindZone((int)Math.Floor(x), (int)Math.Floor(y)));
    }

    /// <summary>
    /// Returns the local ambient scale, including a smooth architectural transition across doorways.
    /// </summary>
    public double GetAmbientScale(double x, double y, WolfensteinDoors doors)
    {
        ValidatePosition(x, y);
        ArgumentNullException.ThrowIfNull(doors);
        if (!ReferenceEquals(m_map, doors.Map))
            throw new ArgumentException("The door collection belongs to a different map.", nameof(doors));

        var ambientScale = GetZoneAmbientScale(FindZone((int)Math.Floor(x), (int)Math.Floor(y)));
        var nearestDoorDistanceSquared = double.PositiveInfinity;
        foreach (var transition in m_doorTransitions)
        {
            var door = doors.Get(transition.X, transition.Y);
            if (door == null)
                continue;
            var normalOffset = transition.IsVertical ? x - (transition.X + 0.5) : y - (transition.Y + 0.5);
            var tangentOffset = transition.IsVertical ? y - (transition.Y + 0.5) : x - (transition.X + 0.5);
            if (Math.Abs(normalOffset) > DoorBlendRadius || Math.Abs(tangentOffset) > DoorBlendHalfWidth)
                continue;
            var distanceSquared = (normalOffset * normalOffset) + (tangentOffset * tangentOffset);
            if (distanceSquared >= nearestDoorDistanceSquared)
                continue;

            var blendPosition = Math.Clamp(
                (normalOffset + DoorBlendRadius) / (DoorBlendRadius * 2.0),
                0.0,
                1.0);
            var smoothBlend = blendPosition * blendPosition * (3.0 - (2.0 * blendPosition));
            var doorAmbient = Lerp(
                GetZoneAmbientScale(transition.NegativeZone),
                GetZoneAmbientScale(transition.PositiveZone),
                smoothBlend);
            ambientScale = doorAmbient;
            nearestDoorDistanceSquared = distanceSquared;
        }
        return ambientScale;
    }

    private static ZoneLayout BuildZoneLayout(WolfensteinMap map)
    {
        var rawZoneByTile = Enumerable.Repeat(-1, map.Width * map.Height).ToArray();
        var rawTileCounts = new List<int>();
        var pending = new Queue<(int X, int Y)>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (!IsWalkableArea(map.GetWall(x, y)) || GetZone(rawZoneByTile, map, x, y) >= 0)
                    continue;
                var rawZone = rawTileCounts.Count;
                var tileCount = 0;
                rawZoneByTile[(y * map.Width) + x] = rawZone;
                pending.Enqueue((x, y));
                while (pending.TryDequeue(out var tile))
                {
                    tileCount++;
                    TryQueueFloor(map, rawZoneByTile, pending, rawZone, tile.X - 1, tile.Y);
                    TryQueueFloor(map, rawZoneByTile, pending, rawZone, tile.X + 1, tile.Y);
                    TryQueueFloor(map, rawZoneByTile, pending, rawZone, tile.X, tile.Y - 1);
                    TryQueueFloor(map, rawZoneByTile, pending, rawZone, tile.X, tile.Y + 1);
                }
                rawTileCounts.Add(tileCount);
            }
        }

        var mergedZones = new DisjointSet(rawTileCounts.Count);
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map.GetObject(x, y) != PushwallMarker)
                    continue;
                var adjoiningZones = new[]
                {
                    GetZone(rawZoneByTile, map, x - 1, y),
                    GetZone(rawZoneByTile, map, x + 1, y),
                    GetZone(rawZoneByTile, map, x, y - 1),
                    GetZone(rawZoneByTile, map, x, y + 1)
                }.Where(zone => zone >= 0).Distinct().ToArray();
                for (var index = 1; index < adjoiningZones.Length; index++)
                    mergedZones.Union(adjoiningZones[0], adjoiningZones[index]);
            }
        }

        var zoneByTile = Enumerable.Repeat(-1, rawZoneByTile.Length).ToArray();
        var mergedZoneIds = new Dictionary<int, int>();
        var tileCounts = new List<int>();
        for (var index = 0; index < rawZoneByTile.Length; index++)
        {
            if (rawZoneByTile[index] < 0)
                continue;
            var root = mergedZones.Find(rawZoneByTile[index]);
            if (!mergedZoneIds.TryGetValue(root, out var zone))
            {
                zone = mergedZoneIds.Count;
                mergedZoneIds[root] = zone;
                tileCounts.Add(0);
            }
            zoneByTile[index] = zone;
            tileCounts[zone]++;
        }
        return new ZoneLayout(zoneByTile, tileCounts);
    }

    private static void TryQueueFloor(
        WolfensteinMap map,
        int[] zoneByTile,
        Queue<(int X, int Y)> pending,
        int zone,
        int x,
        int y)
    {
        if (x < 0 || x >= map.Width || y < 0 || y >= map.Height ||
            !IsWalkableArea(map.GetWall(x, y)) ||
            GetZone(zoneByTile, map, x, y) >= 0)
        {
            return;
        }
        zoneByTile[(y * map.Width) + x] = zone;
        pending.Enqueue((x, y));
    }

    private static bool IsWalkableArea(ushort tile) => tile == AmbushTile || tile >= FirstAreaTile;

    private static bool ContributesToRoomAmbient(int spriteNumber)
    {
        // Green ceiling lights remain local light pools; counting them globally makes blue-stone corridors fully bright.
        return spriteNumber is 5 or 6;
    }

    private static double CalculateAmbientScale(
        double lightCoverage,
        double fixtureStrength,
        double chandelierStrength,
        int tileCount)
    {
        var coverageRatio = lightCoverage / Math.Max(1, tileCount);
        var coverageLevel = Math.Clamp(coverageRatio / FullLightingCoverage, 0.0, 1.0);
        var fixtureLevel = Math.Clamp(fixtureStrength / FullFixtureStrength, 0.0, 1.0);
        var lightLevel = Math.Min(coverageLevel, fixtureLevel);
        var smoothLightLevel = lightLevel * lightLevel * (3.0 - (2.0 * lightLevel));
        var chandelierLevel = Math.Clamp(chandelierStrength / FullChandelierStrength, 0.0, 1.0);
        var chandelierBoost = ChandelierAmbientBoost * chandelierLevel * coverageLevel;
        return Lerp(MinimumAmbientScale, 1.0, smoothLightLevel) + chandelierBoost;
    }

    private static void InheritSmallRoomAmbient(
        double[] ambientScales,
        IReadOnlyList<int> fixtureCounts,
        IReadOnlyList<int> tileCounts,
        IReadOnlyList<DoorTransition> transitions)
    {
        var originalScales = ambientScales.ToArray();
        for (var zone = 0; zone < ambientScales.Length; zone++)
        {
            if (tileCounts[zone] > SmallRoomTileCount || fixtureCounts[zone] > 0)
                continue;
            var adjoiningScale = transitions
                .Where(transition => transition.NegativeZone == zone || transition.PositiveZone == zone)
                .Select(transition => transition.NegativeZone == zone
                    ? originalScales[transition.PositiveZone]
                    : originalScales[transition.NegativeZone])
                .DefaultIfEmpty(originalScales[zone])
                .Max();
            if (adjoiningScale > ambientScales[zone])
            {
                ambientScales[zone] = Lerp(
                    ambientScales[zone],
                    adjoiningScale,
                    SmallRoomInheritance);
            }
        }
    }

    private static IReadOnlyList<DoorTransition> FindDoorTransitions(WolfensteinMap map, int[] zoneByTile)
    {
        var transitions = new List<DoorTransition>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var tile = map.GetWall(x, y);
                if (tile is < 90 or > 101)
                    continue;
                var isVertical = (tile & 1) == 0;
                var negativeZone = isVertical
                    ? GetZone(zoneByTile, map, x - 1, y)
                    : GetZone(zoneByTile, map, x, y - 1);
                var positiveZone = isVertical
                    ? GetZone(zoneByTile, map, x + 1, y)
                    : GetZone(zoneByTile, map, x, y + 1);
                if (negativeZone >= 0 && positiveZone >= 0 && negativeZone != positiveZone)
                    transitions.Add(new DoorTransition(x, y, isVertical, negativeZone, positiveZone));
            }
        }
        return transitions;
    }

    private int FindZone(int x, int y)
    {
        var zone = GetZone(m_zoneByTile, m_map, x, y);
        if (zone >= 0)
            return zone;
        zone = GetZone(m_zoneByTile, m_map, x - 1, y);
        if (zone >= 0)
            return zone;
        zone = GetZone(m_zoneByTile, m_map, x + 1, y);
        if (zone >= 0)
            return zone;
        zone = GetZone(m_zoneByTile, m_map, x, y - 1);
        return zone >= 0 ? zone : GetZone(m_zoneByTile, m_map, x, y + 1);
    }

    private double GetZoneAmbientScale(int zone) =>
        zone >= 0 && zone < m_zoneAmbientScales.Count ? m_zoneAmbientScales[zone] : 1.0;

    private static int GetZone(int[] zoneByTile, WolfensteinMap map, int x, int y) =>
        x < 0 || x >= map.Width || y < 0 || y >= map.Height
            ? -1
            : zoneByTile[(y * map.Width) + x];

    private static void ValidatePosition(double x, double y)
    {
        if (!double.IsFinite(x))
            throw new ArgumentOutOfRangeException(nameof(x));
        if (!double.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(y));
    }

    private static double Lerp(double first, double second, double amount) =>
        first + ((second - first) * amount);

    private sealed record ZoneLayout(int[] ZoneByTile, IReadOnlyList<int> TileCounts);

    private readonly record struct DoorTransition(
        int X,
        int Y,
        bool IsVertical,
        int NegativeZone,
        int PositiveZone);

    private sealed class DisjointSet
    {
        private readonly int[] m_parents;

        public DisjointSet(int count) => m_parents = Enumerable.Range(0, count).ToArray();

        public int Find(int item)
        {
            while (m_parents[item] != item)
            {
                m_parents[item] = m_parents[m_parents[item]];
                item = m_parents[item];
            }
            return item;
        }

        public void Union(int first, int second)
        {
            var firstRoot = Find(first);
            var secondRoot = Find(second);
            if (firstRoot != secondRoot)
                m_parents[secondRoot] = firstRoot;
        }
    }
}
