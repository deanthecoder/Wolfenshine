// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Wolfenshine.Graphics;

/// <summary>
/// Contains the original large intermission font and BJ breathing pictures.
/// </summary>
public sealed class WolfensteinIntermissionGraphics
{
    public WolfensteinIntermissionGraphics(
        IReadOnlyList<WolfensteinGraphic> bjFrames,
        IReadOnlyDictionary<char, WolfensteinGraphic> characters)
    {
        ArgumentNullException.ThrowIfNull(bjFrames);
        ArgumentNullException.ThrowIfNull(characters);
        BjFrames = bjFrames;
        Characters = characters;
    }

    public IReadOnlyList<WolfensteinGraphic> BjFrames { get; }
    public IReadOnlyDictionary<char, WolfensteinGraphic> Characters { get; }
}
