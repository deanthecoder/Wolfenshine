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
using Wolfenshine.Game;
using Wolfenshine.Graphics;

namespace Wolfenshine.Views;

/// <summary>
/// Recreates the original 320 x 160 level-completion playfield from indexed graphics.
/// </summary>
public sealed class IntermissionViewport : Control
{
    private const int ViewportWidth = 320;
    private const int ViewportHeight = 160;
    private const byte BackgroundIndex = 127;
    private readonly byte[] m_pixels = new byte[ViewportWidth * ViewportHeight * 4];
    private WriteableBitmap m_bitmap;

    public static readonly StyledProperty<WolfensteinLevelStats> StatsProperty =
        AvaloniaProperty.Register<IntermissionViewport, WolfensteinLevelStats>(nameof(Stats));
    public static readonly StyledProperty<WolfensteinIntermissionGraphics> GraphicsProperty =
        AvaloniaProperty.Register<IntermissionViewport, WolfensteinIntermissionGraphics>(nameof(Graphics));
    public static readonly StyledProperty<WolfensteinPalette> PaletteProperty =
        AvaloniaProperty.Register<IntermissionViewport, WolfensteinPalette>(nameof(Palette));
    public static readonly StyledProperty<int> BjFrameProperty =
        AvaloniaProperty.Register<IntermissionViewport, int>(nameof(BjFrame));

    static IntermissionViewport() => AffectsRender<IntermissionViewport>(
        StatsProperty,
        GraphicsProperty,
        PaletteProperty,
        BjFrameProperty);

    public WolfensteinLevelStats Stats { get => GetValue(StatsProperty); set => SetValue(StatsProperty, value); }
    public WolfensteinIntermissionGraphics Graphics { get => GetValue(GraphicsProperty); set => SetValue(GraphicsProperty, value); }
    public WolfensteinPalette Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }
    public int BjFrame { get => GetValue(BjFrameProperty); set => SetValue(BjFrameProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Stats == null || Graphics == null || Palette == null)
            return;
        RenderFrame();
        context.DrawImage(m_bitmap, Bounds);
    }

    private void RenderFrame()
    {
        Fill(BackgroundIndex);
        DrawGraphic(Graphics.BjFrames[BjFrame % Graphics.BjFrames.Count], 0, 16);
        DrawText(14, 2, "FLOOR\nCOMPLETED");
        DrawText(26, 2, Stats.Floor.ToString());
        DrawText(14, 7, "BONUS");
        DrawRightAligned(36, 7, Stats.BonusText);
        DrawText(16, 10, "TIME");
        DrawText(26, 10, Stats.TimeText);
        DrawText(16, 12, "PAR");
        DrawText(26, 12, Stats.ParText);
        DrawText(9, 14, "KILL RATIO");
        DrawRightAligned(37, 14, Stats.KillText);
        DrawText(5, 16, "SECRET RATIO");
        DrawRightAligned(37, 16, Stats.SecretText);
        DrawText(1, 18, "TREASURE RATIO");
        DrawRightAligned(37, 18, Stats.TreasureText);

        m_bitmap ??= new WriteableBitmap(
            new PixelSize(ViewportWidth, ViewportHeight),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Opaque);
        using var frameBuffer = m_bitmap.Lock();
        var rowBytes = ViewportWidth * 4;
        for (var y = 0; y < ViewportHeight; y++)
        {
            Marshal.Copy(
                m_pixels,
                y * rowBytes,
                IntPtr.Add(frameBuffer.Address, y * frameBuffer.RowBytes),
                rowBytes);
        }
    }

    private void Fill(byte paletteIndex)
    {
        var color = Palette.GetColor(paletteIndex);
        for (var offset = 0; offset < m_pixels.Length; offset += 4)
        {
            m_pixels[offset] = color.Red;
            m_pixels[offset + 1] = color.Green;
            m_pixels[offset + 2] = color.Blue;
            m_pixels[offset + 3] = byte.MaxValue;
        }
    }

    private void DrawText(int x, int y, string text)
    {
        var originX = x * 8;
        var left = originX;
        var top = y * 8;
        foreach (var character in text.ToUpperInvariant())
        {
            if (character == '\n')
            {
                left = originX;
                top += 16;
                continue;
            }
            if (character == ' ')
            {
                left += 16;
                continue;
            }
            if (!Graphics.Characters.TryGetValue(character, out var graphic))
                continue;
            DrawGraphic(graphic, left, top);
            left += character is ':' or '!' or '\'' ? 8 : 16;
        }
    }

    private void DrawRightAligned(int right, int y, string text)
    {
        var width = text.Sum(character => character is ':' or '!' or '\'' ? 1 : 2);
        DrawText(right - width, y, text);
    }

    private void DrawGraphic(WolfensteinGraphic graphic, int left, int top)
    {
        for (var y = 0; y < graphic.Height && top + y < ViewportHeight; y++)
        {
            for (var x = 0; x < graphic.Width && left + x < ViewportWidth; x++)
            {
                var color = Palette.GetColor(graphic.GetIndex(x, y));
                var offset = (((top + y) * ViewportWidth) + left + x) * 4;
                m_pixels[offset] = color.Red;
                m_pixels[offset + 1] = color.Green;
                m_pixels[offset + 2] = color.Blue;
                m_pixels[offset + 3] = byte.MaxValue;
            }
        }
    }
}
