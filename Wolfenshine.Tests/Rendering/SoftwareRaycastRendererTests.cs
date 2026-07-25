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
using Wolfenshine.Rendering;

namespace Wolfenshine.Tests.Rendering;

/// <summary>
/// Verifies conversion of wall columns into the software framebuffer.
/// </summary>
/// <remarks>
/// Pixel-level checks protect the RGBA layout shared by the software and future GPU renderers.
/// </remarks>
public sealed class SoftwareRaycastRendererTests
{
    [Test]
    public void GivenWallColumnCheckBackgroundAndWallAreRendered()
    {
        var column = new WallColumn(2.0, 0.5, 1, WallSide.Vertical);
        var pixels = new byte[16];

        SoftwareRaycastRenderer.Render(new[] { column }, 4, pixels);

        Assert.Multiple(() =>
        {
            Assert.That(pixels, Has.Length.EqualTo(16));
            Assert.That(GetPixel(pixels, 0), Is.EqualTo(new byte[] { 45, 48, 55, 255 }));
            Assert.That(GetPixel(pixels, 1), Is.Not.EqualTo(GetPixel(pixels, 0)));
            Assert.That(GetPixel(pixels, 2), Is.EqualTo(GetPixel(pixels, 1)));
            Assert.That(GetPixel(pixels, 3), Is.EqualTo(new byte[] { 61, 57, 53, 255 }));
        });
    }

    [Test]
    public void GivenIncorrectPixelBufferSizeCheckUsefulExceptionIsThrown()
    {
        var column = new WallColumn(2.0, 0.5, 1, WallSide.Vertical);

        var exception = Assert.Throws<ArgumentException>(() =>
            SoftwareRaycastRenderer.Render(new[] { column }, 4, new byte[15]));

        Assert.That(exception.Message, Does.Contain("exactly 16 bytes"));
    }

    private static byte[] GetPixel(byte[] pixels, int row) => pixels[(row * 4)..((row + 1) * 4)];
}
