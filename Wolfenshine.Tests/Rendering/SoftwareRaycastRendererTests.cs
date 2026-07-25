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
            Assert.That(GetPixel(pixels, 0), Is.EqualTo(new byte[] { 56, 56, 56, 255 }));
            Assert.That(GetPixel(pixels, 1), Is.Not.EqualTo(GetPixel(pixels, 0)));
            Assert.That(GetPixel(pixels, 2), Is.EqualTo(GetPixel(pixels, 1)));
            Assert.That(GetPixel(pixels, 3), Is.EqualTo(new byte[] { 113, 113, 113, 255 }));
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

    [Test]
    public void GivenIndexedWallCheckPaletteColorIsRendered()
    {
        var pages = Enumerable.Range(0, 8)
            .Select(page => new WolfensteinWallTexture(
                Enumerable.Repeat((byte)(page == 1 ? 7 : 0), WolfensteinWallTexture.DataLength).ToArray()))
            .ToArray();
        var textures = new WolfensteinWallTextures(pages, pages.Length);
        var paletteData = new byte[WolfensteinPalette.VgaDataLength];
        paletteData[7 * 3] = 63;
        paletteData[(0x1D * 3) + 2] = 63;
        paletteData[(0x19 * 3) + 1] = 63;
        var palette = WolfensteinPalette.FromVgaDac(paletteData);
        var pixels = new byte[16];
        var column = new WallColumn(2.0, 0.5, 1, WallSide.Vertical);

        SoftwareRaycastRenderer.Render(new[] { column }, 4, pixels, textures, palette);

        Assert.That(GetPixel(pixels, 1), Is.EqualTo(new byte[] { 255, 0, 0, 255 }));
        Assert.That(GetPixel(pixels, 2), Is.EqualTo(new byte[] { 255, 0, 0, 255 }));
        Assert.That(GetPixel(pixels, 0), Is.EqualTo(new byte[] { 0, 0, 255, 255 }));
        Assert.That(GetPixel(pixels, 3), Is.EqualTo(new byte[] { 0, 255, 0, 255 }));
    }

    [Test]
    public void GivenIndexedSpriteCheckOnlyOpaquePixelsAreComposited()
    {
        var indices = new byte[WolfensteinSprite.PixelCount];
        var opacity = new bool[WolfensteinSprite.PixelCount];
        var spritePixel = (32 * WolfensteinSprite.Size) + 32;
        indices[spritePixel] = 7;
        opacity[spritePixel] = true;
        var sprite = new WolfensteinSprite(indices, opacity);
        var paletteData = new byte[WolfensteinPalette.VgaDataLength];
        paletteData[7 * 3] = 63;
        var palette = WolfensteinPalette.FromVgaDac(paletteData);
        var pixels = Enumerable.Repeat((byte)11, 16).ToArray();

        SoftwareRaycastRenderer.DrawSprite(sprite, palette, 1, 2, 2, pixels, 2, 2);

        Assert.That(GetPixel(pixels, 0), Is.EqualTo(new byte[] { 11, 11, 11, 11 }));
        Assert.That(GetPixel(pixels, 3), Is.EqualTo(new byte[] { 255, 0, 0, 255 }));
    }

    [Test]
    public void GivenWorldSpriteCheckWallDepthOccludesIndividualColumns()
    {
        var sprite = new WolfensteinSprite(
            Enumerable.Repeat((byte)7, WolfensteinSprite.PixelCount).ToArray(),
            Enumerable.Repeat(true, WolfensteinSprite.PixelCount).ToArray());
        var sprites = new WolfensteinSpriteSet(Enumerable.Repeat(sprite, 20).ToArray());
        var paletteData = new byte[WolfensteinPalette.VgaDataLength];
        paletteData[7 * 3] = 63;
        var palette = WolfensteinPalette.FromVgaDac(paletteData);
        WallColumn[] walls =
        [
            new(1.0, 0.0, 1, WallSide.Horizontal),
            new(3.0, 0.0, 1, WallSide.Horizontal)
        ];
        var pixels = Enumerable.Repeat((byte)11, 16).ToArray();
        ProjectedWorldSprite[] projected = [new(0, 2.0, 1, 2)];

        SoftwareRaycastRenderer.DrawWorldSprites(projected, sprites, palette, walls, pixels, 2, 2);

        Assert.Multiple(() =>
        {
            Assert.That(GetPixel(pixels, 0), Is.EqualTo(new byte[] { 11, 11, 11, 11 }));
            Assert.That(GetPixel(pixels, 1), Is.EqualTo(new byte[] { 255, 0, 0, 255 }));
            Assert.That(GetPixel(pixels, 2), Is.EqualTo(new byte[] { 11, 11, 11, 11 }));
            Assert.That(GetPixel(pixels, 3), Is.EqualTo(new byte[] { 255, 0, 0, 255 }));
        });
    }

    private static byte[] GetPixel(byte[] pixels, int row) => pixels[(row * 4)..((row + 1) * 4)];
}
