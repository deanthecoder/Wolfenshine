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
/// Verifies activation and one-way movement of secret pushwalls.
/// </summary>
/// <remarks>
/// Synthetic maps cover the original two-tile limit and blocked-second-tile behavior.
/// </remarks>
public sealed class WolfensteinPushWallsTests
{
    [Test]
    public void GivenClearPathCheckPushwallMovesTwoTilesAndNeverReturns()
    {
        var map = CreateMap();
        var walls = new WolfensteinPushWalls(map);
        bool CanEnter(int x, int y) => !map.IsSolid(x, y) || walls.IsOriginalWallSuppressed(x, y);

        var activated = walls.TryPush(3, 4, 0, -1, CanEnter);
        walls.Update(128.0 / 70.0, CanEnter);
        walls.Update(128.0 / 70.0, CanEnter);
        walls.Update(10.0, CanEnter);

        Assert.Multiple(() =>
        {
            Assert.That(activated, Is.True);
            Assert.That(walls.Items[0].Distance, Is.EqualTo(2.0));
            Assert.That(walls.Items[0].IsMoving, Is.False);
            Assert.That(walls.Items[0].X, Is.EqualTo(3.5));
            Assert.That(walls.Items[0].Y, Is.EqualTo(2.5));
        });
    }

    [Test]
    public void GivenBlockedSecondTileCheckPushwallStopsAfterOneTile()
    {
        var map = CreateMap(blockSecondTile: true);
        var walls = new WolfensteinPushWalls(map);
        bool CanEnter(int x, int y) => !map.IsSolid(x, y) || walls.IsOriginalWallSuppressed(x, y);

        walls.TryPush(3, 4, 0, -1, CanEnter);
        walls.Update(128.0 / 70.0, CanEnter);

        Assert.Multiple(() =>
        {
            Assert.That(walls.Items[0].Distance, Is.EqualTo(1.0));
            Assert.That(walls.Items[0].IsMoving, Is.False);
        });
    }

    private static WolfensteinMap CreateMap(bool blockSecondTile = false)
    {
        const int size = 7;
        var wallTiles = Enumerable.Repeat((ushort)107, size * size).ToArray();
        for (var index = 0; index < size; index++)
        {
            wallTiles[index] = 1;
            wallTiles[((size - 1) * size) + index] = 1;
            wallTiles[index * size] = 1;
            wallTiles[(index * size) + size - 1] = 1;
        }
        wallTiles[(4 * size) + 3] = 2;
        if (blockSecondTile)
            wallTiles[(2 * size) + 3] = 1;
        var objects = new ushort[size * size];
        objects[(4 * size) + 3] = 98;
        return new WolfensteinMap(0, "Pushwall", size, size, wallTiles, objects);
    }
}
