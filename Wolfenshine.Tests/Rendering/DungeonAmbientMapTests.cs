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
/// Verifies jail-pattern detection and smooth dungeon ambient transitions.
/// </summary>
[TestFixture]
public sealed class DungeonAmbientMapTests
{
    [Test]
    public void GivenBlueStoneJailWallCheckDarknessFadesSmoothlyWithDistance()
    {
        var map = CreateMap(
            (12, 12, 7),
            (11, 12, 8),
            (13, 12, 9));
        var ambientMap = DungeonAmbientMap.FromMap(map);

        Assert.Multiple(() =>
        {
            Assert.That(ambientMap.GetDarkness(12.5, 12.5), Is.EqualTo(1.0));
            Assert.That(ambientMap.GetDarkness(16.5, 12.5), Is.EqualTo(1.0));
            Assert.That(ambientMap.GetDarkness(19.5, 12.5), Is.EqualTo(0.5).Within(0.0001));
            Assert.That(ambientMap.GetDarkness(22.5, 12.5), Is.Zero);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    public void GivenJailTileWithoutEnoughBlueStoneCheckItDoesNotDarkenArea(int blueNeighborCount)
    {
        var walls = new List<(int X, int Y, ushort Tile)> { (12, 12, 7) };
        if (blueNeighborCount > 0)
            walls.Add((11, 12, 8));
        var ambientMap = DungeonAmbientMap.FromMap(CreateMap(walls.ToArray()));

        Assert.That(ambientMap.GetDarkness(12.5, 12.5), Is.Zero);
    }

    private static WolfensteinMap CreateMap(params (int X, int Y, ushort Tile)[] wallTiles)
    {
        const int size = 25;
        var walls = Enumerable.Repeat((ushort)107, size * size).ToArray();
        foreach (var (x, y, tile) in wallTiles)
            walls[(y * size) + x] = tile;
        return new WolfensteinMap(0, "Dungeon Test", size, size, walls, new ushort[size * size]);
    }
}
