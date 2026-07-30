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
/// Verifies enhanced partial coverage around pixel-art silhouettes.
/// </summary>
public sealed class SpriteOutlineSmootherTests
{
    [Test]
    public void GivenOpaqueCornerCheckMissingPixelReceivesBlendedPartialCoverage()
    {
        var source = new byte[3 * 3 * 4];
        SetPixel(source, 0, 0, 3, 30, 60, 90, 255);
        SetPixel(source, 1, 0, 3, 60, 90, 120, 255);
        SetPixel(source, 0, 1, 3, 90, 120, 150, 255);
        var destination = source.ToArray();

        SpriteOutlineSmoother.AddCornerCoverage(source, destination, 3, 3);

        Assert.That(GetPixel(destination, 1, 1, 3), Is.EqualTo(new byte[] { 60, 90, 120, 230 }));
    }

    [Test]
    public void GivenStraightEdgeCheckTransparentPixelsRemainTransparent()
    {
        var source = new byte[3 * 3 * 4];
        for (var y = 0; y < 3; y++)
            SetPixel(source, 0, y, 3, 100, 110, 120, 255);
        var destination = source.ToArray();

        SpriteOutlineSmoother.AddCornerCoverage(source, destination, 3, 3);

        Assert.That(GetPixel(destination, 1, 1, 3), Is.All.Zero);
    }

    [Test]
    public void GivenColoredInteriorNearGreyCornerCheckCoverageIncludesInteriorColor()
    {
        var source = new byte[5 * 5 * 4];
        SetPixel(source, 1, 1, 5, 30, 30, 30, 255);
        SetPixel(source, 2, 1, 5, 30, 30, 30, 255);
        SetPixel(source, 1, 2, 5, 30, 30, 30, 255);
        SetPixel(source, 1, 3, 5, 210, 60, 30, 255);
        var destination = source.ToArray();

        SpriteOutlineSmoother.AddCornerCoverage(source, destination, 5, 5);

        Assert.That(GetPixel(destination, 2, 2, 5), Is.EqualTo(new byte[] { 75, 37, 30, 230 }));
    }

    [Test]
    public void GivenGeneratedCoverageCheckItDoesNotCascadeBeyondOriginalOutline()
    {
        var source = new byte[4 * 4 * 4];
        SetPixel(source, 0, 0, 4, 255, 0, 0, 255);
        SetPixel(source, 1, 0, 4, 255, 0, 0, 255);
        SetPixel(source, 0, 1, 4, 255, 0, 0, 255);
        var destination = source.ToArray();

        SpriteOutlineSmoother.AddCornerCoverage(source, destination, 4, 4);

        Assert.Multiple(() =>
        {
            Assert.That(GetPixel(destination, 1, 1, 4)[3], Is.EqualTo(230));
            Assert.That(GetPixel(destination, 2, 2, 4), Is.All.Zero);
        });
    }

    private static void SetPixel(
        byte[] pixels,
        int x,
        int y,
        int width,
        byte red,
        byte green,
        byte blue,
        byte alpha)
    {
        var offset = ((y * width) + x) * 4;
        pixels[offset] = red;
        pixels[offset + 1] = green;
        pixels[offset + 2] = blue;
        pixels[offset + 3] = alpha;
    }

    private static byte[] GetPixel(byte[] pixels, int x, int y, int width)
    {
        var offset = ((y * width) + x) * 4;
        return pixels[offset..(offset + 4)];
    }
}
