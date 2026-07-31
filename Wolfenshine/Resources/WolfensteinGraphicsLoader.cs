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
    private const int FirstWeaponPictureOffset = 5;
    private const int NoKeyPictureOffset = 9;
    private const int GoldKeyPictureOffset = 10;
    private const int SilverKeyPictureOffset = 11;
    private const int BlankDigitPictureOffset = 12;
    private const int ZeroDigitPictureOffset = 13;
    private const int FirstFacePictureOffset = 23;
    private const int FacePictureCount = 23;
    private const int IntermissionGuyOffset = -43;
    private const int DifficultyCursorWidth = 24;
    private const int DifficultyCursorHeight = 16;
    private const int DifficultyMouseLegendWidth = 104;
    private const int DifficultyMouseLegendHeight = 16;
    private const int DifficultyFaceWidth = 24;
    private const int DifficultyFaceHeight = 32;
    private const int DifficultyFaceCount = 4;
    private const int EpisodePictureWidth = 48;
    private const int EpisodePictureHeight = 24;
    private const int EpisodePictureCount = 6;
    private const int GetPsychedWidth = 224;
    private const int GetPsychedHeight = 48;
    private const int MenuFontChunk = 2;
    private const int PausePictureOffset = 47;

    public static WolfensteinHudGraphics LoadHudGraphics(WolfensteinResources resources)
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
            var noKey = ReadPicture(reader, offsets, dictionary, pictures, chunk + NoKeyPictureOffset);
            var goldKey = ReadPicture(reader, offsets, dictionary, pictures, chunk + GoldKeyPictureOffset);
            var silverKey = ReadPicture(reader, offsets, dictionary, pictures, chunk + SilverKeyPictureOffset);
            var blankDigit = ReadPicture(reader, offsets, dictionary, pictures, chunk + BlankDigitPictureOffset);
            var weaponIcons = Enumerable.Range(FirstWeaponPictureOffset, 4)
                .Select(relativeChunk => ReadPicture(reader, offsets, dictionary, pictures, chunk + relativeChunk))
                .ToArray();
            var digits = Enumerable.Range(ZeroDigitPictureOffset, 10)
                .Select(relativeChunk => ReadPicture(reader, offsets, dictionary, pictures, chunk + relativeChunk))
                .ToArray();
            var faces = Enumerable.Range(FirstFacePictureOffset, FacePictureCount)
                .Select(relativeChunk => ReadPicture(reader, offsets, dictionary, pictures, chunk + relativeChunk))
                .ToArray();
            Logger.Instance.Info($"Loaded {width} x {height} HUD graphics from VGAGRAPH chunk {chunk}.");
            return new WolfensteinHudGraphics(
                statusBar, weaponIcons, noKey, goldKey, silverKey, blankDigit, digits, faces);
        }

        throw new InvalidDataException("The graphics data does not contain a 320 x 40 status-bar picture.");
    }

    public static WolfensteinIntermissionGraphics LoadIntermissionGraphics(WolfensteinResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var dictionary = ReadDictionary(resources);
        var offsets = ReadOffsets(resources);
        using var reader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.GraphicsData));
        var pictureTableData = ReadChunk(reader, offsets, 0, dictionary);
        var pictures = ReadPictureTable(pictureTableData);
        var statusChunk = FindPictureChunk(pictures, StatusBarWidth, StatusBarHeight);
        var firstChunk = statusChunk + IntermissionGuyOffset;
        var characters = new Dictionary<char, WolfensteinGraphic>
        {
            [':'] = ReadPicture(reader, offsets, dictionary, pictures, firstChunk + 1),
            ['%'] = ReadPicture(reader, offsets, dictionary, pictures, firstChunk + 12),
            ['!'] = ReadPicture(reader, offsets, dictionary, pictures, firstChunk + 39),
            ['\''] = ReadPicture(reader, offsets, dictionary, pictures, firstChunk + 40)
        };
        for (var digit = 0; digit < 10; digit++)
            characters[(char)('0' + digit)] = ReadPicture(reader, offsets, dictionary, pictures, firstChunk + 2 + digit);
        for (var letter = 0; letter < 26; letter++)
            characters[(char)('A' + letter)] = ReadPicture(reader, offsets, dictionary, pictures, firstChunk + 13 + letter);
        WolfensteinGraphic[] bjFrames =
        [
            ReadPicture(reader, offsets, dictionary, pictures, firstChunk),
            ReadPicture(reader, offsets, dictionary, pictures, firstChunk + 41)
        ];
        Logger.Instance.Info($"Loaded original intermission graphics relative to VGAGRAPH chunk {statusChunk}.");
        return new WolfensteinIntermissionGraphics(bjFrames, characters);
    }

    public static WolfensteinDifficultyGraphics LoadDifficultyGraphics(WolfensteinResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var dictionary = ReadDictionary(resources);
        var offsets = ReadOffsets(resources);
        using var reader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.GraphicsData));
        var pictureTableData = ReadChunk(reader, offsets, 0, dictionary);
        var pictures = ReadPictureTable(pictureTableData);
        var cursorChunk = FindPictureChunk(pictures, DifficultyCursorWidth, DifficultyCursorHeight);
        var mouseLegendChunk = FindPictureChunk(
            pictures,
            DifficultyMouseLegendWidth,
            DifficultyMouseLegendHeight);
        var firstFaceChunk = FindConsecutivePictureChunks(
            pictures,
            DifficultyFaceWidth,
            DifficultyFaceHeight,
            DifficultyFaceCount);
        var firstEpisodeChunk = FindConsecutivePictureChunks(
            pictures,
            EpisodePictureWidth,
            EpisodePictureHeight,
            EpisodePictureCount);
        var cursor = ReadPicture(reader, offsets, dictionary, pictures, cursorChunk);
        var mouseLegend = ReadPicture(reader, offsets, dictionary, pictures, mouseLegendChunk);
        var episodePictures = Enumerable.Range(firstEpisodeChunk, EpisodePictureCount)
            .Select(chunk => ReadPicture(reader, offsets, dictionary, pictures, chunk))
            .ToArray();
        var faces = Enumerable.Range(firstFaceChunk, DifficultyFaceCount)
            .Select(chunk => ReadPicture(reader, offsets, dictionary, pictures, chunk))
            .ToArray();
        var font = ReadFont(ReadChunk(reader, offsets, MenuFontChunk, dictionary));
        var statusChunk = FindPictureChunk(pictures, StatusBarWidth, StatusBarHeight);
        var title = ReadPicture(reader, offsets, dictionary, pictures, statusChunk + 1);
        var pause = ReadPicture(reader, offsets, dictionary, pictures, statusChunk + PausePictureOffset);
        var getPsychedChunk = FindPictureChunk(pictures, GetPsychedWidth, GetPsychedHeight);
        var getPsyched = ReadPicture(reader, offsets, dictionary, pictures, getPsychedChunk);
        Logger.Instance.Info(
            "Loaded the original title, episode, menu, pause, and Get Psyched graphics from VGAGRAPH.");
        return new WolfensteinDifficultyGraphics(
            title,
            cursor,
            mouseLegend,
            episodePictures,
            faces,
            font,
            pause,
            getPsyched);
    }

    private static WolfensteinFont ReadFont(ReadOnlySpan<byte> data)
    {
        const int characterCount = 256;
        const int locationTableOffset = sizeof(ushort);
        const int widthTableOffset = locationTableOffset + (characterCount * sizeof(ushort));
        const int headerLength = widthTableOffset + characterCount;
        if (data.Length < headerLength)
            throw new InvalidDataException("A VGAGRAPH font chunk is shorter than its header.");
        var height = BitConverter.ToUInt16(data);
        var glyphs = new Dictionary<char, WolfensteinGraphic>();
        for (var character = 32; character < 127; character++)
        {
            var width = data[widthTableOffset + character];
            if (width == 0)
                continue;
            var location = BitConverter.ToUInt16(data.Slice(locationTableOffset + (character * sizeof(ushort))));
            var length = checked(width * height);
            if (location > data.Length - length)
                throw new InvalidDataException($"VGAGRAPH font glyph {character} lies outside its chunk.");
            glyphs[(char)character] = new WolfensteinGraphic(width, height, data.Slice(location, length).ToArray());
        }
        return new WolfensteinFont(height, glyphs);
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
            throw new InvalidDataException("The graphics dictionary does not contain all 255 Huffman nodes.");
        return Enumerable.Range(0, HuffmanNodeCount)
            .Select(_ => (reader.ReadUInt16(), reader.ReadUInt16()))
            .ToArray();
    }

    private static int[] ReadOffsets(WolfensteinResources resources)
    {
        using var reader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.GraphicsHeader));
        if (reader.BaseStream.Length < 6 || (reader.BaseStream.Length % 3) != 0)
            throw new InvalidDataException("The graphics header does not contain a valid table of 24-bit offsets.");
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

    private static int FindPictureChunk(IReadOnlyList<(int Width, int Height)> pictures, int width, int height)
    {
        for (var picture = 0; picture < pictures.Count; picture++)
        {
            if (pictures[picture].Width == width && pictures[picture].Height == height)
                return FirstPictureChunk + picture;
        }
        throw new InvalidDataException($"The graphics data does not contain a {width} x {height} picture.");
    }

    private static int FindConsecutivePictureChunks(
        IReadOnlyList<(int Width, int Height)> pictures,
        int width,
        int height,
        int count)
    {
        for (var firstPicture = 0; firstPicture <= pictures.Count - count; firstPicture++)
        {
            var matches = true;
            for (var offset = 0; offset < count; offset++)
            {
                var picture = pictures[firstPicture + offset];
                if (picture.Width == width && picture.Height == height)
                    continue;
                matches = false;
                break;
            }
            if (matches)
                return FirstPictureChunk + firstPicture;
        }
        throw new InvalidDataException(
            $"VGAGRAPH does not contain {count} consecutive {width} x {height} pictures.");
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

}
