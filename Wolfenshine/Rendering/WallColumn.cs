// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Wolfenshine.Rendering;

/// <summary>
/// Describes the first wall hit by one screen-column ray.
/// </summary>
/// <remarks>
/// Both renderers consume the same compact raycast result; enhanced rendering also uses the concave-corner flags.
/// </remarks>
public readonly record struct WallColumn(
    double Distance,
    double TextureU,
    ushort Tile,
    WallSide Side,
    bool IsDoorJamb = false,
    bool HasConcaveTextureStart = false,
    bool HasConcaveTextureEnd = false);
