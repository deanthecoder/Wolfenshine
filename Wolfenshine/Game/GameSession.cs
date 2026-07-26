// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

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
    private const double PlayerRadius = 0.2;
    private const double OriginalPlayerRadius = 0x5800 / FixedUnitsPerTile;
    private const double MaximumMovementStep = 0.1;
    private bool m_useWasDown;
    private bool m_attackWasDown;
    private int m_attackStep;
    private double m_attackTimeRemaining;
    private PlayerWeapon m_chosenWeapon = PlayerWeapon.Pistol;
    private readonly IReadOnlyList<WolfensteinActor> m_actors;
    private readonly List<WorldSprite> m_staticObjects;

    public GameSession(
        WolfensteinMap map,
        RaycastCamera camera,
        IReadOnlyList<WolfensteinActor> actors = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(camera);
        Map = map;
        Camera = camera;
        Doors = WolfensteinDoors.FromMap(map);
        PushWalls = new WolfensteinPushWalls(map);
        SecretTotal = map.Objects.Count(marker => marker == 98);
        m_actors = actors ?? [];
        m_staticObjects = WolfensteinStaticObjects.FromMap(map).ToList();
    }

    public WolfensteinMap Map { get; }
    public RaycastCamera Camera { get; private set; }
    public WolfensteinDoors Doors { get; }
    public WolfensteinPushWalls PushWalls { get; }
    public PlayerWeapon Weapon { get; private set; } = PlayerWeapon.Pistol;
    public int WeaponFrame { get; private set; }
    public int Ammo { get; private set; } = 8;
    public int Health { get; private set; } = MaximumHealth;
    public int Score { get; private set; }
    public int TreasureCount { get; private set; }
    public int SecretCount { get; private set; }
    public int SecretTotal { get; }
    public bool IsAttacking { get; private set; }
    public IReadOnlyList<WorldSprite> StaticObjects => m_staticObjects;

#if DEBUG
    public bool ReloadDebugState()
    {
        var changed = Ammo != MaximumAmmo || Health != MaximumHealth ||
                      !IsAttacking && Weapon != m_chosenWeapon;
        GiveAmmo(MaximumAmmo);
        Health = MaximumHealth;
        return changed;
    }
#endif

    public bool Update(double elapsedSeconds, PlayerInput input)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0.0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

        var changed = Doors.Update(elapsedSeconds, CanDoorClose);
        changed |= PushWalls.Update(elapsedSeconds, CanPushWallEnterTile);
        if (input.Use && !m_useWasDown)
            changed |= OperateAhead();
        m_useWasDown = input.Use;
        changed |= UpdateWeapon(elapsedSeconds, input);
        changed |= CollectPickups(Camera.X, Camera.Y);

        var horizontal = (input.TurnRight ? 1.0 : 0.0) - (input.TurnLeft ? 1.0 : 0.0);
        var turn = input.Strafe ? 0.0 : horizontal;
        var strafe = input.Strafe ? horizontal : 0.0;
        var movement = (input.MoveForward ? 1.0 : 0.0) - (input.MoveBackward ? 1.0 : 0.0);
        if ((elapsedSeconds == 0.0 || turn == 0.0 && movement == 0.0 && strafe == 0.0) && !changed)
            return false;

        var x = Camera.X;
        var y = Camera.Y;
        var directionX = Camera.DirectionX;
        var directionY = Camera.DirectionY;
        var planeX = Camera.PlaneX;
        var planeY = Camera.PlaneY;

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
            for (var step = 0; step < stepCount; step++)
            {
                // Resolve each axis independently so the player slides naturally along nearby walls.
                var nextX = x + stepX;
                if (CanOccupy(nextX, y))
                    x = nextX;
                var nextY = y + stepY;
                if (CanOccupy(x, nextY))
                    y = nextY;
                changed |= CollectPickups(x, y);
            }
        }

        // A fresh snapshot also publishes door-only animation changes to the viewport binding.
        Camera = new RaycastCamera(x, y, directionX, directionY, planeX, planeY);
        return true;
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
        if (PushWalls.TryPush(pushWallX, pushWallY, directionX, directionY, CanPushWallEnterTile))
        {
            SecretCount++;
            return true;
        }

        // Follow a short use ray so slightly angled players can still operate the door they are facing.
        for (var distance = 0.25; distance <= 1.5; distance += 0.1)
        {
            var x = (int)Math.Floor(Camera.X + (Camera.DirectionX * distance));
            var y = (int)Math.Floor(Camera.Y + (Camera.DirectionY * distance));
            var door = Doors.Get(x, y);
            if (door != null)
                return door.Operate(CanDoorClose(door));
            if (Map.IsSolid(x, y) && !PushWalls.IsOriginalWallSuppressed(x, y))
                return false;
        }

        return false;
    }

    private (int X, int Y) GetCardinalDirection() => Math.Abs(Camera.DirectionX) > Math.Abs(Camera.DirectionY)
        ? (Math.Sign(Camera.DirectionX), 0)
        : (0, Math.Sign(Camera.DirectionY));

    private bool CanPushWallEnterTile(int x, int y)
    {
        if (x < 0 || x >= Map.Width || y < 0 || y >= Map.Height ||
            Doors.Get(x, y) != null || PushWalls.IsTileReserved(x, y) ||
            !PushWalls.IsOriginalWallSuppressed(x, y) && Map.IsSolid(x, y) ||
            WolfensteinStaticObjects.BlocksMovement(Map.GetObject(x, y)))
        {
            return false;
        }
        return m_actors.All(actor => (int)Math.Floor(actor.X) != x || (int)Math.Floor(actor.Y) != y);
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
                pickupType == WolfensteinPickupType.AmmoClip && Ammo == MaximumAmmo)
            {
                continue;
            }

            switch (pickupType)
            {
                case WolfensteinPickupType.AmmoClip:
                    GiveAmmo(8);
                    break;
                case WolfensteinPickupType.Cross:
                    CollectTreasure(100);
                    break;
                case WolfensteinPickupType.Chalice:
                    CollectTreasure(500);
                    break;
                case WolfensteinPickupType.Bible:
                    CollectTreasure(1000);
                    break;
                case WolfensteinPickupType.Crown:
                    CollectTreasure(5000);
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

    private static (double X, double Y) Rotate(double x, double y, double angle)
    {
        var cosine = Math.Cos(angle);
        var sine = Math.Sin(angle);
        return ((x * cosine) - (y * sine), (x * sine) + (y * cosine));
    }

    private bool UpdateWeapon(double elapsedSeconds, PlayerInput input)
    {
        var changed = false;
        if (!IsAttacking && Ammo > 0 && input.WeaponSelection is { } selection && selection != Weapon)
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
        if (Weapon == PlayerWeapon.Knife || Ammo == 0)
            return;
        Ammo--;
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
}
