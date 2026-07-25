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
    public static WolfensteinWallTextures LoadWallTextures(WolfensteinResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        using var reader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.SwapData));
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
        var walls = new WolfensteinWallTexture[spriteStart];
        for (var page = 0; page < walls.Length; page++)
        {
            if (lengths[page] != WolfensteinWallTexture.DataLength ||
                offsets[page] > reader.BaseStream.Length - lengths[page])
            {
                throw new InvalidDataException(
                    $"VSWAP wall page {page} is not a complete {WolfensteinWallTexture.Size} x {WolfensteinWallTexture.Size} texture.");
            }

            reader.BaseStream.Position = offsets[page];
            walls[page] = new WolfensteinWallTexture(reader.ReadBytes(lengths[page]));
        }

        Logger.Instance.Info(
            $"Loaded {walls.Length} indexed wall pages from VSWAP ({pageCount} pages, sprites at {spriteStart}, sounds at {soundStart}).");
        return new WolfensteinWallTextures(walls, spriteStart);
    }
}
