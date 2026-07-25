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
using Wolfenshine.Graphics;

namespace Wolfenshine.Resources;

/// <summary>
/// Loads indexed wall pages from the original VSWAP container.
/// </summary>
/// <remarks>
/// Sprite and digitized-sound page boundaries are retained for the asset milestones that follow wall rendering.
/// </remarks>
public static class WolfensteinVSwapLoader
{
    private const int WeaponSpriteCount = 20;
    private const int PistolReadyWeaponOffset = 5;

    public static WolfensteinWallTextures LoadWallTextures(WolfensteinResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        using var reader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.SwapData));
        var directory = ReadDirectory(reader);
        var walls = new WolfensteinWallTexture[directory.SpriteStart];
        for (var page = 0; page < walls.Length; page++)
        {
            if (directory.Lengths[page] != WolfensteinWallTexture.DataLength)
            {
                throw new InvalidDataException(
                    $"VSWAP wall page {page} is not a complete {WolfensteinWallTexture.Size} x {WolfensteinWallTexture.Size} texture.");
            }
            walls[page] = new WolfensteinWallTexture(ReadPage(reader, directory, page));
        }

        Logger.Instance.Info(
            $"Loaded {walls.Length} indexed wall pages from VSWAP ({directory.PageCount} pages, sprites at {directory.SpriteStart}, sounds at {directory.SoundStart}).");
        return new WolfensteinWallTextures(walls, directory.SpriteStart);
    }

    public static WolfensteinSprite LoadPistolReadySprite(WolfensteinResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        using var reader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.SwapData));
        var directory = ReadDirectory(reader);
        if (directory.SoundStart - directory.SpriteStart < WeaponSpriteCount)
            throw new InvalidDataException("VSWAP.WL6 does not contain the expected weapon sprite pages.");
        var page = directory.SoundStart - WeaponSpriteCount + PistolReadyWeaponOffset;
        var sprite = DecodeSprite(ReadPage(reader, directory, page));
        Logger.Instance.Info($"Loaded ready-pistol sprite from VSWAP page {page}.");
        return sprite;
    }

    private static VSwapDirectory ReadDirectory(BinaryReader reader)
    {
        if (reader.BaseStream.Length < 6)
            throw new InvalidDataException("VSWAP.WL6 is too short to contain its page header.");
        var pageCount = reader.ReadUInt16();
        var spriteStart = reader.ReadUInt16();
        var soundStart = reader.ReadUInt16();
        if (pageCount == 0 || spriteStart == 0 || spriteStart > soundStart || soundStart > pageCount)
            throw new InvalidDataException("VSWAP.WL6 contains invalid page boundaries.");
        var tableByteLength = checked(pageCount * (sizeof(uint) + sizeof(ushort)));
        if (reader.BaseStream.Position + tableByteLength > reader.BaseStream.Length)
            throw new InvalidDataException("VSWAP.WL6 ends inside its page tables.");
        var offsets = Enumerable.Range(0, pageCount).Select(_ => reader.ReadUInt32()).ToArray();
        var lengths = Enumerable.Range(0, pageCount).Select(_ => reader.ReadUInt16()).ToArray();
        return new VSwapDirectory(pageCount, spriteStart, soundStart, offsets, lengths);
    }

    private static byte[] ReadPage(BinaryReader reader, VSwapDirectory directory, int page)
    {
        var offset = directory.Offsets[page];
        var length = directory.Lengths[page];
        if (length == 0 || offset > reader.BaseStream.Length - length)
            throw new InvalidDataException($"VSWAP page {page} is empty or outside the file.");
        reader.BaseStream.Position = offset;
        return reader.ReadBytes(length);
    }

    private static WolfensteinSprite DecodeSprite(byte[] data)
    {
        if (data.Length < 6)
            throw new InvalidDataException("The VSWAP sprite page is too short to contain a shape header.");
        var left = BitConverter.ToUInt16(data, 0);
        var right = BitConverter.ToUInt16(data, 2);
        if (left > right || right >= WolfensteinSprite.Size)
            throw new InvalidDataException("The VSWAP sprite has invalid horizontal bounds.");
        var columnCount = right - left + 1;
        if (4 + (columnCount * sizeof(ushort)) > data.Length)
            throw new InvalidDataException("The VSWAP sprite ends inside its column table.");

        var indices = new byte[WolfensteinSprite.PixelCount];
        var opacity = new bool[WolfensteinSprite.PixelCount];
        for (var x = left; x <= right; x++)
        {
            var commandOffset = BitConverter.ToUInt16(data, 4 + ((x - left) * sizeof(ushort)));
            while (true)
            {
                if (commandOffset > data.Length - sizeof(ushort))
                    throw new InvalidDataException("A VSWAP sprite column points outside its page.");
                var endWord = BitConverter.ToUInt16(data, commandOffset);
                commandOffset += sizeof(ushort);
                if (endWord == 0)
                    break;
                if (commandOffset > data.Length - (2 * sizeof(ushort)))
                    throw new InvalidDataException("A VSWAP sprite column ends inside a post command.");
                var sourceOffset = BitConverter.ToUInt16(data, commandOffset);
                var startWord = BitConverter.ToUInt16(data, commandOffset + sizeof(ushort));
                commandOffset += 2 * sizeof(ushort);
                if ((startWord & 1) != 0 || (endWord & 1) != 0)
                    throw new InvalidDataException("A VSWAP sprite post contains an unaligned row coordinate.");
                var start = startWord / 2;
                var end = endWord / 2;
                if (start > end || end > WolfensteinSprite.Size)
                    throw new InvalidDataException("A VSWAP sprite post has invalid vertical bounds.");
                for (var y = start; y < end; y++)
                {
                    var pixelSource = unchecked((ushort)(sourceOffset + y));
                    if (pixelSource >= data.Length)
                        throw new InvalidDataException("A VSWAP sprite post references pixel data outside its page.");
                    var pixel = (y * WolfensteinSprite.Size) + x;
                    indices[pixel] = data[pixelSource];
                    opacity[pixel] = true;
                }
            }
        }
        return new WolfensteinSprite(indices, opacity);
    }

    private sealed record VSwapDirectory(
        ushort PageCount,
        ushort SpriteStart,
        ushort SoundStart,
        uint[] Offsets,
        ushort[] Lengths);
}
