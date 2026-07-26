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
/// Recreates Wolfenstein 3D's original new-game difficulty menu.
/// </summary>
public sealed class DifficultyViewport : Control
{
    private const int ViewportWidth = 320;
    private const int ViewportHeight = 200;
    private const byte BorderColor = 0x29;
    private const byte DarkBorderColor = 0x23;
    private const byte InactiveBorderColor = 0x2b;
    private const byte BackgroundColor = 0x2d;
    private const byte HeadingColor = 0x47;
    private const byte TextColor = 0x17;
    private const byte HighlightColor = 0x13;
    private static readonly string[] s_options =
    [
        "Can I play, Daddy?",
        "Don't hurt me.",
        "Bring 'em on!",
        "I am Death incarnate!"
    ];
    private readonly byte[] m_pixels = new byte[ViewportWidth * ViewportHeight * 4];
    private WriteableBitmap m_bitmap;

    public static readonly StyledProperty<WolfensteinDifficultyGraphics> GraphicsProperty =
        AvaloniaProperty.Register<DifficultyViewport, WolfensteinDifficultyGraphics>(nameof(Graphics));
    public static readonly StyledProperty<WolfensteinPalette> PaletteProperty =
        AvaloniaProperty.Register<DifficultyViewport, WolfensteinPalette>(nameof(Palette));
    public static readonly StyledProperty<int> SelectionProperty =
        AvaloniaProperty.Register<DifficultyViewport, int>(nameof(Selection), 2);

    static DifficultyViewport() => AffectsRender<DifficultyViewport>(GraphicsProperty, PaletteProperty, SelectionProperty);

    public WolfensteinDifficultyGraphics Graphics { get => GetValue(GraphicsProperty); set => SetValue(GraphicsProperty, value); }
    public WolfensteinPalette Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }
    public int Selection { get => GetValue(SelectionProperty); set => SetValue(SelectionProperty, value); }

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
        Fill(BorderColor);
        DrawGraphic(Graphics.MouseLegend, 112, 184);
        DrawText("How tough are you?", 70, 68, HeadingColor);
        FillRectangle(45, 90, 225, 67, BackgroundColor);
        DrawHorizontalLine(45, 270, 90, InactiveBorderColor);
        DrawVerticalLine(90, 157, 45, InactiveBorderColor);
        DrawHorizontalLine(45, 270, 157, DarkBorderColor);
        DrawVerticalLine(90, 157, 270, DarkBorderColor);
        for (var index = 0; index < s_options.Length; index++)
            DrawText(s_options[index], 74, 100 + (index * 13), index == Selection ? HighlightColor : TextColor);
        DrawGraphic(Graphics.Cursor, 50, 98 + (Selection * 13));
        DrawGraphic(Graphics.Faces[Selection], 235, 107);

        m_bitmap ??= new WriteableBitmap(
            new PixelSize(ViewportWidth, ViewportHeight),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Opaque);
        using var frameBuffer = m_bitmap.Lock();
        var rowBytes = ViewportWidth * 4;
        for (var y = 0; y < ViewportHeight; y++)
            Marshal.Copy(m_pixels, y * rowBytes, IntPtr.Add(frameBuffer.Address, y * frameBuffer.RowBytes), rowBytes);
    }

    private void DrawText(string text, int left, int top, byte colorIndex)
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
                        SetPixel(left + x, top + y, colorIndex);
                }
            }
            left += glyph.Width;
        }
    }

    private void DrawGraphic(WolfensteinGraphic graphic, int left, int top)
    {
        for (var y = 0; y < graphic.Height; y++)
        {
            for (var x = 0; x < graphic.Width; x++)
                SetPixel(left + x, top + y, graphic.GetIndex(x, y));
        }
    }

    private void Fill(byte colorIndex) => FillRectangle(0, 0, ViewportWidth, ViewportHeight, colorIndex);

    private void FillRectangle(int left, int top, int width, int height, byte colorIndex)
    {
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
                SetPixel(x, y, colorIndex);
        }
    }

    private void DrawHorizontalLine(int left, int right, int y, byte colorIndex)
    {
        for (var x = left; x <= right; x++)
            SetPixel(x, y, colorIndex);
    }

    private void DrawVerticalLine(int top, int bottom, int x, byte colorIndex)
    {
        for (var y = top; y <= bottom; y++)
            SetPixel(x, y, colorIndex);
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
