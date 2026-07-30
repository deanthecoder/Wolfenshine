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
/// Verifies that static light candidates follow door connectivity rather than camera distance alone.
/// </summary>
[TestFixture]
public sealed class AccessibleLightCacheTests
{
    [Test]
    public void GivenClosedDoorCheckOnlyLightsOnPlayerSideAreAccessible()
    {
        var (map, doors, lights) = CreateDoorMap();
        var cache = new AccessibleLightCache();

        var changed = cache.Refresh(map, doors, new WolfensteinPushWalls(map), CreateCamera(2.5), lights);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(cache.Lights, Is.EqualTo(new[] { lights[0] }));
        });
    }

    [Test]
    public void GivenDoorStartsOpeningCheckLightsBeyondItBecomeAccessibleImmediately()
    {
        var (map, doors, lights) = CreateDoorMap();
        var cache = new AccessibleLightCache();
        var pushWalls = new WolfensteinPushWalls(map);
        cache.Refresh(map, doors, pushWalls, CreateCamera(2.5), lights);

        doors.Items[0].Open();
        var changed = cache.Refresh(map, doors, pushWalls, CreateCamera(2.5), lights);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(cache.Lights, Is.EqualTo(lights));
        });
    }

    [Test]
    public void GivenDoorClosesCheckLightsRemainAccessibleUntilFullyClosed()
    {
        var (map, doors, lights) = CreateDoorMap();
        var cache = new AccessibleLightCache();
        doors.Items[0].Open();
        doors.Update(1.0);
        var pushWalls = new WolfensteinPushWalls(map);
        cache.Refresh(map, doors, pushWalls, CreateCamera(2.5), lights);

        doors.Items[0].Operate(canClose: true);
        var changedWhileClosing = cache.Refresh(map, doors, pushWalls, CreateCamera(2.5), lights);
        doors.Update(1.0);
        var changedWhenClosed = cache.Refresh(map, doors, pushWalls, CreateCamera(2.5), lights);

        Assert.Multiple(() =>
        {
            Assert.That(changedWhileClosing, Is.False);
            Assert.That(changedWhenClosed, Is.True);
            Assert.That(cache.Lights, Is.EqualTo(new[] { lights[0] }));
        });
    }

    [Test]
    public void GivenCameraMovesWithinAccessibleAreaCheckCacheIsReused()
    {
        var (map, doors, lights) = CreateDoorMap();
        var cache = new AccessibleLightCache();
        var pushWalls = new WolfensteinPushWalls(map);
        cache.Refresh(map, doors, pushWalls, CreateCamera(2.5), lights);

        var changed = cache.Refresh(map, doors, pushWalls, CreateCamera(3.25), lights);

        Assert.That(changed, Is.False);
    }

    [Test]
    public void GivenPushwallMovesAsideCheckLightsInSecretRoomBecomeAccessible()
    {
        var (map, pushWalls, lights) = CreatePushWallMap();
        var doors = WolfensteinDoors.FromMap(map);
        var cache = new AccessibleLightCache();
        bool CanEnter(int x, int y) =>
            !map.IsSolid(x, y) || pushWalls.IsOriginalWallSuppressed(x, y);
        cache.Refresh(map, doors, pushWalls, CreateCamera(2.5), lights);

        pushWalls.TryPush(4, 2, 1, 0, CanEnter);
        pushWalls.Update(128.0 / 70.0, CanEnter);
        pushWalls.Update(128.0 / 70.0, CanEnter);
        var changed = cache.Refresh(map, doors, pushWalls, CreateCamera(2.5), lights);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(pushWalls.Items[0].Distance, Is.EqualTo(2.0));
            Assert.That(cache.Lights, Is.EqualTo(lights));
        });
    }

    private static (
        WolfensteinMap Map,
        WolfensteinDoors Doors,
        IReadOnlyList<WorldSprite> Lights) CreateDoorMap()
    {
        const int width = 9;
        const int height = 5;
        var walls = Enumerable.Repeat((ushort)1, width * height).ToArray();
        for (var y = 1; y <= 3; y++)
        {
            for (var x = 1; x <= 3; x++)
                walls[(y * width) + x] = 107;
            for (var x = 5; x <= 7; x++)
                walls[(y * width) + x] = 107;
        }
        walls[(2 * width) + 4] = 90;
        var map = new WolfensteinMap(
            0,
            "Accessible Light Test",
            width,
            height,
            walls,
            new ushort[width * height]);
        IReadOnlyList<WorldSprite> lights =
        [
            new WorldSprite(2.5, 2.5, 5),
            new WorldSprite(6.5, 2.5, 5)
        ];
        return (map, WolfensteinDoors.FromMap(map), lights);
    }

    private static (
        WolfensteinMap Map,
        WolfensteinPushWalls PushWalls,
        IReadOnlyList<WorldSprite> Lights) CreatePushWallMap()
    {
        const int width = 9;
        const int height = 5;
        var walls = Enumerable.Repeat((ushort)1, width * height).ToArray();
        for (var y = 1; y <= 3; y++)
        {
            for (var x = 1; x <= 3; x++)
                walls[(y * width) + x] = 107;
            for (var x = 5; x <= 7; x++)
                walls[(y * width) + x] = 107;
        }
        walls[(2 * width) + 4] = 2;
        var objects = new ushort[width * height];
        objects[(2 * width) + 4] = 98;
        var map = new WolfensteinMap(
            0,
            "Accessible Pushwall Light Test",
            width,
            height,
            walls,
            objects);
        IReadOnlyList<WorldSprite> lights =
        [
            new WorldSprite(2.5, 2.5, 5),
            new WorldSprite(7.5, 2.5, 5)
        ];
        return (map, new WolfensteinPushWalls(map), lights);
    }

    private static RaycastCamera CreateCamera(double x) => new(x, 2.5, 1.0, 0.0, 0.0, 0.66);
}
