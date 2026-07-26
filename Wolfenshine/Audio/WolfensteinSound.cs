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
/// Contains one unsigned, mono PCM sound ready for OpenAL.
/// </summary>
/// <remarks>
/// A common representation allows digitized VSWAP samples and synthesized PC-speaker effects to share playback.
/// </remarks>
public sealed record WolfensteinSound(byte[] Samples, int SampleRate);
