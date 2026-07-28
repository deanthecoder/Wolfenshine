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
using Wolfenshine.Game;
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
    private const int LightCount = 32;
    private const int LightChannelCount = 4;
    private const int LightRadiusChannelCount = 2;
    private readonly float[] m_wallColumns = new float[ViewportWidth * ColumnChannelCount];
    private readonly float[] m_sceneLights = new float[LightCount * LightChannelCount];
    private readonly float[] m_sceneLightRadii = new float[LightCount * LightRadiusChannelCount];
    private readonly double[] m_sceneLightDistances = new double[LightCount];
    private readonly float[] m_playerPosition = new float[2];
    private readonly float[] m_cameraDirection = new float[2];
    private readonly float[] m_cameraPlane = new float[2];
    private readonly byte[] m_weaponPixels = new byte[ViewportWidth * ViewportHeight * 4];
    private WorldSprite[] m_litWorldSprites = [];
    private readonly SKBitmap m_overlayBitmap = new(new SKImageInfo(
        ViewportWidth,
        ViewportHeight,
        SKColorType.Rgba8888,
        SKAlphaType.Unpremul));
    private readonly SKBitmap m_weaponOverlayBitmap = new(new SKImageInfo(
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
    public static readonly StyledProperty<double> MuzzleFlashProperty =
        AvaloniaProperty.Register<EnhancedViewport, double>(nameof(MuzzleFlash));
    public static readonly StyledProperty<IReadOnlyList<WorldSprite>> LightObjectsProperty =
        AvaloniaProperty.Register<EnhancedViewport, IReadOnlyList<WorldSprite>>(nameof(LightObjects));
    public static readonly StyledProperty<IReadOnlyList<WorldLight>> DynamicLightsProperty =
        AvaloniaProperty.Register<EnhancedViewport, IReadOnlyList<WorldLight>>(nameof(DynamicLights));

    static EnhancedViewport() => AffectsRender<EnhancedViewport>(
        ViewBobProperty,
        MuzzleFlashProperty,
        LightObjectsProperty,
        DynamicLightsProperty);

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

    public double MuzzleFlash
    {
        get => GetValue(MuzzleFlashProperty);
        set => SetValue(MuzzleFlashProperty, value);
    }

    public IReadOnlyList<WorldSprite> LightObjects
    {
        get => GetValue(LightObjectsProperty);
        set => SetValue(LightObjectsProperty, value);
    }

    public IReadOnlyList<WorldLight> DynamicLights
    {
        get => GetValue(DynamicLightsProperty);
        set => SetValue(DynamicLightsProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Map == null || Doors == null || Camera == null || WallTextures == null || Palette == null)
            return;

        EnsureWallAtlas();
        BuildColumnBuffer();
        BuildLightBuffer();
        BuildSoftwareOverlays();
        m_playerPosition[0] = (float)Camera.X;
        m_playerPosition[1] = (float)Camera.Y;
        m_cameraDirection[0] = (float)Camera.DirectionX;
        m_cameraDirection[1] = (float)Camera.DirectionY;
        m_cameraPlane[0] = (float)Camera.PlaneX;
        m_cameraPlane[1] = (float)Camera.PlaneY;
        context.Custom(new ShaderDrawOperation(
            new Rect(Bounds.Size),
            m_effect,
            m_wallAtlas,
            m_overlayBitmap,
            m_weaponOverlayBitmap,
            m_wallColumns,
            m_sceneLights,
            m_sceneLightRadii,
            m_playerPosition,
            m_cameraDirection,
            m_cameraPlane,
            (float)MuzzleFlash,
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
        m_weaponOverlayBitmap.Dispose();
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
            var flags = m_columns[column].Side == WallSide.Horizontal ? 1 : 0;
            if (m_columns[column].HasConcaveTextureStart)
                flags |= 2;
            if (m_columns[column].HasConcaveTextureEnd)
                flags |= 4;
            m_wallColumns[target + 3] = flags;
        }
    }

    private void BuildLightBuffer()
    {
        Array.Clear(m_sceneLights);
        Array.Clear(m_sceneLightRadii);
        Array.Fill(m_sceneLightDistances, double.PositiveInfinity);
        if (LightObjects != null)
        {
            foreach (var sprite in LightObjects)
            {
                var (upward, downward) = WolfensteinStaticObjects.GetLightBrightness(sprite.SpriteNumber);
                var (upwardRadius, downwardRadius) = WolfensteinStaticObjects.GetLightRadii(sprite.SpriteNumber);
                if (upward <= 0.0f && downward <= 0.0f)
                    continue;
                InsertLight(sprite.X, sprite.Y, upward, downward, upwardRadius, downwardRadius);
            }
        }
        if (DynamicLights == null)
            return;
        foreach (var light in DynamicLights)
        {
            InsertLight(
                light.X,
                light.Y,
                light.UpwardBrightness,
                light.DownwardBrightness,
                light.UpwardRadius,
                light.DownwardRadius);
        }
    }

    private void InsertLight(
        double x,
        double y,
        float upward,
        float downward,
        float upwardRadius,
        float downwardRadius)
    {
        var deltaX = x - Camera.X;
        var deltaY = y - Camera.Y;
        var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
        var insertAt = -1;
        for (var index = 0; index < LightCount; index++)
        {
            if (distanceSquared >= m_sceneLightDistances[index])
                continue;
            insertAt = index;
            break;
        }
        if (insertAt < 0)
            return;

        for (var index = LightCount - 1; index > insertAt; index--)
        {
            m_sceneLightDistances[index] = m_sceneLightDistances[index - 1];
            Array.Copy(
                m_sceneLights,
                (index - 1) * LightChannelCount,
                m_sceneLights,
                index * LightChannelCount,
                LightChannelCount);
            Array.Copy(
                m_sceneLightRadii,
                (index - 1) * LightRadiusChannelCount,
                m_sceneLightRadii,
                index * LightRadiusChannelCount,
                LightRadiusChannelCount);
        }

        m_sceneLightDistances[insertAt] = distanceSquared;
        var target = insertAt * LightChannelCount;
        m_sceneLights[target] = (float)x;
        m_sceneLights[target + 1] = (float)y;
        m_sceneLights[target + 2] = upward;
        m_sceneLights[target + 3] = downward;
        var radiusTarget = insertAt * LightRadiusChannelCount;
        m_sceneLightRadii[radiusTarget] = upwardRadius;
        m_sceneLightRadii[radiusTarget + 1] = downwardRadius;
    }

    private void BuildSoftwareOverlays()
    {
        Array.Clear(m_pixels);
        Array.Clear(m_weaponPixels);
        var playViewPixels = m_pixels.AsSpan(0, ViewportWidth * PlayViewHeight * 4);
        if (Sprites != null && StaticObjects != null)
        {
            if (m_litWorldSprites.Length != StaticObjects.Count)
                m_litWorldSprites = new WorldSprite[StaticObjects.Count];
            for (var index = 0; index < StaticObjects.Count; index++)
            {
                var sprite = StaticObjects[index];
                m_litWorldSprites[index] = sprite with
                {
                    Brightness = sprite.IsActor ? CalculateActorBrightness(sprite.X, sprite.Y) : 1.0f
                };
            }
            if (m_projectedSprites.Length != StaticObjects.Count)
                m_projectedSprites = new ProjectedWorldSprite[StaticObjects.Count];
            var visibleSpriteCount = WorldSpriteProjector.Project(
                m_litWorldSprites,
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
                m_weaponPixels.AsSpan(0, ViewportWidth * PlayViewHeight * 4),
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
        CopyPixelsToBitmap(m_pixels, m_overlayBitmap);
        CopyPixelsToBitmap(m_weaponPixels, m_weaponOverlayBitmap);
    }

    private float CalculateActorBrightness(double x, double y)
    {
        var illumination = 0.60f;
        if (LightObjects != null)
        {
            foreach (var sprite in LightObjects)
            {
                var (_, downward) = WolfensteinStaticObjects.GetLightBrightness(sprite.SpriteNumber);
                var (_, downwardRadius) = WolfensteinStaticObjects.GetLightRadii(sprite.SpriteNumber);
                illumination += CalculateLightContribution(
                    x,
                    y,
                    sprite.X,
                    sprite.Y,
                    downward,
                    downwardRadius);
            }
        }
        if (DynamicLights != null)
        {
            foreach (var light in DynamicLights)
            {
                illumination += CalculateLightContribution(
                    x,
                    y,
                    light.X,
                    light.Y,
                    light.DownwardBrightness,
                    light.DownwardRadius);
            }
        }
        if (MuzzleFlash > 0.0)
        {
            illumination += CalculateLightContribution(
                x,
                y,
                Camera.X + (Camera.DirectionX * 0.35),
                Camera.Y + (Camera.DirectionY * 0.35),
                (float)MuzzleFlash * 0.90f,
                2.50f);
        }
        return Math.Clamp(illumination, 0.20f, 1.0f);
    }

    private static float CalculateLightContribution(
        double x,
        double y,
        double lightX,
        double lightY,
        float brightness,
        float radius)
    {
        if (brightness <= 0.0f || radius <= 0.0f)
            return 0.0f;
        var deltaX = x - lightX;
        var deltaY = y - lightY;
        var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        var conePosition = Math.Clamp(distance / radius, 0.0, 1.0);
        var cone = 1.0 - (conePosition * conePosition * (3.0 - (2.0 * conePosition)));
        return brightness * (float)cone * 0.80f;
    }

    private static void CopyPixelsToBitmap(byte[] pixels, SKBitmap bitmap)
    {
        var sourceRowBytes = ViewportWidth * 4;
        for (var y = 0; y < ViewportHeight; y++)
        {
            Marshal.Copy(
                pixels,
                y * sourceRowBytes,
                IntPtr.Add(bitmap.GetPixels(), y * bitmap.RowBytes),
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
        SKBitmap weaponOverlay,
        float[] wallColumns,
        float[] sceneLights,
        float[] sceneLightRadii,
        float[] playerPosition,
        float[] cameraDirection,
        float[] cameraPlane,
        float muzzleFlash,
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
            using var weaponShader = weaponOverlay.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
            var uniforms = new SKRuntimeEffectUniforms(effect)
            {
                ["outputResolution"] = new[] { (float)Bounds.Width, (float)Bounds.Height },
                ["wallColumns"] = wallColumns,
                ["sceneLights"] = sceneLights,
                ["sceneLightRadii"] = sceneLightRadii,
                ["playerPosition"] = playerPosition,
                ["cameraDirection"] = cameraDirection,
                ["cameraPlane"] = cameraPlane,
                ["muzzleFlash"] = muzzleFlash,
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
                ["softwareSpriteOverlay"] = overlayShader,
                ["softwareWeaponOverlay"] = weaponShader
            };
            using var shader = effect.ToShader(false, uniforms, children);
            using var paint = new SKPaint { Shader = shader, IsAntialias = false };
            lease.SkCanvas.DrawRect(SKRect.Create((float)Bounds.Width, (float)Bounds.Height), paint);
        }
    }
}
