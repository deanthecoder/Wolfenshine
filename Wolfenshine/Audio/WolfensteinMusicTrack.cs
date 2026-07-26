// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Wolfenshine.Audio;

/// <summary>
/// Contains the original IMF register writes and delays for one AdLib music track.
/// </summary>
public sealed class WolfensteinMusicTrack
{
    public WolfensteinMusicTrack(IReadOnlyList<WolfensteinMusicCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        Commands = commands;
    }

    public IReadOnlyList<WolfensteinMusicCommand> Commands { get; }
}

/// <summary>
/// Describes one OPL register write followed by an IMF delay.
/// </summary>
public readonly record struct WolfensteinMusicCommand(byte Register, byte Value, ushort Delay);
