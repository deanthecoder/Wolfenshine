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

        var column = Raycaster.Cast(map, camera, 1)[0];

        Assert.Multiple(() =>
        {
            Assert.That(column.Tile, Is.EqualTo(1));
            Assert.That(column.Side, Is.EqualTo(WallSide.Horizontal));
            Assert.That(column.Distance, Is.EqualTo(1.5).Within(0.0001));
            Assert.That(column.TextureU, Is.EqualTo(0.5).Within(0.0001));
        });
    }

    [Test]
    public void GivenSymmetricRoomCheckRayDistancesAreSymmetric()
    {
        var map = CreateMap(19);
        var camera = RaycastCamera.FromPlayerStart(map);

        var columns = Raycaster.Cast(map, camera, 5);

        Assert.That(columns[0].Distance, Is.EqualTo(columns[^1].Distance).Within(0.0001));
        Assert.That(columns[1].Distance, Is.EqualTo(columns[^2].Distance).Within(0.0001));
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
}
