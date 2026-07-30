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
/// Recreates the original Get Psyched level-loading screen and progress bar.
/// </summary>
public sealed class GetPsychedViewport : Control
{
    private const int ViewportWidth = 320;
    private const int ViewportHeight = 200;
    private const int PlayAreaHeight = 160;
    private const int GraphicLeft = 48;
    private const int GraphicTop = 56;
    private const int ProgressLeft = 53;
    private const int ProgressTop = 101;
    private const int ProgressWidth = 214;
    private const byte BackgroundColor = 127;
    private const byte ProgressBackgroundColor = 0;
    private const byte ProgressColor = 0x37;
    private const byte ProgressHighlightColor = 0x32;
    private readonly byte[] m_pixels = new byte[ViewportWidth * ViewportHeight * 4];
    private WriteableBitmap m_bitmap;

    public static readonly StyledProperty<WolfensteinGraphic> GraphicProperty =
        AvaloniaProperty.Register<GetPsychedViewport, WolfensteinGraphic>(nameof(Graphic));
    public static readonly StyledProperty<WolfensteinGraphic> StatusBarProperty =
        AvaloniaProperty.Register<GetPsychedViewport, WolfensteinGraphic>(nameof(StatusBar));
    public static readonly StyledProperty<WolfensteinPalette> PaletteProperty =
        AvaloniaProperty.Register<GetPsychedViewport, WolfensteinPalette>(nameof(Palette));
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<GetPsychedViewport, double>(nameof(Progress));

    static GetPsychedViewport() => AffectsRender<GetPsychedViewport>(
        GraphicProperty,
        StatusBarProperty,
        PaletteProperty,
        ProgressProperty);

    public WolfensteinGraphic Graphic { get => GetValue(GraphicProperty); set => SetValue(GraphicProperty, value); }
    public WolfensteinGraphic StatusBar { get => GetValue(StatusBarProperty); set => SetValue(StatusBarProperty, value); }
    public WolfensteinPalette Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }
    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Graphic == null || Palette == null)
            return;
        RenderFrame();
        context.DrawImage(m_bitmap, Bounds);
    }

    /// <summary>
    /// Composes the original artwork, current HUD, and two-tone progress indicator into the reusable bitmap.
    /// </summary>
    private void RenderFrame()
    {
        FillRectangle(0, 0, ViewportWidth, PlayAreaHeight, BackgroundColor);
        FillRectangle(0, PlayAreaHeight, ViewportWidth, ViewportHeight - PlayAreaHeight, ProgressBackgroundColor);
        if (StatusBar != null)
            DrawGraphic(StatusBar, 0, PlayAreaHeight);
        DrawGraphic(Graphic, GraphicLeft, GraphicTop);
        FillRectangle(ProgressLeft, ProgressTop, ProgressWidth, 2, ProgressBackgroundColor);
        var progressWidth = (int)Math.Round(ProgressWidth * Math.Clamp(Progress, 0.0, 1.0));
        if (progressWidth > 0)
        {
            FillRectangle(ProgressLeft, ProgressTop, progressWidth, 2, ProgressColor);
            FillRectangle(ProgressLeft, ProgressTop, Math.Max(0, progressWidth - 1), 1, ProgressHighlightColor);
        }

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
