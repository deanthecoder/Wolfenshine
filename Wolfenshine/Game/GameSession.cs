// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Wolfenshine.Audio;
using Wolfenshine.Maps;
using Wolfenshine.Rendering;

namespace Wolfenshine.Game;

/// <summary>
/// Advances the current Wolfenshine level in response to player input.
/// </summary>
/// <remarks>
/// Movement and collision remain independent of the desktop game loop and can be tested deterministically.
/// </remarks>
public sealed class GameSession
{
    private const double OriginalTicksPerSecond = 70.0;
    private const double FixedUnitsPerTile = 65536.0;
    private const double WalkInput = 35.0;
    private const double RunInput = 70.0;
    private const double ForwardMovementScale = 150.0;
    private const double BackwardMovementScale = 100.0;
    private const double AngleScale = 20.0;
    private const double AttackFrameDuration = 6.0 / OriginalTicksPerSecond;
    private const int MaximumAmmo = 99;
    private const int MaximumHealth = 100;
    private const double PickupDistance = 0.5;
    private const double MinimumActorDistance = 1.0;
    private const double MinimumActorSeparation = 0.5;
    private const double PlayerRadius = 0.2;
    private const double OriginalPlayerRadius = 0x5800 / FixedUnitsPerTile;
    private const double MaximumMovementStep = 0.1;
    private const double MaximumMotionTimeStep = 1.0 / OriginalTicksPerSecond;
    private const double MovementAccelerationTime = 0.14;
    private const double MovementFrictionTime = 0.22;
    private const double TurnAccelerationTime = 0.10;
    private const double TurnFrictionTime = 0.14;
    private const int CombatViewportWidth = 320;
    private const int CrosshairHalfWidth = 20;
    private const double MinimumShootingDistance = 0.75;
    private const double DogAttackDistance = 1.5;
    private const double DeathFadeDuration = 70.0 / OriginalTicksPerSecond;
    private const double DeathDuration = 100.0 / OriginalTicksPerSecond;
    private const ushort ElevatorSwitchTile = 21;
    private const double LevelFadeDuration = 0.5;
    private const double WallHitSoundInterval = 0.2;
    private const double EnemyMuzzleFlashDuration = 6.0 / OriginalTicksPerSecond;
    private const double DeathRotationRadiansPerSecond = 140.0 * Math.PI / 180.0;
    private bool m_useWasDown;
    private double m_useRepeatTime;
    private bool m_attackWasDown;
    private int m_attackStep;
    private double m_attackTimeRemaining;
    private PlayerWeapon m_chosenWeapon = PlayerWeapon.Pistol;
    private PlayerWeapon m_bestWeapon = PlayerWeapon.Pistol;
    private bool m_playerMadeNoise;
    private double m_faceTime;
    private double m_nextFaceChange = 1.0;
    private double m_chaingunGrinTime;
    private int m_faceFrame;
    private uint m_randomState = 0x5f3759df;
    private double m_levelFade;
    private bool m_isCompletingLevel;
    private bool m_isFadingIn;
    private double m_deathTime;
    private double m_damageCount;
    private double m_wallHitSoundTime;
    private double m_velocityX;
    private double m_velocityY;
    private double m_angularVelocity;
    private double? m_killerX;
    private double? m_killerY;
    private int m_keyMask;
    private readonly RaycastCamera m_startCamera;
    private readonly IReadOnlyList<WolfensteinActor> m_actorDefinitions;
    private IReadOnlyList<WolfensteinActorState> m_actors;
    private readonly List<WorldSprite> m_staticObjects;
    private readonly List<WolfensteinSoundEvent> m_soundEvents = [];
    private readonly List<TimedWorldLight> m_enemyMuzzleFlashes = [];
    private IReadOnlyList<WorldLight> m_enemyMuzzleFlashSnapshot = [];

    public GameSession(
        WolfensteinMap map,
        RaycastCamera camera,
        IReadOnlyList<WolfensteinActor> actors = null,
        GameDifficulty difficulty = GameDifficulty.Normal,
        WolfensteinPlayerState? playerState = null,
        bool startFaded = false)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(camera);
        Map = map;
        Difficulty = difficulty;
        Camera = camera;
        m_startCamera = camera;
        Doors = WolfensteinDoors.FromMap(map);
        PushWalls = new WolfensteinPushWalls(map);
        SecretTotal = map.Objects.Count(marker => marker == 98);
        m_actorDefinitions = actors ?? [];
        m_actors = CreateActorStates();
        m_staticObjects = WolfensteinStaticObjects.FromMap(map).ToList();
        TreasureTotal = m_staticObjects.Count(item =>
            IsTreasure(WolfensteinStaticObjects.GetPickupType(item.SpriteNumber)));
        if (playerState is { } state)
        {
            Health = state.Health;
            Ammo = state.Ammo;
            Lives = state.Lives;
            Score = state.Score;
            Weapon = state.Weapon;
            m_chosenWeapon = state.ChosenWeapon;
            m_bestWeapon = state.BestWeapon;
        }
        m_isFadingIn = startFaded;
        m_levelFade = startFaded ? 1.0 : 0.0;
    }

    public WolfensteinMap Map { get; }
    public GameDifficulty Difficulty { get; }
    public RaycastCamera Camera { get; private set; }
    public WolfensteinDoors Doors { get; private set; }
    public WolfensteinPushWalls PushWalls { get; private set; }
    public WolfensteinElevatorSwitch ElevatorSwitch { get; private set; }
    public PlayerWeapon Weapon { get; private set; } = PlayerWeapon.Pistol;
    public PlayerWeapon BestWeapon => m_bestWeapon;
    public int WeaponFrame { get; private set; }
    public int Ammo { get; private set; } = 8;
    public int Health { get; private set; } = MaximumHealth;
    public int Lives { get; private set; } = 3;
    public int Score { get; private set; }
    public bool HasGoldKey => (m_keyMask & 1) != 0;
    public bool HasSilverKey => (m_keyMask & 2) != 0;
    public int TreasureCount { get; private set; }
    public int TreasureTotal { get; }
    public int KillCount { get; private set; }
    public int KillTotal => m_actorDefinitions.Count;
    public double LevelElapsedSeconds { get; private set; }
    public int SecretCount { get; private set; }
    public int SecretTotal { get; }
    public bool IsAttacking { get; private set; }
    public bool IsDying { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsCompletingLevel => m_isCompletingLevel;
    public bool IsReadyForNextLevel => m_isCompletingLevel && m_levelFade >= 1.0;
    public double MuzzleFlash => Weapon != PlayerWeapon.Knife && WeaponFrame == 3 ? 1.0 : 0.0;
    public double PlayerSpeed => Math.Sqrt((m_velocityX * m_velocityX) + (m_velocityY * m_velocityY));
    public double LevelFade => m_levelFade;
    public double DeathFade => IsDying ? Math.Min(1.0, m_deathTime / DeathFadeDuration) : 0.0;
    public double DamageFlash => m_damageCount > 0.0
        ? Math.Min(6.0, Math.Floor(m_damageCount / 10.0) + 1.0) / 8.0
        : 0.0;
    public IReadOnlyList<WorldSprite> StaticObjects => m_staticObjects;
    public IReadOnlyList<WorldSprite> ActorSprites => m_actors.Select(actor => actor.ToWorldSprite()).ToArray();
    public IReadOnlyList<WolfensteinActorState> Actors => m_actors;
    public IReadOnlyList<WorldLight> EnemyMuzzleFlashes => m_enemyMuzzleFlashSnapshot;
    public int ActorRevision { get; private set; }
    public int RestartRevision { get; private set; }
    public int FacePictureIndex => m_chaingunGrinTime > 0.0
        ? 22
        : Health == 0
            ? 21
            : Math.Min(6, (MaximumHealth - Health) / 16) * 3 + m_faceFrame;

    public IReadOnlyList<WolfensteinSoundEvent> DrainSoundEvents()
    {
        if (m_soundEvents.Count == 0)
            return [];
        var events = m_soundEvents.ToArray();
        m_soundEvents.Clear();
        return events;
    }

#if DEBUG
    public bool ReloadDebugState()
    {
        var changed = Ammo != MaximumAmmo || Health != MaximumHealth ||
                      m_bestWeapon != PlayerWeapon.Chaingun ||
                      !IsAttacking && Weapon != m_chosenWeapon;
        m_bestWeapon = PlayerWeapon.Chaingun;
        GiveAmmo(MaximumAmmo);
        Health = MaximumHealth;
        return changed;
    }
#endif

    public bool Update(double elapsedSeconds, PlayerInput input, bool useMomentumMovement = false)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0.0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        var feedbackChanged = UpdateFeedback(elapsedSeconds);
        if (IsGameOver)
            return false;
        if (IsDying)
            return UpdateDeath(elapsedSeconds, input) || feedbackChanged;
        if (m_isCompletingLevel)
            return UpdateLevelFade(elapsedSeconds) || feedbackChanged;
        if (m_isFadingIn)
        {
            m_levelFade = Math.Max(0.0, m_levelFade - (elapsedSeconds / LevelFadeDuration));
            m_isFadingIn = m_levelFade > 0.0;
            return elapsedSeconds > 0.0 || feedbackChanged;
        }

        LevelElapsedSeconds += elapsedSeconds;
        var doorsClosingBeforeUpdate = Doors.Items.Where(door => door.IsClosing).ToHashSet();
        var changed = Doors.Update(elapsedSeconds, CanDoorClose) || feedbackChanged;
        foreach (var door in Doors.Items.Where(door => door.IsClosing && !doorsClosingBeforeUpdate.Contains(door)))
            PlaySound(WolfensteinSoundEffect.CloseDoor, door.X + 0.5, door.Y + 0.5);
        changed |= PushWalls.Update(elapsedSeconds, CanPushWallEnterTile);
        if (input.Use && !m_useWasDown)
        {
            changed |= OperateAhead();
            m_useRepeatTime = 0.2;
        }
        else if (input.Use)
        {
            m_useRepeatTime -= elapsedSeconds;
            if (m_useRepeatTime <= 0.0 && IsFacingOrdinaryWall())
            {
                PlaySound(WolfensteinSoundEffect.DoNothing);
                m_useRepeatTime += 0.2;
                changed = true;
            }
        }
        m_useWasDown = input.Use;
        changed |= UpdateWeapon(elapsedSeconds, input);
        changed |= UpdateActors(elapsedSeconds, input.Run);
        changed |= UpdateFace(elapsedSeconds);
        changed |= CollectPickups(Camera.X, Camera.Y);

        var horizontal = (input.TurnRight ? 1.0 : 0.0) - (input.TurnLeft ? 1.0 : 0.0);
        var turn = input.Strafe ? 0.0 : horizontal;
        var strafe = input.Strafe ? horizontal : 0.0;
        var movement = (input.MoveForward ? 1.0 : 0.0) - (input.MoveBackward ? 1.0 : 0.0);
        if (!useMomentumMovement)
            ResetMovementMomentum();
        var hasMomentum = useMomentumMovement && (Math.Abs(m_velocityX) > double.Epsilon ||
                          Math.Abs(m_velocityY) > double.Epsilon ||
                          Math.Abs(m_angularVelocity) > double.Epsilon);
        if ((elapsedSeconds == 0.0 || turn == 0.0 && movement == 0.0 && strafe == 0.0 && !hasMomentum) && !changed)
            return false;

        var x = Camera.X;
        var y = Camera.Y;
        var directionX = Camera.DirectionX;
        var directionY = Camera.DirectionY;
        var planeX = Camera.PlaneX;
        var planeY = Camera.PlaneY;

        if (!useMomentumMovement)
        {
            if (turn != 0.0)
            {
                var inputScale = input.Run ? RunInput : WalkInput;
                var degreesPerSecond = inputScale * OriginalTicksPerSecond / AngleScale;
                var angle = turn * degreesPerSecond * Math.PI / 180.0 * elapsedSeconds;
                (directionX, directionY) = Rotate(directionX, directionY, angle);
                (planeX, planeY) = Rotate(planeX, planeY, angle);
            }

            if (movement != 0.0 || strafe != 0.0)
            {
                var inputScale = input.Run ? RunInput : WalkInput;
                var movementScale = movement > 0.0 ? ForwardMovementScale : BackwardMovementScale;
                var forwardDistance = movement * inputScale * movementScale * OriginalTicksPerSecond /
                                      FixedUnitsPerTile * elapsedSeconds;
                var strafeDistance = strafe * inputScale * ForwardMovementScale * OriginalTicksPerSecond /
                                     FixedUnitsPerTile * elapsedSeconds;
                var moveX = (directionX * forwardDistance) - (directionY * strafeDistance);
                var moveY = (directionY * forwardDistance) + (directionX * strafeDistance);
                var distance = Math.Sqrt((moveX * moveX) + (moveY * moveY));
                var stepCount = Math.Max(1, (int)Math.Ceiling(distance / MaximumMovementStep));
                var stepX = moveX / stepCount;
                var stepY = moveY / stepCount;
                var hitObstacle = false;
                for (var step = 0; step < stepCount; step++)
                {
                    // Resolve each axis independently so the player slides naturally along nearby walls.
                    var nextX = x + stepX;
                    if (CanOccupy(nextX, y))
                        x = nextX;
                    else if (Math.Abs(stepX) > double.Epsilon)
                        hitObstacle = true;
                    var nextY = y + stepY;
                    if (CanOccupy(x, nextY))
                        y = nextY;
                    else if (Math.Abs(stepY) > double.Epsilon)
                        hitObstacle = true;
                    changed |= CollectPickups(x, y);
                }
                if (hitObstacle && m_wallHitSoundTime <= 0.0)
                {
                    PlaySound(WolfensteinSoundEffect.HitWall);
                    m_wallHitSoundTime = WallHitSoundInterval;
                }
            }

            Camera = new RaycastCamera(x, y, directionX, directionY, planeX, planeY);
            return true;
        }

        var remainingSeconds = elapsedSeconds;
        while (remainingSeconds > 0.0)
        {
            var motionSeconds = Math.Min(remainingSeconds, MaximumMotionTimeStep);
            remainingSeconds -= motionSeconds;
            var inputScale = input.Run ? RunInput : WalkInput;
            var targetAngularVelocity = turn * inputScale * OriginalTicksPerSecond /
                                        AngleScale * Math.PI / 180.0;
            var previousAngularVelocity = m_angularVelocity;
            if (targetAngularVelocity == 0.0)
            {
                m_angularVelocity *= Math.Pow(0.01, motionSeconds / TurnFrictionTime);
                if (Math.Abs(m_angularVelocity) < 0.0001)
                    m_angularVelocity = 0.0;
            }
            else
            {
                m_angularVelocity = MoveTowards(
                    m_angularVelocity,
                    targetAngularVelocity,
                    Math.Abs(targetAngularVelocity) / TurnAccelerationTime * motionSeconds);
            }
            var angle = (previousAngularVelocity + m_angularVelocity) * 0.5 * motionSeconds;
            if (Math.Abs(angle) > double.Epsilon)
            {
                (directionX, directionY) = Rotate(directionX, directionY, angle);
                (planeX, planeY) = Rotate(planeX, planeY, angle);
                changed = true;
            }

            var movementScale = movement > 0.0 ? ForwardMovementScale : BackwardMovementScale;
            var forwardSpeed = movement * inputScale * movementScale * OriginalTicksPerSecond / FixedUnitsPerTile;
            var strafeSpeed = strafe * inputScale * ForwardMovementScale * OriginalTicksPerSecond / FixedUnitsPerTile;
            var targetVelocityX = (directionX * forwardSpeed) - (directionY * strafeSpeed);
            var targetVelocityY = (directionY * forwardSpeed) + (directionX * strafeSpeed);
            var previousVelocityX = m_velocityX;
            var previousVelocityY = m_velocityY;
            var targetSpeed = Math.Sqrt(
                (targetVelocityX * targetVelocityX) + (targetVelocityY * targetVelocityY));
            if (targetSpeed == 0.0)
            {
                var friction = Math.Pow(0.01, motionSeconds / MovementFrictionTime);
                m_velocityX *= friction;
                m_velocityY *= friction;
                if (PlayerSpeed < 0.0001)
                {
                    m_velocityX = 0.0;
                    m_velocityY = 0.0;
                }
            }
            else
            {
                MoveTowards(
                    ref m_velocityX,
                    ref m_velocityY,
                    targetVelocityX,
                    targetVelocityY,
                    targetSpeed / MovementAccelerationTime * motionSeconds);
            }
            var moveX = (previousVelocityX + m_velocityX) * 0.5 * motionSeconds;
            var moveY = (previousVelocityY + m_velocityY) * 0.5 * motionSeconds;
            var distance = Math.Sqrt((moveX * moveX) + (moveY * moveY));
            if (distance <= double.Epsilon)
                continue;
            var stepCount = Math.Max(1, (int)Math.Ceiling(distance / MaximumMovementStep));
            var stepX = moveX / stepCount;
            var stepY = moveY / stepCount;
            var hitObstacle = false;
            for (var step = 0; step < stepCount; step++)
            {
                // Resolve each axis independently so the player slides naturally along nearby walls.
                var nextX = x + stepX;
                if (CanOccupy(nextX, y))
                    x = nextX;
                else if (Math.Abs(stepX) > double.Epsilon)
                {
                    m_velocityX = 0.0;
                    hitObstacle = true;
                }
                var nextY = y + stepY;
                if (CanOccupy(x, nextY))
                    y = nextY;
                else if (Math.Abs(stepY) > double.Epsilon)
                {
                    m_velocityY = 0.0;
                    hitObstacle = true;
                }
                changed |= CollectPickups(x, y);
            }
            changed = true;
            if (hitObstacle && m_wallHitSoundTime <= 0.0)
            {
                PlaySound(WolfensteinSoundEffect.HitWall);
                m_wallHitSoundTime = WallHitSoundInterval;
            }
        }

        // A fresh snapshot also publishes door-only animation changes to the viewport binding.
        Camera = new RaycastCamera(x, y, directionX, directionY, planeX, planeY);
        return true;
    }

    private static double MoveTowards(double current, double target, double maximumDelta)
    {
        var delta = target - current;
        return Math.Abs(delta) <= maximumDelta
            ? target
            : current + (Math.Sign(delta) * maximumDelta);
    }

    private static void MoveTowards(
        ref double currentX,
        ref double currentY,
        double targetX,
        double targetY,
        double maximumDelta)
    {
        var deltaX = targetX - currentX;
        var deltaY = targetY - currentY;
        var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= maximumDelta || distance <= double.Epsilon)
        {
            currentX = targetX;
            currentY = targetY;
            return;
        }
        var scale = maximumDelta / distance;
        currentX += deltaX * scale;
        currentY += deltaY * scale;
    }

    private void ResetMovementMomentum()
    {
        m_velocityX = 0.0;
        m_velocityY = 0.0;
        m_angularVelocity = 0.0;
    }

    private bool CanOccupy(double x, double y)
    {
        if (IsSolid((int)Math.Floor(x - PlayerRadius), (int)Math.Floor(y - PlayerRadius)) ||
            IsSolid((int)Math.Floor(x + PlayerRadius), (int)Math.Floor(y - PlayerRadius)) ||
            IsSolid((int)Math.Floor(x - PlayerRadius), (int)Math.Floor(y + PlayerRadius)) ||
            IsSolid((int)Math.Floor(x + PlayerRadius), (int)Math.Floor(y + PlayerRadius)))
        {
            return false;
        }

        // Wolf3D uses an axis-aligned one-tile exclusion box around every shootable actor.
        return m_actors.All(actor =>
            actor.IsDead ||
            Math.Abs(x - actor.X) > MinimumActorDistance ||
            Math.Abs(y - actor.Y) > MinimumActorDistance);
    }

    private bool IsSolid(int x, int y)
    {
        var door = Doors.Get(x, y);
        if (door != null)
            return !door.IsFullyOpen;
        if (PushWalls.IsTileReserved(x, y))
            return true;
        return !PushWalls.IsOriginalWallSuppressed(x, y) && Map.IsSolid(x, y) ||
               WolfensteinStaticObjects.BlocksMovement(Map.GetObject(x, y));
    }

    private bool OperateAhead()
    {
        var (directionX, directionY) = GetCardinalDirection();
        var pushWallX = (int)Math.Floor(Camera.X) + directionX;
        var pushWallY = (int)Math.Floor(Camera.Y) + directionY;
        var isPushWall = Map.GetObject(pushWallX, pushWallY) == 98;
        if (PushWalls.TryPush(pushWallX, pushWallY, directionX, directionY, CanPushWallEnterTile))
        {
            SecretCount++;
            PlaySound(WolfensteinSoundEffect.PushWall);
            return true;
        }
        if (isPushWall)
        {
            PlaySound(WolfensteinSoundEffect.CannotUse);
            return true;
        }

        if (directionX != 0 && Map.GetWall(pushWallX, pushWallY) == ElevatorSwitchTile)
        {
            ElevatorSwitch = new WolfensteinElevatorSwitch(pushWallX, pushWallY);
            m_isCompletingLevel = true;
            m_levelFade = 0.0;
            PlaySound(WolfensteinSoundEffect.LevelDone);
            return true;
        }

        // Follow a short use ray so slightly angled players can still operate the door they are facing.
        for (var distance = 0.25; distance <= 1.5; distance += 0.1)
        {
            var x = (int)Math.Floor(Camera.X + (Camera.DirectionX * distance));
            var y = (int)Math.Floor(Camera.Y + (Camera.DirectionY * distance));
            var door = Doors.Get(x, y);
            if (door != null)
            {
                var wasOpen = door.IsFullyOpen;
                if (!door.Operate(CanDoorClose(door), m_keyMask))
                {
                    PlaySound(WolfensteinSoundEffect.CannotUse);
                    return true;
                }
                PlaySound(
                    wasOpen ? WolfensteinSoundEffect.CloseDoor : WolfensteinSoundEffect.OpenDoor,
                    door.X + 0.5,
                    door.Y + 0.5);
                return true;
            }
            if (Map.IsSolid(x, y) && !PushWalls.IsOriginalWallSuppressed(x, y))
            {
                PlaySound(WolfensteinSoundEffect.DoNothing);
                return true;
            }
        }

        PlaySound(WolfensteinSoundEffect.DoNothing);
        return true;
    }

    private (int X, int Y) GetCardinalDirection() => Math.Abs(Camera.DirectionX) > Math.Abs(Camera.DirectionY)
        ? (Math.Sign(Camera.DirectionX), 0)
        : (0, Math.Sign(Camera.DirectionY));

    private bool IsFacingOrdinaryWall()
    {
        var (directionX, directionY) = GetCardinalDirection();
        var x = (int)Math.Floor(Camera.X) + directionX;
        var y = (int)Math.Floor(Camera.Y) + directionY;
        return Doors.Get(x, y) == null && Map.GetObject(x, y) != 98 &&
               Map.GetWall(x, y) != ElevatorSwitchTile && Map.IsSolid(x, y);
    }

    private bool CanPushWallEnterTile(int x, int y)
    {
        if (x < 0 || x >= Map.Width || y < 0 || y >= Map.Height ||
            Doors.Get(x, y) != null || PushWalls.IsTileReserved(x, y) ||
            !PushWalls.IsOriginalWallSuppressed(x, y) && Map.IsSolid(x, y) ||
            WolfensteinStaticObjects.BlocksMovement(Map.GetObject(x, y)))
        {
            return false;
        }
        return m_actors.All(actor => actor.IsDead ||
            (int)Math.Floor(actor.X) != x || (int)Math.Floor(actor.Y) != y);
    }

    private bool CanDoorClose(WolfensteinDoor door)
    {
        if (OccupiesDoorway(Camera.X, Camera.Y, door))
            return false;
        return m_actors.All(actor => !OccupiesDoorway(actor.X, actor.Y, door));
    }

    private static bool OccupiesDoorway(double x, double y, WolfensteinDoor door)
    {
        var tileX = (int)Math.Floor(x);
        var tileY = (int)Math.Floor(y);
        if (tileX == door.X && tileY == door.Y)
            return true;
        return door.Orientation == DoorOrientation.Vertical
            ? tileY == door.Y &&
              ((int)Math.Floor(x - OriginalPlayerRadius) == door.X ||
               (int)Math.Floor(x + OriginalPlayerRadius) == door.X)
            : tileX == door.X &&
              ((int)Math.Floor(y - OriginalPlayerRadius) == door.Y ||
               (int)Math.Floor(y + OriginalPlayerRadius) == door.Y);
    }

    private bool CollectPickups(double x, double y)
    {
        var changed = false;
        for (var index = m_staticObjects.Count - 1; index >= 0; index--)
        {
            var item = m_staticObjects[index];
            if (Math.Abs(x - item.X) > PickupDistance || Math.Abs(y - item.Y) > PickupDistance)
                continue;

            var pickupType = WolfensteinStaticObjects.GetPickupType(item.SpriteNumber);
            if (pickupType == WolfensteinPickupType.None ||
                pickupType == WolfensteinPickupType.AmmoClip && Ammo == MaximumAmmo ||
                pickupType is WolfensteinPickupType.DogFood or WolfensteinPickupType.Food or
                    WolfensteinPickupType.FirstAid &&
                Health == MaximumHealth)
            {
                continue;
            }

            switch (pickupType)
            {
                case WolfensteinPickupType.DogFood:
                    Heal(4);
                    PlaySound(WolfensteinSoundEffect.Health);
                    break;
                case WolfensteinPickupType.Food:
                    Heal(10);
                    PlaySound(WolfensteinSoundEffect.Health);
                    break;
                case WolfensteinPickupType.FirstAid:
                    Heal(25);
                    PlaySound(WolfensteinSoundEffect.FirstAid);
                    break;
                case WolfensteinPickupType.FullHeal:
                    Heal(99);
                    GiveAmmo(25);
                    Lives++;
                    TreasureCount++;
                    PlaySound(WolfensteinSoundEffect.ExtraLife);
                    break;
                case WolfensteinPickupType.GoldKey:
                    m_keyMask |= 1;
                    PlaySound(WolfensteinSoundEffect.GetKey);
                    break;
                case WolfensteinPickupType.SilverKey:
                    m_keyMask |= 2;
                    PlaySound(WolfensteinSoundEffect.GetKey);
                    break;
                case WolfensteinPickupType.AmmoClip:
                    GiveAmmo(8);
                    PlaySound(WolfensteinSoundEffect.GetAmmo);
                    break;
                case WolfensteinPickupType.MachineGun:
                    GiveWeapon(PlayerWeapon.MachineGun);
                    PlaySound(WolfensteinSoundEffect.GetMachineGun);
                    break;
                case WolfensteinPickupType.Chaingun:
                    GiveWeapon(PlayerWeapon.Chaingun);
                    PlaySound(WolfensteinSoundEffect.GetGatling);
                    break;
                case WolfensteinPickupType.Cross:
                    CollectTreasure(100);
                    PlaySound(WolfensteinSoundEffect.BonusCross);
                    break;
                case WolfensteinPickupType.Chalice:
                    CollectTreasure(500);
                    PlaySound(WolfensteinSoundEffect.BonusChalice);
                    break;
                case WolfensteinPickupType.Bible:
                    CollectTreasure(1000);
                    PlaySound(WolfensteinSoundEffect.BonusBible);
                    break;
                case WolfensteinPickupType.Crown:
                    CollectTreasure(5000);
                    PlaySound(WolfensteinSoundEffect.BonusCrown);
                    break;
            }
            m_staticObjects.RemoveAt(index);
            changed = true;
        }
        return changed;
    }

    private void CollectTreasure(int points)
    {
        Score += points;
        TreasureCount++;
    }

    public WolfensteinLevelStats CreateLevelStats() => WolfensteinLevelStats.Create(
        Map.Slot,
        LevelElapsedSeconds,
        KillCount,
        KillTotal,
        SecretCount,
        SecretTotal,
        TreasureCount,
        TreasureTotal);

    public void ApplyLevelBonus(int bonus)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bonus);
        Score += bonus;
    }

    private static bool IsTreasure(WolfensteinPickupType pickup) => pickup is
        WolfensteinPickupType.Cross or WolfensteinPickupType.Chalice or WolfensteinPickupType.Bible or
        WolfensteinPickupType.Crown or WolfensteinPickupType.FullHeal;

    private void Heal(int amount) => Health = Math.Min(MaximumHealth, Health + amount);

    private static (double X, double Y) Rotate(double x, double y, double angle)
    {
        var cosine = Math.Cos(angle);
        var sine = Math.Sin(angle);
        return ((x * cosine) - (y * sine), (x * sine) + (y * cosine));
    }

    private bool UpdateWeapon(double elapsedSeconds, PlayerInput input)
    {
        var changed = false;
        if (!IsAttacking && Ammo > 0 && input.WeaponSelection is { } selection &&
            selection <= m_bestWeapon && selection != Weapon)
        {
            m_chosenWeapon = selection;
            Weapon = selection;
            WeaponFrame = 0;
            changed = true;
        }

        if (!IsAttacking && input.Attack && !m_attackWasDown)
        {
            if (Weapon != PlayerWeapon.Knife && Ammo == 0)
                Weapon = PlayerWeapon.Knife;
            IsAttacking = true;
            m_attackStep = 0;
            WeaponFrame = 1;
            m_attackTimeRemaining = AttackFrameDuration;
            changed = true;
        }
        m_attackWasDown = input.Attack;

        if (!IsAttacking || elapsedSeconds == 0.0)
            return changed;
        m_attackTimeRemaining -= elapsedSeconds;
        while (IsAttacking && m_attackTimeRemaining <= 0.0)
        {
            changed = true;
            switch (m_attackStep)
            {
                case 0:
                    SetAttackStep(1);
                    break;
                case 1:
                    FireCurrentWeapon();
                    SetAttackStep(2);
                    break;
                case 2 when Weapon == PlayerWeapon.MachineGun && input.Attack && Ammo > 0:
                    SetAttackStep(1);
                    break;
                case 2 when Weapon == PlayerWeapon.Chaingun && Ammo > 0:
                    FireCurrentWeapon();
                    SetAttackStep(input.Attack && Ammo > 0 ? 1 : 3);
                    break;
                case 2:
                    SetAttackStep(3);
                    break;
                default:
                    IsAttacking = false;
                    WeaponFrame = 0;
                    if (Weapon != PlayerWeapon.Knife && Ammo == 0)
                        Weapon = PlayerWeapon.Knife;
                    else if (Ammo > 0 && Weapon != m_chosenWeapon)
                        Weapon = m_chosenWeapon;
                    break;
            }
        }
        return changed;
    }

    private void SetAttackStep(int step)
    {
        m_attackStep = step;
        WeaponFrame = step + 1;
        m_attackTimeRemaining += AttackFrameDuration;
    }

    private void FireCurrentWeapon()
    {
        if (Weapon != PlayerWeapon.Knife && Ammo == 0)
            return;
        if (Weapon != PlayerWeapon.Knife)
        {
            Ammo--;
            m_playerMadeNoise = true;
        }
        PlaySound(Weapon switch
        {
            PlayerWeapon.Knife => WolfensteinSoundEffect.AttackKnife,
            PlayerWeapon.Pistol => WolfensteinSoundEffect.AttackPistol,
            PlayerWeapon.MachineGun => WolfensteinSoundEffect.AttackMachineGun,
            _ => WolfensteinSoundEffect.AttackGatling
        });
        DamageTarget(Weapon == PlayerWeapon.Knife);
    }

    private void PlaySound(WolfensteinSoundEffect effect, double? x = null, double? y = null) =>
        m_soundEvents.Add(new WolfensteinSoundEvent(effect, x, y));

    private static WolfensteinSoundEffect GetEnemyAttackSound(WolfensteinActorType type) => type switch
    {
        WolfensteinActorType.Ss => WolfensteinSoundEffect.SsFire,
        WolfensteinActorType.Dog => WolfensteinSoundEffect.DogAttack,
        _ => WolfensteinSoundEffect.GuardFire
    };

    private void DamageTarget(bool isKnife)
    {
        Span<WallColumn> columns = stackalloc WallColumn[CombatViewportWidth];
        Raycaster.Cast(Map, Doors, PushWalls, Camera, columns);
        WolfensteinActorState target = null;
        var nearestDepth = double.PositiveInfinity;
        Span<ProjectedWorldSprite> projected = stackalloc ProjectedWorldSprite[1];
        foreach (var actor in m_actors)
        {
            if (actor.IsDead)
                continue;
            WorldSprite[] sprite = [actor.ToWorldSprite()];
            if (WorldSpriteProjector.Project(sprite, Camera, CombatViewportWidth, 160, 200, projected) == 0)
                continue;
            var projection = projected[0];
            if (Math.Abs(projection.CenterX - (CombatViewportWidth / 2)) >= CrosshairHalfWidth ||
                projection.Depth >= nearestDepth)
            {
                continue;
            }
            var column = Math.Clamp(projection.CenterX, 0, CombatViewportWidth - 1);
            if (projection.Depth >= columns[column].Distance)
                continue;
            target = actor;
            nearestDepth = projection.Depth;
        }
        if (target == null || isKnife && nearestDepth > 1.5)
            return;
        var damage = GetPlayerAttackDamage(target, isKnife);
        if (target.Behavior == WolfensteinActorBehavior.Dormant)
            damage *= 2;
        if (!target.Damage(damage))
            return;
        if (target.IsDead)
        {
            Score += target.Score;
            KillCount++;
            PlaySound(GetEnemyDeathSound(target.Actor.Type), target.X, target.Y);
            var dropSprite = target.Actor.Type switch
            {
                WolfensteinActorType.Dog => -1,
                WolfensteinActorType.Ss when m_bestWeapon < PlayerWeapon.MachineGun => 29,
                _ => 28
            };
            if (dropSprite >= 0)
                m_staticObjects.Add(new WorldSprite(target.X, target.Y, dropSprite));
        }
        ActorRevision++;
    }

    private int GetPlayerAttackDamage(WolfensteinActorState target, bool isKnife)
    {
        if (isKnife)
            return NextRandomByte() >> 4;
        var distanceX = Math.Abs((int)Math.Floor(target.X) - (int)Math.Floor(Camera.X));
        var distanceY = Math.Abs((int)Math.Floor(target.Y) - (int)Math.Floor(Camera.Y));
        var distance = Math.Max(distanceX, distanceY);
        if (distance < 2)
            return NextRandomByte() / 4;
        if (distance < 4)
            return NextRandomByte() / 6;
        if (NextRandomByte() / 12 < distance)
            return 0;
        return NextRandomByte() / 6;
    }

    private void GiveAmmo(int amount)
    {
        var wasEmpty = Ammo == 0;
        Ammo = Math.Min(MaximumAmmo, Ammo + amount);
        if (wasEmpty && !IsAttacking)
        {
            Weapon = m_chosenWeapon;
            WeaponFrame = 0;
        }
    }

    private void GiveWeapon(PlayerWeapon weapon)
    {
        GiveAmmo(6);
        if (weapon == PlayerWeapon.Chaingun)
            m_chaingunGrinTime = 1.0;
        if (weapon <= m_bestWeapon)
            return;
        m_bestWeapon = weapon;
        m_chosenWeapon = weapon;
        Weapon = weapon;
        WeaponFrame = 0;
    }

    private bool UpdateFace(double elapsedSeconds)
    {
        if (m_chaingunGrinTime > 0.0)
        {
            m_chaingunGrinTime = Math.Max(0.0, m_chaingunGrinTime - elapsedSeconds);
            return elapsedSeconds > 0.0;
        }
        if (Health == 0)
            return false;
        m_faceTime += elapsedSeconds;
        if (m_faceTime < m_nextFaceChange)
            return false;
        m_faceTime = 0.0;
        var random = NextRandomByte();
        m_faceFrame = random >> 6;
        if (m_faceFrame == 3)
            m_faceFrame = 1;
        m_nextFaceChange = (NextRandomByte() + 1.0) / OriginalTicksPerSecond;
        return true;
    }

    private byte NextRandomByte()
    {
        m_randomState ^= m_randomState << 13;
        m_randomState ^= m_randomState >> 17;
        m_randomState ^= m_randomState << 5;
        return (byte)m_randomState;
    }

    private bool UpdateActors(double elapsedSeconds, bool playerIsRunning)
    {
        var changed = false;
        foreach (var actor in m_actors)
        {
            changed |= actor.Update(elapsedSeconds);
            if (actor.IsDead)
                continue;
            if (actor.Behavior == WolfensteinActorBehavior.Shooting)
            {
                changed |= actor.UpdateShooting(elapsedSeconds, out var fired);
                if (fired)
                {
                    PlaySound(GetEnemyAttackSound(actor.Actor.Type), actor.X, actor.Y);
                    AddEnemyMuzzleFlash(actor.X, actor.Y);
                    if (HasLineOfSight(actor.X, actor.Y, Camera.X, Camera.Y) &&
                        TryGetEnemyAttackDamage(actor, playerIsRunning, out var damage))
                    {
                        TakeDamage(damage, actor.X, actor.Y);
                    }
                }
                continue;
            }

            if (actor.Behavior == WolfensteinActorBehavior.Dormant)
            {
                var canHear = m_playerMadeNoise && !actor.Actor.IsAmbush && CanHearPlayer(actor.X, actor.Y);
                if (!canHear && !CanSeePlayer(actor))
                    continue;
                if (actor.Alert())
                {
                    changed = true;
                    var alertSound = GetEnemyAlertSound(actor.Actor.Type);
                    if (alertSound != null)
                        PlaySound(alertSound.Value, actor.X, actor.Y);
                }
            }

            actor.AttackCooldown = Math.Max(0.0, actor.AttackCooldown - elapsedSeconds);
            var distance = Math.Sqrt(
                Math.Pow(Camera.X - actor.X, 2.0) +
                Math.Pow(Camera.Y - actor.Y, 2.0));
            var shouldAttack = actor.Actor.Type == WolfensteinActorType.Dog
                ? Math.Abs(Camera.X - actor.X) <= DogAttackDistance &&
                  Math.Abs(Camera.Y - actor.Y) <= DogAttackDistance
                : distance >= MinimumShootingDistance &&
                  HasLineOfSight(actor.X, actor.Y, Camera.X, Camera.Y);
            if (actor.AttackCooldown == 0.0 && shouldAttack)
            {
                actor.AttackCooldown = actor.Profile.AttackCooldown;
                changed |= actor.BeginShooting();
                continue;
            }
            changed |= MoveActorTowardPlayer(actor, elapsedSeconds);
        }
        if (changed)
            ActorRevision++;
        return changed;
    }

    private bool TryGetEnemyAttackDamage(
        WolfensteinActorState actor,
        bool playerIsRunning,
        out int damage)
    {
        if (actor.Actor.Type == WolfensteinActorType.Dog)
        {
            if (NextRandomByte() >= 180)
            {
                damage = 0;
                return false;
            }
            damage = NextRandomByte() >> 4;
            return true;
        }

        var distanceX = Math.Abs((int)Math.Floor(actor.X) - (int)Math.Floor(Camera.X));
        var distanceY = Math.Abs((int)Math.Floor(actor.Y) - (int)Math.Floor(Camera.Y));
        var distance = Math.Max(distanceX, distanceY);
        if (actor.Actor.Type == WolfensteinActorType.Ss)
            distance = distance * 2 / 3;
        var visible = IsInPlayerView(actor.X, actor.Y);
        var hitChance = (playerIsRunning ? 160 : 256) - (distance * (visible ? 16 : 8));
        if (NextRandomByte() >= hitChance)
        {
            damage = 0;
            return false;
        }

        damage = distance switch
        {
            < 2 => NextRandomByte() >> 2,
            < 4 => NextRandomByte() >> 3,
            _ => NextRandomByte() >> 4
        };
        return true;
    }

    private bool IsInPlayerView(double x, double y)
    {
        var deltaX = x - Camera.X;
        var deltaY = y - Camera.Y;
        return (deltaX * Camera.DirectionX) + (deltaY * Camera.DirectionY) > 0.0;
    }

    private bool CanSeePlayer(WolfensteinActorState actor)
    {
        var deltaX = Camera.X - actor.X;
        var deltaY = Camera.Y - actor.Y;
        if (Math.Abs(deltaX) >= 1.5 || Math.Abs(deltaY) >= 1.5)
        {
            var facing = actor.Direction switch
            {
                0 => (X: 1.0, Y: 0.0),
                1 => (X: 0.0, Y: -1.0),
                2 => (X: -1.0, Y: 0.0),
                _ => (X: 0.0, Y: 1.0)
            };
            if ((deltaX * facing.X) + (deltaY * facing.Y) <= 0.0)
                return false;
        }
        return HasLineOfSight(actor.X, actor.Y, Camera.X, Camera.Y);
    }

    private bool HasLineOfSight(double fromX, double fromY, double toX, double toY)
    {
        var deltaX = toX - fromX;
        var deltaY = toY - fromY;
        var steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(deltaX), Math.Abs(deltaY)) * 10.0));
        for (var step = 1; step < steps; step++)
        {
            var x = (int)Math.Floor(fromX + (deltaX * step / steps));
            var y = (int)Math.Floor(fromY + (deltaY * step / steps));
            if (IsSolid(x, y))
                return false;
        }
        return true;
    }

    private bool CanHearPlayer(double actorX, double actorY)
    {
        var actorArea = Map.GetWall((int)Math.Floor(actorX), (int)Math.Floor(actorY));
        var playerArea = Map.GetWall((int)Math.Floor(Camera.X), (int)Math.Floor(Camera.Y));
        if (actorArea >= 107 && actorArea == playerArea)
            return true;
        return FindNextPathTile((int)actorX, (int)actorY, allowClosedDoors: false) != null;
    }

    private static WolfensteinSoundEffect? GetEnemyAlertSound(WolfensteinActorType type) => type switch
    {
        WolfensteinActorType.Guard => WolfensteinSoundEffect.GuardAlert,
        WolfensteinActorType.Officer => WolfensteinSoundEffect.OfficerAlert,
        WolfensteinActorType.Ss => WolfensteinSoundEffect.SsAlert,
        WolfensteinActorType.Dog => WolfensteinSoundEffect.DogAlert,
        _ => null
    };

    private WolfensteinSoundEffect GetEnemyDeathSound(WolfensteinActorType type)
    {
        WolfensteinSoundEffect[] guardSounds =
        [
            WolfensteinSoundEffect.GuardDeath1,
            WolfensteinSoundEffect.GuardDeath2,
            WolfensteinSoundEffect.GuardDeath3,
            WolfensteinSoundEffect.GuardDeath4,
            WolfensteinSoundEffect.GuardDeath5,
            WolfensteinSoundEffect.GuardDeath7,
            WolfensteinSoundEffect.GuardDeath8,
            WolfensteinSoundEffect.GuardDeath9
        ];
        return type switch
        {
            WolfensteinActorType.Guard => guardSounds[NextRandomByte() % guardSounds.Length],
            WolfensteinActorType.Officer => WolfensteinSoundEffect.OfficerDeath,
            WolfensteinActorType.Ss => WolfensteinSoundEffect.SsDeath,
            WolfensteinActorType.Dog => WolfensteinSoundEffect.DogDeath,
            _ => WolfensteinSoundEffect.MutantDeath
        };
    }

    private bool MoveActorTowardPlayer(WolfensteinActorState actor, double elapsedSeconds)
    {
        var next = actor.PathTarget;
        if (next == null)
        {
            var currentX = (int)Math.Floor(actor.X);
            var currentY = (int)Math.Floor(actor.Y);
            next = FindNextPathTile(currentX, currentY, allowClosedDoors: true);
            if (next == null)
                return false;
            actor.SetPathTarget(next.Value.X, next.Value.Y);
        }
        var door = Doors.Get(next.Value.X, next.Value.Y);
        if (door != null && !door.IsFullyOpen)
        {
            var wasOpening = door.IsOpening;
            var opened = door.Open();
            if (opened && !wasOpening)
                PlaySound(WolfensteinSoundEffect.OpenDoor, door.X + 0.5, door.Y + 0.5);
            return opened;
        }
        var targetX = next.Value.X + 0.5;
        var targetY = next.Value.Y + 0.5;
        var deltaX = targetX - actor.X;
        var deltaY = targetY - actor.Y;
        var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (distance <= double.Epsilon)
            return false;
        var travel = Math.Min(distance, actor.Profile.ChaseSpeed * elapsedSeconds);
        var stepCount = Math.Max(1, (int)Math.Ceiling(travel / MaximumMovementStep));
        var stepX = deltaX / distance * travel / stepCount;
        var stepY = deltaY / distance * travel / stepCount;
        var x = actor.X;
        var y = actor.Y;
        var moved = false;
        for (var step = 0; step < stepCount; step++)
        {
            var nextX = x + stepX;
            var nextY = y + stepY;
            if (!CanActorOccupy(actor, nextX, nextY))
                break;
            x = nextX;
            y = nextY;
            moved = true;
        }
        if (!moved)
            return false;
        var direction = Math.Abs(deltaX) > Math.Abs(deltaY)
            ? deltaX > 0.0 ? 0 : 2
            : deltaY < 0.0 ? 1 : 3;
        actor.MoveTo(x, y, direction, elapsedSeconds);
        if (Math.Abs(x - targetX) < 0.0001 && Math.Abs(y - targetY) < 0.0001)
            actor.ClearPathTarget();
        return true;
    }

    private bool CanActorOccupy(WolfensteinActorState movingActor, double x, double y)
    {
        if (IsSolid((int)Math.Floor(x), (int)Math.Floor(y)) ||
            Math.Abs(x - Camera.X) <= MinimumActorDistance &&
            Math.Abs(y - Camera.Y) <= MinimumActorDistance)
        {
            return false;
        }
        return m_actors.All(actor => ReferenceEquals(actor, movingActor) || actor.IsDead ||
            Math.Abs(x - actor.X) >= MinimumActorSeparation ||
            Math.Abs(y - actor.Y) >= MinimumActorSeparation);
    }

    private (int X, int Y)? FindNextPathTile(int startX, int startY, bool allowClosedDoors)
    {
        var targetX = (int)Math.Floor(Camera.X);
        var targetY = (int)Math.Floor(Camera.Y);
        if (startX == targetX && startY == targetY)
            return null;
        var visited = new bool[Map.Width * Map.Height];
        var previous = new (int X, int Y)?[visited.Length];
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((startX, startY));
        visited[(startY * Map.Width) + startX] = true;
        (int X, int Y)[] directions = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in directions)
            {
                var next = (X: current.X + direction.X, Y: current.Y + direction.Y);
                if (next.X < 0 || next.X >= Map.Width || next.Y < 0 || next.Y >= Map.Height)
                    continue;
                var index = (next.Y * Map.Width) + next.X;
                if (visited[index] || !CanActorPathThrough(next.X, next.Y, allowClosedDoors))
                    continue;
                visited[index] = true;
                previous[index] = current;
                if (next.X == targetX && next.Y == targetY)
                {
                    while (previous[(next.Y * Map.Width) + next.X] is { } parent &&
                           (parent.X != startX || parent.Y != startY))
                    {
                        next = parent;
                    }
                    return next;
                }
                queue.Enqueue(next);
            }
        }
        return null;
    }

    private bool CanActorPathThrough(int x, int y, bool allowClosedDoors)
    {
        var door = Doors.Get(x, y);
        if (door != null)
            return !door.IsLocked && (allowClosedDoors || door.IsFullyOpen);
        return !Map.IsSolid(x, y) && !PushWalls.IsTileReserved(x, y) &&
               !WolfensteinStaticObjects.BlocksMovement(Map.GetObject(x, y));
    }

    private void TakeDamage(int damage, double attackerX, double attackerY)
    {
        if (Health == 0)
            return;
        if (Difficulty == GameDifficulty.Baby)
            damage >>= 2;
        m_damageCount += damage;
        Health = Math.Max(0, Health - damage);
        if (Health > 0)
            return;
        IsDying = true;
        m_killerX = attackerX;
        m_killerY = attackerY;
        PlaySound(WolfensteinSoundEffect.PlayerDeath);
        IsAttacking = false;
        WeaponFrame = 0;
        m_deathTime = 0.0;
    }

    private bool UpdateFeedback(double elapsedSeconds)
    {
        var previousDamageCount = m_damageCount;
        m_damageCount = Math.Max(0.0, m_damageCount - (elapsedSeconds * OriginalTicksPerSecond));
        m_wallHitSoundTime = Math.Max(0.0, m_wallHitSoundTime - elapsedSeconds);
        return UpdateEnemyMuzzleFlashes(elapsedSeconds) || m_damageCount != previousDamageCount;
    }

    private bool UpdateEnemyMuzzleFlashes(double elapsedSeconds)
    {
        if (elapsedSeconds <= 0.0 || m_enemyMuzzleFlashes.Count == 0)
            return false;
        for (var index = m_enemyMuzzleFlashes.Count - 1; index >= 0; index--)
        {
            var flash = m_enemyMuzzleFlashes[index];
            flash.RemainingSeconds -= elapsedSeconds;
            if (flash.RemainingSeconds <= 0.0)
                m_enemyMuzzleFlashes.RemoveAt(index);
        }
        RebuildEnemyMuzzleFlashSnapshot();
        return true;
    }

    private void AddEnemyMuzzleFlash(double x, double y)
    {
        var flash = m_enemyMuzzleFlashes.FirstOrDefault(item => item.X == x && item.Y == y);
        if (flash == null)
            m_enemyMuzzleFlashes.Add(new TimedWorldLight(x, y, EnemyMuzzleFlashDuration));
        else
            flash.RemainingSeconds = EnemyMuzzleFlashDuration;
        RebuildEnemyMuzzleFlashSnapshot();
    }

    private void RebuildEnemyMuzzleFlashSnapshot()
    {
        m_enemyMuzzleFlashSnapshot = m_enemyMuzzleFlashes
            .Select(flash =>
            {
                var intensity = (float)Math.Clamp(
                    flash.RemainingSeconds / EnemyMuzzleFlashDuration,
                    0.0,
                    1.0);
                return new WorldLight(flash.X, flash.Y, intensity, intensity, 2.5f, 2.5f);
            })
            .ToArray();
    }

    private bool UpdateDeath(double elapsedSeconds, PlayerInput input)
    {
        RotateTowardKiller(elapsedSeconds);
        m_deathTime += elapsedSeconds;
        var skipDelay = m_deathTime >= DeathFadeDuration && HasInput(input);
        if (m_deathTime < DeathDuration && !skipDelay)
            return elapsedSeconds > 0.0;
        Lives--;
        if (Lives <= 0)
        {
            IsGameOver = true;
            return true;
        }
        RestartLevel();
        return true;
    }

    private void RotateTowardKiller(double elapsedSeconds)
    {
        if (m_killerX == null || m_killerY == null)
            return;
        var targetAngle = Math.Atan2(m_killerY.Value - Camera.Y, m_killerX.Value - Camera.X);
        var currentAngle = Math.Atan2(Camera.DirectionY, Camera.DirectionX);
        var difference = Math.Atan2(Math.Sin(targetAngle - currentAngle), Math.Cos(targetAngle - currentAngle));
        var rotation = Math.Clamp(
            difference,
            -DeathRotationRadiansPerSecond * elapsedSeconds,
            DeathRotationRadiansPerSecond * elapsedSeconds);
        if (Math.Abs(rotation) <= double.Epsilon)
            return;
        var (directionX, directionY) = Rotate(Camera.DirectionX, Camera.DirectionY, rotation);
        var (planeX, planeY) = Rotate(Camera.PlaneX, Camera.PlaneY, rotation);
        Camera = new RaycastCamera(Camera.X, Camera.Y, directionX, directionY, planeX, planeY);
    }

    private static bool HasInput(PlayerInput input) =>
        input.MoveForward || input.MoveBackward || input.TurnLeft || input.TurnRight ||
        input.Use || input.Run || input.Attack || input.Strafe || input.WeaponSelection != null;

    private bool UpdateLevelFade(double elapsedSeconds)
    {
        m_levelFade = Math.Min(1.0, m_levelFade + (elapsedSeconds / LevelFadeDuration));
        return elapsedSeconds > 0.0;
    }

    /// <summary>
    /// Captures player progress that should survive a successful level transition.
    /// </summary>
    public WolfensteinPlayerState CapturePlayerState() => new(
        Health,
        Ammo,
        Lives,
        Score,
        Weapon,
        m_chosenWeapon,
        m_bestWeapon);

    private void RestartLevel()
    {
        Camera = m_startCamera;
        Doors = WolfensteinDoors.FromMap(Map);
        PushWalls = new WolfensteinPushWalls(Map);
        ElevatorSwitch = null;
        m_actors = CreateActorStates();
        m_staticObjects.Clear();
        m_staticObjects.AddRange(WolfensteinStaticObjects.FromMap(Map));
        Health = MaximumHealth;
        Ammo = 8;
        Weapon = PlayerWeapon.Pistol;
        m_chosenWeapon = PlayerWeapon.Pistol;
        m_bestWeapon = PlayerWeapon.Pistol;
        m_keyMask = 0;
        WeaponFrame = 0;
        IsAttacking = false;
        IsDying = false;
        m_useWasDown = false;
        m_attackWasDown = false;
        m_attackStep = 0;
        m_attackTimeRemaining = 0.0;
        m_playerMadeNoise = false;
        m_faceTime = 0.0;
        m_nextFaceChange = 1.0;
        m_chaingunGrinTime = 0.0;
        m_damageCount = 0.0;
        m_wallHitSoundTime = 0.0;
        ResetMovementMomentum();
        m_enemyMuzzleFlashes.Clear();
        m_enemyMuzzleFlashSnapshot = [];
        m_killerX = null;
        m_killerY = null;
        m_faceFrame = 0;
        TreasureCount = 0;
        KillCount = 0;
        LevelElapsedSeconds = 0.0;
        SecretCount = 0;
        ActorRevision++;
        RestartRevision++;
    }

    private IReadOnlyList<WolfensteinActorState> CreateActorStates() =>
        m_actorDefinitions.Select(actor => new WolfensteinActorState(actor, Difficulty)).ToArray();

    private sealed class TimedWorldLight(double x, double y, double remainingSeconds)
    {
        public double X { get; } = x;
        public double Y { get; } = y;
        public double RemainingSeconds { get; set; } = remainingSeconds;
    }
}
