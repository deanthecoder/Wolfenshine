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
/// Identifies the map-grid side crossed by a wall ray.
/// </summary>
/// <remarks>
/// The side supplies a wall normal for directional shading and future textured rendering.
/// </remarks>
public enum WallSide
{
    Vertical,
    Horizontal
}
