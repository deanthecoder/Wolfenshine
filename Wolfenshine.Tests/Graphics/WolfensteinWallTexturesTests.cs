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
using Wolfenshine.Graphics;
using Wolfenshine.Rendering;

namespace Wolfenshine.Tests.Graphics;

/// <summary>
/// Verifies the original wall, door, and door-jamb VSWAP page rules.
/// </summary>
/// <remarks>
/// Page identity checks protect special wall-face mappings independently of the renderer.
/// </remarks>
public sealed class WolfensteinWallTexturesTests
{
    [TestCase(WallSide.Horizontal, 2)]
    [TestCase(WallSide.Vertical, 3)]
    public void GivenDoorJambCheckSpecialDoorSidePageIsSelected(WallSide side, int expectedPage)
    {
        var pages = Enumerable.Range(0, 8)
            .Select(_ => new WolfensteinWallTexture(new byte[WolfensteinWallTexture.DataLength]))
            .ToArray();
        var textures = new WolfensteinWallTextures(pages, pages.Length);
        var column = new WallColumn(1.0, 0.0, 1, side, true);

        var texture = textures.GetTexture(column);

        Assert.That(texture, Is.SameAs(pages[expectedPage]));
    }
}
