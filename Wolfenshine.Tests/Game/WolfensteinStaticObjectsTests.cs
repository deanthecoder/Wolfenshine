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
}
