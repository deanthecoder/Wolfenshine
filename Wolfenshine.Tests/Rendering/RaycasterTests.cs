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
/// Verifies player-camera setup and grid-based wall raycasting.
/// </summary>
/// <remarks>
/// Small synthetic maps keep ray distances and wall sides deterministic.
/// </remarks>
public sealed class RaycasterTests
{
    [TestCase(19, 0.0, -1.0)]
    [TestCase(20, 1.0, 0.0)]
    [TestCase(21, 0.0, 1.0)]
    [TestCase(22, -1.0, 0.0)]
    public void GivenPlayerMarkerCheckCameraDirection(int marker, double expectedX, double expectedY)
    {
        var map = CreateMap((ushort)marker);

        var camera = RaycastCamera.FromPlayerStart(map);

        Assert.Multiple(() =>
        {
            Assert.That(camera.X, Is.EqualTo(2.5));
            Assert.That(camera.Y, Is.EqualTo(2.5));
            Assert.That(camera.DirectionX, Is.EqualTo(expectedX));
            Assert.That(camera.DirectionY, Is.EqualTo(expectedY));
        });
    }

    [Test]
    public void GivenNorthFacingCameraCheckCenterRayHitsNorthWall()
    {
        var map = CreateMap(19);
        var camera = RaycastCamera.FromPlayerStart(map);

        var columns = new WallColumn[1];
        Raycaster.Cast(map, WolfensteinDoors.FromMap(map), camera, columns);
        var column = columns[0];

        Assert.Multiple(() =>
        {
            Assert.That(column.Tile, Is.EqualTo(1));
            Assert.That(column.Side, Is.EqualTo(WallSide.Horizontal));
            Assert.That(column.Distance, Is.EqualTo(1.5).Within(0.0001));
            Assert.That(column.TextureU, Is.EqualTo(0.5).Within(0.0001));
            Assert.That(column.HasConcaveTextureStart, Is.False);
            Assert.That(column.HasConcaveTextureEnd, Is.False);
        });
    }

    [Test]
    public void GivenInwardWallJoinCheckConcaveTextureStartIsIdentified()
    {
        var map = CreateMap(19);
        ((ushort[])map.Walls)[(1 * map.Width) + 1] = 1;
        var camera = RaycastCamera.FromPlayerStart(map);
        var columns = new WallColumn[1];

        Raycaster.Cast(map, WolfensteinDoors.FromMap(map), camera, columns);

        Assert.Multiple(() =>
        {
            Assert.That(columns[0].HasConcaveTextureStart, Is.True);
            Assert.That(columns[0].HasConcaveTextureEnd, Is.False);
        });
    }

    [Test]
    public void GivenMirroredInwardWallJoinCheckConcaveFlagFollowsTextureOrientation()
    {
        var map = CreateMap(19);
        ((ushort[])map.Walls)[(3 * map.Width) + 1] = 1;
        var camera = new RaycastCamera(2.5, 2.5, 0.0, 1.0, 0.0, 0.0);
        var columns = new WallColumn[1];

        Raycaster.Cast(map, WolfensteinDoors.FromMap(map), camera, columns);

        Assert.Multiple(() =>
        {
            Assert.That(columns[0].HasConcaveTextureStart, Is.False);
            Assert.That(columns[0].HasConcaveTextureEnd, Is.True);
        });
    }

    [Test]
    public void GivenAmbushMarkerCheckRayContinuesToWallBehindIt()
    {
        var map = CreateMap(19);
        ((ushort[])map.Walls)[(1 * map.Width) + 2] = 106;
        var camera = RaycastCamera.FromPlayerStart(map);
        var columns = new WallColumn[1];

        Raycaster.Cast(map, WolfensteinDoors.FromMap(map), camera, columns);

        Assert.Multiple(() =>
        {
            Assert.That(columns[0].Tile, Is.EqualTo(1));
            Assert.That(columns[0].Distance, Is.EqualTo(1.5).Within(0.0001));
        });
    }

    [Test]
    public void GivenUnenclosedMapPaddingCheckBoundaryIsRenderedAsSolid()
    {
        const int size = 3;
        var walls = Enumerable.Repeat((ushort)107, size * size).ToArray();
        var objects = new ushort[size * size];
        var map = new WolfensteinMap(0, "Open Map", size, size, walls, objects);
        var camera = new RaycastCamera(1.5, 1.5, 1.0, 0.0, 0.0, 0.0);
        var columns = new WallColumn[1];

        Raycaster.Cast(map, WolfensteinDoors.FromMap(map), camera, columns);

        Assert.Multiple(() =>
        {
            Assert.That(columns[0].Tile, Is.EqualTo(1));
            Assert.That(columns[0].Side, Is.EqualTo(WallSide.Vertical));
            Assert.That(columns[0].Distance, Is.EqualTo(1.5).Within(0.0001));
        });
    }

    [Test]
    public void GivenSymmetricRoomCheckRayDistancesAreSymmetric()
    {
        var map = CreateMap(19);
        var camera = RaycastCamera.FromPlayerStart(map);

        var columns = new WallColumn[5];
        Raycaster.Cast(map, WolfensteinDoors.FromMap(map), camera, columns);

        Assert.That(columns[0].Distance, Is.EqualTo(columns[^1].Distance).Within(0.0001));
        Assert.That(columns[1].Distance, Is.EqualTo(columns[^2].Distance).Within(0.0001));
    }

    [TestCase(1.0, 0.0, 24)]
    [TestCase(-1.0, 0.0, 39)]
    [TestCase(0.0, -1.0, 16)]
    [TestCase(0.0, 1.0, 47)]
    public void GivenWallFaceCheckTextureDirectionMatchesOriginal(
        double directionX,
        double directionY,
        int expectedTextureColumn)
    {
        var map = CreateMap(19);
        var camera = new RaycastCamera(2.25, 2.375, directionX, directionY, 0.0, 0.0);
        var columns = new WallColumn[1];

        Raycaster.Cast(map, WolfensteinDoors.FromMap(map), camera, columns);

        var textureColumn = (int)(columns[0].TextureU * 64);
        Assert.That(textureColumn, Is.EqualTo(expectedTextureColumn));
    }

    [Test]
    public void GivenOpeningDoorCheckExposedRaysContinueThroughDoorway()
    {
        var map = CreateDoorMap();
        var camera = RaycastCamera.FromPlayerStart(map);
        var doors = WolfensteinDoors.FromMap(map);
        var columns = new WallColumn[1];
        Raycaster.Cast(map, doors, camera, columns);
        var closedDistance = columns[0].Distance;
        doors.Items[0].Open();
        doors.Update(0.6);

        Raycaster.Cast(map, doors, camera, columns);

        Assert.That(closedDistance, Is.EqualTo(1.0).Within(0.0001));
        Assert.That(columns[0].Distance, Is.GreaterThan(2.0));
    }

    [Test]
    public void GivenRayIntoDoorRecessCheckInnerWallIsMarkedAsJamb()
    {
        var map = CreateDoorMap();
        var camera = new RaycastCamera(2.5, 3.5, -0.75, -1.0, 0.0, 0.0);
        var columns = new WallColumn[1];

        Raycaster.Cast(map, WolfensteinDoors.FromMap(map), camera, columns);

        Assert.That(columns[0].IsDoorJamb, Is.True);
    }

    [Test]
    public void GivenMovingPushwallCheckRayHitsItsContinuousPosition()
    {
        var map = CreateMap(19);
        var wallTiles = (ushort[])map.Walls;
        var objects = (ushort[])map.Objects;
        wallTiles[(1 * map.Width) + 2] = 2;
        objects[(1 * map.Width) + 2] = 98;
        var camera = RaycastCamera.FromPlayerStart(map);
        var pushWalls = new WolfensteinPushWalls(map);
        pushWalls.TryPush(2, 1, 0, -1, (_, _) => true);
        pushWalls.Update(64.0 / 70.0, (_, _) => true);
        var columns = new WallColumn[1];

        Raycaster.Cast(map, WolfensteinDoors.FromMap(map), pushWalls, camera, columns);

        Assert.That(columns[0].Distance, Is.EqualTo(1.0).Within(0.0001));
    }

    private static WolfensteinMap CreateMap(ushort playerMarker)
    {
        const int size = 5;
        var walls = Enumerable.Repeat((ushort)107, size * size).ToArray();
        for (var i = 0; i < size; i++)
        {
            walls[i] = 1;
            walls[((size - 1) * size) + i] = 1;
            walls[i * size] = 1;
            walls[(i * size) + size - 1] = 1;
        }

        var objects = new ushort[size * size];
        objects[(2 * size) + 2] = playerMarker;
        return new WolfensteinMap(0, "Test Map", size, size, walls, objects);
    }

    private static WolfensteinMap CreateDoorMap()
    {
        const int size = 5;
        var walls = Enumerable.Repeat((ushort)107, size * size).ToArray();
        for (var i = 0; i < size; i++)
        {
            walls[i] = 1;
            walls[((size - 1) * size) + i] = 1;
            walls[i * size] = 1;
            walls[(i * size) + size - 1] = 1;
        }

        walls[(2 * size) + 1] = 1;
        walls[(2 * size) + 2] = 91;
        walls[(2 * size) + 3] = 1;
        var objects = new ushort[size * size];
        objects[(3 * size) + 2] = 19;
        return new WolfensteinMap(0, "Door Map", size, size, walls, objects);
    }
}
