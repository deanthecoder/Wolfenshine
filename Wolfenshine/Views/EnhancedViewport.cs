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
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using Wolfenshine.Graphics;
using Wolfenshine.Rendering;

namespace Wolfenshine.Views;

/// <summary>
/// Renders raycast walls in SKSL and composites software-rendered sprites and UI over them.
/// </summary>
/// <remarks>
/// The CPU supplies compact wall-column data rather than a completed scene, leaving lighting and projection to the shader.
/// </remarks>
public sealed class EnhancedViewport : SoftwareViewport
{
    private const byte CeilingPaletteIndex = 0x1D;
    private const byte FloorPaletteIndex = 0x19;
    private const int ColumnChannelCount = 4;
    private readonly float[] m_wallColumns = new float[ViewportWidth * ColumnChannelCount];
    private readonly SKBitmap m_overlayBitmap = new(new SKImageInfo(
        ViewportWidth,
        ViewportHeight,
        SKColorType.Rgba8888,
        SKAlphaType.Unpremul));
    private readonly SKRuntimeEffect m_effect;
    private SKBitmap m_wallAtlas;
    private WolfensteinWallTextures m_atlasWallTextures;
    private WolfensteinPalette m_atlasPalette;

    public static readonly StyledProperty<double> ViewBobProperty =
        AvaloniaProperty.Register<EnhancedViewport, double>(nameof(ViewBob));

    static EnhancedViewport() => AffectsRender<EnhancedViewport>(ViewBobProperty);

    public EnhancedViewport()
    {
        using var stream = AssetLoader.Open(new Uri("avares://Wolfenshine/Assets/enhanced.sksl"));
        using var reader = new StreamReader(stream);
        m_effect = SKRuntimeEffect.Create(reader.ReadToEnd(), out var error);
        if (m_effect == null)
            throw new InvalidDataException($"Enhanced shader compilation failed: {error}");
    }

    public double ViewBob
    {
        get => GetValue(ViewBobProperty);
        set => SetValue(ViewBobProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Map == null || Doors == null || Camera == null || WallTextures == null || Palette == null)
            return;

        EnsureWallAtlas();
        BuildColumnBuffer();
        BuildSpriteOverlay();
        context.Custom(new ShaderDrawOperation(
            new Rect(Bounds.Size),
            m_effect,
            m_wallAtlas,
            m_overlayBitmap,
            m_wallColumns,
            (float)ViewBob,
            (float)DamageFlash,
            (float)DeathFade,
            (float)LevelFade,
            ToFloats(Palette.GetColor(CeilingPaletteIndex)),
            ToFloats(Palette.GetColor(FloorPaletteIndex)),
            ToFloats(Palette.GetColor(4))));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        m_overlayBitmap.Dispose();
        m_wallAtlas?.Dispose();
        m_effect.Dispose();
    }

    private void EnsureWallAtlas()
    {
        if (ReferenceEquals(m_atlasWallTextures, WallTextures) && ReferenceEquals(m_atlasPalette, Palette))
            return;
        m_wallAtlas?.Dispose();
        m_wallAtlas = new SKBitmap(new SKImageInfo(
            WolfensteinWallTexture.Size,
            WallTextures.Pages.Count * WolfensteinWallTexture.Size,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque));
        for (var page = 0; page < WallTextures.Pages.Count; page++)
        {
            var texture = WallTextures.Pages[page];
            for (var y = 0; y < WolfensteinWallTexture.Size; y++)
            {
                for (var x = 0; x < WolfensteinWallTexture.Size; x++)
                {
                    var color = Palette.GetColor(texture.GetIndex(x, y));
                    m_wallAtlas.SetPixel(
                        x,
                        (page * WolfensteinWallTexture.Size) + y,
                        new SKColor(color.Red, color.Green, color.Blue, color.Alpha));
                }
            }
        }
        m_atlasWallTextures = WallTextures;
        m_atlasPalette = Palette;
    }

    private void BuildColumnBuffer()
    {
        Raycaster.Cast(Map, Doors, PushWalls, ElevatorSwitch, Camera, m_columns);
        for (var column = 0; column < m_columns.Length; column++)
        {
            var target = column * ColumnChannelCount;
            m_wallColumns[target] = (float)m_columns[column].Distance;
            m_wallColumns[target + 1] = (float)m_columns[column].TextureU;
            m_wallColumns[target + 2] = WallTextures.GetPageIndex(m_columns[column]);
            m_wallColumns[target + 3] = m_columns[column].Side == WallSide.Horizontal ? 1.0f : 0.0f;
        }
    }

    private void BuildSpriteOverlay()
    {
        Array.Clear(m_pixels);
        var playViewPixels = m_pixels.AsSpan(0, ViewportWidth * PlayViewHeight * 4);
        if (Sprites != null && StaticObjects != null)
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
        if (WeaponSprite != null)
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
        if (StatusBar != null)
        {
            SoftwareRaycastRenderer.DrawGraphic(
                StatusBar,
                Palette,
                0,
                PlayViewHeight,
                m_pixels,
                ViewportWidth,
                ViewportHeight);
        }
        var sourceRowBytes = ViewportWidth * 4;
        for (var y = 0; y < ViewportHeight; y++)
        {
            Marshal.Copy(
                m_pixels,
                y * sourceRowBytes,
                IntPtr.Add(m_overlayBitmap.GetPixels(), y * m_overlayBitmap.RowBytes),
                sourceRowBytes);
        }
    }

    private static float[] ToFloats(RgbaColor color) =>
        [color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f, color.Alpha / 255.0f];

    private sealed class ShaderDrawOperation(
        Rect bounds,
        SKRuntimeEffect effect,
        SKBitmap wallTextures,
        SKBitmap spriteOverlay,
        float[] wallColumns,
        float viewBob,
        float damageFlash,
        float deathFade,
        float levelFade,
        float[] ceilingColor,
        float[] floorColor,
        float[] deathColor) : ICustomDrawOperation
    {
        public Rect Bounds { get; } = bounds;

        public void Dispose()
        {
        }

        public bool HitTest(Point point) => false;

        public bool Equals(ICustomDrawOperation other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null)
                return;
            using var lease = leaseFeature.Lease();
            using var textureShader = wallTextures.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
            using var overlayShader = spriteOverlay.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
            var uniforms = new SKRuntimeEffectUniforms(effect)
            {
                ["outputResolution"] = new[] { (float)Bounds.Width, (float)Bounds.Height },
                ["wallColumns"] = wallColumns,
                ["viewBob"] = viewBob,
                ["damageFlash"] = damageFlash,
                ["deathFade"] = deathFade,
                ["levelFade"] = levelFade,
                ["ceilingColor"] = ceilingColor,
                ["floorColor"] = floorColor,
                ["deathColor"] = deathColor
            };
            var children = new SKRuntimeEffectChildren(effect)
            {
                ["wallTextureAtlas"] = textureShader,
                ["softwareSpriteOverlay"] = overlayShader
            };
            using var shader = effect.ToShader(false, uniforms, children);
            using var paint = new SKPaint { Shader = shader, IsAntialias = false };
            lease.SkCanvas.DrawRect(SKRect.Create((float)Bounds.Width, (float)Bounds.Height), paint);
        }
    }
}
