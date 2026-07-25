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
/// Verifies enemy marker, difficulty, direction, and ambush interpretation.
/// </summary>
/// <remarks>
/// Synthetic marker rows cover placement without requiring original map data.
/// </remarks>
public sealed class WolfensteinActorsTests
{
    [Test]
    public void GivenNormalDifficultyMarkersCheckExpectedActorsArePlaced()
    {
        var map = CreateMap(108, 149, 188, 126, 139, 220);

        var actors = WolfensteinActors.FromMap(map);

        Assert.Multiple(() =>
        {
            Assert.That(actors, Has.Count.EqualTo(5));
            Assert.That(actors[0], Is.EqualTo(new WolfensteinActor(
                0.5, 0.5, WolfensteinActorType.Guard, 0, false, true, 50)));
            Assert.That(actors[1].IsPatrolling, Is.True);
            Assert.That(actors[1].Direction, Is.EqualTo(1));
            Assert.That(actors[1].BaseSpriteNumber, Is.EqualTo(58));
            Assert.That(actors[2].Type, Is.EqualTo(WolfensteinActorType.Ss));
            Assert.That(actors[3].Type, Is.EqualTo(WolfensteinActorType.Dog));
            Assert.That(actors[4].Type, Is.EqualTo(WolfensteinActorType.Mutant));
            Assert.That(actors[4].IsPatrolling, Is.True);
        });
    }

    [Test]
    public void GivenHardDifficultyCheckHardOnlyActorIsIncluded()
    {
        var map = CreateMap(188);

        var actors = WolfensteinActors.FromMap(map, GameDifficulty.Hard);

        Assert.That(actors, Has.Count.EqualTo(1));
        Assert.That(actors[0].Type, Is.EqualTo(WolfensteinActorType.Officer));
    }

    private static WolfensteinMap CreateMap(params ushort[] markers)
    {
        var walls = Enumerable.Repeat((ushort)107, markers.Length).ToArray();
        walls[0] = 106;
        return new WolfensteinMap(0, "Actors", markers.Length, 1, walls, markers);
    }
}
