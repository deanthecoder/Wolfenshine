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
/// Estimates ambient light from fixture coverage within Wolfenstein map areas and blends it across doors.
/// </summary>
/// <remarks>
/// Original area tiles provide room boundaries. Both fixture strength and illuminated floor coverage are considered,
/// preventing either one strong lamp or several weak lamps in a large corridor from making it fully bright.
/// </remarks>
public sealed class AreaAmbientMap
{
    private const ushort FirstAreaTile = 107;
    private const double MinimumAmbientScale = 0.35;
    private const double FullLightingCoverage = 0.38;
    private const double FullFixtureStrength = 2.4;
    private const double DoorBlendRadius = 1.50;
    private const double DoorBlendHalfWidth = 0.85;
    private readonly WolfensteinMap m_map;
    private readonly IReadOnlyDictionary<ushort, double> m_areaAmbientScales;
    private readonly IReadOnlyList<DoorTransition> m_doorTransitions;

    private AreaAmbientMap(
        WolfensteinMap map,
        IReadOnlyDictionary<ushort, double> areaAmbientScales,
        IReadOnlyList<DoorTransition> doorTransitions)
    {
        m_map = map;
        m_areaAmbientScales = areaAmbientScales;
        m_doorTransitions = doorTransitions;
    }

    /// <summary>
    /// Measures static light coverage for every numbered floor area in a map.
    /// </summary>
    public static AreaAmbientMap FromMap(WolfensteinMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var areaTileCounts = new Dictionary<ushort, int>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var area = map.GetWall(x, y);
                if (area >= FirstAreaTile)
                    areaTileCounts[area] = areaTileCounts.GetValueOrDefault(area) + 1;
            }
        }

        var areaLightCoverage = new Dictionary<ushort, double>();
        var areaFixtureStrength = new Dictionary<ushort, double>();
        foreach (var worldSprite in WolfensteinStaticObjects.FromMap(map))
        {
            if (WolfensteinStaticObjects.GetPickupType(worldSprite.SpriteNumber) != WolfensteinPickupType.None)
                continue;
            var (_, downwardBrightness) = WolfensteinStaticObjects.GetLightBrightness(worldSprite.SpriteNumber);
            var (_, downwardRadius) = WolfensteinStaticObjects.GetLightRadii(worldSprite.SpriteNumber);
            if (downwardBrightness <= 0.0f || downwardRadius <= 0.0f)
                continue;
            var area = FindArea(map, (int)worldSprite.X, (int)worldSprite.Y);
            if (area < FirstAreaTile)
                continue;
            var coverage = Math.PI * downwardRadius * downwardRadius * downwardBrightness;
            areaLightCoverage[area] = areaLightCoverage.GetValueOrDefault(area) + coverage;
            areaFixtureStrength[area] = areaFixtureStrength.GetValueOrDefault(area) + downwardBrightness;
        }

        var areaAmbientScales = areaTileCounts.ToDictionary(
            pair => pair.Key,
            pair => CalculateAmbientScale(
                areaLightCoverage.GetValueOrDefault(pair.Key),
                areaFixtureStrength.GetValueOrDefault(pair.Key),
                pair.Value));
        return new AreaAmbientMap(map, areaAmbientScales, FindDoorTransitions(map));
    }

    /// <summary>
    /// Returns the local ambient scale, including a smooth transition when passing through an open door.
    /// </summary>
    public double GetAmbientScale(double x, double y, WolfensteinDoors doors)
    {
        if (!double.IsFinite(x))
            throw new ArgumentOutOfRangeException(nameof(x));
        if (!double.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(y));
        ArgumentNullException.ThrowIfNull(doors);
        if (!ReferenceEquals(m_map, doors.Map))
            throw new ArgumentException("The door collection belongs to a different map.", nameof(doors));

        var currentArea = FindArea(m_map, (int)Math.Floor(x), (int)Math.Floor(y));
        var ambientScale = GetAreaAmbientScale(currentArea);
        var nearestDoorDistanceSquared = double.PositiveInfinity;
        foreach (var transition in m_doorTransitions)
        {
            var door = doors.Get(transition.X, transition.Y);
            if (door == null || door.OpenAmount <= 0.0)
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
                GetAreaAmbientScale(transition.NegativeArea),
                GetAreaAmbientScale(transition.PositiveArea),
                smoothBlend);
            ambientScale = Lerp(ambientScale, doorAmbient, door.OpenAmount);
            nearestDoorDistanceSquared = distanceSquared;
        }
        return ambientScale;
    }

    private static double CalculateAmbientScale(double lightCoverage, double fixtureStrength, int areaTileCount)
    {
        var coverageRatio = lightCoverage / Math.Max(1, areaTileCount);
        var coverageLevel = Math.Clamp(coverageRatio / FullLightingCoverage, 0.0, 1.0);
        var fixtureLevel = Math.Clamp(fixtureStrength / FullFixtureStrength, 0.0, 1.0);
        var lightLevel = Math.Min(coverageLevel, fixtureLevel);
        var smoothLightLevel = lightLevel * lightLevel * (3.0 - (2.0 * lightLevel));
        return Lerp(MinimumAmbientScale, 1.0, smoothLightLevel);
    }

    private double GetAreaAmbientScale(ushort area) =>
        m_areaAmbientScales.GetValueOrDefault(area, 1.0);

    private static IReadOnlyList<DoorTransition> FindDoorTransitions(WolfensteinMap map)
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
                var negativeArea = isVertical ? FindArea(map, x - 1, y) : FindArea(map, x, y - 1);
                var positiveArea = isVertical ? FindArea(map, x + 1, y) : FindArea(map, x, y + 1);
                if (negativeArea >= FirstAreaTile && positiveArea >= FirstAreaTile)
                    transitions.Add(new DoorTransition(x, y, isVertical, negativeArea, positiveArea));
            }
        }
        return transitions;
    }

    private static ushort FindArea(WolfensteinMap map, int x, int y)
    {
        if (x < 0 || x >= map.Width || y < 0 || y >= map.Height)
            return 0;
        var area = map.GetWall(x, y);
        if (area >= FirstAreaTile)
            return area;
        if (x > 0 && map.GetWall(x - 1, y) >= FirstAreaTile)
            return map.GetWall(x - 1, y);
        if (x + 1 < map.Width && map.GetWall(x + 1, y) >= FirstAreaTile)
            return map.GetWall(x + 1, y);
        if (y > 0 && map.GetWall(x, y - 1) >= FirstAreaTile)
            return map.GetWall(x, y - 1);
        return y + 1 < map.Height && map.GetWall(x, y + 1) >= FirstAreaTile
            ? map.GetWall(x, y + 1)
            : (ushort)0;
    }

    private static double Lerp(double first, double second, double amount) =>
        first + ((second - first) * amount);

    private readonly record struct DoorTransition(
        int X,
        int Y,
        bool IsVertical,
        ushort NegativeArea,
        ushort PositiveArea);
}
