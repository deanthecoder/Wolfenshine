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
/// Contains one decoded, row-major indexed picture from VGAGRAPH.
/// </summary>
/// <remarks>
/// Pictures remain palette-indexed so both software and future GPU renderers can share their source data.
/// </remarks>
public sealed class WolfensteinGraphic
{
    private readonly byte[] m_indices;

    public WolfensteinGraphic(int width, int height, IReadOnlyList<byte> indices)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(indices);
        if (indices.Count != checked(width * height))
            throw new ArgumentException("The graphic buffer must contain exactly width x height pixels.", nameof(indices));
        Width = width;
        Height = height;
        m_indices = indices.ToArray();
    }

    public int Width { get; }
    public int Height { get; }

    public byte GetIndex(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, Width);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, Height);
        return m_indices[(y * Width) + x];
    }
}
