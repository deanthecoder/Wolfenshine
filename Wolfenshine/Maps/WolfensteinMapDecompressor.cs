// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Buffers.Binary;

namespace Wolfenshine.Maps;

/// <summary>
/// Expands the two compression stages used by Carmackized Wolfenstein 3D map planes.
/// </summary>
/// <remarks>
/// Keeping decompression separate from file navigation makes the format rules independently testable.
/// </remarks>
internal static class WolfensteinMapDecompressor
{
    private const byte NearTag = 0xA7;
    private const byte FarTag = 0xA8;

    public static ushort[] Expand(ReadOnlySpan<byte> source, ushort rlewTag, int expectedTileCount)
    {
        if (source.Length < sizeof(ushort))
            throw new InvalidDataException("The map plane is missing its Carmack-expanded length.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedTileCount);

        var sourceOffset = 0;
        var carmackByteLength = ReadUInt16(source, ref sourceOffset, "Carmack-expanded length");
        if ((carmackByteLength & 1) != 0)
            throw new InvalidDataException("The Carmack-expanded map plane length must be an even number of bytes.");

        var rlewSource = ExpandCarmack(source[sourceOffset..], carmackByteLength / sizeof(ushort));
        return ExpandRlew(rlewSource, rlewTag, expectedTileCount);
    }

    private static ushort[] ExpandCarmack(ReadOnlySpan<byte> source, int expectedWordCount)
    {
        var output = new ushort[expectedWordCount];
        var sourceOffset = 0;
        var outputOffset = 0;
        while (outputOffset < output.Length)
        {
            var value = ReadUInt16(source, ref sourceOffset, "Carmack-compressed word");
            var tag = (byte)(value >> 8);
            if (tag != NearTag && tag != FarTag)
            {
                output[outputOffset++] = value;
                continue;
            }

            var count = (byte)value;
            if (count == 0)
            {
                if (sourceOffset >= source.Length)
                    throw new InvalidDataException("A Carmack tag escape is missing its literal byte.");
                output[outputOffset++] = (ushort)((tag << 8) | source[sourceOffset++]);
                continue;
            }

            int copyOffset;
            if (tag == NearTag)
            {
                if (sourceOffset >= source.Length)
                    throw new InvalidDataException("A near Carmack copy is missing its offset.");
                var distance = source[sourceOffset++];
                copyOffset = outputOffset - distance;
            }
            else
            {
                copyOffset = ReadUInt16(source, ref sourceOffset, "far Carmack copy offset");
            }

            if (copyOffset < 0 || copyOffset >= outputOffset)
                throw new InvalidDataException("A Carmack copy points outside the expanded data.");
            if (outputOffset + count > output.Length)
                throw new InvalidDataException("A Carmack copy exceeds the declared expanded length.");

            for (var i = 0; i < count; i++)
                output[outputOffset++] = output[copyOffset++];
        }

        return output;
    }

    private static ushort[] ExpandRlew(ReadOnlySpan<ushort> source, ushort rlewTag, int expectedTileCount)
    {
        if (source.Length == 0)
            throw new InvalidDataException("The map plane is missing its RLEW-expanded length.");

        var declaredByteLength = source[0];
        var expectedByteLength = checked(expectedTileCount * sizeof(ushort));
        if (declaredByteLength != expectedByteLength)
        {
            throw new InvalidDataException(
                $"The map plane declares {declaredByteLength} expanded bytes; {expectedByteLength} were expected.");
        }

        var output = new ushort[expectedTileCount];
        var sourceOffset = 1;
        var outputOffset = 0;
        while (outputOffset < output.Length)
        {
            if (sourceOffset >= source.Length)
                throw new InvalidDataException("The RLEW-compressed map plane ended early.");

            var value = source[sourceOffset++];
            if (value != rlewTag)
            {
                output[outputOffset++] = value;
                continue;
            }

            if (sourceOffset + 1 >= source.Length)
                throw new InvalidDataException("An RLEW run is missing its count or value.");
            var count = source[sourceOffset++];
            var repeatedValue = source[sourceOffset++];
            if (count == 0 || outputOffset + count > output.Length)
                throw new InvalidDataException("An RLEW run exceeds the declared expanded length.");
            Array.Fill(output, repeatedValue, outputOffset, count);
            outputOffset += count;
        }

        return output;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> source, ref int offset, string description)
    {
        if (offset + sizeof(ushort) > source.Length)
            throw new InvalidDataException($"The map plane ended while reading its {description}.");
        var value = BinaryPrimitives.ReadUInt16LittleEndian(source[offset..]);
        offset += sizeof(ushort);
        return value;
    }
}
