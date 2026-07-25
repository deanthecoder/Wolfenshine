// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core;
using NUnit.Framework;
using Wolfenshine.Graphics;
using Wolfenshine.Resources;

namespace Wolfenshine.Tests.Resources;

/// <summary>
/// Verifies VSWAP page-table parsing and indexed wall layout.
/// </summary>
/// <remarks>
/// Synthetic pages protect the container format without requiring original game artwork in tests.
/// </remarks>
public sealed class WolfensteinVSwapLoaderTests
{
    [Test]
    public void GivenWallPagesCheckColumnMajorIndicesAreLoaded()
    {
        using var tempDirectory = CreateResources();

        var textures = WolfensteinVSwapLoader.LoadWallTextures(WolfensteinResources.Load(tempDirectory));

        Assert.That(textures.Pages, Has.Count.EqualTo(8));
        Assert.That(textures.Pages[0].GetIndex(3, 5), Is.EqualTo(8));
        Assert.That(textures.Pages[1].GetIndex(3, 5), Is.EqualTo(42));
    }

    [Test]
    public void GivenCompiledSpriteCheckOpaquePostIsDecoded()
    {
        using var tempDirectory = CreateSpriteResources();

        var sprite = WolfensteinVSwapLoader.LoadPistolReadySprite(WolfensteinResources.Load(tempDirectory));

        Assert.Multiple(() =>
        {
            Assert.That(sprite.TryGetIndex(32, 63, out var index), Is.True);
            Assert.That(index, Is.EqualTo(7));
            Assert.That(sprite.TryGetIndex(32, 62, out _), Is.False);
        });
    }

    [Test]
    public void GivenSpriteRegionCheckAllSpritesAndReadyPistolAreLoaded()
    {
        using var tempDirectory = CreateSpriteResources();

        var sprites = WolfensteinVSwapLoader.LoadSprites(WolfensteinResources.Load(tempDirectory));

        Assert.Multiple(() =>
        {
            Assert.That(sprites.Count, Is.EqualTo(20));
            Assert.That(sprites.PistolReady.TryGetIndex(32, 63, out var index), Is.True);
            Assert.That(index, Is.EqualTo(7));
        });
    }

    private static TempDirectory CreateResources()
    {
        var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        foreach (var fileName in WolfensteinResources.FileNames.Values)
            File.WriteAllBytes(Path.Combine(directory.FullName, fileName), [1]);

        var firstPage = new byte[WolfensteinWallTexture.DataLength];
        for (var x = 0; x < WolfensteinWallTexture.Size; x++)
        {
            for (var y = 0; y < WolfensteinWallTexture.Size; y++)
                firstPage[(x * WolfensteinWallTexture.Size) + y] = (byte)(x + y);
        }
        var secondPage = Enumerable.Repeat((byte)42, WolfensteinWallTexture.DataLength).ToArray();
        const int pageCount = 8;
        var pages = new[] { firstPage, secondPage }
            .Concat(Enumerable.Range(2, pageCount - 2).Select(_ => new byte[WolfensteinWallTexture.DataLength]))
            .ToArray();
        var dataOffset = 6 + (pageCount * sizeof(uint)) + (pageCount * sizeof(ushort));
        using var writer = new BinaryWriter(
            new FileInfo(Path.Combine(directory.FullName, "VSWAP.WL6")).Open(FileMode.Create, FileAccess.Write));
        writer.Write((ushort)pageCount);
        writer.Write((ushort)pageCount);
        writer.Write((ushort)pageCount);
        for (var page = 0; page < pages.Length; page++)
            writer.Write((uint)(dataOffset + (page * WolfensteinWallTexture.DataLength)));
        foreach (var _ in pages)
            writer.Write((ushort)WolfensteinWallTexture.DataLength);
        foreach (var page in pages)
            writer.Write(page);
        return tempDirectory;
    }

    private static TempDirectory CreateSpriteResources()
    {
        var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        foreach (var fileName in WolfensteinResources.FileNames.Values)
            File.WriteAllBytes(Path.Combine(directory.FullName, fileName), [1]);

        const int pageCount = 28;
        const int spriteStart = 8;
        const int soundStart = 28;
        var sprite = CreateSinglePixelSprite();
        var dataOffset = 6 + (pageCount * sizeof(uint)) + (pageCount * sizeof(ushort));
        using var writer = new BinaryWriter(
            new FileInfo(Path.Combine(directory.FullName, "VSWAP.WL6")).Open(FileMode.Create, FileAccess.Write));
        writer.Write((ushort)pageCount);
        writer.Write((ushort)spriteStart);
        writer.Write((ushort)soundStart);
        for (var page = 0; page < pageCount; page++)
        {
            writer.Write(page >= spriteStart
                ? (uint)(dataOffset + ((page - spriteStart) * sprite.Length))
                : 0U);
        }
        for (var page = 0; page < pageCount; page++)
            writer.Write(page >= spriteStart ? (ushort)sprite.Length : (ushort)0);
        for (var page = spriteStart; page < soundStart; page++)
            writer.Write(sprite);
        return tempDirectory;
    }

    private static byte[] CreateSinglePixelSprite()
    {
        var data = new byte[16];
        using var stream = new MemoryStream(data);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)32); // Left bound.
        writer.Write((ushort)32); // Right bound.
        writer.Write((ushort)8); // Column-command offset.
        writer.Write((byte)7); // The single palette index.
        writer.Write((byte)0); // Word alignment.
        writer.Write((ushort)128); // End row 64, doubled.
        writer.Write(unchecked((ushort)(6 - 63))); // Wrapped source offset for row 63.
        writer.Write((ushort)126); // Start row 63, doubled.
        writer.Write((ushort)0); // End of column.
        return data;
    }
}
