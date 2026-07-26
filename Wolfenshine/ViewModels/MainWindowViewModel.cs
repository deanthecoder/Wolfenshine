// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core.ViewModels;
using DTC.Core;
using Wolfenshine.Game;
using Wolfenshine.Graphics;
using Wolfenshine.Maps;
using Wolfenshine.Rendering;
using Wolfenshine.Resources;

namespace Wolfenshine.ViewModels;

/// <summary>
/// Supplies the initial state for the main Wolfenshine window.
/// </summary>
/// <remarks>
/// The native viewport remains 320 x 200, while its presentation size accounts for DOS-era non-square pixels.
/// </remarks>
public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly GameSession m_gameSession;
    private readonly WolfensteinHudGraphics m_hudGraphics;
    private RaycastCamera m_camera;
    private WolfensteinSprite m_weaponSprite;
    private WolfensteinGraphic m_statusBar;
    private PlayerWeapon m_hudWeapon;
    private int m_hudAmmo = -1;
    private int m_hudScore = -1;
    private int m_hudHealth = -1;
    private int m_actorRevision = -1;
    private IReadOnlyList<WorldSprite> m_worldObjects = [];

    public MainWindowViewModel()
        : this(new WolfensteinDataNotFoundException(
            WolfensteinResourceLocator.GetDefaultDirectory(),
            WolfensteinResources.FileNames.Values.ToArray()))
    {
    }

    public MainWindowViewModel(
        WolfensteinResources resources,
        WolfensteinMapSet maps,
        WolfensteinWallTextures wallTextures = null,
        WolfensteinPalette palette = null,
        WolfensteinSprite weaponSprite = null,
        WolfensteinSpriteSet sprites = null,
        WolfensteinHudGraphics hudGraphics = null)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(maps);
        Resources = resources;
        Maps = maps;
        WallTextures = wallTextures;
        Palette = palette;
        Sprites = sprites;
        m_hudGraphics = hudGraphics;
        SelectedMap = maps.Maps.FirstOrDefault();
        Actors = SelectedMap == null ? [] : WolfensteinActors.FromMap(SelectedMap);
        if (SelectedMap != null)
        {
            m_camera = RaycastCamera.FromPlayerStart(SelectedMap);
            m_gameSession = new GameSession(SelectedMap, m_camera, Actors);
            StaticObjects = m_gameSession.StaticObjects;
            m_worldObjects = CreateWorldObjects();
            m_weaponSprite = sprites?.GetWeaponFrame(m_gameSession.Weapon, m_gameSession.WeaponFrame) ?? weaponSprite;
            UpdateHud();
        }
        else
        {
            StaticObjects = [];
        }
        StatusText = SelectedMap == null
            ? "Wolfenstein 3D data loaded, but it contains no maps"
            : $"{SelectedMap.Name} · arrows move and turn · Alt strafes · Shift runs · Command fires · 1–4 select weapons · Space opens doors";
    }

    public MainWindowViewModel(WolfensteinDataNotFoundException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        StatusText = "Wolfenstein 3D data files were not found";
        DataErrorMessage =
            $"Copy the required original game and palette files into:\n{exception.Directory.FullName}\n\n" +
            $"Missing: {string.Join(", ", exception.MissingFileNames)}";
    }

    public string Title => "Wolfenshine";
    public WolfensteinResources Resources { get; }
    public WolfensteinMapSet Maps { get; }
    public WolfensteinMap SelectedMap { get; }
    public RaycastCamera Camera => m_camera;
    public WolfensteinDoors Doors => m_gameSession?.Doors;
    public WolfensteinPushWalls PushWalls => m_gameSession?.PushWalls;
    public WolfensteinWallTextures WallTextures { get; }
    public WolfensteinPalette Palette { get; }
    public WolfensteinSprite WeaponSprite => m_weaponSprite;
    public WolfensteinSpriteSet Sprites { get; }
    public WolfensteinGraphic StatusBar => m_statusBar;
    public IReadOnlyList<WorldSprite> StaticObjects { get; }
    public IReadOnlyList<WolfensteinActor> Actors { get; }
    public IReadOnlyList<WorldSprite> WorldObjects => m_worldObjects;
    public string StatusText { get; }
    public string DataErrorMessage { get; }
    public bool HasGameData => Resources != null;
    public int NativeViewportWidth => 320;
    public int NativeViewportHeight => 200;
    public int PresentationViewportWidth => 320;
    public int PresentationViewportHeight => 240;

    public void UpdateGame(double elapsedSeconds, PlayerInput input)
    {
        if (m_gameSession?.Update(elapsedSeconds, input) == true)
        {
            SetField(ref m_camera, m_gameSession.Camera, nameof(Camera));
            if (m_worldObjects.Count != StaticObjects.Count + Actors.Count ||
                m_actorRevision != m_gameSession.ActorRevision)
            {
                m_actorRevision = m_gameSession.ActorRevision;
                SetField(ref m_worldObjects, CreateWorldObjects(), nameof(WorldObjects));
            }
            if (Sprites != null)
            {
                var sprite = Sprites.GetWeaponFrame(m_gameSession.Weapon, m_gameSession.WeaponFrame);
                SetField(ref m_weaponSprite, sprite, nameof(WeaponSprite));
            }
            UpdateHud();
        }
    }

#if DEBUG
    public void ReloadDebugState()
    {
        if (m_gameSession?.ReloadDebugState() != true)
            return;
        UpdateHud();
        Logger.Instance.Info("Wolfenshine debug reload: health 100, ammo 99.");
    }

    public void DumpDebugInfo()
    {
        if (m_gameSession == null)
        {
            Logger.Instance.Info("Wolfenshine debug snapshot: no active game session.");
            return;
        }

        Logger.Instance.Info(
            $"Wolfenshine debug snapshot: map {SelectedMap.Slot} ({SelectedMap.Name}), " +
            $"camera ({Camera.X:0.000}, {Camera.Y:0.000}), direction ({Camera.DirectionX:0.000}, {Camera.DirectionY:0.000}), " +
            $"plane ({Camera.PlaneX:0.000}, {Camera.PlaneY:0.000}).");
        Logger.Instance.Info(
            $"Player weapon {m_gameSession.Weapon}, frame {m_gameSession.WeaponFrame}, " +
            $"attacking {m_gameSession.IsAttacking}, health {m_gameSession.Health}, ammo {m_gameSession.Ammo}, " +
            $"score {m_gameSession.Score}, treasure {m_gameSession.TreasureCount}, " +
            $"secrets {m_gameSession.SecretCount}/{m_gameSession.SecretTotal}.");
        foreach (var wall in m_gameSession.PushWalls.Items)
        {
            Logger.Instance.Info(
                $"Pushwall origin ({wall.OriginX}, {wall.OriginY}), position ({wall.X:0.000}, {wall.Y:0.000}), " +
                $"distance {wall.Distance:0.000}, moving {wall.IsMoving}.");
        }
        foreach (var door in m_gameSession.Doors.Items)
        {
            Logger.Instance.Info(
                $"Door ({door.X}, {door.Y}), tile {door.Tile}, orientation {door.Orientation}, " +
                $"open {door.OpenAmount:0.000}, opening {door.IsOpening}, closing {door.IsClosing}.");
        }

        foreach (var actor in Actors.OrderBy(actor => DistanceToCamera(actor.X, actor.Y)))
        {
            WorldSprite[] sprite = [actor.ToWorldSprite()];
            var projected = new ProjectedWorldSprite[1];
            var projectedCount = WorldSpriteProjector.Project(sprite, Camera, 320, 160, 200, projected);
            var projection = projectedCount == 0
                ? "not projected"
                : $"sprite {projected[0].SpriteNumber}, screen x {projected[0].CenterX}, " +
                  $"depth {projected[0].Depth:0.000}, size {projected[0].RenderedSize}";
            var nearbyDecorations = StaticObjects
                .Where(item => Math.Abs(item.X - actor.X) <= 2.0 && Math.Abs(item.Y - actor.Y) <= 2.0)
                .Select(item => $"{item.SpriteNumber}@({item.X:0.0},{item.Y:0.0})");
            Logger.Instance.Info(
                $"Actor {actor.Type} at ({actor.X:0.0}, {actor.Y:0.0}), distance {DistanceToCamera(actor.X, actor.Y):0.000}, " +
                $"direction {actor.Direction}, base sprite {actor.BaseSpriteNumber}, patrol {actor.IsPatrolling}, " +
                $"ambush {actor.IsAmbush}; {projection}; nearby decorations [{string.Join(", ", nearbyDecorations)}].");
        }
    }
#endif

    private void UpdateHud()
    {
        if (m_hudGraphics == null || m_gameSession == null ||
            m_hudWeapon == m_gameSession.Weapon && m_hudAmmo == m_gameSession.Ammo &&
            m_hudScore == m_gameSession.Score && m_hudHealth == m_gameSession.Health)
        {
            return;
        }
        m_hudWeapon = m_gameSession.Weapon;
        m_hudAmmo = m_gameSession.Ammo;
        m_hudScore = m_gameSession.Score;
        m_hudHealth = m_gameSession.Health;
        SetField(
            ref m_statusBar,
            m_hudGraphics.Render(m_hudWeapon, m_hudAmmo, m_hudScore, m_hudHealth),
            nameof(StatusBar));
    }

    private IReadOnlyList<WorldSprite> CreateWorldObjects() =>
        StaticObjects.Concat(m_gameSession.ActorSprites).ToArray();

#if DEBUG
    private double DistanceToCamera(double x, double y)
    {
        var deltaX = x - Camera.X;
        var deltaY = y - Camera.Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
#endif
}
