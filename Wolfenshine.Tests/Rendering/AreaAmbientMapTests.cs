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
/// Verifies area light coverage and doorway ambient transitions.
/// </summary>
[TestFixture]
public sealed class AreaAmbientMapTests
{
    [Test]
    public void GivenUnlitAreaCheckMinimumAmbientIsUsed()
    {
        var map = CreateSingleAreaMap(10, 10);
        var ambientMap = AreaAmbientMap.FromMap(map);

        var ambientScale = ambientMap.GetAmbientScale(5.5, 5.5, WolfensteinDoors.FromMap(map));

        Assert.That(ambientScale, Is.EqualTo(0.35).Within(0.0001));
    }

    [Test]
    public void GivenSingleLampInSmallRoomCheckMediumAmbientIsUsed()
    {
        var map = CreateSingleAreaMap(7, 7, (3, 3, 26));
        var ambientMap = AreaAmbientMap.FromMap(map);

        var ambientScale = ambientMap.GetAmbientScale(3.5, 3.5, WolfensteinDoors.FromMap(map));

        Assert.That(ambientScale, Is.InRange(0.45, 0.55));
    }

    [Test]
    public void GivenThreeLampsInSmallRoomCheckFullAmbientIsUsed()
    {
        var map = CreateSingleAreaMap(7, 7, (2, 3, 26), (3, 3, 26), (4, 3, 26));
        var ambientMap = AreaAmbientMap.FromMap(map);

        var ambientScale = ambientMap.GetAmbientScale(3.5, 3.5, WolfensteinDoors.FromMap(map));

        Assert.That(ambientScale, Is.EqualTo(1.0).Within(0.001));
    }

    [Test]
    public void GivenSparseLampInLargeAreaCheckDimAmbientIsUsed()
    {
        var map = CreateSingleAreaMap(10, 10, (5, 5, 26));
        var ambientMap = AreaAmbientMap.FromMap(map);

        var ambientScale = ambientMap.GetAmbientScale(5.5, 5.5, WolfensteinDoors.FromMap(map));

        Assert.That(ambientScale, Is.InRange(0.45, 0.55));
    }

    [Test]
    public void GivenOpenDoorBetweenDarkAndLightAreasCheckAmbientBlendsAcrossThreshold()
    {
        var map = CreateTwoAreaDoorMap();
        var doors = WolfensteinDoors.FromMap(map);
        doors.Items[0].Open();
        doors.Update(1.0);
        var ambientMap = AreaAmbientMap.FromMap(map);

        var darkSide = ambientMap.GetAmbientScale(2.0, 2.5, doors);
        var threshold = ambientMap.GetAmbientScale(3.5, 2.5, doors);
        var lightSide = ambientMap.GetAmbientScale(5.0, 2.5, doors);

        Assert.Multiple(() =>
        {
            Assert.That(darkSide, Is.EqualTo(0.35).Within(0.0001));
            Assert.That(threshold, Is.EqualTo((darkSide + lightSide) * 0.5).Within(0.0001));
            Assert.That(lightSide, Is.EqualTo(1.0).Within(0.001));
        });
    }

    [Test]
    public void GivenClosedDoorCheckAmbientDoesNotLeakBetweenAreas()
    {
        var map = CreateTwoAreaDoorMap();
        var doors = WolfensteinDoors.FromMap(map);
        var ambientMap = AreaAmbientMap.FromMap(map);

        var darkSide = ambientMap.GetAmbientScale(2.75, 2.5, doors);

        Assert.That(darkSide, Is.EqualTo(0.35).Within(0.0001));
    }

    private static WolfensteinMap CreateSingleAreaMap(
        int width,
        int height,
        params (int X, int Y, ushort Marker)[] objects)
    {
        var walls = Enumerable.Repeat((ushort)107, width * height).ToArray();
        var objectPlane = new ushort[width * height];
        foreach (var (x, y, marker) in objects)
            objectPlane[(y * width) + x] = marker;
        return new WolfensteinMap(0, "Ambient Test", width, height, walls, objectPlane);
    }

    private static WolfensteinMap CreateTwoAreaDoorMap()
    {
        const int width = 7;
        const int height = 5;
        var walls = new ushort[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                walls[(y * width) + x] = x < 3 ? (ushort)107 : (ushort)108;
        }
        walls[(2 * width) + 3] = 90;
        var objects = new ushort[width * height];
        objects[(1 * width) + 5] = 26;
        objects[(2 * width) + 5] = 26;
        objects[(3 * width) + 5] = 26;
        return new WolfensteinMap(0, "Door Ambient Test", width, height, walls, objects);
    }
}
