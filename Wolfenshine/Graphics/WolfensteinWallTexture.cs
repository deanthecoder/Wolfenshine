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
/// Contains one original 64 x 64 indexed wall page.
/// </summary>
/// <remarks>
/// VSWAP stores each vertical texture column contiguously rather than using conventional row-major order.
/// </remarks>
public sealed class WolfensteinWallTexture
{
    public const int Size = 64;
    public const int DataLength = Size * Size;
    private readonly byte[] m_indices;

    public WolfensteinWallTexture(byte[] indices)
    {
        ArgumentNullException.ThrowIfNull(indices);
        if (indices.Length != DataLength)
            throw new ArgumentException($"A wall texture must contain exactly {DataLength} indices.", nameof(indices));
        m_indices = indices;
    }

    public byte GetIndex(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Size);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Size);
        return m_indices[(x * Size) + y];
    }
}
