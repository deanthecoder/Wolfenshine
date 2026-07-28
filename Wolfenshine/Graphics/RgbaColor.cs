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
/// Represents one renderer-independent 32-bit color.
/// </summary>
/// <remarks>
/// Explicit channels avoid coupling game assets to Avalonia or a future GPU API's packed byte order.
/// </remarks>
public readonly record struct RgbaColor(
    byte Red,
    byte Green,
    byte Blue,
    byte Alpha = byte.MaxValue)
{
    public RgbaColor Scale(double amount) => new(
        (byte)(Red * amount),
        (byte)(Green * amount),
        (byte)(Blue * amount),
        Alpha);

    public RgbaColor Blend(RgbaColor other, double amount)
    {
        amount = Math.Clamp(amount, 0.0, 1.0);
        return new RgbaColor(
            (byte)Math.Round(Red + ((other.Red - Red) * amount)),
            (byte)Math.Round(Green + ((other.Green - Green) * amount)),
            (byte)Math.Round(Blue + ((other.Blue - Blue) * amount)),
            Alpha);
    }
}
