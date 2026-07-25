// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Wolfenshine.Maps;

/// <summary>
/// Represents the maps available in one Wolfenstein 3D data set.
/// </summary>
/// <remarks>
/// Map slots are retained because sparse releases can omit entries from the fixed-size header table.
/// </remarks>
public sealed class WolfensteinMapSet
{
    public WolfensteinMapSet(ushort rlewTag, IReadOnlyList<WolfensteinMap> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        RlewTag = rlewTag;
        Maps = maps;
    }

    public ushort RlewTag { get; }
    public IReadOnlyList<WolfensteinMap> Maps { get; }
}
