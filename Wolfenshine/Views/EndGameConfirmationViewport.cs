// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Wolfenshine.Graphics;

namespace Wolfenshine.Views;

/// <summary>
/// Draws the original end-game confirmation window over frozen gameplay.
/// </summary>
public sealed class EndGameConfirmationViewport : Control
{
    private const int ViewportWidth = 320;
    private const int ViewportHeight = 200;
    private const byte BackgroundColor = 0x17;
    private const byte HighlightColor = 0x13;
    private static readonly string[] s_lines =
    [
        "Are you sure you want",
        "to end the game you",
        "are playing? (Y or N):"
    ];
    private readonly byte[] m_pixels = new byte[ViewportWidth * ViewportHeight * 4];
    private WriteableBitmap m_bitmap;

    public static readonly StyledProperty<WolfensteinDifficultyGraphics> GraphicsProperty =
        AvaloniaProperty.Register<EndGameConfirmationViewport, WolfensteinDifficultyGraphics>(nameof(Graphics));
    public static readonly StyledProperty<WolfensteinPalette> PaletteProperty =
        AvaloniaProperty.Register<EndGameConfirmationViewport, WolfensteinPalette>(nameof(Palette));
    public static readonly StyledProperty<bool> ShowCursorProperty =
        AvaloniaProperty.Register<EndGameConfirmationViewport, bool>(nameof(ShowCursor), true);

    static EndGameConfirmationViewport() => AffectsRender<EndGameConfirmationViewport>(
        GraphicsProperty,
        PaletteProperty,
        ShowCursorProperty);

    public WolfensteinDifficultyGraphics Graphics { get => GetValue(GraphicsProperty); set => SetValue(GraphicsProperty, value); }
    public WolfensteinPalette Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }
    public bool ShowCursor { get => GetValue(ShowCursorProperty); set => SetValue(ShowCursorProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Graphics == null || Palette == null)
            return;
        RenderFrame();
        context.DrawImage(m_bitmap, Bounds);
    }

    private void RenderFrame()
    {
        Array.Clear(m_pixels);
        var lineHeight = Graphics.Font.Height;
        var textWidth = s_lines.Max(MeasureText);
        var textHeight = lineHeight * s_lines.Length;
        var windowWidth = textWidth + 20;
        var windowHeight = textHeight + 10;
        var windowLeft = (ViewportWidth - windowWidth) / 2;
        var windowTop = (ViewportHeight - windowHeight) / 2;
        FillRectangle(windowLeft, windowTop, windowWidth, windowHeight, BackgroundColor);
        DrawOutline(windowLeft, windowTop, windowWidth, windowHeight);

        var textLeft = windowLeft + 10;
        var textTop = windowTop + 5;
        for (var line = 0; line < s_lines.Length; line++)
            DrawText(s_lines[line], textLeft, textTop + (line * lineHeight));
        if (ShowCursor)
            DrawText("_", textLeft + MeasureText(s_lines[^1]), textTop + (2 * lineHeight));

        m_bitmap ??= new WriteableBitmap(
            new PixelSize(ViewportWidth, ViewportHeight),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Premul);
        using var frameBuffer = m_bitmap.Lock();
        var rowBytes = ViewportWidth * 4;
        for (var y = 0; y < ViewportHeight; y++)
            Marshal.Copy(m_pixels, y * rowBytes, IntPtr.Add(frameBuffer.Address, y * frameBuffer.RowBytes), rowBytes);
    }

    private int MeasureText(string text) => text.Sum(character =>
        Graphics.Font.Glyphs.TryGetValue(character, out var glyph) ? glyph.Width : 0);

    private void DrawText(string text, int left, int top)
    {
        foreach (var character in text)
        {
            if (!Graphics.Font.Glyphs.TryGetValue(character, out var glyph))
                continue;
            for (var y = 0; y < glyph.Height; y++)
            {
                for (var x = 0; x < glyph.Width; x++)
                {
                    if (glyph.GetIndex(x, y) != 0)
                        SetPixel(left + x, top + y, 0);
                }
            }
            left += glyph.Width;
        }
    }

    private void FillRectangle(int left, int top, int width, int height, byte colorIndex)
    {
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
                SetPixel(x, y, colorIndex);
        }
    }

    private void DrawOutline(int left, int top, int width, int height)
    {
        for (var x = left; x < left + width; x++)
        {
            SetPixel(x, top, HighlightColor);
            SetPixel(x, top + height - 1, 0);
        }
        for (var y = top; y < top + height; y++)
        {
            SetPixel(left, y, HighlightColor);
            SetPixel(left + width - 1, y, 0);
        }
    }

    private void SetPixel(int x, int y, byte colorIndex)
    {
        if (x < 0 || x >= ViewportWidth || y < 0 || y >= ViewportHeight)
            return;
        var color = Palette.GetColor(colorIndex);
        var offset = ((y * ViewportWidth) + x) * 4;
        m_pixels[offset] = color.Red;
        m_pixels[offset + 1] = color.Green;
        m_pixels[offset + 2] = color.Blue;
        m_pixels[offset + 3] = byte.MaxValue;
    }
}
