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
/// Draws the original pause plaque at its native Wolfenstein 3D position.
/// </summary>
public sealed class PauseViewport : Control
{
    private const int NativeWidth = 320;
    private const int NativeHeight = 200;
    private const int NativeLeft = 128;
    private const int NativeTop = 64;
    private WriteableBitmap m_bitmap;
    private WolfensteinGraphic m_renderedGraphic;
    private WolfensteinPalette m_renderedPalette;

    public static readonly StyledProperty<WolfensteinGraphic> GraphicProperty =
        AvaloniaProperty.Register<PauseViewport, WolfensteinGraphic>(nameof(Graphic));
    public static readonly StyledProperty<WolfensteinPalette> PaletteProperty =
        AvaloniaProperty.Register<PauseViewport, WolfensteinPalette>(nameof(Palette));

    static PauseViewport() => AffectsRender<PauseViewport>(GraphicProperty, PaletteProperty);

    public WolfensteinGraphic Graphic { get => GetValue(GraphicProperty); set => SetValue(GraphicProperty, value); }
    public WolfensteinPalette Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Graphic == null || Palette == null)
            return;
        EnsureBitmap();
        var scaleX = Bounds.Width / NativeWidth;
        var scaleY = Bounds.Height / NativeHeight;
        context.DrawImage(m_bitmap, new Rect(
            NativeLeft * scaleX,
            NativeTop * scaleY,
            Graphic.Width * scaleX,
            Graphic.Height * scaleY));
    }

    private void EnsureBitmap()
    {
        if (ReferenceEquals(m_renderedGraphic, Graphic) && ReferenceEquals(m_renderedPalette, Palette))
            return;
        var pixels = new byte[Graphic.Width * Graphic.Height * 4];
        for (var y = 0; y < Graphic.Height; y++)
        {
            for (var x = 0; x < Graphic.Width; x++)
            {
                var color = Palette.GetColor(Graphic.GetIndex(x, y));
                var offset = ((y * Graphic.Width) + x) * 4;
                pixels[offset] = color.Red;
                pixels[offset + 1] = color.Green;
                pixels[offset + 2] = color.Blue;
                pixels[offset + 3] = byte.MaxValue;
            }
        }
        m_bitmap?.Dispose();
        m_bitmap = new WriteableBitmap(
            new PixelSize(Graphic.Width, Graphic.Height),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Opaque);
        using var frameBuffer = m_bitmap.Lock();
        var rowBytes = Graphic.Width * 4;
        for (var y = 0; y < Graphic.Height; y++)
            Marshal.Copy(pixels, y * rowBytes, IntPtr.Add(frameBuffer.Address, y * frameBuffer.RowBytes), rowBytes);
        m_renderedGraphic = Graphic;
        m_renderedPalette = Palette;
    }
}
