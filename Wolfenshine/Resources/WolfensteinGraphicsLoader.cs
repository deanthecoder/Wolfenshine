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
/// Loads Huffman-compressed indexed pictures from the original VGA graphics containers.
/// </summary>
/// <remarks>
/// Picture dimensions are discovered from STRUCTPIC, avoiding version-specific generated chunk identifiers.
/// </remarks>
public static class WolfensteinGraphicsLoader
{
    private const int HuffmanNodeCount = 255;
    private const int HuffmanRootNode = HuffmanNodeCount - 1;
    private const int FirstPictureChunk = 3;
    private const int StatusBarWidth = 320;
    private const int StatusBarHeight = 40;
    private const int PistolPictureOffset = 6;
    private const int NoKeyPictureOffset = 9;
    private const int BlankDigitPictureOffset = 12;
    private const int ZeroDigitPictureOffset = 13;
    private const int HealthyFacePictureOffset = 23;

    public static WolfensteinGraphic LoadStatusBar(WolfensteinResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var dictionary = ReadDictionary(resources);
        var offsets = ReadOffsets(resources);
        using var reader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.GraphicsData));
        var pictureTableData = ReadChunk(reader, offsets, 0, dictionary);
        if ((pictureTableData.Length % (2 * sizeof(ushort))) != 0)
            throw new InvalidDataException("The VGAGRAPH picture table has an invalid byte length.");

        var pictureCount = pictureTableData.Length / (2 * sizeof(ushort));
        for (var picture = 0; picture < pictureCount; picture++)
        {
            var offset = picture * 2 * sizeof(ushort);
            var width = BitConverter.ToUInt16(pictureTableData, offset);
            var height = BitConverter.ToUInt16(pictureTableData, offset + sizeof(ushort));
            if (width != StatusBarWidth || height != StatusBarHeight)
                continue;

            var chunk = FirstPictureChunk + picture;
            var pictures = ReadPictureTable(pictureTableData);
            var statusBar = ReadPicture(reader, offsets, dictionary, pictures, chunk);
            var indices = CopyIndices(statusBar);
            DrawPicture(indices, ReadPicture(reader, offsets, dictionary, pictures, chunk + HealthyFacePictureOffset), 17 * 8, 4);
            DrawPicture(indices, ReadPicture(reader, offsets, dictionary, pictures, chunk + PistolPictureOffset), 32 * 8, 8);
            var noKey = ReadPicture(reader, offsets, dictionary, pictures, chunk + NoKeyPictureOffset);
            DrawPicture(indices, noKey, 30 * 8, 4);
            DrawPicture(indices, noKey, 30 * 8, 20);
            DrawNumber(reader, offsets, dictionary, pictures, indices, chunk, 2, 16, 2, 1);
            DrawNumber(reader, offsets, dictionary, pictures, indices, chunk, 6, 16, 6, 0);
            DrawNumber(reader, offsets, dictionary, pictures, indices, chunk, 14, 16, 1, 3);
            DrawNumber(reader, offsets, dictionary, pictures, indices, chunk, 21, 16, 3, 100);
            DrawNumber(reader, offsets, dictionary, pictures, indices, chunk, 27, 16, 2, 8);
            Logger.Instance.Info($"Loaded and populated {width} x {height} status bar from VGAGRAPH chunk {chunk}.");
            return new WolfensteinGraphic(width, height, indices);
        }

        throw new InvalidDataException("VGAGRAPH.WL6 does not contain a 320 x 40 status-bar picture.");
    }

    public static byte[] ExpandHuffman(
        ReadOnlySpan<byte> compressed,
        IReadOnlyList<(ushort Bit0, ushort Bit1)> dictionary,
        int expandedLength)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentOutOfRangeException.ThrowIfNegative(expandedLength);
        if (dictionary.Count != HuffmanNodeCount)
            throw new ArgumentException($"The graphics dictionary must contain {HuffmanNodeCount} nodes.", nameof(dictionary));

        var expanded = new byte[expandedLength];
        var sourceByte = 0;
        var sourceMask = 1;
        var node = HuffmanRootNode;
        for (var destination = 0; destination < expanded.Length;)
        {
            if (sourceByte >= compressed.Length)
                throw new InvalidDataException("A VGAGRAPH chunk ended before reaching its expanded length.");
            var value = (compressed[sourceByte] & sourceMask) == 0
                ? dictionary[node].Bit0
                : dictionary[node].Bit1;
            sourceMask <<= 1;
            if (sourceMask == 256)
            {
                sourceMask = 1;
                sourceByte++;
            }

            if (value < 256)
            {
                expanded[destination++] = (byte)value;
                node = HuffmanRootNode;
            }
            else
            {
                node = value - 256;
                if (node < 0 || node >= dictionary.Count)
                    throw new InvalidDataException("A VGAGRAPH Huffman node references an invalid child.");
            }
        }
        return expanded;
    }

    private static (ushort Bit0, ushort Bit1)[] ReadDictionary(WolfensteinResources resources)
    {
        using var reader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.GraphicsDictionary));
        if (reader.BaseStream.Length < HuffmanNodeCount * 2 * sizeof(ushort))
            throw new InvalidDataException("VGADICT.WL6 does not contain all 255 Huffman nodes.");
        return Enumerable.Range(0, HuffmanNodeCount)
            .Select(_ => (reader.ReadUInt16(), reader.ReadUInt16()))
            .ToArray();
    }

    private static int[] ReadOffsets(WolfensteinResources resources)
    {
        using var reader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.GraphicsHeader));
        if (reader.BaseStream.Length < 6 || (reader.BaseStream.Length % 3) != 0)
            throw new InvalidDataException("VGAHEAD.WL6 does not contain a valid table of 24-bit offsets.");
        var offsets = new int[reader.BaseStream.Length / 3];
        for (var i = 0; i < offsets.Length; i++)
        {
            var low = reader.ReadByte();
            var middle = reader.ReadByte();
            var high = reader.ReadByte();
            var value = low | (middle << 8) | (high << 16);
            offsets[i] = value == 0xFFFFFF ? -1 : value;
        }
        return offsets;
    }

    private static byte[] ReadChunk(
        BinaryReader reader,
        IReadOnlyList<int> offsets,
        int chunk,
        IReadOnlyList<(ushort Bit0, ushort Bit1)> dictionary)
    {
        if (chunk < 0 || chunk >= offsets.Count - 1 || offsets[chunk] < 0)
            throw new InvalidDataException($"VGAGRAPH chunk {chunk} is unavailable.");
        var nextChunk = chunk + 1;
        while (nextChunk < offsets.Count && offsets[nextChunk] < 0)
            nextChunk++;
        if (nextChunk >= offsets.Count)
            throw new InvalidDataException($"VGAGRAPH chunk {chunk} has no terminating offset.");

        var offset = offsets[chunk];
        var compressedLength = offsets[nextChunk] - offset;
        if (compressedLength < sizeof(uint) || offset > reader.BaseStream.Length - compressedLength)
            throw new InvalidDataException($"VGAGRAPH chunk {chunk} lies outside its data file.");
        reader.BaseStream.Position = offset;
        var expandedLength = reader.ReadUInt32();
        if (expandedLength > int.MaxValue)
            throw new InvalidDataException($"VGAGRAPH chunk {chunk} has an unsupported expanded length.");
        var compressed = reader.ReadBytes(compressedLength - sizeof(uint));
        return ExpandHuffman(compressed, dictionary, (int)expandedLength);
    }

    private static byte[] ConvertPlanarToRowMajor(IReadOnlyList<byte> planar, int width, int height)
    {
        var pixelCount = checked(width * height);
        if (planar.Count != pixelCount || (width % 4) != 0)
            throw new InvalidDataException("A VGAGRAPH picture has invalid planar dimensions.");
        var rowMajor = new byte[pixelCount];
        var planeLength = pixelCount / 4;
        var planeWidth = width / 4;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                rowMajor[(y * width) + x] = planar[((x & 3) * planeLength) + (y * planeWidth) + (x / 4)];
        }
        return rowMajor;
    }

    private static (int Width, int Height)[] ReadPictureTable(byte[] data)
    {
        var pictures = new (int Width, int Height)[data.Length / (2 * sizeof(ushort))];
        for (var picture = 0; picture < pictures.Length; picture++)
        {
            var offset = picture * 2 * sizeof(ushort);
            pictures[picture] = (
                BitConverter.ToUInt16(data, offset),
                BitConverter.ToUInt16(data, offset + sizeof(ushort)));
        }
        return pictures;
    }

    private static WolfensteinGraphic ReadPicture(
        BinaryReader reader,
        IReadOnlyList<int> offsets,
        IReadOnlyList<(ushort Bit0, ushort Bit1)> dictionary,
        IReadOnlyList<(int Width, int Height)> pictures,
        int chunk)
    {
        var picture = chunk - FirstPictureChunk;
        if (picture < 0 || picture >= pictures.Count)
            throw new InvalidDataException($"VGAGRAPH chunk {chunk} has no picture-table entry.");
        var (width, height) = pictures[picture];
        var planarIndices = ReadChunk(reader, offsets, chunk, dictionary);
        return new WolfensteinGraphic(width, height, ConvertPlanarToRowMajor(planarIndices, width, height));
    }

    private static byte[] CopyIndices(WolfensteinGraphic graphic)
    {
        var indices = new byte[graphic.Width * graphic.Height];
        for (var y = 0; y < graphic.Height; y++)
        {
            for (var x = 0; x < graphic.Width; x++)
                indices[(y * graphic.Width) + x] = graphic.GetIndex(x, y);
        }
        return indices;
    }

    private static void DrawPicture(Span<byte> destination, WolfensteinGraphic source, int left, int top)
    {
        if (left < 0 || top < 0 || left + source.Width > StatusBarWidth || top + source.Height > StatusBarHeight)
            throw new InvalidDataException("A status-bar picture lies outside the 320 x 40 HUD.");
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
                destination[((top + y) * StatusBarWidth) + left + x] = source.GetIndex(x, y);
        }
    }

    private static void DrawNumber(
        BinaryReader reader,
        IReadOnlyList<int> offsets,
        IReadOnlyList<(ushort Bit0, ushort Bit1)> dictionary,
        IReadOnlyList<(int Width, int Height)> pictures,
        Span<byte> destination,
        int statusBarChunk,
        int x,
        int y,
        int width,
        int value)
    {
        var text = value.ToString();
        var sourceIndex = Math.Max(0, text.Length - width);
        for (var position = 0; position < width; position++)
        {
            var padding = position < width - Math.Min(width, text.Length);
            var pictureOffset = padding ? BlankDigitPictureOffset : ZeroDigitPictureOffset + text[sourceIndex++] - '0';
            var picture = ReadPicture(reader, offsets, dictionary, pictures, statusBarChunk + pictureOffset);
            DrawPicture(destination, picture, (x + position) * 8, y);
        }
    }
}
