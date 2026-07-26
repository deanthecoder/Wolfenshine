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
/// Requests playback of a gameplay effect, optionally from a map position.
/// </summary>
/// <remarks>
/// World coordinates remain independent of the audio backend and are transformed relative to the listener at playback.
/// </remarks>
public sealed record WolfensteinSoundEvent(WolfensteinSoundEffect Effect, double? X = null, double? Y = null);
