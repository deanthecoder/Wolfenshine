// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Text;
using DTC.Core;
using Wolfenshine.Resources;

namespace Wolfenshine.Maps;

/// <summary>
/// Loads Wolfenstein 3D level headers and tile planes from the original map containers.
/// </summary>
/// <remarks>
/// The loader understands the shared container format without relying on release-specific level names or chunk IDs.
/// </remarks>
public static class WolfensteinMapLoader
{
    private const uint SparseMapOffset = uint.MaxValue;
    private const int WolfensteinMapCount = 60;
    private const int MapPlaneCount = 3;
    private const int LoadedPlaneCount = 2;
    private const int MapNameLength = 16;
    private const int MapHeaderLength =
        (MapPlaneCount * sizeof(uint)) +
        (MapPlaneCount * sizeof(ushort)) +
        (2 * sizeof(ushort)) +
        MapNameLength;

    public static WolfensteinMapSet Load(WolfensteinResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        Logger.Instance.Info("Loading Wolfenstein 3D maps.");

        using var headerReader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.MapHeader));
        if (headerReader.BaseStream.Length < sizeof(ushort) + sizeof(uint))
            throw new InvalidDataException("MAPHEAD.WL6 is too short to contain a map offset table.");

        var rlewTag = headerReader.ReadUInt16();
        var headerOffsets = new List<uint>();
        while (headerOffsets.Count < WolfensteinMapCount &&
               headerReader.BaseStream.Position + sizeof(uint) <= headerReader.BaseStream.Length)
            headerOffsets.Add(headerReader.ReadUInt32());

        using var mapReader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.MapData));
        var maps = new List<WolfensteinMap>();
        for (var slot = 0; slot < headerOffsets.Count; slot++)
        {
            var headerOffset = headerOffsets[slot];
            if (headerOffset == SparseMapOffset)
                continue;
            maps.Add(LoadMap(mapReader, slot, headerOffset, rlewTag));
        }

        Logger.Instance.Info($"Loaded {maps.Count} Wolfenstein 3D maps using RLEW tag 0x{rlewTag:X4}.");
        return new WolfensteinMapSet(rlewTag, maps);
    }

    private static WolfensteinMap LoadMap(BinaryReader reader, int slot, uint headerOffset, ushort rlewTag)
    {
        EnsureRange(reader.BaseStream, headerOffset, MapHeaderLength, $"map slot {slot} header");
        reader.BaseStream.Position = headerOffset;

        var planeOffsets = Enumerable.Range(0, MapPlaneCount).Select(_ => reader.ReadUInt32()).ToArray();
        var planeLengths = Enumerable.Range(0, MapPlaneCount).Select(_ => reader.ReadUInt16()).ToArray();
        var width = reader.ReadUInt16();
        var height = reader.ReadUInt16();
        var name = ReadDosString(reader.ReadBytes(MapNameLength));
        if (width == 0 || height == 0)
            throw new InvalidDataException($"Map slot {slot} has invalid dimensions {width} x {height}.");
        if (string.IsNullOrWhiteSpace(name))
            name = $"Map {slot + 1}";

        var tileCount = checked(width * height);
        var planes = new ushort[LoadedPlaneCount][];
        for (var plane = 0; plane < LoadedPlaneCount; plane++)
        {
            var length = planeLengths[plane];
            EnsureRange(reader.BaseStream, planeOffsets[plane], length, $"map slot {slot} plane {plane}");
            reader.BaseStream.Position = planeOffsets[plane];
            planes[plane] = WolfensteinMapDecompressor.Expand(reader.ReadBytes(length), rlewTag, tileCount);
        }

        Logger.Instance.Info($"Loaded map slot {slot}: {name} ({width} x {height}).");
        return new WolfensteinMap(slot, name, width, height, planes[0], planes[1]);
    }

    private static string ReadDosString(byte[] bytes)
    {
        var terminator = Array.IndexOf(bytes, (byte)0);
        var length = terminator >= 0 ? terminator : bytes.Length;
        return Encoding.ASCII.GetString(bytes, 0, length).TrimEnd();
    }

    private static void EnsureRange(Stream stream, long offset, int length, string description)
    {
        if (length <= 0 || offset < 0 || offset > stream.Length - length)
            throw new InvalidDataException($"The {description} lies outside GAMEMAPS.WL6.");
    }
}
