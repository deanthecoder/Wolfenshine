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
/// Verifies enemy damage and death sprite progression.
/// </summary>
/// <remarks>
/// Death animation tests protect the original sprite ordering and permanent corpse state.
/// </remarks>
public sealed class WolfensteinActorStateTests
{
    [TestCase(GameDifficulty.Baby, 45)]
    [TestCase(GameDifficulty.Easy, 55)]
    [TestCase(GameDifficulty.Normal, 55)]
    [TestCase(GameDifficulty.Hard, 65)]
    public void GivenMutantDifficultyCheckOriginalHitPointsAreSelected(
        GameDifficulty difficulty,
        int expectedHitPoints)
    {
        var actor = new WolfensteinActorState(new WolfensteinActor(
            1.5, 2.5, WolfensteinActorType.Mutant, 0, false, false, 187), difficulty);

        Assert.That(actor.HitPoints, Is.EqualTo(expectedHitPoints));
    }

    [Test]
    public void GivenNonLethalOfficerDamageCheckPainSpriteReturnsToStandingSprite()
    {
        var actor = new WolfensteinActorState(new WolfensteinActor(
            1.5, 2.5, WolfensteinActorType.Officer, 0, false, false, 238));

        actor.Damage(25);
        Assert.That(actor.CurrentSpriteNumber, Is.EqualTo(278));

        actor.Update(10.0 / 70.0);

        Assert.Multiple(() =>
        {
            Assert.That(actor.IsDead, Is.False);
            Assert.That(actor.IsHurt, Is.False);
            Assert.That(actor.CurrentSpriteNumber, Is.EqualTo(238));
        });
    }

    [Test]
    public void GivenLethalGuardDamageCheckDeathAnimationEndsOnCorpseSprite()
    {
        var actor = new WolfensteinActorState(new WolfensteinActor(
            1.5, 2.5, WolfensteinActorType.Guard, 0, false, false, 50));

        actor.Damage(25);
        Assert.That(actor.CurrentSpriteNumber, Is.EqualTo(91));

        actor.Update(45.0 / 70.0);

        Assert.Multiple(() =>
        {
            Assert.That(actor.IsDead, Is.True);
            Assert.That(actor.IsDeathAnimationComplete, Is.True);
            Assert.That(actor.CurrentSpriteNumber, Is.EqualTo(95));
            Assert.That(actor.ToWorldSprite().FacingDirection, Is.EqualTo(-1));
        });
    }
}
