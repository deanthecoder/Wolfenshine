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
/// Places one VSWAP sprite at a world-space position.
/// </summary>
/// <remarks>
/// Static decorations use tile centers, while actors supply continuously changing positions and lighting state.
/// </remarks>
public readonly record struct WorldSprite(
    double X,
    double Y,
    int SpriteNumber,
    int FacingDirection = -1,
    bool IsActor = false,
    float Brightness = 1.0f,
    float GodRayAmount = 0.0f);
