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
using Wolfenshine.Maps;
using Wolfenshine.Game;
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
    private const int StatusBarHeight = 40;
    private const int PlayViewHeight = ViewportHeight - StatusBarHeight;
    private WriteableBitmap m_bitmap;
    private readonly WallColumn[] m_columns = new WallColumn[ViewportWidth];
    private readonly byte[] m_pixels = new byte[ViewportWidth * ViewportHeight * 4];
    private ProjectedWorldSprite[] m_projectedSprites = [];
    private WolfensteinMap m_renderedMap;
    private RaycastCamera m_renderedCamera;

    public static readonly StyledProperty<WolfensteinMap> MapProperty =
        AvaloniaProperty.Register<SoftwareViewport, WolfensteinMap>(nameof(Map));
    public static readonly StyledProperty<RaycastCamera> CameraProperty =
        AvaloniaProperty.Register<SoftwareViewport, RaycastCamera>(nameof(Camera));
    public static readonly StyledProperty<WolfensteinDoors> DoorsProperty =
        AvaloniaProperty.Register<SoftwareViewport, WolfensteinDoors>(nameof(Doors));
    public static readonly StyledProperty<WolfensteinPushWalls> PushWallsProperty =
        AvaloniaProperty.Register<SoftwareViewport, WolfensteinPushWalls>(nameof(PushWalls));
    public static readonly StyledProperty<WolfensteinWallTextures> WallTexturesProperty =
        AvaloniaProperty.Register<SoftwareViewport, WolfensteinWallTextures>(nameof(WallTextures));
    public static readonly StyledProperty<WolfensteinPalette> PaletteProperty =
        AvaloniaProperty.Register<SoftwareViewport, WolfensteinPalette>(nameof(Palette));
    public static readonly StyledProperty<WolfensteinSprite> WeaponSpriteProperty =
        AvaloniaProperty.Register<SoftwareViewport, WolfensteinSprite>(nameof(WeaponSprite));
    public static readonly StyledProperty<WolfensteinSpriteSet> SpritesProperty =
        AvaloniaProperty.Register<SoftwareViewport, WolfensteinSpriteSet>(nameof(Sprites));
    public static readonly StyledProperty<IReadOnlyList<WorldSprite>> StaticObjectsProperty =
        AvaloniaProperty.Register<SoftwareViewport, IReadOnlyList<WorldSprite>>(nameof(StaticObjects));
    public static readonly StyledProperty<WolfensteinGraphic> StatusBarProperty =
        AvaloniaProperty.Register<SoftwareViewport, WolfensteinGraphic>(nameof(StatusBar));

    static SoftwareViewport() => AffectsRender<SoftwareViewport>(
        MapProperty,
        CameraProperty,
        DoorsProperty,
        PushWallsProperty,
        WallTexturesProperty,
        PaletteProperty,
        WeaponSpriteProperty,
        SpritesProperty,
        StaticObjectsProperty,
        StatusBarProperty);

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

    public WolfensteinDoors Doors
    {
        get => GetValue(DoorsProperty);
        set => SetValue(DoorsProperty, value);
    }

    public WolfensteinPushWalls PushWalls
    {
        get => GetValue(PushWallsProperty);
        set => SetValue(PushWallsProperty, value);
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

    public WolfensteinSprite WeaponSprite
    {
        get => GetValue(WeaponSpriteProperty);
        set => SetValue(WeaponSpriteProperty, value);
    }

    public WolfensteinSpriteSet Sprites
    {
        get => GetValue(SpritesProperty);
        set => SetValue(SpritesProperty, value);
    }

    public IReadOnlyList<WorldSprite> StaticObjects
    {
        get => GetValue(StaticObjectsProperty);
        set => SetValue(StaticObjectsProperty, value);
    }

    public WolfensteinGraphic StatusBar
    {
        get => GetValue(StatusBarProperty);
        set => SetValue(StatusBarProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Map == null || Doors == null || Camera == null)
            return;
        if (!ReferenceEquals(m_renderedMap, Map) || !ReferenceEquals(m_renderedCamera, Camera))
            RenderFrame();
        context.DrawImage(m_bitmap, Bounds);
    }

    private void RenderFrame()
    {
        // Raycasting and shading produce the complete native-resolution image independently of Avalonia.
        Raycaster.Cast(Map, Doors, PushWalls, Camera, m_columns);
        var playViewPixels = m_pixels.AsSpan(0, ViewportWidth * PlayViewHeight * 4);
        // The HUD clips the play view to 160 rows, but the original 200-row projection scale preserves its proportions.
        SoftwareRaycastRenderer.Render(
            m_columns,
            PlayViewHeight,
            ViewportHeight,
            playViewPixels,
            WallTextures,
            Palette);
        if (Sprites != null && StaticObjects != null && Palette != null)
        {
            if (m_projectedSprites.Length != StaticObjects.Count)
                m_projectedSprites = new ProjectedWorldSprite[StaticObjects.Count];
            var visibleSpriteCount = WorldSpriteProjector.Project(
                StaticObjects,
                Camera,
                ViewportWidth,
                PlayViewHeight,
                ViewportHeight,
                m_projectedSprites);
            SoftwareRaycastRenderer.DrawWorldSprites(
                m_projectedSprites.AsSpan(0, visibleSpriteCount),
                Sprites,
                Palette,
                m_columns,
                playViewPixels,
                ViewportWidth,
                PlayViewHeight);
        }
        if (WeaponSprite != null && Palette != null)
        {
            SoftwareRaycastRenderer.DrawSprite(
                WeaponSprite,
                Palette,
                ViewportWidth / 2,
                PlayViewHeight,
                PlayViewHeight + 1,
                playViewPixels,
                ViewportWidth,
                PlayViewHeight);
        }
        if (StatusBar != null && Palette != null)
            SoftwareRaycastRenderer.DrawGraphic(StatusBar, Palette, 0, PlayViewHeight, m_pixels, ViewportWidth, ViewportHeight);

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
