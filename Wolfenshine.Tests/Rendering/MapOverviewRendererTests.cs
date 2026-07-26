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
using Wolfenshine.Maps;
using Wolfenshine.Rendering;

namespace Wolfenshine.Tests.Rendering;

/// <summary>
/// Verifies textured debug-map tiles and special-location highlights.
/// </summary>
/// <remarks>
/// Synthetic indexed textures make the eight-pixel overview deterministic without commercial assets.
/// </remarks>
public sealed class MapOverviewRendererTests
{
    [Test]
    public void GivenOrdinaryWallCheckItsActualTextureColorsAreSampled()
    {
        var map = new WolfensteinMap(0, "Map", 2, 1, new ushort[] { 1, 107 }, new ushort[] { 0, 0 });
        var pixels = Render(map);

        Assert.That(GetColor(pixels, 16, 4, 4), Is.EqualTo(new byte[] { 255, 0, 0, 255 }));
    }

    [Test]
    public void GivenWallBuriedInSolidPaddingCheckItRendersBlack()
    {
        var map = new WolfensteinMap(
            0,
            "Map",
            3,
            3,
            Enumerable.Repeat((ushort)1, 9).ToArray(),
            new ushort[9]);
        var pixels = Render(map);

        Assert.That(GetColor(pixels, 24, 12, 12), Is.EqualTo(new byte[] { 0, 0, 0, 255 }));
    }

    [Test]
    public void GivenSecretWallCheckItHasMagentaHighlightBorder()
    {
        var map = new WolfensteinMap(0, "Map", 1, 1, new ushort[] { 1 }, new ushort[] { 98 });
        var pixels = Render(map);

        Assert.That(GetColor(pixels, 8, 0, 0), Is.EqualTo(new byte[] { 255, 64, 192, 255 }));
    }

    [Test]
    public void GivenNormalElevatorDoorCheckItHasGreenHighlightBorder()
    {
        var map = new WolfensteinMap(0, "Map", 2, 1, new ushort[] { 140, 100 }, new ushort[] { 0, 0 });
        var pixels = Render(map);

        Assert.That(GetColor(pixels, 16, 8, 0), Is.EqualTo(new byte[] { 64, 240, 96, 255 }));
    }

    [Test]
    public void GivenElevatorDoorBesideVersionSpecificAreaCheckItUsesReliableExitColor()
    {
        var map = new WolfensteinMap(0, "Map", 2, 1, new ushort[] { 107, 100 }, new ushort[] { 0, 0 });
        var pixels = Render(map);

        Assert.That(GetColor(pixels, 16, 8, 0), Is.EqualTo(new byte[] { 64, 240, 96, 255 }));
    }

    [Test]
    public void GivenElevatorSwitchWallCheckItIsNotHighlightedAsExit()
    {
        var map = new WolfensteinMap(0, "Map", 2, 1, new ushort[] { 140, 21 }, new ushort[] { 0, 0 });
        var pixels = Render(map);

        Assert.That(GetColor(pixels, 16, 8, 0), Is.EqualTo(new byte[] { 255, 0, 0, 255 }));
    }

    private static byte[] Render(WolfensteinMap map)
    {
        var texturePages = Enumerable.Range(0, 64)
            .Select(_ => new WolfensteinWallTexture(Enumerable.Repeat((byte)7, 64 * 64).ToArray()))
            .ToArray();
        var paletteData = new byte[WolfensteinPalette.VgaDataLength];
        paletteData[7 * 3] = 63;
        var pixels = new byte[map.Width * 8 * map.Height * 8 * 4];

        MapOverviewRenderer.Render(
            map,
            new WolfensteinWallTextures(texturePages, texturePages.Length),
            WolfensteinPalette.FromVgaDac(paletteData),
            pixels);
        return pixels;
    }

    private static byte[] GetColor(byte[] pixels, int width, int x, int y)
    {
        var offset = ((y * width) + x) * 4;
        return pixels[offset..(offset + 4)];
    }
}
