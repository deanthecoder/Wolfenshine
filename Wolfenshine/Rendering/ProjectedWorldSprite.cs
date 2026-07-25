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
/// Describes one visible world sprite in screen space.
/// </summary>
/// <remarks>
/// Depth is retained for wall-column clipping and back-to-front compositing.
/// </remarks>
public readonly record struct ProjectedWorldSprite(
    int SpriteNumber,
    double Depth,
    int CenterX,
    int RenderedSize);
