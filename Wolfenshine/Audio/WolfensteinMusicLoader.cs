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
using Wolfenshine.Resources;

namespace Wolfenshine.Audio;

/// <summary>
/// Loads the original Wolfenstein 3D IMF music sequences from the selected audio-data edition.
/// </summary>
public static class WolfensteinMusicLoader
{
    private const int FirstMusicChunk = 261;

    public static IReadOnlyList<WolfensteinMusicTrack> Load(WolfensteinResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        using var headerReader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.AudioHeader));
        var offsets = new uint[headerReader.BaseStream.Length / sizeof(uint)];
        for (var index = 0; index < offsets.Length; index++)
            offsets[index] = headerReader.ReadUInt32();

        using var dataReader = new BinaryReader(resources.OpenRead(WolfensteinResourceKind.AudioData));
        var tracks = new List<WolfensteinMusicTrack>();
        for (var chunk = FirstMusicChunk; chunk < offsets.Length - 1; chunk++)
        {
            var start = offsets[chunk];
            var end = offsets[chunk + 1];
            if (end <= start || start > dataReader.BaseStream.Length - sizeof(ushort))
                continue;
            dataReader.BaseStream.Position = start;
            var byteLength = dataReader.ReadUInt16();
            if (byteLength % 4 != 0 || byteLength > end - start - sizeof(ushort))
                throw new InvalidDataException($"Music chunk {chunk} has an invalid IMF length.");
            var commands = new WolfensteinMusicCommand[byteLength / 4];
            for (var index = 0; index < commands.Length; index++)
            {
                commands[index] = new WolfensteinMusicCommand(
                    dataReader.ReadByte(),
                    dataReader.ReadByte(),
                    dataReader.ReadUInt16());
            }
            tracks.Add(new WolfensteinMusicTrack(commands));
        }
        Logger.Instance.Info($"Loaded {tracks.Count} AdLib music sequences.");
        return tracks;
    }
}
