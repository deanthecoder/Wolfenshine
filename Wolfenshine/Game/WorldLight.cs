// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Wolfenshine.Game;

/// <summary>
/// Describes a directional point light positioned in the map.
/// </summary>
/// <remarks>
/// Dynamic effects can use the same directional brightness and radius data as static scenery lights.
/// </remarks>
public readonly record struct WorldLight(
    double X,
    double Y,
    float UpwardBrightness,
    float DownwardBrightness,
    float UpwardRadius,
    float DownwardRadius);
