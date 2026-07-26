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
using Wolfenshine.Audio;
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
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private GameSession m_gameSession;
    private readonly WolfensteinAudioPlayer m_audioPlayer;
    private readonly WolfenshineSettings m_settings;
    private readonly WolfensteinHudGraphics m_hudGraphics;
    private RaycastCamera m_camera;
    private WolfensteinSprite m_weaponSprite;
    private WolfensteinGraphic m_statusBar;
    private PlayerWeapon m_hudWeapon;
    private int m_hudAmmo = -1;
    private int m_hudScore = -1;
    private int m_hudHealth = -1;
    private int m_hudFace = -1;
    private int m_hudLives = -1;
    private int m_actorRevision = -1;
    private int m_restartRevision;
    private bool m_isGameOver;
    private double m_deathFade;
    private double m_levelFade;
    private WolfensteinElevatorSwitch m_elevatorSwitch;
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
        WolfensteinHudGraphics hudGraphics = null,
        WolfensteinAudioPlayer audioPlayer = null,
        WolfenshineSettings settings = null)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(maps);
        Resources = resources;
        Maps = maps;
        WallTextures = wallTextures;
        Palette = palette;
        Sprites = sprites;
        m_hudGraphics = hudGraphics;
        m_audioPlayer = audioPlayer;
        m_settings = settings;
        m_weaponSprite = weaponSprite;
        SelectedMap = maps.Maps.FirstOrDefault();
        if (SelectedMap != null)
            StartMap(SelectedMap, null, startFaded: false);
        else
        {
            Actors = [];
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
    public WolfensteinMap SelectedMap { get; private set; }
    public RaycastCamera Camera => m_camera;
    public WolfensteinDoors Doors => m_gameSession?.Doors;
    public WolfensteinPushWalls PushWalls => m_gameSession?.PushWalls;
    public WolfensteinElevatorSwitch ElevatorSwitch => m_elevatorSwitch;
    public WolfensteinWallTextures WallTextures { get; }
    public WolfensteinPalette Palette { get; }
    public WolfensteinSprite WeaponSprite => m_weaponSprite;
    public WolfensteinSpriteSet Sprites { get; }
    public WolfensteinGraphic StatusBar => m_statusBar;
    public IReadOnlyList<WorldSprite> StaticObjects { get; private set; }
    public IReadOnlyList<WolfensteinActor> Actors { get; private set; }
    public IReadOnlyList<WorldSprite> WorldObjects => m_worldObjects;
    public string StatusText { get; private set; }
    public string DataErrorMessage { get; }
    public bool HasGameData => Resources != null;
    public bool IsGameOver => m_isGameOver;
    public double DeathFade => m_deathFade;
    public double LevelFade => m_levelFade;
    public int NativeViewportWidth => 320;
    public int NativeViewportHeight => 200;
    public int PresentationViewportWidth => 320;
    public int PresentationViewportHeight => 240;

    public void UpdateGame(double elapsedSeconds, PlayerInput input)
    {
        if (m_gameSession?.Update(elapsedSeconds, input) != true)
            return;
        foreach (var soundEvent in m_gameSession.DrainSoundEvents())
            m_audioPlayer?.Play(soundEvent, m_gameSession.Camera);
        if (m_gameSession.IsReadyForNextLevel)
            AdvanceMap();
        if (m_restartRevision != m_gameSession.RestartRevision)
        {
            m_restartRevision = m_gameSession.RestartRevision;
            m_audioPlayer?.PlayMusic(SelectedMap.Slot);
            OnPropertyChanged(nameof(Doors));
            OnPropertyChanged(nameof(PushWalls));
        }
        m_audioPlayer?.SetMusicFade(Math.Max(m_gameSession.DeathFade, m_gameSession.LevelFade));
        SetField(ref m_camera, m_gameSession.Camera, nameof(Camera));
        SetField(ref m_deathFade, m_gameSession.DeathFade, nameof(DeathFade));
        SetField(ref m_levelFade, m_gameSession.LevelFade, nameof(LevelFade));
        SetField(ref m_elevatorSwitch, m_gameSession.ElevatorSwitch, nameof(ElevatorSwitch));
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
        SetField(ref m_isGameOver, m_gameSession.IsGameOver, nameof(IsGameOver));
    }

    public void Dispose()
    {
        m_audioPlayer?.Dispose();
        m_settings?.Dispose();
    }

    private void AdvanceMap()
    {
        var currentIndex = 0;
        for (var index = 0; index < Maps.Maps.Count; index++)
        {
            if (ReferenceEquals(Maps.Maps[index], SelectedMap))
            {
                currentIndex = index;
                break;
            }
        }
        var nextMap = Maps.Maps[(currentIndex + 1) % Maps.Maps.Count];
        var playerState = m_gameSession.CapturePlayerState();
        StartMap(nextMap, playerState, startFaded: true);
        NotifyMapChanged();
    }

    private void NotifyMapChanged()
    {
        OnPropertyChanged(nameof(SelectedMap));
        OnPropertyChanged(nameof(Camera));
        OnPropertyChanged(nameof(Actors));
        OnPropertyChanged(nameof(StaticObjects));
        OnPropertyChanged(nameof(WorldObjects));
        OnPropertyChanged(nameof(WeaponSprite));
        OnPropertyChanged(nameof(StatusBar));
        OnPropertyChanged(nameof(Doors));
        OnPropertyChanged(nameof(PushWalls));
        OnPropertyChanged(nameof(ElevatorSwitch));
        OnPropertyChanged(nameof(StatusText));
    }

    private void StartMap(
        WolfensteinMap map,
        WolfensteinPlayerState? playerState,
        bool startFaded,
        RaycastCamera camera = null)
    {
        SelectedMap = map;
        Actors = WolfensteinActors.FromMap(map, GameDifficulty.Normal);
        m_camera = camera ?? RaycastCamera.FromPlayerStart(map);
        m_gameSession = new GameSession(
            map,
            m_camera,
            Actors,
            GameDifficulty.Normal,
            playerState,
            startFaded);
        m_elevatorSwitch = m_gameSession.ElevatorSwitch;
        StaticObjects = m_gameSession.StaticObjects;
        m_actorRevision = m_gameSession.ActorRevision;
        m_restartRevision = m_gameSession.RestartRevision;
        m_audioPlayer?.PlayMusic(map.Slot);
        m_audioPlayer?.SetMusicFade(startFaded ? 1.0 : 0.0);
        m_worldObjects = CreateWorldObjects();
        m_weaponSprite = Sprites?.GetWeaponFrame(m_gameSession.Weapon, m_gameSession.WeaponFrame) ?? m_weaponSprite;
        StatusText = $"{map.Name} · arrows move and turn · Alt strafes · Shift runs · Command fires · " +
                     "1–4 select weapons · Space opens doors";
        UpdateHud();
    }

#if DEBUG
    public void SaveDebugPosition()
    {
        if (m_gameSession == null || m_settings == null)
            return;
        m_settings.SavedMapSlot = SelectedMap.Slot;
        m_settings.SavedX = Camera.X;
        m_settings.SavedY = Camera.Y;
        m_settings.SavedDirectionX = Camera.DirectionX;
        m_settings.SavedDirectionY = Camera.DirectionY;
        Logger.Instance.Info(
            $"Saved debug position on map {SelectedMap.Slot} at ({Camera.X:0.000}, {Camera.Y:0.000}).");
    }

    public void LoadDebugPosition()
    {
        if (m_settings == null || m_settings.SavedMapSlot < 0)
        {
            Logger.Instance.Info("No debug position has been saved.");
            return;
        }
        var map = Maps.Maps.FirstOrDefault(item => item.Slot == m_settings.SavedMapSlot);
        if (map == null)
        {
            Logger.Instance.Warn($"Saved debug map slot {m_settings.SavedMapSlot} is unavailable.");
            return;
        }
        var directionLength = Math.Sqrt(
            (m_settings.SavedDirectionX * m_settings.SavedDirectionX) +
            (m_settings.SavedDirectionY * m_settings.SavedDirectionY));
        if (directionLength <= double.Epsilon)
        {
            Logger.Instance.Warn("Saved debug direction is invalid.");
            return;
        }
        var directionX = m_settings.SavedDirectionX / directionLength;
        var directionY = m_settings.SavedDirectionY / directionLength;
        var camera = new RaycastCamera(
            m_settings.SavedX,
            m_settings.SavedY,
            directionX,
            directionY,
            -directionY * 0.66,
            directionX * 0.66);
        StartMap(map, null, startFaded: false, camera);
        NotifyMapChanged();
        Logger.Instance.Info(
            $"Loaded debug position on map {map.Slot} at ({camera.X:0.000}, {camera.Y:0.000}).");
    }

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
            $"attacking {m_gameSession.IsAttacking}, health {m_gameSession.Health}, lives {m_gameSession.Lives}, " +
            $"ammo {m_gameSession.Ammo}, " +
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

        foreach (var actorState in m_gameSession.Actors.OrderBy(actor => DistanceToCamera(actor.X, actor.Y)))
        {
            var actor = actorState.Actor;
            WorldSprite[] sprite = [actorState.ToWorldSprite()];
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
                $"Actor {actor.Type} at ({actorState.X:0.0}, {actorState.Y:0.0}), " +
                $"distance {DistanceToCamera(actorState.X, actorState.Y):0.000}, behavior {actorState.Behavior}, " +
                $"direction {actor.Direction}, base sprite {actor.BaseSpriteNumber}, patrol {actor.IsPatrolling}, " +
                $"ambush {actor.IsAmbush}; {projection}; nearby decorations [{string.Join(", ", nearbyDecorations)}].");
        }
    }
#endif

    private void UpdateHud()
    {
        if (m_hudGraphics == null || m_gameSession == null ||
            m_hudWeapon == m_gameSession.Weapon && m_hudAmmo == m_gameSession.Ammo &&
            m_hudScore == m_gameSession.Score && m_hudHealth == m_gameSession.Health &&
            m_hudFace == m_gameSession.FacePictureIndex && m_hudLives == m_gameSession.Lives)
        {
            return;
        }
        m_hudWeapon = m_gameSession.Weapon;
        m_hudAmmo = m_gameSession.Ammo;
        m_hudScore = m_gameSession.Score;
        m_hudHealth = m_gameSession.Health;
        m_hudFace = m_gameSession.FacePictureIndex;
        m_hudLives = m_gameSession.Lives;
        SetField(
            ref m_statusBar,
            m_hudGraphics.Render(
                m_hudWeapon,
                m_hudAmmo,
                m_hudScore,
                m_hudHealth,
                m_hudFace,
                m_hudLives),
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
