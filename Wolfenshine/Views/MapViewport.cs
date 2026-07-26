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
using Wolfenshine.Maps;
using Wolfenshine.Rendering;

namespace Wolfenshine.Views;

/// <summary>
/// Displays a cached textured map with a live player-position overlay.
/// </summary>
/// <remarks>
/// Camera changes redraw only the inexpensive marker; map pixels are regenerated only when their sources change.
/// </remarks>
public sealed class MapViewport : Control
{
    private WriteableBitmap m_bitmap;
    private WolfensteinMap m_renderedMap;
    private WolfensteinWallTextures m_renderedWallTextures;
    private WolfensteinPalette m_renderedPalette;

    public static readonly StyledProperty<WolfensteinMap> MapProperty =
        AvaloniaProperty.Register<MapViewport, WolfensteinMap>(nameof(Map));
    public static readonly StyledProperty<RaycastCamera> CameraProperty =
        AvaloniaProperty.Register<MapViewport, RaycastCamera>(nameof(Camera));
    public static readonly StyledProperty<WolfensteinWallTextures> WallTexturesProperty =
        AvaloniaProperty.Register<MapViewport, WolfensteinWallTextures>(nameof(WallTextures));
    public static readonly StyledProperty<WolfensteinPalette> PaletteProperty =
        AvaloniaProperty.Register<MapViewport, WolfensteinPalette>(nameof(Palette));
    public static readonly StyledProperty<IReadOnlyList<WorldSprite>> StaticObjectsProperty =
        AvaloniaProperty.Register<MapViewport, IReadOnlyList<WorldSprite>>(nameof(StaticObjects));

    static MapViewport()
    {
        AffectsRender<MapViewport>(
            MapProperty, CameraProperty, WallTexturesProperty, PaletteProperty, StaticObjectsProperty);
        AffectsMeasure<MapViewport>(MapProperty);
    }

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

    public WolfensteinWallTextures WallTextures
    {
        get => GetValue(WallTexturesProperty);
        set => SetValue(WallTexturesProperty, value);
    }

    public WolfensteinPalette Palette
    {
        get => GetValue(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    public IReadOnlyList<WorldSprite> StaticObjects
    {
        get => GetValue(StaticObjectsProperty);
        set => SetValue(StaticObjectsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => Map == null
        ? default
        : new Size(Map.Width * MapOverviewRenderer.TileSize, Map.Height * MapOverviewRenderer.TileSize);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Map == null || WallTextures == null || Palette == null)
            return;
        EnsureMapBitmap();
        context.DrawImage(m_bitmap, new Rect(0, 0, m_bitmap.PixelSize.Width, m_bitmap.PixelSize.Height));
        DrawKeyMarkers(context);
        if (Camera == null)
            return;

        var center = new Point(
            Camera.X * MapOverviewRenderer.TileSize,
            Camera.Y * MapOverviewRenderer.TileSize);
        context.DrawEllipse(Brushes.Black, null, center, 3.5, 3.5);
        context.DrawEllipse(Brushes.White, null, center, 2.5, 2.5);
    }

    private void DrawKeyMarkers(DrawingContext context)
    {
        if (StaticObjects == null)
            return;
        foreach (var item in StaticObjects)
        {
            var pickupType = WolfensteinStaticObjects.GetPickupType(item.SpriteNumber);
            var brush = pickupType switch
            {
                WolfensteinPickupType.GoldKey => Brushes.Gold,
                WolfensteinPickupType.SilverKey => Brushes.LightCyan,
                _ => null
            };
            if (brush == null)
                continue;

            var center = new Point(item.X * MapOverviewRenderer.TileSize, item.Y * MapOverviewRenderer.TileSize);
            context.DrawEllipse(Brushes.Black, null, center, 3.5, 3.5);
            context.DrawEllipse(brush, null, center, 2.5, 2.5);
        }
    }

    private void EnsureMapBitmap()
    {
        if (ReferenceEquals(m_renderedMap, Map) &&
            ReferenceEquals(m_renderedWallTextures, WallTextures) &&
            ReferenceEquals(m_renderedPalette, Palette))
        {
            return;
        }

        var width = Map.Width * MapOverviewRenderer.TileSize;
        var height = Map.Height * MapOverviewRenderer.TileSize;
        var pixels = new byte[width * height * 4];
        MapOverviewRenderer.Render(Map, WallTextures, Palette, pixels);
        m_bitmap?.Dispose();
        m_bitmap = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Opaque);
        using var frameBuffer = m_bitmap.Lock();
        var sourceRowBytes = width * 4;
        for (var y = 0; y < height; y++)
        {
            Marshal.Copy(
                pixels,
                y * sourceRowBytes,
                IntPtr.Add(frameBuffer.Address, y * frameBuffer.RowBytes),
                sourceRowBytes);
        }

        m_renderedMap = Map;
        m_renderedWallTextures = WallTextures;
        m_renderedPalette = Palette;
    }
}
