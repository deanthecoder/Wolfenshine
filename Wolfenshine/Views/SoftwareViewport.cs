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
using Wolfenshine.Maps;
using Wolfenshine.Rendering;

namespace Wolfenshine.Views;

/// <summary>
/// Presents the native-resolution software-raycast framebuffer.
/// </summary>
/// <remarks>
/// Avalonia only scales and displays the completed 320 x 200 image; scene rendering happens entirely in software.
/// </remarks>
public sealed class SoftwareViewport : Control
{
    private const int ViewportWidth = 320;
    private const int ViewportHeight = 200;
    private WriteableBitmap m_bitmap;
    private readonly byte[] m_pixels = new byte[ViewportWidth * ViewportHeight * 4];
    private WolfensteinMap m_renderedMap;
    private RaycastCamera m_renderedCamera;

    public static readonly StyledProperty<WolfensteinMap> MapProperty =
        AvaloniaProperty.Register<SoftwareViewport, WolfensteinMap>(nameof(Map));
    public static readonly StyledProperty<RaycastCamera> CameraProperty =
        AvaloniaProperty.Register<SoftwareViewport, RaycastCamera>(nameof(Camera));

    static SoftwareViewport() => AffectsRender<SoftwareViewport>(MapProperty, CameraProperty);

    public WolfensteinMap Map
    {
        get => GetValue(MapProperty);
        set => SetValue(MapProperty, value);
    }

    public RaycastCamera Camera
    {
        get => GetValue(CameraProperty);
        set => SetValue(CameraProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Map == null || Camera == null)
            return;
        if (!ReferenceEquals(m_renderedMap, Map) || !ReferenceEquals(m_renderedCamera, Camera))
            RenderFrame();
        context.DrawImage(m_bitmap, Bounds);
    }

    private void RenderFrame()
    {
        // Raycasting and shading produce the complete native-resolution image independently of Avalonia.
        var columns = Raycaster.Cast(Map, Camera, ViewportWidth);
        SoftwareRaycastRenderer.Render(columns, ViewportHeight, m_pixels);

        // The viewport size never changes, so retain its native bitmap and update only the locked pixel memory.
        m_bitmap ??= new WriteableBitmap(
                new PixelSize(ViewportWidth, ViewportHeight),
                new Vector(96, 96),
                PixelFormat.Rgba8888,
                AlphaFormat.Opaque);
        using var frameBuffer = m_bitmap.Lock();
        var sourceRowBytes = ViewportWidth * 4;
        // Copy rows separately because Avalonia is free to pad its framebuffer stride.
        for (var y = 0; y < ViewportHeight; y++)
        {
            Marshal.Copy(
                m_pixels,
                y * sourceRowBytes,
                IntPtr.Add(frameBuffer.Address, y * frameBuffer.RowBytes),
                sourceRowBytes);
        }

        m_renderedMap = Map;
        m_renderedCamera = Camera;
    }
}
