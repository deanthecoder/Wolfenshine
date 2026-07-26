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
/// Verifies the original level-completion ratios, par times, and bonuses.
/// </summary>
public sealed class WolfensteinLevelStatsTests
{
    [Test]
    public void GivenPerfectFastE1M1CheckOriginalRatiosAndBonusesAreCalculated()
    {
        var stats = WolfensteinLevelStats.Create(0, 60.9, 4, 4, 1, 1, 3, 3);

        Assert.Multiple(() =>
        {
            Assert.That(stats.Floor, Is.EqualTo(1));
            Assert.That(stats.TimeText, Is.EqualTo("01:00"));
            Assert.That(stats.ParText, Is.EqualTo("01:30"));
            Assert.That(stats.KillRatio, Is.EqualTo(100));
            Assert.That(stats.SecretRatio, Is.EqualTo(100));
            Assert.That(stats.TreasureRatio, Is.EqualTo(100));
            Assert.That(stats.Bonus, Is.EqualTo(45000));
        });
    }

    [Test]
    public void GivenBossFloorWithNoTotalsCheckUnknownParAndZeroRatiosAreUsed()
    {
        var stats = WolfensteinLevelStats.Create(8, 30.0, 0, 0, 0, 0, 0, 0);

        Assert.Multiple(() =>
        {
            Assert.That(stats.ParText, Is.EqualTo("??:??"));
            Assert.That(stats.KillRatio, Is.Zero);
            Assert.That(stats.SecretRatio, Is.Zero);
            Assert.That(stats.TreasureRatio, Is.Zero);
            Assert.That(stats.Bonus, Is.Zero);
        });
    }
}
