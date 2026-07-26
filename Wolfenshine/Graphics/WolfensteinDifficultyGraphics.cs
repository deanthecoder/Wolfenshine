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
/// Groups the original artwork used by the new-game difficulty menu.
/// </summary>
public sealed record WolfensteinDifficultyGraphics(
    WolfensteinGraphic Cursor,
    WolfensteinGraphic MouseLegend,
    IReadOnlyList<WolfensteinGraphic> Faces,
    WolfensteinFont Font,
    WolfensteinGraphic Pause);
