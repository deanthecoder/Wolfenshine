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
    private const int RenewedAmmo = 99;
    private const double PlayerRadius = 0.2;
    private const double MaximumMovementStep = 0.1;
    private bool m_useWasDown;
    private bool m_attackWasDown;
    private int m_attackStep;
    private double m_attackTimeRemaining;

    public GameSession(WolfensteinMap map, RaycastCamera camera)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(camera);
        Map = map;
        Camera = camera;
        Doors = WolfensteinDoors.FromMap(map);
    }

    public WolfensteinMap Map { get; }
    public RaycastCamera Camera { get; private set; }
    public WolfensteinDoors Doors { get; }
    public PlayerWeapon Weapon { get; private set; } = PlayerWeapon.Pistol;
    public int WeaponFrame { get; private set; }
    public int Ammo { get; private set; } = 8;
    public bool IsAttacking { get; private set; }

    public bool Update(double elapsedSeconds, PlayerInput input)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0.0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));

        var changed = Doors.Update(elapsedSeconds);
        if (input.Use && !m_useWasDown)
            changed |= OpenDoorAhead();
        m_useWasDown = input.Use;
        changed |= UpdateWeapon(elapsedSeconds, input);

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
            }
        }

        // A fresh snapshot also publishes door-only animation changes to the viewport binding.
        Camera = new RaycastCamera(x, y, directionX, directionY, planeX, planeY);
        return true;
    }

    private bool CanOccupy(double x, double y) =>
        !IsSolid((int)Math.Floor(x - PlayerRadius), (int)Math.Floor(y - PlayerRadius)) &&
        !IsSolid((int)Math.Floor(x + PlayerRadius), (int)Math.Floor(y - PlayerRadius)) &&
        !IsSolid((int)Math.Floor(x - PlayerRadius), (int)Math.Floor(y + PlayerRadius)) &&
        !IsSolid((int)Math.Floor(x + PlayerRadius), (int)Math.Floor(y + PlayerRadius));

    private bool IsSolid(int x, int y)
    {
        var door = Doors.Get(x, y);
        return door == null ? Map.IsSolid(x, y) : !door.IsFullyOpen;
    }

    private bool OpenDoorAhead()
    {
        // Follow a short use ray so slightly angled players can still operate the door they are facing.
        for (var distance = 0.25; distance <= 1.5; distance += 0.1)
        {
            var x = (int)Math.Floor(Camera.X + (Camera.DirectionX * distance));
            var y = (int)Math.Floor(Camera.Y + (Camera.DirectionY * distance));
            var door = Doors.Get(x, y);
            if (door != null)
                return door.Open();
            if (Map.IsSolid(x, y))
                return false;
        }

        return false;
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
        if (!IsAttacking && input.WeaponSelection is { } selection && selection != Weapon)
        {
            Weapon = selection;
            WeaponFrame = 0;
            changed = true;
        }

        if (!IsAttacking && input.Attack && !m_attackWasDown)
        {
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
                case 2 when Weapon == PlayerWeapon.MachineGun && input.Attack:
                    SetAttackStep(1);
                    break;
                case 2 when Weapon == PlayerWeapon.Chaingun:
                    FireCurrentWeapon();
                    SetAttackStep(input.Attack ? 1 : 3);
                    break;
                case 2:
                    SetAttackStep(3);
                    break;
                default:
                    IsAttacking = false;
                    WeaponFrame = 0;
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
        if (Weapon == PlayerWeapon.Knife)
            return;
        Ammo--;
        if (Ammo <= 0)
            Ammo = RenewedAmmo;
    }
}
