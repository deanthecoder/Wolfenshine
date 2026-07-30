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
using Wolfenshine.Resources;

namespace Wolfenshine.Tests.Resources;

/// <summary>
/// Verifies the embedded original VGA palette.
/// </summary>
public sealed class WolfensteinPaletteLoaderTests
{
    [Test]
    public void GivenEmbeddedPaletteCheckOriginalColorsAreLoaded()
    {
        var palette = WolfensteinPaletteLoader.Load();

        Assert.Multiple(() =>
        {
            Assert.That(palette.GetColor(0), Is.EqualTo(new RgbaColor(0, 0, 0)));
            Assert.That(palette.GetColor(1), Is.EqualTo(new RgbaColor(0, 0, 170)));
            Assert.That(palette.GetColor(15), Is.EqualTo(new RgbaColor(255, 255, 255)));
        });
    }
}
