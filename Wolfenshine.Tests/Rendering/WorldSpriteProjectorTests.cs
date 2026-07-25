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
using Wolfenshine.Rendering;

namespace Wolfenshine.Tests.Rendering;

/// <summary>
/// Verifies world-to-screen projection and visibility ordering for sprites.
/// </summary>
/// <remarks>
/// Deterministic cardinal-facing examples protect camera-space sign and depth rules.
/// </remarks>
public sealed class WorldSpriteProjectorTests
{
    [Test]
    public void GivenSpritesAheadAndBehindCheckVisibleSpritesAreSortedFarToNear()
    {
        var camera = new RaycastCamera(2.5, 3.5, 0.0, -1.0, 0.66, 0.0);
        WorldSprite[] sprites =
        [
            new(2.5, 2.5, 1),
            new(2.5, 0.5, 2),
            new(2.5, 4.5, 3)
        ];
        var projected = new ProjectedWorldSprite[sprites.Length];

        var count = WorldSpriteProjector.Project(sprites, camera, 320, 200, projected);

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(2));
            Assert.That(projected[0].SpriteNumber, Is.EqualTo(2));
            Assert.That(projected[1].SpriteNumber, Is.EqualTo(1));
            Assert.That(projected[0].CenterX, Is.EqualTo(160));
        });
    }

    [Test]
    public void GivenClippedViewportCheckIndependentProjectionHeightControlsSpriteScale()
    {
        var camera = new RaycastCamera(2.5, 3.5, 0.0, -1.0, 0.66, 0.0);
        WorldSprite[] sprites = [new(2.5, 1.375, 1)];
        var projected = new ProjectedWorldSprite[1];

        var count = WorldSpriteProjector.Project(sprites, camera, 320, 160, 200, projected);

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(1));
            Assert.That(projected[0].RenderedSize, Is.EqualTo(100));
        });
    }

    [Test]
    public void GivenDirectionalActorCheckRotationFacingCameraIsSelected()
    {
        WorldSprite[] sprites = [new(2.5, 2.5, 50, 0)];
        var projected = new ProjectedWorldSprite[1];
        var cameraEastOfActor = new RaycastCamera(3.5, 2.5, -1.0, 0.0, 0.0, 0.66);

        WorldSpriteProjector.Project(sprites, cameraEastOfActor, 320, 200, projected);
        var viewedFromFront = projected[0].SpriteNumber;

        var cameraNorthOfActor = new RaycastCamera(2.5, 1.5, 0.0, 1.0, -0.66, 0.0);
        WorldSpriteProjector.Project(sprites, cameraNorthOfActor, 320, 200, projected);

        Assert.Multiple(() =>
        {
            Assert.That(viewedFromFront, Is.EqualTo(50));
            Assert.That(projected[0].SpriteNumber, Is.EqualTo(52));
        });
    }

    [Test]
    public void GivenSouthFacingGuardAndNegativeViewAngleCheckRotationWrapsWithinActorSprites()
    {
        WorldSprite[] sprites = [new(12.5, 17.5, 50, 3)];
        var projected = new ProjectedWorldSprite[1];
        var camera = new RaycastCamera(9.512, 18.667, 0.910, -0.416, 0.274, 0.600);

        var count = WorldSpriteProjector.Project(sprites, camera, 320, 160, 200, projected);

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(1));
            Assert.That(projected[0].SpriteNumber, Is.EqualTo(56));
            Assert.That(projected[0].SpriteNumber, Is.InRange(50, 57));
        });
    }
}
