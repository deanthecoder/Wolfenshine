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
/// Stores one decoded 64 x 64 indexed sprite and its structural transparency mask.
/// </summary>
/// <remarks>
/// VSWAP sprites encode opaque posts rather than reserving a palette index for transparency.
/// </remarks>
public sealed class WolfensteinSprite
{
    public const int Size = 64;
    public const int PixelCount = Size * Size;
    private readonly byte[] m_indices;
    private readonly bool[] m_opacity;

    public WolfensteinSprite(IReadOnlyList<byte> indices, IReadOnlyList<bool> opacity)
    {
        ArgumentNullException.ThrowIfNull(indices);
        ArgumentNullException.ThrowIfNull(opacity);
        if (indices.Count != PixelCount || opacity.Count != PixelCount)
            throw new ArgumentException($"Sprite buffers must each contain exactly {PixelCount} pixels.");
        m_indices = indices.ToArray();
        m_opacity = opacity.ToArray();
    }

    public bool TryGetIndex(int x, int y, out byte index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Size);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Size);
        var offset = (y * Size) + x;
        index = m_indices[offset];
        return m_opacity[offset];
    }
}
