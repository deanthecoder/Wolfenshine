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

namespace Wolfenshine.Tests.Game;

/// <summary>
/// Verifies static map-marker conversion into world sprite instances.
/// </summary>
/// <remarks>
/// Boundary markers protect the original plane-one to sprite-number mapping.
/// </remarks>
public sealed class WolfensteinStaticObjectsTests
{
    [Test]
    public void GivenStaticMarkersCheckWorldSpritesAreCreatedAtTileCenters()
    {
        var map = new WolfensteinMap(
            0,
            "Objects",
            3,
            1,
            new ushort[] { 107, 107, 107 },
            new ushort[] { 23, 70, 19 });

        var objects = WolfensteinStaticObjects.FromMap(map);

        Assert.That(objects, Is.EqualTo(new[]
        {
            new WorldSprite(0.5, 0.5, 2),
            new WorldSprite(1.5, 0.5, 49)
        }));
    }

    [TestCase(26)] // Floor lamp.
    [TestCase(31)] // Tree.
    [TestCase(58)] // Barrel.
    public void GivenSolidSceneryMarkerCheckMovementIsBlocked(int marker)
    {
        Assert.That(WolfensteinStaticObjects.BlocksMovement((ushort)marker), Is.True);
    }

    [TestCase(23)] // Puddle.
    [TestCase(27)] // Chandelier.
    [TestCase(47)] // Food.
    [TestCase(70)] // Vines.
    public void GivenNonSolidSceneryMarkerCheckMovementIsAllowed(int marker)
    {
        Assert.That(WolfensteinStaticObjects.BlocksMovement((ushort)marker), Is.False);
    }

    [TestCase(8, WolfensteinPickupType.DogFood)]
    [TestCase(22, WolfensteinPickupType.GoldKey)]
    [TestCase(23, WolfensteinPickupType.SilverKey)]
    [TestCase(28, WolfensteinPickupType.AmmoClip)]
    [TestCase(26, WolfensteinPickupType.Food)]
    [TestCase(27, WolfensteinPickupType.FirstAid)]
    [TestCase(35, WolfensteinPickupType.FullHeal)]
    [TestCase(29, WolfensteinPickupType.MachineGun)]
    [TestCase(30, WolfensteinPickupType.Chaingun)]
    [TestCase(31, WolfensteinPickupType.Cross)]
    [TestCase(32, WolfensteinPickupType.Chalice)]
    [TestCase(33, WolfensteinPickupType.Bible)]
    [TestCase(34, WolfensteinPickupType.Crown)]
    [TestCase(2, WolfensteinPickupType.None)]
    public void GivenStaticSpriteCheckPickupTypeIsIdentified(int spriteNumber, WolfensteinPickupType expected)
    {
        Assert.That(WolfensteinStaticObjects.GetPickupType(spriteNumber), Is.EqualTo(expected));
    }
}
