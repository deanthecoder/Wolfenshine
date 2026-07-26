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

namespace Wolfenshine.Tests.Game;

/// <summary>
/// Verifies original door-tile interpretation and opening animation.
/// </summary>
/// <remarks>
/// Ordinary doors follow the original opening, waiting, obstruction, and closing cycle.
/// </remarks>
public sealed class WolfensteinDoorTests
{
    [TestCase(90, DoorOrientation.Vertical)]
    [TestCase(91, DoorOrientation.Horizontal)]
    public void GivenDoorTileCheckOrientation(int tile, DoorOrientation expectedOrientation)
    {
        var door = new WolfensteinDoor(1, 2, (ushort)tile);

        Assert.That(door.Orientation, Is.EqualTo(expectedOrientation));
    }

    [Test]
    public void GivenOrdinaryDoorCheckItOpensOverOneSecond()
    {
        var door = new WolfensteinDoor(1, 2, 90);

        var opened = door.Open();
        door.Update(0.4);
        door.Update(0.6);

        Assert.That(opened, Is.True);
        Assert.That(door.IsFullyOpen, Is.True);
        Assert.That(door.IsOpening, Is.False);
    }

    [Test]
    public void GivenLockedDoorCheckItDoesNotOpen()
    {
        var door = new WolfensteinDoor(1, 2, 92);

        var opened = door.Open();

        Assert.That(opened, Is.False);
        Assert.That(door.OpenAmount, Is.Zero);
    }

    [Test]
    public void GivenFullyOpenDoorCheckItWaitsThreeHundredOriginalTicksBeforeClosing()
    {
        var door = new WolfensteinDoor(1, 2, 90);
        door.Open();
        door.Update(1.0);

        door.Update((300.0 / 70.0) - 0.01);
        Assert.That(door.IsClosing, Is.False);

        door.Update(0.01);
        Assert.That(door.IsClosing, Is.True);
        door.Update(0.1);
        Assert.That(door.OpenAmount, Is.EqualTo(0.9).Within(0.0001));
    }

    [Test]
    public void GivenObstructedOpenDoorCheckItWaitsUntilClearBeforeClosing()
    {
        var door = new WolfensteinDoor(1, 2, 90);
        door.Open();
        door.Update(1.0);

        door.Update(5.0, canClose: false);
        Assert.That(door.IsFullyOpen, Is.True);

        door.Update(0.01, canClose: true);
        Assert.That(door.IsClosing, Is.True);
    }

    [Test]
    public void GivenObstructionDuringClosingCheckDoorReopens()
    {
        var door = new WolfensteinDoor(1, 2, 90);
        door.Open();
        door.Update(1.0);
        door.Update(5.0);
        door.Update(0.2);

        door.Update(0.01, canClose: false);

        Assert.That(door.IsOpening, Is.True);
    }
}
