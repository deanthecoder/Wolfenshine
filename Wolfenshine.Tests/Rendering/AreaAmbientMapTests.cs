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
/// Verifies geometry-derived room light coverage and doorway ambient transitions.
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

        Assert.That(ambientScale, Is.EqualTo(AreaAmbientMap.MinimumAmbientScale).Within(0.0001));
    }

    [Test]
    public void GivenSingleLampInSmallRoomCheckMediumAmbientIsUsed()
    {
        var map = CreateSingleAreaMap(7, 7, (3, 3, 26));
        var ambientMap = AreaAmbientMap.FromMap(map);

        var ambientScale = ambientMap.GetAmbientScale(3.5, 3.5, WolfensteinDoors.FromMap(map));

        Assert.That(ambientScale, Is.InRange(0.55, 0.60));
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
    public void GivenThreeChandeliersCheckTheyRaiseAmbientAboveOrdinaryFullLighting()
    {
        var map = CreateSingleAreaMap(12, 10, (3, 5, 27), (6, 5, 27), (9, 5, 27));
        var ambientMap = AreaAmbientMap.FromMap(map);

        var ambientScale = ambientMap.GetAmbientScale(6.5, 5.5);

        Assert.That(ambientScale, Is.EqualTo(AreaAmbientMap.MaximumAmbientScale).Within(0.001));
    }

    [Test]
    public void GivenSparseLampInLargeAreaCheckDimAmbientIsUsed()
    {
        var map = CreateSingleAreaMap(10, 10, (5, 5, 26));
        var ambientMap = AreaAmbientMap.FromMap(map);

        var ambientScale = ambientMap.GetAmbientScale(5.5, 5.5, WolfensteinDoors.FromMap(map));

        Assert.That(ambientScale, Is.InRange(0.55, 0.60));
    }

    [Test]
    public void GivenGreenCeilingLightsCheckTheyRemainLocalLightsOnly()
    {
        var map = CreateSingleAreaMap(7, 7, (2, 3, 37), (3, 3, 37), (4, 3, 37));
        var ambientMap = AreaAmbientMap.FromMap(map);

        var ambientScale = ambientMap.GetAmbientScale(3.5, 3.5, WolfensteinDoors.FromMap(map));

        Assert.That(ambientScale, Is.EqualTo(AreaAmbientMap.MinimumAmbientScale).Within(0.0001));
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
        var threshold = ambientMap.GetAmbientScale(4.5, 2.5, doors);
        var lightSide = ambientMap.GetAmbientScale(7.0, 2.5, doors);

        Assert.Multiple(() =>
        {
            Assert.That(darkSide, Is.EqualTo(AreaAmbientMap.MinimumAmbientScale).Within(0.0001));
            Assert.That(threshold, Is.EqualTo((darkSide + lightSide) * 0.5).Within(0.0001));
            Assert.That(lightSide, Is.EqualTo(1.0).Within(0.001));
        });
    }

    [Test]
    public void GivenPartlyOpenDoorCheckBlendExtendsIntoBothRooms()
    {
        var map = CreateTwoAreaDoorMap();
        var doors = WolfensteinDoors.FromMap(map);
        doors.Items[0].Open();
        doors.Update(0.5);
        var ambientMap = AreaAmbientMap.FromMap(map);

        var farDarkSide = ambientMap.GetAmbientScale(2.0, 2.5, doors);
        var nearDarkSide = ambientMap.GetAmbientScale(3.5, 2.5, doors);
        var threshold = ambientMap.GetAmbientScale(4.5, 2.5, doors);
        var nearLightSide = ambientMap.GetAmbientScale(5.5, 2.5, doors);

        Assert.Multiple(() =>
        {
            Assert.That(farDarkSide, Is.EqualTo(AreaAmbientMap.MinimumAmbientScale).Within(0.0001));
            Assert.That(nearDarkSide, Is.GreaterThan(farDarkSide));
            Assert.That(threshold, Is.GreaterThan(nearDarkSide));
            Assert.That(nearLightSide, Is.GreaterThan(threshold));
            Assert.That(nearLightSide, Is.LessThan(1.0));
        });
    }

    [Test]
    public void GivenClosedDoorCheckAmbientDoesNotLeakBetweenAreas()
    {
        var map = CreateTwoAreaDoorMap();
        var doors = WolfensteinDoors.FromMap(map);
        var ambientMap = AreaAmbientMap.FromMap(map);

        var darkSide = ambientMap.GetAmbientScale(3.75, 2.5, doors);

        Assert.That(darkSide, Is.EqualTo(AreaAmbientMap.MinimumAmbientScale).Within(0.0001));
    }

    [Test]
    public void GivenUnlitSmallRoomBesideBrightRoomCheckItInheritsRestrainedAmbient()
    {
        var map = CreateSmallRoomMap(hasDoor: true, lightMarker: 26);
        var ambientMap = AreaAmbientMap.FromMap(map);

        var smallRoom = ambientMap.GetAmbientScale(1.5, 2.5);
        var brightRoom = ambientMap.GetAmbientScale(6.5, 2.5);

        Assert.Multiple(() =>
        {
            Assert.That(smallRoom, Is.InRange(0.80, 0.90));
            Assert.That(brightRoom, Is.EqualTo(1.0).Within(0.001));
        });
    }

    [Test]
    public void GivenUnlitSmallRoomBesideGreenLitDungeonCheckItRemainsDark()
    {
        var map = CreateSmallRoomMap(hasDoor: true, lightMarker: 37);
        var ambientMap = AreaAmbientMap.FromMap(map);

        Assert.Multiple(() =>
        {
            Assert.That(
                ambientMap.GetAmbientScale(1.5, 2.5),
                Is.EqualTo(AreaAmbientMap.MinimumAmbientScale).Within(0.0001));
            Assert.That(
                ambientMap.GetAmbientScale(6.5, 2.5),
                Is.EqualTo(AreaAmbientMap.MinimumAmbientScale).Within(0.0001));
        });
    }

    [Test]
    public void GivenSecretWallBetweenDarkAndBrightFloorCheckLightingDoesNotRevealIt()
    {
        var map = CreateSmallRoomMap(hasDoor: false, lightMarker: 26);
        var ambientMap = AreaAmbientMap.FromMap(map);

        var visibleSide = ambientMap.GetAmbientScale(1.5, 2.5);
        var secretSide = ambientMap.GetAmbientScale(6.5, 2.5);

        Assert.That(visibleSide, Is.EqualTo(secretSide).Within(0.0001));
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
        const int width = 9;
        const int height = 5;
        var walls = new ushort[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                walls[(y * width) + x] = 107;
            walls[(y * width) + 4] = 1;
        }
        walls[(2 * width) + 4] = 90;
        var objects = new ushort[width * height];
        objects[(1 * width) + 7] = 26;
        objects[(2 * width) + 7] = 26;
        objects[(3 * width) + 7] = 26;
        return new WolfensteinMap(0, "Door Ambient Test", width, height, walls, objects);
    }

    private static WolfensteinMap CreateSmallRoomMap(bool hasDoor, ushort lightMarker)
    {
        const int width = 9;
        const int height = 5;
        var walls = Enumerable.Repeat((ushort)1, width * height).ToArray();
        for (var y = 1; y <= 3; y++)
        {
            for (var x = 1; x <= 2; x++)
                walls[(y * width) + x] = 107;
            for (var x = 4; x <= 7; x++)
                walls[(y * width) + x] = 107;
        }
        var objects = new ushort[width * height];
        if (hasDoor)
        {
            walls[(2 * width) + 3] = 90;
        }
        else
        {
            walls[(2 * width) + 3] = 1;
            objects[(2 * width) + 3] = 98;
        }
        objects[(1 * width) + 5] = lightMarker;
        objects[(2 * width) + 6] = lightMarker;
        objects[(3 * width) + 5] = lightMarker;
        return new WolfensteinMap(0, "Small Room Ambient Test", width, height, walls, objects);
    }
}
