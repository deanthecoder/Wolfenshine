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

namespace Wolfenshine.Tests.Graphics;

/// <summary>
/// Verifies conversion from six-bit VGA channels to renderer-independent RGBA colors.
/// </summary>
/// <remarks>
/// Palette indices remain the canonical texture data while colors can change independently.
/// </remarks>
public sealed class WolfensteinPaletteTests
{
    [Test]
    public void GivenVgaPaletteCheckChannelsExpandToEightBits()
    {
        var data = new byte[WolfensteinPalette.VgaDataLength];
        data[3] = 63;
        data[4] = 42;
        data[5] = 21;

        var palette = WolfensteinPalette.FromVgaDac(data);

        Assert.That(palette.GetColor(1), Is.EqualTo(new RgbaColor(255, 170, 85)));
    }

    [Test]
    public void GivenOutOfRangeVgaChannelCheckUsefulExceptionIsThrown()
    {
        var data = new byte[WolfensteinPalette.VgaDataLength];
        data[0] = 64;

        var exception = Assert.Throws<InvalidDataException>(() => WolfensteinPalette.FromVgaDac(data));

        Assert.That(exception.Message, Does.Contain("six-bit range"));
    }
}
