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
using Wolfenshine.Maps;
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
    private const int LightCount = AccessibleLightCache.ShaderLightCapacity;
    private const int LightChannelCount = 4;
    private const int LightRadiusChannelCount = 2;
    private const int LightColorChannelCount = 4;
    private const int DoorwaySpillCount = 32;
    private const int DoorwaySpillChannelCount = 4;
    private const int PickupSpotlightCount = 8;
    private const int PickupSpotlightChannelCount = 4;
    private const int AmbientMapPixelsPerTile = 16;
    private const float FogStartDistance = 5.0f;
    private const float FogEndDistance = 18.0f;
    private const float MaximumFogAmount = 0.30f;
    private static readonly RgbaColor FogColor = new(18, 24, 34);
    private readonly float[] m_wallColumns = new float[ViewportWidth * ColumnChannelCount];
    private readonly float[] m_sceneLights = new float[LightCount * LightChannelCount];
    private readonly float[] m_sceneLightRadii = new float[LightCount * LightRadiusChannelCount];
    private readonly float[] m_sceneLightUpColors = new float[LightCount * LightColorChannelCount];
    private readonly float[] m_sceneLightDownColors = new float[LightCount * LightColorChannelCount];
    private readonly float[] m_sceneLightHeights = new float[LightCount];
    private readonly double[] m_sceneLightDistances = new double[LightCount];
    private readonly float[] m_doorwaySpills = new float[DoorwaySpillCount * DoorwaySpillChannelCount];
    private readonly double[] m_doorwaySpillDistances = new double[DoorwaySpillCount];
    private readonly float[] m_pickupSpotlights = new float[PickupSpotlightCount * PickupSpotlightChannelCount];
    private readonly double[] m_pickupSpotlightDistances = new double[PickupSpotlightCount];
    private readonly float[] m_playerPosition = new float[2];
    private readonly float[] m_cameraDirection = new float[2];
    private readonly float[] m_cameraPlane = new float[2];
    private readonly byte[] m_weaponPixels = new byte[ViewportWidth * ViewportHeight * 4];
    private readonly byte[] m_bloomPixels = new byte[ViewportWidth * ViewportHeight * 4];
    private byte[] m_areaAmbientPixels = [];
    private byte[] m_areaAmbientBasePixels = [];
    private byte[] m_navigationRoutePixels = [];
    private bool m_areaAmbientBitmapDirty;
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
    private readonly SKBitmap m_bloomOverlayBitmap = new(new SKImageInfo(
        ViewportWidth,
        ViewportHeight,
        SKColorType.Rgba8888,
        SKAlphaType.Opaque));
    private readonly SKRuntimeEffect m_effect;
    private SKBitmap m_wallAtlas;
    private SKBitmap m_wallHeightAtlas;
    private SKBitmap m_wallMaterialMap;
    private SKBitmap m_areaAmbientBitmap;
    private SKBitmap m_navigationRouteBitmap;
    private WolfensteinWallTextures m_atlasWallTextures;
    private WolfensteinPalette m_atlasPalette;
    private WolfensteinMap m_areaAmbientSourceMap;
    private AreaAmbientMap m_areaAmbientMap;
    private WolfensteinMap m_navigationRouteSourceMap;
    private int m_navigationRoutePlayerX = int.MinValue;
    private int m_navigationRoutePlayerY = int.MinValue;
    private int m_navigationRouteStaticObjectCount = -1;
    private int m_navigationRoutePushWallState;
    private bool m_navigationRouteHasGoldKey;
    private bool m_navigationRouteHasSilverKey;
    private WolfensteinSpriteSet m_bloomSpriteSet;
    private WolfensteinPalette m_bloomPalette;
    private readonly Dictionary<int, BloomProfile> m_bloomProfiles = [];

    public static readonly StyledProperty<double> ViewBobProperty =
        AvaloniaProperty.Register<EnhancedViewport, double>(nameof(ViewBob));
    public static readonly StyledProperty<double> WeaponSwayProperty =
        AvaloniaProperty.Register<EnhancedViewport, double>(nameof(WeaponSway));
    public static readonly StyledProperty<double> MuzzleFlashProperty =
        AvaloniaProperty.Register<EnhancedViewport, double>(nameof(MuzzleFlash));
    public static readonly StyledProperty<double> DamageTraumaProperty =
        AvaloniaProperty.Register<EnhancedViewport, double>(nameof(DamageTrauma));
    public static readonly StyledProperty<double> DamageDirectionProperty =
        AvaloniaProperty.Register<EnhancedViewport, double>(nameof(DamageDirection));
    public static readonly StyledProperty<int> DamageSeedProperty =
        AvaloniaProperty.Register<EnhancedViewport, int>(nameof(DamageSeed));
    public static readonly StyledProperty<double> DamageTintProperty =
        AvaloniaProperty.Register<EnhancedViewport, double>(nameof(DamageTint));
    public static readonly StyledProperty<double> BloodAmountProperty =
        AvaloniaProperty.Register<EnhancedViewport, double>(nameof(BloodAmount));
    public static readonly StyledProperty<bool> IsWeaponFlashFrameProperty =
        AvaloniaProperty.Register<EnhancedViewport, bool>(nameof(IsWeaponFlashFrame));
    public static readonly StyledProperty<IReadOnlyList<WorldSprite>> LightObjectsProperty =
        AvaloniaProperty.Register<EnhancedViewport, IReadOnlyList<WorldSprite>>(nameof(LightObjects));
    public static readonly StyledProperty<IReadOnlyList<WorldLight>> DynamicLightsProperty =
        AvaloniaProperty.Register<EnhancedViewport, IReadOnlyList<WorldLight>>(nameof(DynamicLights));
    public static readonly StyledProperty<bool> HasGoldKeyProperty =
        AvaloniaProperty.Register<EnhancedViewport, bool>(nameof(HasGoldKey));
    public static readonly StyledProperty<bool> HasSilverKeyProperty =
        AvaloniaProperty.Register<EnhancedViewport, bool>(nameof(HasSilverKey));
    public static readonly StyledProperty<double> NavigationGuideVisibilityProperty =
        AvaloniaProperty.Register<EnhancedViewport, double>(nameof(NavigationGuideVisibility));
    public static readonly StyledProperty<double> NavigationGuideTimeProperty =
        AvaloniaProperty.Register<EnhancedViewport, double>(nameof(NavigationGuideTime));

    static EnhancedViewport() => AffectsRender<EnhancedViewport>(
        ViewBobProperty,
        WeaponSwayProperty,
        MuzzleFlashProperty,
        DamageTraumaProperty,
        DamageDirectionProperty,
        DamageSeedProperty,
        DamageTintProperty,
        BloodAmountProperty,
        IsWeaponFlashFrameProperty,
        LightObjectsProperty,
        DynamicLightsProperty,
        HasGoldKeyProperty,
        HasSilverKeyProperty,
        NavigationGuideVisibilityProperty,
        NavigationGuideTimeProperty);

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

    public double WeaponSway
    {
        get => GetValue(WeaponSwayProperty);
        set => SetValue(WeaponSwayProperty, value);
    }

    public double MuzzleFlash
    {
        get => GetValue(MuzzleFlashProperty);
        set => SetValue(MuzzleFlashProperty, value);
    }

    public double DamageTrauma
    {
        get => GetValue(DamageTraumaProperty);
        set => SetValue(DamageTraumaProperty, value);
    }

    public double DamageDirection
    {
        get => GetValue(DamageDirectionProperty);
        set => SetValue(DamageDirectionProperty, value);
    }

    public int DamageSeed
    {
        get => GetValue(DamageSeedProperty);
        set => SetValue(DamageSeedProperty, value);
    }

    public double DamageTint
    {
        get => GetValue(DamageTintProperty);
        set => SetValue(DamageTintProperty, value);
    }

    public double BloodAmount
    {
        get => GetValue(BloodAmountProperty);
        set => SetValue(BloodAmountProperty, value);
    }

    public bool IsWeaponFlashFrame
    {
        get => GetValue(IsWeaponFlashFrameProperty);
        set => SetValue(IsWeaponFlashFrameProperty, value);
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

    public bool HasGoldKey
    {
        get => GetValue(HasGoldKeyProperty);
        set => SetValue(HasGoldKeyProperty, value);
    }

    public bool HasSilverKey
    {
        get => GetValue(HasSilverKeyProperty);
        set => SetValue(HasSilverKeyProperty, value);
    }

    public double NavigationGuideVisibility
    {
        get => GetValue(NavigationGuideVisibilityProperty);
        set => SetValue(NavigationGuideVisibilityProperty, value);
    }

    public double NavigationGuideTime
    {
        get => GetValue(NavigationGuideTimeProperty);
        set => SetValue(NavigationGuideTimeProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Map == null || Doors == null || Camera == null || WallTextures == null || Palette == null)
            return;

        EnsureWallAtlas();
        EnsureAreaAmbientMap();
        UpdateAreaAmbientBitmap();
        EnsureNavigationRouteMap();
        var playerAmbientScale = (float)m_areaAmbientMap.GetAmbientScale(Camera.X, Camera.Y, Doors);
        BuildColumnBuffer();
        BuildLightBuffer();
        BuildDoorwaySpillBuffer();
        BuildPickupSpotlightBuffer();
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
            m_wallHeightAtlas,
            m_wallMaterialMap,
            m_areaAmbientBitmap,
            m_navigationRouteBitmap,
            m_overlayBitmap,
            m_weaponOverlayBitmap,
            m_bloomOverlayBitmap,
            m_wallColumns,
            m_sceneLights,
            m_sceneLightRadii,
            m_sceneLightUpColors,
            m_sceneLightDownColors,
            m_sceneLightHeights,
            m_doorwaySpills,
            m_pickupSpotlights,
            m_playerPosition,
            m_cameraDirection,
            m_cameraPlane,
            (float)MuzzleFlash,
            IsWeaponFlashFrame ? 1.0f : 0.0f,
            (float)NavigationGuideVisibility,
            (float)NavigationGuideTime,
            playerAmbientScale,
            (float)AreaAmbientMap.MaximumAmbientScale,
            AmbientMapPixelsPerTile,
            ToFloats(FogColor),
            [FogStartDistance, FogEndDistance, MaximumFogAmount, 0.0f],
            (float)ViewBob,
            (float)WeaponSway,
            (float)DamageFlash,
            (float)DamageTrauma,
            (float)DamageDirection,
            DamageSeed,
            (float)DamageTint,
            (float)BloodAmount,
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
        m_bloomOverlayBitmap.Dispose();
        m_wallAtlas?.Dispose();
        m_wallHeightAtlas?.Dispose();
        m_wallMaterialMap?.Dispose();
        m_areaAmbientBitmap?.Dispose();
        m_navigationRouteBitmap?.Dispose();
        m_effect.Dispose();
    }

    private void EnsureWallAtlas()
    {
        if (ReferenceEquals(m_atlasWallTextures, WallTextures) && ReferenceEquals(m_atlasPalette, Palette))
            return;
        m_wallAtlas?.Dispose();
        m_wallHeightAtlas?.Dispose();
        m_wallMaterialMap?.Dispose();
        m_wallAtlas = new SKBitmap(new SKImageInfo(
            WolfensteinWallTexture.Size,
            WallTextures.Pages.Count * WolfensteinWallTexture.Size,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque));
        m_wallHeightAtlas = new SKBitmap(new SKImageInfo(
            WolfensteinWallTexture.Size,
            WallTextures.Pages.Count * WolfensteinWallTexture.Size,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque));
        m_wallMaterialMap = new SKBitmap(new SKImageInfo(
            WallTextures.Pages.Count,
            1,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque));
        for (var page = 0; page < WallTextures.Pages.Count; page++)
        {
            var texture = WallTextures.Pages[page];
            var heights = BuildWallHeightMap(texture);
            for (var y = 0; y < WolfensteinWallTexture.Size; y++)
            {
                for (var x = 0; x < WolfensteinWallTexture.Size; x++)
                {
                    var color = Palette.GetColor(texture.GetIndex(x, y));
                    var height = heights[(y * WolfensteinWallTexture.Size) + x];
                    m_wallAtlas.SetPixel(
                        x,
                        (page * WolfensteinWallTexture.Size) + y,
                        new SKColor(color.Red, color.Green, color.Blue, color.Alpha));
                    m_wallHeightAtlas.SetPixel(
                        x,
                        (page * WolfensteinWallTexture.Size) + y,
                        new SKColor(height, height, height));
                }
            }
            var material = ClassifyWallMaterial(texture, page >= WallTextures.SpriteStart - 8);
            m_wallMaterialMap.SetPixel(
                page,
                0,
                new SKColor(
                    ToByte(material.BumpStrength),
                    ToByte(material.SpecularStrength),
                    ToByte(material.Gloss)));
        }
        m_atlasWallTextures = WallTextures;
        m_atlasPalette = Palette;
    }

    private byte[] BuildWallHeightMap(WolfensteinWallTexture texture)
    {
        var heights = new double[WolfensteinWallTexture.DataLength];
        var minimum = double.MaxValue;
        var maximum = double.MinValue;
        for (var y = 0; y < WolfensteinWallTexture.Size; y++)
        {
            for (var x = 0; x < WolfensteinWallTexture.Size; x++)
            {
                var weightedLuminance = 0.0;
                var totalWeight = 0.0;
                for (var offsetY = -1; offsetY <= 1; offsetY++)
                {
                    for (var offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        var sampleX = Math.Clamp(x + offsetX, 0, WolfensteinWallTexture.Size - 1);
                        var sampleY = Math.Clamp(y + offsetY, 0, WolfensteinWallTexture.Size - 1);
                        var color = Palette.GetColor(texture.GetIndex(sampleX, sampleY));
                        var weight = (offsetX == 0 ? 2.0 : 1.0) * (offsetY == 0 ? 2.0 : 1.0);
                        weightedLuminance += GetLuminance(color) * weight;
                        totalWeight += weight;
                    }
                }
                var luminance = weightedLuminance / totalWeight;
                heights[(y * WolfensteinWallTexture.Size) + x] = luminance;
                minimum = Math.Min(minimum, luminance);
                maximum = Math.Max(maximum, luminance);
            }
        }

        var result = new byte[heights.Length];
        var range = Math.Max(maximum - minimum, 0.05);
        for (var index = 0; index < heights.Length; index++)
        {
            result[index] = ToByte((heights[index] - minimum) / range);
        }
        return result;
    }

    private WallMaterial ClassifyWallMaterial(WolfensteinWallTexture texture, bool isDoor)
    {
        if (isDoor)
            return new WallMaterial(0.28, 0.46, 0.72);

        var red = 0.0;
        var green = 0.0;
        var blue = 0.0;
        for (var y = 0; y < WolfensteinWallTexture.Size; y++)
        {
            for (var x = 0; x < WolfensteinWallTexture.Size; x++)
            {
                var color = Palette.GetColor(texture.GetIndex(x, y));
                red += color.Red;
                green += color.Green;
                blue += color.Blue;
            }
        }
        var scale = 1.0 / (WolfensteinWallTexture.DataLength * byte.MaxValue);
        red *= scale;
        green *= scale;
        blue *= scale;
        var chroma = Math.Max(red, Math.Max(green, blue)) - Math.Min(red, Math.Min(green, blue));
        if (blue > red * 1.25 && blue > green * 1.10)
            return new WallMaterial(1.00, 0.28, 0.50); // Blue stone and brick.
        if (red > blue * 1.28 && red > green * 1.04)
            return new WallMaterial(0.50, 0.06, 0.16); // Soft, matte wood paneling.
        if (chroma < 0.10)
            return new WallMaterial(0.82, 0.18, 0.30); // Grey stone and concrete.
        return new WallMaterial(0.62, 0.11, 0.25);
    }

    private static double GetLuminance(RgbaColor color) =>
        ((color.Red * 0.2126) + (color.Green * 0.7152) + (color.Blue * 0.0722)) / byte.MaxValue;

    private static byte ToByte(double value) =>
        (byte)Math.Round(Math.Clamp(value, 0.0, 1.0) * byte.MaxValue);

    private void EnsureAreaAmbientMap()
    {
        if (ReferenceEquals(m_areaAmbientSourceMap, Map))
            return;
        m_areaAmbientSourceMap = Map;
        m_areaAmbientMap = AreaAmbientMap.FromMap(Map);
        m_areaAmbientBitmap?.Dispose();
        m_areaAmbientBitmap = new SKBitmap(new SKImageInfo(
            Map.Width * AmbientMapPixelsPerTile,
            Map.Height * AmbientMapPixelsPerTile,
            SKColorType.Rgba8888,
            SKAlphaType.Opaque));
        m_areaAmbientPixels = new byte[
            m_areaAmbientBitmap.Width *
            m_areaAmbientBitmap.Height *
            4];
        m_areaAmbientBasePixels = new byte[m_areaAmbientPixels.Length];
        BuildAreaAmbientBasePixels();
        m_areaAmbientBitmapDirty = true;
    }

    private void BuildAreaAmbientBasePixels()
    {
        for (var tileY = 0; tileY < Map.Height; tileY++)
        {
            for (var tileX = 0; tileX < Map.Width; tileX++)
            {
                var ambientScale = m_areaAmbientMap.GetAmbientScale(tileX + 0.5, tileY + 0.5);
                var firstPixelX = tileX * AmbientMapPixelsPerTile;
                var firstPixelY = tileY * AmbientMapPixelsPerTile;
                for (var localY = 0; localY < AmbientMapPixelsPerTile; localY++)
                {
                    for (var localX = 0; localX < AmbientMapPixelsPerTile; localX++)
                    {
                        SetAreaAmbientPixel(
                            m_areaAmbientBasePixels,
                            firstPixelX + localX,
                            firstPixelY + localY,
                            ambientScale);
                    }
                }
            }
        }
    }

    private void UpdateAreaAmbientBitmap()
    {
        if (!m_areaAmbientBitmapDirty)
            return;

        Array.Copy(m_areaAmbientBasePixels, m_areaAmbientPixels, m_areaAmbientPixels.Length);
        foreach (var door in Doors.Items)
        {
            var vertical = door.Orientation == DoorOrientation.Vertical;
            var radiusX = vertical ? AreaAmbientMap.DoorBlendRadius : AreaAmbientMap.DoorBlendHalfWidth;
            var radiusY = vertical ? AreaAmbientMap.DoorBlendHalfWidth : AreaAmbientMap.DoorBlendRadius;
            var centerX = door.X + 0.5;
            var centerY = door.Y + 0.5;
            var firstPixelX = Math.Max(0, (int)Math.Floor(
                (centerX - radiusX) * AmbientMapPixelsPerTile));
            var lastPixelX = Math.Min(m_areaAmbientBitmap.Width - 1, (int)Math.Ceiling(
                (centerX + radiusX) * AmbientMapPixelsPerTile));
            var firstPixelY = Math.Max(0, (int)Math.Floor(
                (centerY - radiusY) * AmbientMapPixelsPerTile));
            var lastPixelY = Math.Min(m_areaAmbientBitmap.Height - 1, (int)Math.Ceiling(
                (centerY + radiusY) * AmbientMapPixelsPerTile));
            for (var pixelY = firstPixelY; pixelY <= lastPixelY; pixelY++)
            {
                var worldY = (pixelY + 0.5) / AmbientMapPixelsPerTile;
                for (var pixelX = firstPixelX; pixelX <= lastPixelX; pixelX++)
                {
                    var worldX = (pixelX + 0.5) / AmbientMapPixelsPerTile;
                    SetAreaAmbientPixel(
                        m_areaAmbientPixels,
                        pixelX,
                        pixelY,
                        m_areaAmbientMap.GetAmbientScale(worldX, worldY, Doors));
                }
            }
        }
        CopyPixelsToBitmap(m_areaAmbientPixels, m_areaAmbientBitmap);
        m_areaAmbientBitmapDirty = false;
    }

    private void SetAreaAmbientPixel(byte[] pixels, int pixelX, int pixelY, double ambientScale)
    {
        var ambient = (byte)Math.Round(
            Math.Clamp(
                ambientScale / AreaAmbientMap.MaximumAmbientScale,
                0.0,
                1.0) * byte.MaxValue);
        var target = ((pixelY * m_areaAmbientBitmap.Width) + pixelX) * 4;
        pixels[target] = ambient;
        pixels[target + 1] = ambient;
        pixels[target + 2] = ambient;
        pixels[target + 3] = byte.MaxValue;
    }

    private void EnsureNavigationRouteMap()
    {
        if (!ReferenceEquals(m_navigationRouteSourceMap, Map))
        {
            m_navigationRouteSourceMap = Map;
            m_navigationRouteBitmap?.Dispose();
            m_navigationRouteBitmap = new SKBitmap(new SKImageInfo(
                Map.Width,
                Map.Height,
                SKColorType.Rgba8888,
                SKAlphaType.Opaque));
            m_navigationRoutePixels = new byte[Map.Width * Map.Height * 4];
            InvalidateNavigationRoute();
        }

        var playerX = (int)Math.Floor(Camera.X);
        var playerY = (int)Math.Floor(Camera.Y);
        var pushWallState = GetPushWallState();
        var staticObjectCount = StaticObjects?.Count ?? 0;
        if (m_navigationRoutePlayerX == playerX &&
            m_navigationRoutePlayerY == playerY &&
            m_navigationRouteStaticObjectCount == staticObjectCount &&
            m_navigationRoutePushWallState == pushWallState &&
            m_navigationRouteHasGoldKey == HasGoldKey &&
            m_navigationRouteHasSilverKey == HasSilverKey)
        {
            return;
        }

        m_navigationRoutePlayerX = playerX;
        m_navigationRoutePlayerY = playerY;
        m_navigationRouteStaticObjectCount = staticObjectCount;
        m_navigationRoutePushWallState = pushWallState;
        m_navigationRouteHasGoldKey = HasGoldKey;
        m_navigationRouteHasSilverKey = HasSilverKey;
        var route = NavigationRoutePlanner.Find(
            Map,
            Doors,
            PushWalls,
            playerX,
            playerY,
            StaticObjects ?? [],
            HasGoldKey,
            HasSilverKey);
        BuildNavigationRoutePixels(route);
    }

    private void BuildNavigationRoutePixels(NavigationRoute route)
    {
        Array.Clear(m_navigationRoutePixels);
        if (route.Points.Count > 1)
        {
            for (var index = 0; index < route.Points.Count; index++)
            {
                var current = route.Points[index];
                var incoming = index > 0
                    ? GetDirectionCode(route.Points[index - 1], current)
                    : GetDirectionCode(current, route.Points[index + 1]);
                var outgoing = index < route.Points.Count - 1
                    ? GetDirectionCode(current, route.Points[index + 1])
                    : incoming;
                var target = ((current.Y * Map.Width) + current.X) * 4;
                m_navigationRoutePixels[target] = incoming;
                m_navigationRoutePixels[target + 1] = outgoing;
                m_navigationRoutePixels[target + 2] = (byte)(index % byte.MaxValue);
                m_navigationRoutePixels[target + 3] = byte.MaxValue;
            }
        }
        CopyPixelsToBitmap(m_navigationRoutePixels, m_navigationRouteBitmap);
    }

    private void InvalidateNavigationRoute()
    {
        m_navigationRoutePlayerX = int.MinValue;
        m_navigationRoutePlayerY = int.MinValue;
        m_navigationRouteStaticObjectCount = -1;
        m_navigationRoutePushWallState = 0;
        m_navigationRouteHasGoldKey = false;
        m_navigationRouteHasSilverKey = false;
    }

    private int GetPushWallState()
    {
        var hash = new HashCode();
        hash.Add(PushWalls);
        foreach (var wall in PushWalls.Items)
        {
            hash.Add(wall.OriginX);
            hash.Add(wall.OriginY);
            hash.Add((int)Math.Floor(wall.Distance));
            hash.Add(wall.IsMoving);
        }
        return hash.ToHashCode();
    }

    private static byte GetDirectionCode(NavigationRoutePoint from, NavigationRoutePoint to) =>
        (to.X - from.X, to.Y - from.Y) switch
        {
            (1, 0) => 1,
            (0, 1) => 2,
            (-1, 0) => 3,
            (0, -1) => 4,
            _ => 0
        };

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
            if (m_columns[column].Tile is >= 90 and <= 101)
                flags |= 8;
            m_wallColumns[target + 3] = flags;
        }
    }

    private void BuildLightBuffer()
    {
        Array.Clear(m_sceneLights);
        Array.Clear(m_sceneLightRadii);
        Array.Clear(m_sceneLightUpColors);
        Array.Clear(m_sceneLightDownColors);
        Array.Clear(m_sceneLightHeights);
        Array.Fill(m_sceneLightDistances, double.PositiveInfinity);
        if (LightObjects != null)
        {
            foreach (var sprite in LightObjects)
            {
                var (upward, downward) = WolfensteinStaticObjects.GetLightBrightness(sprite.SpriteNumber);
                var (upwardRadius, downwardRadius) = WolfensteinStaticObjects.GetLightRadii(sprite.SpriteNumber);
                var (upwardColor, downwardColor) = WolfensteinStaticObjects.GetLightColors(sprite.SpriteNumber);
                var height = WolfensteinStaticObjects.GetLightHeight(sprite.SpriteNumber);
                if (upward <= 0.0f && downward <= 0.0f)
                    continue;
                InsertLight(
                    sprite.X,
                    sprite.Y,
                    upward,
                    downward,
                    upwardRadius,
                    downwardRadius,
                    upwardColor,
                    downwardColor,
                    height);
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
                light.DownwardRadius,
                light.UpwardColor,
                light.DownwardColor,
                light.Height);
        }
    }

    private void InsertLight(
        double x,
        double y,
        float upward,
        float downward,
        float upwardRadius,
        float downwardRadius,
        RgbaColor upwardColor,
        RgbaColor downwardColor,
        float height)
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
            Array.Copy(
                m_sceneLightUpColors,
                (index - 1) * LightColorChannelCount,
                m_sceneLightUpColors,
                index * LightColorChannelCount,
                LightColorChannelCount);
            Array.Copy(
                m_sceneLightDownColors,
                (index - 1) * LightColorChannelCount,
                m_sceneLightDownColors,
                index * LightColorChannelCount,
                LightColorChannelCount);
            m_sceneLightHeights[index] = m_sceneLightHeights[index - 1];
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
        WriteLightColor(m_sceneLightUpColors, insertAt, upwardColor);
        WriteLightColor(m_sceneLightDownColors, insertAt, downwardColor);
        m_sceneLightHeights[insertAt] = height;
    }

    private void BuildDoorwaySpillBuffer()
    {
        Array.Clear(m_doorwaySpills);
        Array.Fill(m_doorwaySpillDistances, double.PositiveInfinity);
        foreach (var door in Doors.Items)
        {
            if (door.OpenAmount <= 0.0)
                continue;
            var normalX = door.Orientation == DoorOrientation.Vertical ? 1.0 : 0.0;
            var normalY = door.Orientation == DoorOrientation.Horizontal ? 1.0 : 0.0;
            var centerX = door.X + 0.5;
            var centerY = door.Y + 0.5;
            var negativeAmbient = m_areaAmbientMap.GetAmbientScale(centerX - normalX, centerY - normalY);
            var positiveAmbient = m_areaAmbientMap.GetAmbientScale(centerX + normalX, centerY + normalY);
            var ambientDifference = positiveAmbient - negativeAmbient;
            if (Math.Abs(ambientDifference) < 0.01)
                continue;
            var spillStrength = Math.Min(Math.Abs(ambientDifference), 1.0) * door.OpenAmount;
            var direction = ambientDifference > 0.0 ? -1.0 : 1.0;
            InsertDoorwaySpill(
                centerX,
                centerY,
                normalX * direction * spillStrength,
                normalY * direction * spillStrength);
        }
    }

    private void InsertDoorwaySpill(double x, double y, double directionX, double directionY)
    {
        var deltaX = x - Camera.X;
        var deltaY = y - Camera.Y;
        var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
        var insertAt = -1;
        for (var index = 0; index < DoorwaySpillCount; index++)
        {
            if (distanceSquared >= m_doorwaySpillDistances[index])
                continue;
            insertAt = index;
            break;
        }
        if (insertAt < 0)
            return;

        for (var index = DoorwaySpillCount - 1; index > insertAt; index--)
        {
            m_doorwaySpillDistances[index] = m_doorwaySpillDistances[index - 1];
            Array.Copy(
                m_doorwaySpills,
                (index - 1) * DoorwaySpillChannelCount,
                m_doorwaySpills,
                index * DoorwaySpillChannelCount,
                DoorwaySpillChannelCount);
        }
        m_doorwaySpillDistances[insertAt] = distanceSquared;
        var target = insertAt * DoorwaySpillChannelCount;
        m_doorwaySpills[target] = (float)x;
        m_doorwaySpills[target + 1] = (float)y;
        m_doorwaySpills[target + 2] = (float)directionX;
        m_doorwaySpills[target + 3] = (float)directionY;
    }

    private void BuildPickupSpotlightBuffer()
    {
        Array.Clear(m_pickupSpotlights);
        Array.Fill(m_pickupSpotlightDistances, double.PositiveInfinity);
        if (StaticObjects == null)
            return;
        foreach (var sprite in StaticObjects)
        {
            if (!IsSpotlightPickup(sprite))
                continue;
            InsertPickupSpotlight(sprite.X, sprite.Y);
        }
    }

    private void InsertPickupSpotlight(double x, double y)
    {
        var deltaX = x - Camera.X;
        var deltaY = y - Camera.Y;
        var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
        var insertAt = -1;
        for (var index = 0; index < PickupSpotlightCount; index++)
        {
            if (distanceSquared >= m_pickupSpotlightDistances[index])
                continue;
            insertAt = index;
            break;
        }
        if (insertAt < 0)
            return;

        for (var index = PickupSpotlightCount - 1; index > insertAt; index--)
        {
            m_pickupSpotlightDistances[index] = m_pickupSpotlightDistances[index - 1];
            Array.Copy(
                m_pickupSpotlights,
                (index - 1) * PickupSpotlightChannelCount,
                m_pickupSpotlights,
                index * PickupSpotlightChannelCount,
                PickupSpotlightChannelCount);
        }
        m_pickupSpotlightDistances[insertAt] = distanceSquared;
        var target = insertAt * PickupSpotlightChannelCount;
        m_pickupSpotlights[target] = (float)x;
        m_pickupSpotlights[target + 1] = (float)y;
        m_pickupSpotlights[target + 2] = 1.0f;
    }

    private static void WriteLightColor(float[] colors, int lightIndex, RgbaColor color)
    {
        var target = lightIndex * LightColorChannelCount;
        colors[target] = color.Red / 255.0f;
        colors[target + 1] = color.Green / 255.0f;
        colors[target + 2] = color.Blue / 255.0f;
        colors[target + 3] = 1.0f;
    }

    private void BuildSoftwareOverlays()
    {
        Array.Clear(m_pixels);
        Array.Clear(m_weaponPixels);
        Array.Clear(m_bloomPixels);
        var playViewPixels = m_pixels.AsSpan(0, ViewportWidth * PlayViewHeight * 4);
        if (Sprites != null && StaticObjects != null)
        {
            if (m_litWorldSprites.Length != StaticObjects.Count)
                m_litWorldSprites = new WorldSprite[StaticObjects.Count];
            for (var index = 0; index < StaticObjects.Count; index++)
            {
                var sprite = StaticObjects[index];
                var godRayAmount = CalculateSpriteGodRayAmount(sprite);
                var brightness = CalculateSpriteBrightness(sprite) + (godRayAmount * 0.20f);
                if (IsSpotlightPickup(sprite))
                    brightness = Math.Max(brightness, 0.94f);
                m_litWorldSprites[index] = sprite with
                {
                    Brightness = Math.Clamp(brightness, 0.20f, 1.0f),
                    GodRayAmount = godRayAmount
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
            EnsureBloomProfiles();
            for (var index = 0; index < visibleSpriteCount; index++)
                DrawBloom(m_projectedSprites[index]);
            for (var index = 0; index < visibleSpriteCount; index++)
            {
                var projected = m_projectedSprites[index];
                m_projectedSprites[index] = projected with
                {
                    FogAmount = CalculateFogAmount(projected.Depth),
                    SourceHeight = projected.SpriteNumber is 6 or 16
                        ? WolfensteinSprite.Size / 2
                        : WolfensteinSprite.Size
                };
            }
            SoftwareRaycastRenderer.DrawWorldSprites(
                m_projectedSprites.AsSpan(0, visibleSpriteCount),
                Sprites,
                Palette,
                m_columns,
                playViewPixels,
                ViewportWidth,
                PlayViewHeight,
                FogColor,
                drawGroundShadows: true);
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
        CopyPixelsToBitmap(m_bloomPixels, m_bloomOverlayBitmap);
    }

    private void EnsureBloomProfiles()
    {
        if (ReferenceEquals(m_bloomSpriteSet, Sprites) && ReferenceEquals(m_bloomPalette, Palette))
            return;
        m_bloomProfiles.Clear();
        int[] bloomSpriteNumbers = [6, 16, 31, 32, 33, 34];
        foreach (var spriteNumber in bloomSpriteNumbers)
        {
            if (spriteNumber >= Sprites.Count)
                continue;
            var sprite = Sprites.Get(spriteNumber);
            var totalWeight = 0.0;
            var weightedX = 0.0;
            var weightedY = 0.0;
            // Ceiling fixtures include a painted floor shadow in the lower half;
            // exclude it so the glow is centered on the luminous fixture itself.
            var maximumY = spriteNumber is 6 or 16
                ? WolfensteinSprite.Size / 2
                : WolfensteinSprite.Size;
            for (var y = 0; y < maximumY; y++)
            {
                for (var x = 0; x < WolfensteinSprite.Size; x++)
                {
                    if (!sprite.TryGetIndex(x, y, out var paletteIndex))
                        continue;
                    var color = Palette.GetColor(paletteIndex);
                    var brightness = Math.Max(color.Red, Math.Max(color.Green, color.Blue)) / 255.0;
                    var weight = brightness * brightness;
                    totalWeight += weight;
                    weightedX += (x + 0.5) * weight;
                    weightedY += (y + 0.5) * weight;
                }
            }
            if (totalWeight <= 0.0)
                continue;
            var (upwardColor, downwardColor) = WolfensteinStaticObjects.GetLightColors(spriteNumber);
            var bloomColor = spriteNumber == 16 ? upwardColor : downwardColor;
            var radiusScale = spriteNumber switch
            {
                16 => 0.38,
                6 => 0.34,
                _ => 0.26
            };
            m_bloomProfiles[spriteNumber] = new BloomProfile(
                weightedX / totalWeight / WolfensteinSprite.Size,
                weightedY / totalWeight / WolfensteinSprite.Size,
                radiusScale,
                spriteNumber is 6 or 16 ? 0.90 : 0.75,
                bloomColor);
        }
        m_bloomSpriteSet = Sprites;
        m_bloomPalette = Palette;
    }

    private void DrawBloom(ProjectedWorldSprite projected)
    {
        if (!m_bloomProfiles.TryGetValue(projected.SpriteNumber, out var profile))
            return;
        var left = projected.CenterX - (projected.RenderedSize * 0.5);
        var top = (PlayViewHeight - projected.RenderedSize) * 0.5;
        var centerX = left + (profile.CenterX * projected.RenderedSize);
        var centerY = top + (profile.CenterY * projected.RenderedSize);
        var radiusX = Math.Max(2.0, projected.RenderedSize * profile.RadiusScale);
        var radiusY = Math.Max(2.0, radiusX * 0.82);
        var firstX = Math.Max(0, (int)Math.Floor(centerX - radiusX));
        var lastX = Math.Min(ViewportWidth - 1, (int)Math.Ceiling(centerX + radiusX));
        var firstY = Math.Max(0, (int)Math.Floor(centerY - radiusY));
        var lastY = Math.Min(PlayViewHeight - 1, (int)Math.Ceiling(centerY + radiusY));
        var fogScale = 1.0 - CalculateFogAmount(projected.Depth);
        for (var x = firstX; x <= lastX; x++)
        {
            if (projected.Depth >= m_columns[x].Distance)
                continue;
            var normalizedX = (x + 0.5 - centerX) / radiusX;
            for (var y = firstY; y <= lastY; y++)
            {
                var normalizedY = (y + 0.5 - centerY) / radiusY;
                var distanceSquared = (normalizedX * normalizedX) + (normalizedY * normalizedY);
                if (distanceSquared >= 1.0)
                    continue;
                var falloff = 1.0 - Math.Sqrt(distanceSquared);
                var intensity = falloff * falloff * 0.62 * profile.IntensityScale * fogScale;
                AddBloomPixel(x, y, profile.Color, intensity);
            }
        }
    }

    private void AddBloomPixel(int x, int y, RgbaColor color, double intensity)
    {
        var target = ((y * ViewportWidth) + x) * 4;
        m_bloomPixels[target] = (byte)Math.Min(
            byte.MaxValue,
            m_bloomPixels[target] + (color.Red * intensity));
        m_bloomPixels[target + 1] = (byte)Math.Min(
            byte.MaxValue,
            m_bloomPixels[target + 1] + (color.Green * intensity));
        m_bloomPixels[target + 2] = (byte)Math.Min(
            byte.MaxValue,
            m_bloomPixels[target + 2] + (color.Blue * intensity));
        m_bloomPixels[target + 3] = byte.MaxValue;
    }

    private float CalculateSpriteBrightness(WorldSprite worldSprite)
    {
        var ambientScale = (float)m_areaAmbientMap.GetAmbientScale(worldSprite.X, worldSprite.Y);
        var illumination = (worldSprite.IsActor ? 0.60f : 1.0f) *
                           Math.Clamp(
                               ambientScale,
                               (float)AreaAmbientMap.MinimumAmbientScale,
                               (float)AreaAmbientMap.MaximumAmbientScale);
        if (LightObjects != null)
        {
            foreach (var sprite in LightObjects)
            {
                var (_, downward) = WolfensteinStaticObjects.GetLightBrightness(sprite.SpriteNumber);
                var (_, downwardRadius) = WolfensteinStaticObjects.GetLightRadii(sprite.SpriteNumber);
                illumination += CalculateLightContribution(
                    worldSprite.X,
                    worldSprite.Y,
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
                    worldSprite.X,
                    worldSprite.Y,
                    light.X,
                    light.Y,
                    light.DownwardBrightness,
                    light.DownwardRadius);
            }
        }
        if (MuzzleFlash > 0.0)
        {
            illumination += CalculateLightContribution(
                worldSprite.X,
                worldSprite.Y,
                Camera.X + (Camera.DirectionX * 0.35),
                Camera.Y + (Camera.DirectionY * 0.35),
                (float)MuzzleFlash * 0.90f,
                2.50f);
        }
        return Math.Clamp(illumination, 0.20f, 1.0f);
    }

    /// <summary>
    /// Approximates how much of a sprite's center lies inside a descending doorway-light prism.
    /// </summary>
    private float CalculateSpriteGodRayAmount(WorldSprite worldSprite)
    {
        const double shaftLength = 4.5;
        const double baseHalfWidth = 0.36;
        const double widthSlope = 0.20;
        var surfaceHeight = worldSprite.IsActor ? 0.50 : 0.35;
        var illumination = 0.0;
        for (var index = 0; index < DoorwaySpillCount; index++)
        {
            var source = index * DoorwaySpillChannelCount;
            var directionX = m_doorwaySpills[source + 2];
            var directionY = m_doorwaySpills[source + 3];
            var strength = Math.Sqrt((directionX * directionX) + (directionY * directionY));
            if (strength <= 0.0001)
                continue;
            directionX /= (float)strength;
            directionY /= (float)strength;
            var relativeX = worldSprite.X - m_doorwaySpills[source];
            var relativeY = worldSprite.Y - m_doorwaySpills[source + 1];
            var forward = (relativeX * directionX) + (relativeY * directionY);
            if (forward < 0.0 || forward > shaftLength)
                continue;
            var lateral = Math.Abs((relativeX * directionY) - (relativeY * directionX));
            var halfWidth = baseHalfWidth + (forward * widthSlope);
            var prismTop = 1.0 - (forward / shaftLength);
            if (lateral >= halfWidth || surfaceHeight > prismTop + 0.08)
                continue;
            var edgePosition = Math.Clamp(lateral / halfWidth, 0.0, 1.0);
            var edgeFade = 1.0 - (edgePosition * edgePosition * (3.0 - (2.0 * edgePosition)));
            var endPosition = Math.Clamp(forward / shaftLength, 0.0, 1.0);
            var endFade = 1.0 - (endPosition * endPosition * (3.0 - (2.0 * endPosition)));
            illumination += strength * edgeFade * endFade;
        }
        return (float)Math.Clamp(illumination, 0.0, 1.0);
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

    private static float CalculateFogAmount(double distance)
    {
        var position = Math.Clamp(
            (distance - FogStartDistance) / (FogEndDistance - FogStartDistance),
            0.0,
            1.0);
        var smoothPosition = position * position * (3.0 - (2.0 * position));
        return (float)(smoothPosition * MaximumFogAmount);
    }

    private static void CopyPixelsToBitmap(byte[] pixels, SKBitmap bitmap)
    {
        var sourceRowBytes = bitmap.Width * 4;
        for (var y = 0; y < bitmap.Height; y++)
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

    private static bool IsSpotlightPickup(WorldSprite sprite) =>
        WolfensteinStaticObjects.GetPickupType(sprite.SpriteNumber) is
            WolfensteinPickupType.MachineGun or
            WolfensteinPickupType.Chaingun or
            WolfensteinPickupType.GoldKey or
            WolfensteinPickupType.SilverKey or
            WolfensteinPickupType.FullHeal;

    private sealed class ShaderDrawOperation(
        Rect bounds,
        SKRuntimeEffect effect,
        SKBitmap wallTextures,
        SKBitmap wallHeights,
        SKBitmap wallMaterials,
        SKBitmap areaAmbientMap,
        SKBitmap navigationRouteMap,
        SKBitmap spriteOverlay,
        SKBitmap weaponOverlay,
        SKBitmap bloomOverlay,
        float[] wallColumns,
        float[] sceneLights,
        float[] sceneLightRadii,
        float[] sceneLightUpColors,
        float[] sceneLightDownColors,
        float[] sceneLightHeights,
        float[] doorwaySpills,
        float[] pickupSpotlights,
        float[] playerPosition,
        float[] cameraDirection,
        float[] cameraPlane,
        float muzzleFlash,
        float weaponFlash,
        float navigationGuideVisibility,
        float navigationGuideTime,
        float playerAmbientScale,
        float areaAmbientMaximum,
        float areaAmbientPixelsPerTile,
        float[] fogColor,
        float[] fogParameters,
        float viewBob,
        float weaponSway,
        float damageFlash,
        float damageTrauma,
        float damageDirection,
        float damageSeed,
        float damageTint,
        float bloodAmount,
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
            using var heightShader = wallHeights.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
            using var materialShader = wallMaterials.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
            using var areaAmbientShader = areaAmbientMap.ToShader(
                SKShaderTileMode.Clamp,
                SKShaderTileMode.Clamp);
            using var navigationRouteShader = navigationRouteMap.ToShader(
                SKShaderTileMode.Clamp,
                SKShaderTileMode.Clamp);
            using var overlayShader = spriteOverlay.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
            using var weaponShader = weaponOverlay.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
            using var bloomShader = bloomOverlay.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
            var uniforms = new SKRuntimeEffectUniforms(effect)
            {
                ["outputResolution"] = new[] { (float)Bounds.Width, (float)Bounds.Height },
                ["wallColumns"] = wallColumns,
                ["sceneLights"] = sceneLights,
                ["sceneLightRadii"] = sceneLightRadii,
                ["sceneLightUpColors"] = sceneLightUpColors,
                ["sceneLightDownColors"] = sceneLightDownColors,
                ["sceneLightHeights"] = sceneLightHeights,
                ["doorwaySpills"] = doorwaySpills,
                ["pickupSpotlights"] = pickupSpotlights,
                ["playerPosition"] = playerPosition,
                ["cameraDirection"] = cameraDirection,
                ["cameraPlane"] = cameraPlane,
                ["muzzleFlash"] = muzzleFlash,
                ["weaponFlash"] = weaponFlash,
                ["navigationGuideVisibility"] = navigationGuideVisibility,
                ["navigationGuideTime"] = navigationGuideTime,
                ["playerAmbientScale"] = playerAmbientScale,
                ["areaAmbientMaximum"] = areaAmbientMaximum,
                ["areaAmbientPixelsPerTile"] = areaAmbientPixelsPerTile,
                ["fogColor"] = fogColor,
                ["fogParameters"] = fogParameters,
                ["viewBob"] = viewBob,
                ["weaponSway"] = weaponSway,
                ["damageFlash"] = damageFlash,
                ["damageTrauma"] = damageTrauma,
                ["damageDirection"] = damageDirection,
                ["damageSeed"] = damageSeed,
                ["damageTint"] = damageTint,
                ["bloodAmount"] = bloodAmount,
                ["deathFade"] = deathFade,
                ["levelFade"] = levelFade,
                ["ceilingColor"] = ceilingColor,
                ["floorColor"] = floorColor,
                ["deathColor"] = deathColor
            };
            var children = new SKRuntimeEffectChildren(effect)
            {
                ["wallTextureAtlas"] = textureShader,
                ["wallHeightAtlas"] = heightShader,
                ["wallMaterialMap"] = materialShader,
                ["areaAmbientMap"] = areaAmbientShader,
                ["navigationRouteMap"] = navigationRouteShader,
                ["softwareSpriteOverlay"] = overlayShader,
                ["softwareWeaponOverlay"] = weaponShader,
                ["softwareBloomOverlay"] = bloomShader
            };
            using var shader = effect.ToShader(false, uniforms, children);
            using var paint = new SKPaint { Shader = shader, IsAntialias = false };
            lease.SkCanvas.DrawRect(SKRect.Create((float)Bounds.Width, (float)Bounds.Height), paint);
        }
    }

    private readonly record struct BloomProfile(
        double CenterX,
        double CenterY,
        double RadiusScale,
        double IntensityScale,
        RgbaColor Color);

    private readonly record struct WallMaterial(
        double BumpStrength,
        double SpecularStrength,
        double Gloss);
}
