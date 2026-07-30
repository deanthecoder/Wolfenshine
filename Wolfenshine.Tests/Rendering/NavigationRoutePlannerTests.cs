// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using NUnit.Framework;
using Wolfenshine.Game;
using Wolfenshine.Maps;
using Wolfenshine.Rendering;

namespace Wolfenshine.Tests.Rendering;

/// <summary>
/// Verifies key-first navigation routes against doors and secret-wall topology.
/// </summary>
/// <remarks>
/// Synthetic corridors keep route choice deterministic while exercising the same tile rules as gameplay.
/// </remarks>
[TestFixture]
public sealed class NavigationRoutePlannerTests
{
    [Test]
    public void GivenReachableKeyThroughOrdinaryDoorCheckItIsChosenBeforeExit()
    {
        var map = CreateCorridorMap(doorTile: 90);
        var route = Find(map, [new WorldSprite(4.5, 2.5, 22)]);

        Assert.Multiple(() =>
        {
            Assert.That(route.TargetType, Is.EqualTo(NavigationTargetType.GoldKey));
            Assert.That(route.Points[^1], Is.EqualTo(new NavigationRoutePoint(4, 2)));
            Assert.That(route.Points, Does.Contain(new NavigationRoutePoint(3, 2)));
        });
    }

    [Test]
    public void GivenLockedDoorAndMatchingOwnedKeyCheckExitIsReachable()
    {
        var map = CreateCorridorMap(doorTile: 92);
        var route = Find(map, [], hasGoldKey: true);

        Assert.Multiple(() =>
        {
            Assert.That(route.TargetType, Is.EqualTo(NavigationTargetType.Exit));
            Assert.That(route.Points[^1], Is.EqualTo(new NavigationRoutePoint(6, 2)));
        });
    }

    [Test]
    public void GivenKeyBehindUnopenedSecretWallCheckGuideDoesNotRevealIt()
    {
        var (map, pushWalls) = CreateSecretMap();
        var route = Find(map, [new WorldSprite(7.5, 2.5, 23)], pushWalls: pushWalls);

        Assert.That(route.TargetType, Is.EqualTo(NavigationTargetType.Exit));
    }

    [Test]
    public void GivenSecretWallHasMovedCheckKeyBeyondItBecomesObtainable()
    {
        var (map, pushWalls) = CreateSecretMap();
        bool CanEnter(int x, int y) =>
            !map.IsSolid(x, y) || pushWalls.IsOriginalWallSuppressed(x, y);
        pushWalls.TryPush(4, 2, 1, 0, CanEnter);
        pushWalls.Update(128.0 / 70.0, CanEnter);
        pushWalls.Update(128.0 / 70.0, CanEnter);

        var route = Find(map, [new WorldSprite(7.5, 2.5, 23)], pushWalls: pushWalls);

        Assert.Multiple(() =>
        {
            Assert.That(route.TargetType, Is.EqualTo(NavigationTargetType.SilverKey));
            Assert.That(route.Points[^1], Is.EqualTo(new NavigationRoutePoint(7, 2)));
        });
    }

    private static NavigationRoute Find(
        WolfensteinMap map,
        IReadOnlyList<WorldSprite> objects,
        bool hasGoldKey = false,
        bool hasSilverKey = false,
        WolfensteinPushWalls pushWalls = null) =>
        NavigationRoutePlanner.Find(
            map,
            WolfensteinDoors.FromMap(map),
            pushWalls ?? new WolfensteinPushWalls(map),
            2,
            2,
            objects,
            hasGoldKey,
            hasSilverKey);

    private static WolfensteinMap CreateCorridorMap(ushort doorTile)
    {
        const int width = 9;
        const int height = 5;
        var walls = Enumerable.Repeat((ushort)1, width * height).ToArray();
        for (var x = 1; x <= 6; x++)
            walls[(2 * width) + x] = 107;
        walls[(2 * width) + 3] = doorTile;
        walls[(2 * width) + 7] = 21;
        return new WolfensteinMap(0, "Navigation Corridor", width, height, walls, new ushort[width * height]);
    }

    private static (WolfensteinMap Map, WolfensteinPushWalls PushWalls) CreateSecretMap()
    {
        const int width = 10;
        const int height = 5;
        var walls = Enumerable.Repeat((ushort)1, width * height).ToArray();
        for (var y = 1; y <= 3; y++)
        {
            for (var x = 1; x <= 3; x++)
                walls[(y * width) + x] = 107;
            for (var x = 5; x <= 8; x++)
                walls[(y * width) + x] = 107;
        }
        walls[(2 * width) + 1] = 21;
        walls[(2 * width) + 4] = 2;
        var objects = new ushort[width * height];
        objects[(2 * width) + 4] = 98;
        var map = new WolfensteinMap(0, "Navigation Secret", width, height, walls, objects);
        return (map, new WolfensteinPushWalls(map));
    }
}
