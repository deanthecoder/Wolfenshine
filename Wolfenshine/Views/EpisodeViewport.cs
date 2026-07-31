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
/// Recreates Wolfenstein 3D's original episode-selection screen.
/// </summary>
public sealed class EpisodeViewport : Control
{
    private const int ViewportWidth = 320;
    private const int ViewportHeight = 200;
    private const byte BorderColor = 0x29;
    private const byte DarkBorderColor = 0x23;
    private const byte InactiveColor = 0x2b;
    private const byte BackgroundColor = 0x2d;
    private const byte HeadingColor = 0x47;
    private const byte TextColor = 0x17;
    private const byte HighlightColor = 0x13;
    private static readonly string[] s_episodeNames =
    [
        "Escape from Wolfenstein",
        "Operation: Eisenfaust",
        "Die, Fuhrer, Die!",
        "A Dark Secret",
        "Trail of the Madman",
        "Confrontation"
    ];
    private readonly byte[] m_pixels = new byte[ViewportWidth * ViewportHeight * 4];
    private WriteableBitmap m_bitmap;

    public static readonly StyledProperty<WolfensteinDifficultyGraphics> GraphicsProperty =
        AvaloniaProperty.Register<EpisodeViewport, WolfensteinDifficultyGraphics>(nameof(Graphics));
    public static readonly StyledProperty<WolfensteinPalette> PaletteProperty =
        AvaloniaProperty.Register<EpisodeViewport, WolfensteinPalette>(nameof(Palette));
    public static readonly StyledProperty<int> SelectionProperty =
        AvaloniaProperty.Register<EpisodeViewport, int>(nameof(Selection));
    public static readonly StyledProperty<int> AvailableEpisodesProperty =
        AvaloniaProperty.Register<EpisodeViewport, int>(nameof(AvailableEpisodes), 1);

    static EpisodeViewport() => AffectsRender<EpisodeViewport>(
        GraphicsProperty,
        PaletteProperty,
        SelectionProperty,
        AvailableEpisodesProperty);

    public WolfensteinDifficultyGraphics Graphics { get => GetValue(GraphicsProperty); set => SetValue(GraphicsProperty, value); }
    public WolfensteinPalette Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }
    public int Selection { get => GetValue(SelectionProperty); set => SetValue(SelectionProperty, value); }
    public int AvailableEpisodes { get => GetValue(AvailableEpisodesProperty); set => SetValue(AvailableEpisodesProperty, value); }

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
        FillRectangle(0, 0, ViewportWidth, ViewportHeight, BorderColor);
        FillRectangle(6, 19, 308, 162, BackgroundColor);
        DrawHorizontalLine(6, 313, 19, InactiveColor);
        DrawVerticalLine(19, 180, 6, InactiveColor);
        DrawHorizontalLine(6, 313, 180, DarkBorderColor);
        DrawVerticalLine(19, 180, 313, DarkBorderColor);
        DrawGraphic(Graphics.MouseLegend, 112, 184);
        DrawCenteredText("Which episode to play?", 2, HeadingColor);

        for (var episode = 0; episode < s_episodeNames.Length; episode++)
        {
            var top = 23 + (episode * 26);
            DrawGraphic(Graphics.EpisodePictures[episode], 42, top);
            var color = episode >= AvailableEpisodes
                ? InactiveColor
                : episode == Selection ? HighlightColor : TextColor;
            DrawText($"Episode {episode + 1}", 98, top + 1, color);
            DrawText(s_episodeNames[episode], 98, top + 12, color);
        }
        DrawGraphic(Graphics.Cursor, 12, 27 + (Selection * 26));

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

    private void DrawCenteredText(string text, int top, byte colorIndex) =>
        DrawText(text, (ViewportWidth - MeasureText(text)) / 2, top, colorIndex);

    private int MeasureText(string text) => text.Sum(character =>
        Graphics.Font.Glyphs.TryGetValue(character, out var glyph) ? glyph.Width : 0);

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
