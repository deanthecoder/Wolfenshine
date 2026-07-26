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
public class SoftwareViewport : Control
{
    protected const int ViewportWidth = 320;
    protected const int ViewportHeight = 200;
    protected const int StatusBarHeight = 40;
    protected const int PlayViewHeight = ViewportHeight - StatusBarHeight;
    private WriteableBitmap m_bitmap;
    protected readonly WallColumn[] m_columns = new WallColumn[ViewportWidth];
    protected readonly byte[] m_pixels = new byte[ViewportWidth * ViewportHeight * 4];
    protected ProjectedWorldSprite[] m_projectedSprites = [];
    private WolfensteinMap m_renderedMap;
    private RaycastCamera m_renderedCamera;
    private WolfensteinElevatorSwitch m_renderedElevatorSwitch;
    private double m_renderedDeathFade = -1.0;
    private double m_renderedDamageFlash = -1.0;
    private double m_renderedLevelFade = -1.0;

    public static readonly StyledProperty<WolfensteinMap> MapProperty =
        AvaloniaProperty.Register<SoftwareViewport, WolfensteinMap>(nameof(Map));
    public static readonly StyledProperty<RaycastCamera> CameraProperty =
        AvaloniaProperty.Register<SoftwareViewport, RaycastCamera>(nameof(Camera));
    public static readonly StyledProperty<WolfensteinDoors> DoorsProperty =
        AvaloniaProperty.Register<SoftwareViewport, WolfensteinDoors>(nameof(Doors));
    public static readonly StyledProperty<WolfensteinPushWalls> PushWallsProperty =
        AvaloniaProperty.Register<SoftwareViewport, WolfensteinPushWalls>(nameof(PushWalls));
    public static readonly StyledProperty<WolfensteinElevatorSwitch> ElevatorSwitchProperty =
        AvaloniaProperty.Register<SoftwareViewport, WolfensteinElevatorSwitch>(nameof(ElevatorSwitch));
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
    public static readonly StyledProperty<double> DeathFadeProperty =
        AvaloniaProperty.Register<SoftwareViewport, double>(nameof(DeathFade));
    public static readonly StyledProperty<double> DamageFlashProperty =
        AvaloniaProperty.Register<SoftwareViewport, double>(nameof(DamageFlash));
    public static readonly StyledProperty<double> LevelFadeProperty =
        AvaloniaProperty.Register<SoftwareViewport, double>(nameof(LevelFade));

    static SoftwareViewport() => AffectsRender<SoftwareViewport>(
        MapProperty,
        CameraProperty,
        DoorsProperty,
        PushWallsProperty,
        ElevatorSwitchProperty,
        WallTexturesProperty,
        PaletteProperty,
        WeaponSpriteProperty,
        SpritesProperty,
        StaticObjectsProperty,
        StatusBarProperty,
        DamageFlashProperty,
        DeathFadeProperty,
        LevelFadeProperty);

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

    public WolfensteinElevatorSwitch ElevatorSwitch
    {
        get => GetValue(ElevatorSwitchProperty);
        set => SetValue(ElevatorSwitchProperty, value);
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

    public double DeathFade
    {
        get => GetValue(DeathFadeProperty);
        set => SetValue(DeathFadeProperty, value);
    }

    public double DamageFlash
    {
        get => GetValue(DamageFlashProperty);
        set => SetValue(DamageFlashProperty, value);
    }

    public double LevelFade
    {
        get => GetValue(LevelFadeProperty);
        set => SetValue(LevelFadeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Map == null || Doors == null || Camera == null)
            return;
        if (!ReferenceEquals(m_renderedMap, Map) || !ReferenceEquals(m_renderedCamera, Camera) ||
            !ReferenceEquals(m_renderedElevatorSwitch, ElevatorSwitch) ||
            m_renderedDamageFlash != DamageFlash || m_renderedDeathFade != DeathFade ||
            m_renderedLevelFade != LevelFade)
            RenderFrame();
        context.DrawImage(m_bitmap, Bounds);
    }

    protected void RenderFrame()
    {
        // Raycasting and shading produce the complete native-resolution image independently of Avalonia.
        Raycaster.Cast(Map, Doors, PushWalls, ElevatorSwitch, Camera, m_columns);
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
        if (DeathFade > 0.0 && Palette != null)
            ApplyDeathFade(playViewPixels, DeathFade, Palette.GetColor(4));
        if (StatusBar != null && Palette != null)
            SoftwareRaycastRenderer.DrawGraphic(StatusBar, Palette, 0, PlayViewHeight, m_pixels, ViewportWidth, ViewportHeight);
        if (DamageFlash > 0.0)
            ApplyRedFlash(m_pixels, DamageFlash);
        if (LevelFade > 0.0)
            ApplyBlackFade(m_pixels, LevelFade);

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
        m_renderedElevatorSwitch = ElevatorSwitch;
        m_renderedDamageFlash = DamageFlash;
        m_renderedDeathFade = DeathFade;
        m_renderedLevelFade = LevelFade;
    }

    private static void ApplyDeathFade(Span<byte> pixels, double progress, RgbaColor color)
    {
        var threshold = (uint)(Math.Clamp(progress, 0.0, 1.0) * uint.MaxValue);
        for (var pixel = 0; pixel < pixels.Length / 4; pixel++)
        {
            var hash = unchecked((uint)(pixel + 1) * 2654435761u);
            if (hash > threshold)
                continue;
            var offset = pixel * 4;
            pixels[offset] = color.Red;
            pixels[offset + 1] = color.Green;
            pixels[offset + 2] = color.Blue;
            pixels[offset + 3] = color.Alpha;
        }
    }

    private static void ApplyBlackFade(Span<byte> pixels, double progress)
    {
        var scale = 1.0 - Math.Clamp(progress, 0.0, 1.0);
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = (byte)(pixels[offset] * scale);
            pixels[offset + 1] = (byte)(pixels[offset + 1] * scale);
            pixels[offset + 2] = (byte)(pixels[offset + 2] * scale);
        }
    }

    private static void ApplyRedFlash(Span<byte> pixels, double intensity)
    {
        var amount = Math.Clamp(intensity, 0.0, 1.0);
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = (byte)(pixels[offset] + ((byte.MaxValue - pixels[offset]) * amount));
            pixels[offset + 1] = (byte)(pixels[offset + 1] * (1.0 - amount));
            pixels[offset + 2] = (byte)(pixels[offset + 2] * (1.0 - amount));
        }
    }
}
