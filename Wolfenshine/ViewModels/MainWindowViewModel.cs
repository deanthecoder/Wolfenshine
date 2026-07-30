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
    private WolfensteinAudioPlayer m_audioPlayer;
    private readonly WolfenshineSettings m_settings;
    private readonly WolfensteinHudGraphics m_hudGraphics;
    private readonly AccessibleLightCache m_accessibleLightCache = new();
    private double m_bjAnimationTime;
    private int m_bjFrame;
    private bool m_bjHasBreathed;
    private RaycastCamera m_camera;
    private WolfensteinSprite m_weaponSprite;
    private WolfensteinGraphic m_statusBar;
    private PlayerWeapon m_hudWeapon;
    private int m_hudAmmo = -1;
    private int m_hudScore = -1;
    private int m_hudHealth = -1;
    private int m_hudFace = -1;
    private int m_hudLives = -1;
    private bool m_hudGoldKey;
    private bool m_hudSilverKey;
    private int m_actorRevision = -1;
    private int m_restartRevision;
    private bool m_isGameOver;
    private double m_deathFade;
    private double m_damageFlash;
    private double m_damageTrauma;
    private double m_damageDirection;
    private int m_damageRevision;
    private int m_health = 100;
    private double m_bloodAmount;
    private double m_damageTint;
    private double m_muzzleFlash;
    private bool m_isWeaponFlashFrame;
    private double m_levelFade;
    private double m_playerSpeed;
    private bool m_isShowingLevelStats;
    private bool m_statsInputReleased;
    private WolfensteinLevelStats m_levelStats;
    private WolfensteinLevelStats m_finalLevelStats;
    private int m_statsStage;
    private double m_statsCountTime;
    private WolfensteinElevatorSwitch m_elevatorSwitch;
    private IReadOnlyList<WorldSprite> m_worldObjects = [];
    private IReadOnlyList<WorldSprite> m_accessibleLightObjects = [];
    private IReadOnlyList<WorldSprite> m_accessibleEnvironmentalEffects = [];
    private IReadOnlyList<WorldLight> m_enemyMuzzleFlashes = [];
    private bool m_isSelectingDifficulty;
    private bool m_difficultyInputReleased = true;
    private int m_selectedDifficultyIndex = 2;
    private GameDifficulty m_difficulty = GameDifficulty.Normal;
    private bool m_isPaused;
    private bool m_isEnhancedRendering;
    private bool m_isDisposed;

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
        WolfenshineSettings settings = null,
        WolfensteinIntermissionGraphics intermissionGraphics = null,
        WolfensteinDifficultyGraphics difficultyGraphics = null)
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
        m_isEnhancedRendering = settings?.UseEnhancedRenderer == true;
        IntermissionGraphics = intermissionGraphics;
        DifficultyGraphics = difficultyGraphics;
        m_weaponSprite = weaponSprite;
        SelectedMap = maps.Maps.FirstOrDefault();
        if (SelectedMap != null)
        {
            m_isSelectingDifficulty = true;
            Actors = [];
            StaticObjects = [];
            m_audioPlayer?.PlayMusicTrack(14);
        }
        else
        {
            Actors = [];
            StaticObjects = [];
        }
        StatusText = SelectedMap == null
            ? "Wolfenstein 3D data loaded, but it contains no maps"
            : "Select difficulty · arrows choose · Enter, Space, or Command starts";
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
    public WolfensteinIntermissionGraphics IntermissionGraphics { get; }
    public WolfensteinDifficultyGraphics DifficultyGraphics { get; }
    public WolfensteinGraphic PauseGraphic => DifficultyGraphics?.Pause;
    public IReadOnlyList<WorldSprite> StaticObjects { get; private set; }
    public IReadOnlyList<WolfensteinActor> Actors { get; private set; }
    public IReadOnlyList<WorldSprite> WorldObjects => m_worldObjects;
    public IReadOnlyList<WorldSprite> AccessibleLightObjects => m_accessibleLightObjects;
    public IReadOnlyList<WorldSprite> AccessibleEnvironmentalEffects => m_accessibleEnvironmentalEffects;
    public IReadOnlyList<WorldLight> EnemyMuzzleFlashes => m_enemyMuzzleFlashes;
    public string StatusText { get; private set; }
    public string DataErrorMessage { get; }
    public bool HasGameData => Resources != null;
    public bool IsGameOver => m_isGameOver;
    public double DeathFade => m_deathFade;
    public double DamageFlash => m_damageFlash;
    public double DamageTrauma => m_damageTrauma;
    public double DamageDirection => m_damageDirection;
    public int DamageRevision => m_damageRevision;
    public int Health => m_health;
    public double BloodAmount => m_bloodAmount;
    public double DamageTint => m_damageTint;
    public double MuzzleFlash => m_muzzleFlash;
    public bool IsWeaponFlashFrame => m_isWeaponFlashFrame;
    public double LevelFade => m_levelFade;
    public double PlayerSpeed => m_playerSpeed;
    public bool IsShowingLevelStats => m_isShowingLevelStats;
    public bool IsSelectingDifficulty => m_isSelectingDifficulty;
    public bool IsPaused => m_isPaused;
    public bool HasGoldKey => m_gameSession?.HasGoldKey == true;
    public bool HasSilverKey => m_gameSession?.HasSilverKey == true;
    public bool IsEnhancedRendering => m_isEnhancedRendering;
    public bool IsAuthenticRendering => HasGameData && !m_isEnhancedRendering;
    public string RenderModeText => m_isEnhancedRendering ? "RENDERER: ENHANCED" : "RENDERER: AUTHENTIC";
    public int SelectedDifficultyIndex => m_selectedDifficultyIndex;
    public GameDifficulty Difficulty => m_difficulty;
    public WolfensteinLevelStats LevelStats => m_levelStats;
    public int BjFrame => m_bjFrame;
    public int NativeViewportWidth => 320;
    public int NativeViewportHeight => 200;
    public int PresentationViewportWidth => 320;
    public int PresentationViewportHeight => 240;

    public void UpdateGame(double elapsedSeconds, PlayerInput input)
    {
        if (m_isPaused)
            return;
        if (m_isSelectingDifficulty)
        {
            UpdateDifficultySelection(input);
            return;
        }
        if (m_isShowingLevelStats)
        {
            UpdateStatsAnimation(elapsedSeconds);
            if (!HasInput(input))
                m_statsInputReleased = true;
            else if (m_statsInputReleased)
            {
                if (m_statsStage < 4)
                {
                    FinishStatsAnimation();
                    m_statsInputReleased = false;
                }
                else
                    AdvanceMap();
            }
            return;
        }
        if (m_gameSession?.Update(elapsedSeconds, input, m_isEnhancedRendering) != true)
            return;
        RefreshAccessibleLights();
        foreach (var soundEvent in m_gameSession.DrainSoundEvents())
            m_audioPlayer?.Play(soundEvent, m_gameSession.Camera);
        if (m_gameSession.IsGameOver)
        {
            ReturnToDifficultySelection();
            return;
        }
        if (m_gameSession.IsReadyForNextLevel)
        {
            BeginLevelStats();
            return;
        }
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
        SetField(ref m_damageFlash, m_gameSession.DamageFlash, nameof(DamageFlash));
        SetField(ref m_damageTrauma, m_gameSession.DamageTrauma, nameof(DamageTrauma));
        SetField(ref m_damageDirection, m_gameSession.DamageDirection, nameof(DamageDirection));
        SetField(ref m_damageRevision, m_gameSession.DamageRevision, nameof(DamageRevision));
        SetField(ref m_health, m_gameSession.Health, nameof(Health));
        SetField(ref m_bloodAmount, m_gameSession.BloodAmount, nameof(BloodAmount));
        SetField(ref m_damageTint, m_gameSession.DamageTint, nameof(DamageTint));
        SetField(ref m_muzzleFlash, m_gameSession.MuzzleFlash, nameof(MuzzleFlash));
        SetField(
            ref m_isWeaponFlashFrame,
            m_gameSession.IsWeaponFlashFrame,
            nameof(IsWeaponFlashFrame));
        SetField(ref m_levelFade, m_gameSession.LevelFade, nameof(LevelFade));
        SetField(ref m_playerSpeed, m_gameSession.PlayerSpeed, nameof(PlayerSpeed));
        SetField(ref m_elevatorSwitch, m_gameSession.ElevatorSwitch, nameof(ElevatorSwitch));
        SetField(
            ref m_enemyMuzzleFlashes,
            m_gameSession.EnemyMuzzleFlashes,
            nameof(EnemyMuzzleFlashes));
        var staticObjectsChanged = m_worldObjects.Count != StaticObjects.Count + Actors.Count;
        if (staticObjectsChanged ||
            m_actorRevision != m_gameSession.ActorRevision)
        {
            m_actorRevision = m_gameSession.ActorRevision;
            SetField(ref m_worldObjects, CreateWorldObjects(), nameof(WorldObjects));
            if (staticObjectsChanged)
                OnPropertyChanged(nameof(StaticObjects));
        }
        if (Sprites != null)
        {
            var sprite = Sprites.GetWeaponFrame(m_gameSession.Weapon, m_gameSession.WeaponFrame);
            SetField(ref m_weaponSprite, sprite, nameof(WeaponSprite));
        }
        UpdateHud();
        SetField(ref m_isGameOver, m_gameSession.IsGameOver, nameof(IsGameOver));
    }

    public void TogglePause()
    {
        if (m_gameSession == null || m_isSelectingDifficulty || m_isShowingLevelStats || m_gameSession.IsDying)
            return;
        SetField(ref m_isPaused, !m_isPaused, nameof(IsPaused));
        m_audioPlayer?.SetPaused(m_isPaused);
        StatusText = m_isPaused
            ? "Paused · press P to continue"
            : $"{SelectedMap.Name} · arrows move and turn · Alt strafes · Shift runs · Command fires · " +
              "1–4 select weapons · Space opens doors";
        OnPropertyChanged(nameof(StatusText));
    }

    /// <summary>
    /// Attaches audio after background sound decoding has completed.
    /// </summary>
    public void AttachAudioPlayer(WolfensteinAudioPlayer audioPlayer)
    {
        ArgumentNullException.ThrowIfNull(audioPlayer);
        if (m_isDisposed)
        {
            audioPlayer.Dispose();
            return;
        }
        m_audioPlayer?.Dispose();
        m_audioPlayer = audioPlayer;
        if (m_isSelectingDifficulty)
            m_audioPlayer.PlayMusicTrack(14);
        else if (SelectedMap != null)
            m_audioPlayer.PlayMusic(SelectedMap.Slot);
    }

    public void ToggleRenderer()
    {
        if (m_gameSession == null || m_isSelectingDifficulty || m_isShowingLevelStats)
            return;
        SetField(ref m_isEnhancedRendering, !m_isEnhancedRendering, nameof(IsEnhancedRendering));
        if (m_settings != null)
            m_settings.UseEnhancedRenderer = m_isEnhancedRendering;
        OnPropertyChanged(nameof(IsAuthenticRendering));
        OnPropertyChanged(nameof(RenderModeText));
        StatusText = m_isEnhancedRendering
            ? "Enhanced shader renderer · F2 returns to authentic rendering"
            : "Authentic software renderer · F2 enables enhanced rendering";
        OnPropertyChanged(nameof(StatusText));
    }

    private void UpdateDifficultySelection(PlayerInput input)
    {
        var hasInput = HasInput(input);
        if (!hasInput)
        {
            m_difficultyInputReleased = true;
            return;
        }
        if (!m_difficultyInputReleased)
            return;
        m_difficultyInputReleased = false;

        if (input.MoveForward || input.TurnLeft)
        {
            SetField(
                ref m_selectedDifficultyIndex,
                (m_selectedDifficultyIndex + 3) % 4,
                nameof(SelectedDifficultyIndex));
            PlayMenuSound(WolfensteinSoundEffect.MenuMove);
            return;
        }
        if (input.MoveBackward || input.TurnRight)
        {
            SetField(
                ref m_selectedDifficultyIndex,
                (m_selectedDifficultyIndex + 1) % 4,
                nameof(SelectedDifficultyIndex));
            PlayMenuSound(WolfensteinSoundEffect.MenuMove);
            return;
        }
        if (!input.Use && !input.Attack)
            return;

        m_difficulty = (GameDifficulty)m_selectedDifficultyIndex;
        PlayMenuSound(WolfensteinSoundEffect.MenuSelect);
        SetField(ref m_isSelectingDifficulty, false, nameof(IsSelectingDifficulty));
        StartMap(SelectedMap, null, startFaded: true);
        NotifyMapChanged();
    }

    private void PlayMenuSound(WolfensteinSoundEffect effect)
    {
        if (SelectedMap == null)
            return;
        m_audioPlayer?.Play(new WolfensteinSoundEvent(effect), RaycastCamera.FromPlayerStart(SelectedMap));
    }

    private void ReturnToDifficultySelection()
    {
        if (m_isPaused)
            m_audioPlayer?.SetPaused(false);
        m_isPaused = false;
        m_gameSession = null;
        SelectedMap = Maps.Maps.FirstOrDefault();
        Actors = [];
        StaticObjects = [];
        SetField(ref m_worldObjects, [], nameof(WorldObjects));
        SetField(ref m_accessibleLightObjects, [], nameof(AccessibleLightObjects));
        SetField(ref m_accessibleEnvironmentalEffects, [], nameof(AccessibleEnvironmentalEffects));
        SetField(ref m_camera, null, nameof(Camera));
        SetField(ref m_elevatorSwitch, null, nameof(ElevatorSwitch));
        SetField(ref m_deathFade, 0.0, nameof(DeathFade));
        SetField(ref m_damageFlash, 0.0, nameof(DamageFlash));
        SetField(ref m_damageTrauma, 0.0, nameof(DamageTrauma));
        SetField(ref m_damageDirection, 0.0, nameof(DamageDirection));
        SetField(ref m_damageRevision, 0, nameof(DamageRevision));
        SetField(ref m_health, 100, nameof(Health));
        SetField(ref m_bloodAmount, 0.0, nameof(BloodAmount));
        SetField(ref m_damageTint, 0.0, nameof(DamageTint));
        SetField(ref m_levelFade, 0.0, nameof(LevelFade));
        SetField(ref m_playerSpeed, 0.0, nameof(PlayerSpeed));
        SetField(ref m_isGameOver, false, nameof(IsGameOver));
        SetField(ref m_isSelectingDifficulty, true, nameof(IsSelectingDifficulty));
        m_difficultyInputReleased = false;
        m_audioPlayer?.PlayMusicTrack(14);
        m_audioPlayer?.SetMusicFade(0.0);
        StatusText = "Select difficulty · arrows choose · Enter, Space, or Command starts";
        NotifyMapChanged();
    }

    public void Dispose()
    {
        if (m_isDisposed)
            return;
        m_isDisposed = true;
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

    private void BeginLevelStats()
    {
        var stats = m_gameSession.CreateLevelStats();
        m_gameSession.ApplyLevelBonus(stats.Bonus);
        UpdateHud();
        m_finalLevelStats = stats;
        SetField(
            ref m_levelStats,
            stats with { KillRatio = 0, SecretRatio = 0, TreasureRatio = 0, Bonus = 0 },
            nameof(LevelStats));
        SetField(ref m_isShowingLevelStats, true, nameof(IsShowingLevelStats));
        SetField(ref m_levelFade, 0.0, nameof(LevelFade));
        m_bjAnimationTime = 0.0;
        m_bjHasBreathed = false;
        SetField(ref m_bjFrame, 0, nameof(BjFrame));
        m_statsStage = 0;
        m_statsCountTime = 0.0;
        m_statsInputReleased = false;
        m_audioPlayer?.PlayMusicTrack(16);
        m_audioPlayer?.SetMusicFade(0.0);
        StatusText = "Level complete · release the controls, then press any gameplay key to continue";
        OnPropertyChanged(nameof(StatusText));
    }

    private void UpdateStatsAnimation(double elapsedSeconds)
    {
        m_bjAnimationTime += elapsedSeconds;
        var frameDuration = m_bjHasBreathed ? 35.0 / 70.0 : 10.0 / 70.0;
        if (m_bjAnimationTime >= frameDuration)
        {
            m_bjAnimationTime %= frameDuration;
            m_bjHasBreathed = true;
            SetField(ref m_bjFrame, m_bjFrame ^ 1, nameof(BjFrame));
        }

        m_statsCountTime += elapsedSeconds;
        while (m_statsCountTime >= 0.02 && m_statsStage < 4)
        {
            m_statsCountTime -= 0.02;
            AdvanceStatsCount();
        }
    }

    private void AdvanceStatsCount()
    {
        var current = m_levelStats;
        var target = m_statsStage switch
        {
            0 => m_finalLevelStats.TimeBonus / 500,
            1 => m_finalLevelStats.KillRatio,
            2 => m_finalLevelStats.SecretRatio,
            _ => m_finalLevelStats.TreasureRatio
        };
        var value = m_statsStage switch
        {
            0 => current.Bonus / 500,
            1 => current.KillRatio,
            2 => current.SecretRatio,
            _ => current.TreasureRatio
        };
        if (value < target)
        {
            value++;
            current = m_statsStage switch
            {
                0 => current with { Bonus = value * 500 },
                1 => current with { KillRatio = value },
                2 => current with { SecretRatio = value },
                _ => current with { TreasureRatio = value }
            };
            SetField(ref m_levelStats, current, nameof(LevelStats));
            if (value % 10 == 0)
                PlayStatsSound(WolfensteinSoundEffect.EndBonusTick);
            return;
        }

        if (m_statsStage > 0 && target == 100)
        {
            SetField(ref m_levelStats, current with { Bonus = current.Bonus + 10000 }, nameof(LevelStats));
            PlayStatsSound(WolfensteinSoundEffect.PerfectRatio);
        }
        else
            PlayStatsSound(target == 0 ? WolfensteinSoundEffect.NoBonus : WolfensteinSoundEffect.EndBonusDone);
        m_statsStage++;
    }

    private void FinishStatsAnimation()
    {
        SetField(ref m_levelStats, m_finalLevelStats, nameof(LevelStats));
        m_statsStage = 4;
        PlayStatsSound(WolfensteinSoundEffect.EndBonusDone);
    }

    private void PlayStatsSound(WolfensteinSoundEffect effect)
    {
        if (Camera != null)
            m_audioPlayer?.Play(new WolfensteinSoundEvent(effect), Camera);
    }

    private static bool HasInput(PlayerInput input) =>
        input.MoveForward || input.MoveBackward || input.TurnLeft || input.TurnRight ||
        input.Use || input.Run || input.Attack || input.Strafe || input.WeaponSelection != null;

    private void NotifyMapChanged()
    {
        OnPropertyChanged(nameof(SelectedMap));
        OnPropertyChanged(nameof(Camera));
        OnPropertyChanged(nameof(Actors));
        OnPropertyChanged(nameof(StaticObjects));
        OnPropertyChanged(nameof(WorldObjects));
        OnPropertyChanged(nameof(AccessibleLightObjects));
        OnPropertyChanged(nameof(AccessibleEnvironmentalEffects));
        OnPropertyChanged(nameof(WeaponSprite));
        OnPropertyChanged(nameof(StatusBar));
        OnPropertyChanged(nameof(Doors));
        OnPropertyChanged(nameof(PushWalls));
        OnPropertyChanged(nameof(ElevatorSwitch));
        OnPropertyChanged(nameof(HasGoldKey));
        OnPropertyChanged(nameof(HasSilverKey));
        OnPropertyChanged(nameof(StatusText));
    }

    private void StartMap(
        WolfensteinMap map,
        WolfensteinPlayerState? playerState,
        bool startFaded,
        RaycastCamera camera = null)
    {
        SetField(ref m_isShowingLevelStats, false, nameof(IsShowingLevelStats));
        SetField(ref m_levelStats, null, nameof(LevelStats));
        SelectedMap = map;
        Actors = WolfensteinActors.FromMap(map, m_difficulty);
        m_camera = camera ?? RaycastCamera.FromPlayerStart(map);
        m_gameSession = new GameSession(
            map,
            m_camera,
            Actors,
            m_difficulty,
            playerState,
            startFaded);
        m_elevatorSwitch = m_gameSession.ElevatorSwitch;
        StaticObjects = m_gameSession.StaticObjects;
        RefreshAccessibleLights(force: true);
        m_actorRevision = m_gameSession.ActorRevision;
        m_restartRevision = m_gameSession.RestartRevision;
        m_audioPlayer?.PlayMusic(map.Slot);
        m_audioPlayer?.SetMusicFade(startFaded ? 1.0 : 0.0);
        m_worldObjects = CreateWorldObjects();
        m_enemyMuzzleFlashes = [];
        SetField(ref m_damageFlash, 0.0, nameof(DamageFlash));
        SetField(ref m_damageTrauma, 0.0, nameof(DamageTrauma));
        SetField(ref m_damageDirection, 0.0, nameof(DamageDirection));
        SetField(ref m_damageRevision, 0, nameof(DamageRevision));
        SetField(ref m_health, m_gameSession.Health, nameof(Health));
        SetField(ref m_bloodAmount, 0.0, nameof(BloodAmount));
        SetField(ref m_damageTint, 0.0, nameof(DamageTint));
        m_isWeaponFlashFrame = false;
        m_playerSpeed = 0.0;
        m_weaponSprite = Sprites?.GetWeaponFrame(m_gameSession.Weapon, m_gameSession.WeaponFrame) ?? m_weaponSprite;
        StatusText = $"{map.Name} · arrows move and turn · Alt strafes · Shift runs · Command fires · " +
                     "1–4 select weapons · Space opens doors";
        UpdateHud();
    }

    private void RefreshAccessibleLights(bool force = false)
    {
        if (m_gameSession == null ||
            !m_accessibleLightCache.Refresh(
                SelectedMap,
                m_gameSession.Doors,
                m_gameSession.PushWalls,
                m_gameSession.Camera,
                StaticObjects,
                force))
        {
            return;
        }

        SetField(
            ref m_accessibleLightObjects,
            m_accessibleLightCache.Lights,
            nameof(AccessibleLightObjects));
        SetField(
            ref m_accessibleEnvironmentalEffects,
            m_accessibleLightCache.EnvironmentalEffects,
            nameof(AccessibleEnvironmentalEffects));
    }

#if DEBUG
    /// <summary>
    /// Restarts play at the beginning of a nearby map while retaining the player's portable state.
    /// </summary>
    public void SkipDebugLevel(int offset)
    {
        if (m_gameSession == null || Maps.Maps.Count == 0 || offset == 0)
            return;
        var currentIndex = 0;
        for (var index = 0; index < Maps.Maps.Count; index++)
        {
            if (!ReferenceEquals(Maps.Maps[index], SelectedMap))
                continue;
            currentIndex = index;
            break;
        }
        var targetIndex = (currentIndex + offset) % Maps.Maps.Count;
        if (targetIndex < 0)
            targetIndex += Maps.Maps.Count;
        var targetMap = Maps.Maps[targetIndex];
        var playerState = m_gameSession.CapturePlayerState();
        StartMap(targetMap, playerState, startFaded: false);
        NotifyMapChanged();
        Logger.Instance.Info(
            $"Skipped to map {targetMap.Slot} ({targetMap.Name}) with the debug level shortcut.");
    }

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

        var playerAngle = (Math.Atan2(Camera.DirectionY, Camera.DirectionX) * 180.0 / Math.PI + 360.0) % 360.0;
        Logger.Instance.Info(
            $"Wolfenshine debug snapshot: map {SelectedMap.Slot} ({SelectedMap.Name}), " +
            $"player position ({Camera.X:0.000}, {Camera.Y:0.000}), angle {playerAngle:0.0} degrees, " +
            $"direction ({Camera.DirectionX:0.000}, {Camera.DirectionY:0.000}), " +
            $"plane ({Camera.PlaneX:0.000}, {Camera.PlaneY:0.000}).");
        Logger.Instance.Info(
            $"Player weapon {m_gameSession.Weapon}, frame {m_gameSession.WeaponFrame}, " +
            $"attacking {m_gameSession.IsAttacking}, health {m_gameSession.Health}, lives {m_gameSession.Lives}, " +
            $"ammo {m_gameSession.Ammo}, difficulty {m_gameSession.Difficulty}, " +
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
            m_hudFace == m_gameSession.FacePictureIndex && m_hudLives == m_gameSession.Lives &&
            m_hudGoldKey == m_gameSession.HasGoldKey && m_hudSilverKey == m_gameSession.HasSilverKey)
        {
            return;
        }
        m_hudWeapon = m_gameSession.Weapon;
        m_hudAmmo = m_gameSession.Ammo;
        m_hudScore = m_gameSession.Score;
        m_hudHealth = m_gameSession.Health;
        m_hudFace = m_gameSession.FacePictureIndex;
        m_hudLives = m_gameSession.Lives;
        var goldKeyChanged = m_hudGoldKey != m_gameSession.HasGoldKey;
        var silverKeyChanged = m_hudSilverKey != m_gameSession.HasSilverKey;
        m_hudGoldKey = m_gameSession.HasGoldKey;
        m_hudSilverKey = m_gameSession.HasSilverKey;
        if (goldKeyChanged)
            OnPropertyChanged(nameof(HasGoldKey));
        if (silverKeyChanged)
            OnPropertyChanged(nameof(HasSilverKey));
        SetField(
            ref m_statusBar,
            m_hudGraphics.Render(
                m_hudWeapon,
                m_hudAmmo,
                m_hudScore,
                m_hudHealth,
                m_hudFace,
                m_hudLives,
                m_hudGoldKey,
                m_hudSilverKey),
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
