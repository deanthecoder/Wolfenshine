// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Wolfenshine.Rendering;

namespace Wolfenshine.Game;

/// <summary>
/// Produces ordinary player input for Wolfenshine's attract-mode demonstration.
/// </summary>
/// <remarks>
/// The controller never mutates the level directly: movement, combat, pickups, doors, secrets, and exits all use the
/// same input and gameplay paths as a human player.
/// </remarks>
public sealed class AutoPlayerController
{
    private const ushort ElevatorSwitchTile = 21;
    private const double RouteRefreshInterval = 0.25;
    private const double WaypointDistance = 0.24;
    private const double AimTolerance = 0.085;
    private const double ConfirmedHitAimTolerance = 0.13;
    private const double ConfirmedHitLockDuration = 0.65;
    private const double FireBurstDuration = 0.85;
    private const double FireBurstAfterHitDuration = 0.55;
    private const double RepositionDuration = 0.32;
    private const double DoorAimTolerance = 0.16;
    private const double StuckDetectionDuration = 0.9;
    private IReadOnlyList<NavigationRoutePoint> m_route = [];
    private int m_routeIndex;
    private double m_routeRefreshTime;
    private WolfensteinActorState m_targetActor;
    private AutoPlayerObjective m_objective;
    private (int X, int Y)? m_interactionTarget;
    private (int X, int Y)? m_pendingPushWall;
    private double m_pendingPushWallTime;
    private bool m_releaseUse;
    private WolfensteinActorState m_combatTarget;
    private int m_lastTargetHitPoints;
    private double m_confirmedHitLockTime;
    private double m_fireBurstTime;
    private double m_repositionTime;
    private bool m_strafeRight;
    private double m_previousX = double.NaN;
    private double m_previousY = double.NaN;
    private double m_stuckTime;
    private double m_recoveryTime;
    private double m_steeringTime;
    private double m_turnCoastTime;
    private int m_lastTurnDirection;

    public AutoPlayerObjective Objective => m_objective;

    public PlayerInput Update(GameSession session, double elapsedSeconds)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0.0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        m_steeringTime += elapsedSeconds;
        m_turnCoastTime = Math.Max(0.0, m_turnCoastTime - elapsedSeconds);
        if (session.IsDying || session.IsCompletingLevel)
            return default;
        if (m_releaseUse)
        {
            m_releaseUse = false;
            return default;
        }
        if (UpdatePushWallWait(session, elapsedSeconds))
            return default;
        if (m_recoveryTime > 0.0)
        {
            m_recoveryTime = Math.Max(0.0, m_recoveryTime - elapsedSeconds);
            return new PlayerInput(false, true, false, true, Run: true);
        }

        var visibleEnemy = FindVisibleEnemy(session);
        if (visibleEnemy != null)
        {
            if (!ReferenceEquals(m_targetActor, visibleEnemy))
                InvalidateRoute();
            m_targetActor = visibleEnemy;
            m_objective = AutoPlayerObjective.Enemy;
            return EngageEnemy(session, visibleEnemy, elapsedSeconds);
        }
        m_combatTarget = null;
        if (m_targetActor?.IsDead != false)
            m_targetActor = null;

        m_routeRefreshTime -= elapsedSeconds;
        if (m_route.Count == 0 || m_routeRefreshTime <= 0.0 || !IsCurrentRouteUseful(session))
            SelectObjective(session);
        var input = FollowRoute(session);
        UpdateStuckDetection(session, input, elapsedSeconds);
        return input;
    }

    private WolfensteinActorState FindVisibleEnemy(GameSession session)
    {
        if (m_targetActor?.IsDead == false && session.HasLineOfSightTo(m_targetActor.X, m_targetActor.Y))
            return m_targetActor;
        WolfensteinActorState nearest = null;
        var nearestDistance = double.PositiveInfinity;
        foreach (var actor in session.Actors)
        {
            if (actor.IsDead || !session.HasLineOfSightTo(actor.X, actor.Y))
                continue;
            var deltaX = actor.X - session.Camera.X;
            var deltaY = actor.Y - session.Camera.Y;
            var distance = (deltaX * deltaX) + (deltaY * deltaY);
            var inFront = (deltaX * session.Camera.DirectionX) + (deltaY * session.Camera.DirectionY) > 0.0;
            if (!inFront && actor.Behavior == WolfensteinActorBehavior.Dormant)
                continue;
            if (distance >= nearestDistance)
                continue;
            nearest = actor;
            nearestDistance = distance;
        }
        return nearest;
    }

    private PlayerInput EngageEnemy(
        GameSession session,
        WolfensteinActorState target,
        double elapsedSeconds)
    {
        if (!ReferenceEquals(m_combatTarget, target))
            BeginCombat(target);
        if (target.HitPoints < m_lastTargetHitPoints)
        {
            m_confirmedHitLockTime = ConfirmedHitLockDuration;
            m_fireBurstTime = Math.Max(m_fireBurstTime, FireBurstAfterHitDuration);
            m_repositionTime = 0.0;
        }
        m_lastTargetHitPoints = target.HitPoints;
        m_confirmedHitLockTime = Math.Max(0.0, m_confirmedHitLockTime - elapsedSeconds);

        var deltaX = target.X - session.Camera.X;
        var deltaY = target.Y - session.Camera.Y;
        var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        var angle = GetAngleDelta(session.Camera, target.X, target.Y);
        var aimTolerance = m_confirmedHitLockTime > 0.0 ? ConfirmedHitAimTolerance : AimTolerance;
        if (Math.Abs(angle) > aimTolerance)
            return SteerTowards(angle, moveForward: false, run: false);

        if (session.Ammo == 0)
        {
            var knifeAttack = distance <= 1.35 && !session.IsAttacking;
            return new PlayerInput(
                MoveForward: distance > 1.15,
                MoveBackward: false,
                TurnLeft: false,
                TurnRight: false,
                Run: distance > 2.0,
                Attack: knifeAttack,
                WeaponSelection: PlayerWeapon.Knife);
        }

        if (m_repositionTime > 0.0)
        {
            m_repositionTime = Math.Max(0.0, m_repositionTime - elapsedSeconds);
            if (m_repositionTime == 0.0)
                m_fireBurstTime = FireBurstDuration;
            return new PlayerInput(
                MoveForward: false,
                MoveBackward: distance <= 1.4,
                TurnLeft: !m_strafeRight && distance > 1.4,
                TurnRight: m_strafeRight && distance > 1.4,
                Run: false,
                Strafe: distance > 1.4);
        }

        m_fireBurstTime = Math.Max(0.0, m_fireBurstTime - elapsedSeconds);
        var automaticWeapon = session.BestWeapon is PlayerWeapon.MachineGun or PlayerWeapon.Chaingun;
        var fire = automaticWeapon || !session.IsAttacking;
        var input = new PlayerInput(
            MoveForward: false,
            MoveBackward: distance < 1.25,
            TurnLeft: false,
            TurnRight: false,
            Run: distance < 1.25,
            Attack: fire,
            WeaponSelection: session.BestWeapon);
        if (m_fireBurstTime == 0.0)
        {
            m_repositionTime = RepositionDuration;
            m_strafeRight = !m_strafeRight;
        }
        return input;
    }

    private void BeginCombat(WolfensteinActorState target)
    {
        m_combatTarget = target;
        m_lastTargetHitPoints = target.HitPoints;
        m_confirmedHitLockTime = 0.0;
        m_fireBurstTime = FireBurstDuration;
        m_repositionTime = 0.0;
    }

    private void SelectObjective(GameSession session)
    {
        m_routeRefreshTime = RouteRefreshInterval;
        m_interactionTarget = null;
        var actors = session.Actors.Where(actor => !actor.IsDead).ToArray();
        var actorApproaches = actors
            .SelectMany(actor => GetAdjacentTiles(session, (int)Math.Floor(actor.X), (int)Math.Floor(actor.Y)))
            .Distinct()
            .ToArray();
        var route = AutoPlayerRoutePlanner.FindNearest(session, actorApproaches, allowClosedDoors: false);
        if (route.Count > 0)
        {
            var end = route[^1];
            m_targetActor = actors
                .OrderBy(actor => DistanceSquared(actor.X, actor.Y, end.X + 0.5, end.Y + 0.5))
                .FirstOrDefault();
            SetRoute(session, route, AutoPlayerObjective.Enemy);
            return;
        }

        if (session.Health < 100 && TryRouteToPickup(session, IsHealthPickup, AutoPlayerObjective.Health))
            return;
        if (TryRouteToPickup(
                session,
                type => IsWeaponUpgrade(type, session.BestWeapon),
                AutoPlayerObjective.Weapon))
        {
            return;
        }
        if (session.Ammo < 99 && TryRouteToPickup(session, IsAmmoPickup, AutoPlayerObjective.Ammo))
            return;
        if (TryRouteToPickup(session, IsTreasurePickup, AutoPlayerObjective.Treasure))
            return;
        if (TryRouteToSecret(session))
            return;

        var camera = session.Camera;
        var navigationRoute = NavigationRoutePlanner.Find(
            session.Map,
            session.Doors,
            session.PushWalls,
            (int)Math.Floor(camera.X),
            (int)Math.Floor(camera.Y),
            session.StaticObjects,
            session.HasGoldKey,
            session.HasSilverKey);
        if (navigationRoute.Points.Count == 0)
        {
            SetRoute(session, [], AutoPlayerObjective.None);
            return;
        }
        if (navigationRoute.TargetType == NavigationTargetType.Exit)
        {
            var end = navigationRoute.Points[^1];
            m_interactionTarget = FindAdjacentExit(session, end.X, end.Y);
        }
        SetRoute(
            session,
            navigationRoute.Points,
            navigationRoute.TargetType == NavigationTargetType.Exit
                ? AutoPlayerObjective.Exit
                : AutoPlayerObjective.Key);
    }

    private bool TryRouteToPickup(
        GameSession session,
        Func<WolfensteinPickupType, bool> predicate,
        AutoPlayerObjective objective)
    {
        var pickups = session.StaticObjects
            .Where(item => predicate(WolfensteinStaticObjects.GetPickupType(item.SpriteNumber)))
            .Select(item => new NavigationRoutePoint((int)Math.Floor(item.X), (int)Math.Floor(item.Y)))
            .Distinct()
            .ToArray();
        var route = AutoPlayerRoutePlanner.FindNearest(session, pickups, allowClosedDoors: false);
        if (route.Count == 0)
            return false;
        SetRoute(session, route, objective);
        return true;
    }

    private bool TryRouteToSecret(GameSession session)
    {
        var activatedSecrets = session.PushWalls.Items
            .Select(wall => (wall.OriginX, wall.OriginY))
            .ToHashSet();
        var approaches = new List<NavigationRoutePoint>();
        for (var y = 0; y < session.Map.Height; y++)
        {
            for (var x = 0; x < session.Map.Width; x++)
            {
                if (session.Map.GetObject(x, y) != 98 || activatedSecrets.Contains((x, y)))
                    continue;
                approaches.AddRange(GetAdjacentTiles(session, x, y)
                    .Where(point => CanPushFrom(session, point, x, y)));
            }
        }
        var route = AutoPlayerRoutePlanner.FindNearest(session, approaches.Distinct().ToArray(), allowClosedDoors: false);
        if (route.Count == 0)
            return false;
        var end = route[^1];
        m_interactionTarget = FindAdjacentSecret(session, end.X, end.Y, activatedSecrets);
        if (m_interactionTarget == null)
            return false;
        SetRoute(session, route, AutoPlayerObjective.Secret);
        return true;
    }

    private PlayerInput FollowRoute(GameSession session)
    {
        while (m_routeIndex < m_route.Count)
        {
            var point = m_route[m_routeIndex];
            if (DistanceSquared(session.Camera.X, session.Camera.Y, point.X + 0.5, point.Y + 0.5) >
                WaypointDistance * WaypointDistance && !HasPassedWaypoint(session.Camera, m_routeIndex))
            {
                break;
            }
            m_routeIndex++;
        }
        if (m_routeIndex >= m_route.Count)
            return OperateObjective(session);

        var lookAheadIndex = AutoPlayerRoutePlanner.FindStraightLookAhead(
            session,
            m_route,
            m_routeIndex);
        var target = m_route[lookAheadIndex];
        var door = session.Doors.Get(target.X, target.Y);
        var angle = GetAngleDelta(session.Camera, target.X + 0.5, target.Y + 0.5);
        if (door != null && !door.IsFullyOpen)
        {
            var distance = Math.Sqrt(DistanceSquared(
                session.Camera.X,
                session.Camera.Y,
                target.X + 0.5,
                target.Y + 0.5));
            if (Math.Abs(angle) > DoorAimTolerance)
                return SteerTowards(angle, moveForward: false, run: false);
            if (distance <= 1.35 && !door.IsOpening)
                return PressUse();
            return new PlayerInput(distance > 1.0, false, false, false);
        }
        if (Math.Abs(angle) > 0.42)
            return SteerTowards(angle, moveForward: false, run: false);
        return SteerTowards(
            angle,
            moveForward: true,
            run: m_route.Count - m_routeIndex > 2);
    }

    private PlayerInput OperateObjective(GameSession session)
    {
        if (m_objective == AutoPlayerObjective.Enemy)
        {
            InvalidateRoute();
            return default;
        }
        if (m_objective is AutoPlayerObjective.Health or AutoPlayerObjective.Weapon or
            AutoPlayerObjective.Ammo or AutoPlayerObjective.Treasure or AutoPlayerObjective.Key)
        {
            InvalidateRoute();
            return default;
        }
        if (m_interactionTarget is not { } target)
        {
            InvalidateRoute();
            return default;
        }
        var angle = GetAngleDelta(session.Camera, target.X + 0.5, target.Y + 0.5);
        if (Math.Abs(angle) > DoorAimTolerance)
            return SteerTowards(angle, moveForward: false, run: false);
        if (m_objective == AutoPlayerObjective.Secret)
        {
            m_pendingPushWall = target;
            m_pendingPushWallTime = 0.0;
        }
        return PressUse();
    }

    private bool UpdatePushWallWait(GameSession session, double elapsedSeconds)
    {
        if (m_pendingPushWall is not { } target)
            return false;
        m_pendingPushWallTime += elapsedSeconds;
        var wall = session.PushWalls.Items.FirstOrDefault(
            item => item.OriginX == target.X && item.OriginY == target.Y);
        if (wall?.IsMoving == true)
            return true;
        if (wall == null && m_pendingPushWallTime < 0.5)
            return true;
        m_pendingPushWall = null;
        InvalidateRoute();
        return false;
    }

    private bool IsCurrentRouteUseful(GameSession session)
    {
        if (m_objective == AutoPlayerObjective.Enemy && m_targetActor?.IsDead != false)
            return false;
        if (m_routeIndex >= m_route.Count)
            return true;
        var point = m_route[m_routeIndex];
        return !session.PushWalls.IsTileReserved(point.X, point.Y);
    }

    private void UpdateStuckDetection(GameSession session, PlayerInput input, double elapsedSeconds)
    {
        if (!input.MoveForward || !double.IsFinite(m_previousX))
        {
            m_stuckTime = 0.0;
        }
        else if (DistanceSquared(session.Camera.X, session.Camera.Y, m_previousX, m_previousY) < 0.0004)
        {
            m_stuckTime += elapsedSeconds;
            if (m_stuckTime >= StuckDetectionDuration)
            {
                m_stuckTime = 0.0;
                m_recoveryTime = 0.45;
                InvalidateRoute();
            }
        }
        else
        {
            m_stuckTime = 0.0;
        }
        m_previousX = session.Camera.X;
        m_previousY = session.Camera.Y;
    }

    private static IReadOnlyList<NavigationRoutePoint> GetAdjacentTiles(GameSession session, int x, int y)
    {
        NavigationRoutePoint[] candidates =
        [
            new(x - 1, y),
            new(x + 1, y),
            new(x, y - 1),
            new(x, y + 1)
        ];
        return candidates.Where(point => IsFloor(session, point.X, point.Y)).ToArray();
    }

    private static bool IsFloor(GameSession session, int x, int y) =>
        x >= 0 && x < session.Map.Width && y >= 0 && y < session.Map.Height &&
        !session.Map.IsSolid(x, y) && session.Doors.Get(x, y) == null &&
        !session.PushWalls.IsTileReserved(x, y) &&
        !WolfensteinStaticObjects.BlocksMovement(session.Map.GetObject(x, y));

    private static bool CanPushFrom(
        GameSession session,
        NavigationRoutePoint approach,
        int secretX,
        int secretY)
    {
        var directionX = secretX - approach.X;
        var directionY = secretY - approach.Y;
        var destinationX = secretX + directionX;
        var destinationY = secretY + directionY;
        return IsFloor(session, destinationX, destinationY) &&
               session.Actors.All(actor => actor.IsDead ||
                   (int)Math.Floor(actor.X) != destinationX ||
                   (int)Math.Floor(actor.Y) != destinationY);
    }

    private static (int X, int Y)? FindAdjacentExit(GameSession session, int x, int y)
    {
        (int X, int Y)[] candidates = [(x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)];
        foreach (var candidate in candidates)
        {
            if (candidate.X >= 0 && candidate.X < session.Map.Width &&
                candidate.Y >= 0 && candidate.Y < session.Map.Height &&
                session.Map.GetWall(candidate.X, candidate.Y) == ElevatorSwitchTile)
            {
                return candidate;
            }
        }
        return null;
    }

    private static (int X, int Y)? FindAdjacentSecret(
        GameSession session,
        int x,
        int y,
        IReadOnlySet<(int X, int Y)> activatedSecrets)
    {
        (int X, int Y)[] candidates = [(x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)];
        foreach (var candidate in candidates)
        {
            if (candidate.X < 0 || candidate.X >= session.Map.Width ||
                candidate.Y < 0 || candidate.Y >= session.Map.Height ||
                activatedSecrets.Contains(candidate))
            {
                continue;
            }
            if (session.Map.GetObject(candidate.X, candidate.Y) == 98)
                return candidate;
        }
        return null;
    }

    private static bool IsHealthPickup(WolfensteinPickupType type) => type is
        WolfensteinPickupType.DogFood or WolfensteinPickupType.Food or
        WolfensteinPickupType.FirstAid or WolfensteinPickupType.FullHeal;

    private static bool IsAmmoPickup(WolfensteinPickupType type) => type is
        WolfensteinPickupType.AmmoClip or WolfensteinPickupType.MachineGun or WolfensteinPickupType.Chaingun;

    private static bool IsWeaponUpgrade(WolfensteinPickupType type, PlayerWeapon bestWeapon) => type switch
    {
        WolfensteinPickupType.MachineGun => bestWeapon < PlayerWeapon.MachineGun,
        WolfensteinPickupType.Chaingun => bestWeapon < PlayerWeapon.Chaingun,
        _ => false
    };

    private static bool IsTreasurePickup(WolfensteinPickupType type) => type is
        WolfensteinPickupType.Cross or WolfensteinPickupType.Chalice or WolfensteinPickupType.Bible or
        WolfensteinPickupType.Crown or WolfensteinPickupType.FullHeal;

    private PlayerInput SteerTowards(double angle, bool moveForward, bool run)
    {
        var minimumCorrection = moveForward ? 0.045 : 0.015;
        var magnitude = Math.Abs(angle);
        if (magnitude < minimumCorrection)
        {
            m_lastTurnDirection = 0;
            return new PlayerInput(moveForward, false, false, false, Run: run);
        }

        var direction = Math.Sign(angle);
        if (m_lastTurnDirection != 0 && direction != m_lastTurnDirection)
            m_turnCoastTime = Math.Max(m_turnCoastTime, 0.12);
        m_lastTurnDirection = direction;
        if (m_turnCoastTime > 0.0)
            return new PlayerInput(moveForward && magnitude < 0.18, false, false, false);

        var shouldTurn = magnitude switch
        {
            > 0.10 => true,
            > 0.04 => m_steeringTime % 0.10 < 0.065,
            _ => m_steeringTime % 0.14 < 0.045
        };
        return new PlayerInput(
            MoveForward: moveForward,
            MoveBackward: false,
            TurnLeft: shouldTurn && direction < 0,
            TurnRight: shouldTurn && direction > 0,
            Run: run && !shouldTurn);
    }

    private PlayerInput PressUse()
    {
        m_releaseUse = true;
        return new PlayerInput(false, false, false, false, Use: true);
    }

    private void SetRoute(
        GameSession session,
        IReadOnlyList<NavigationRoutePoint> route,
        AutoPlayerObjective objective)
    {
        m_route = AutoPlayerRoutePlanner.SmoothCorners(session, route);
        // Every planner includes the player's current tile. After a refresh the player is commonly no longer at that
        // tile's center, so targeting it would make the controller double back before continuing along the real route.
        m_routeIndex = m_route.Count > 1 ? 1 : 0;
        m_objective = objective;
        m_routeRefreshTime = RouteRefreshInterval;
    }

    private void InvalidateRoute()
    {
        m_route = [];
        m_routeIndex = 0;
        m_routeRefreshTime = 0.0;
        m_interactionTarget = null;
    }

    private bool HasPassedWaypoint(RaycastCamera camera, int routeIndex)
    {
        if (routeIndex <= 0 || routeIndex >= m_route.Count)
            return false;
        var previous = m_route[routeIndex - 1];
        var waypoint = m_route[routeIndex];
        var directionX = waypoint.X - previous.X;
        var directionY = waypoint.Y - previous.Y;
        var playerFromWaypointX = camera.X - (waypoint.X + 0.5);
        var playerFromWaypointY = camera.Y - (waypoint.Y + 0.5);
        return (playerFromWaypointX * directionX) + (playerFromWaypointY * directionY) >= 0.0;
    }

    private static double GetAngleDelta(RaycastCamera camera, double targetX, double targetY)
    {
        var desired = Math.Atan2(targetY - camera.Y, targetX - camera.X);
        var current = Math.Atan2(camera.DirectionY, camera.DirectionX);
        var delta = desired - current;
        while (delta > Math.PI)
            delta -= Math.PI * 2.0;
        while (delta < -Math.PI)
            delta += Math.PI * 2.0;
        return delta;
    }

    private static double DistanceSquared(double x1, double y1, double x2, double y2)
    {
        var deltaX = x2 - x1;
        var deltaY = y2 - y1;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }
}

/// <summary>
/// Identifies the current attract-mode intention for diagnostics and tests.
/// </summary>
public enum AutoPlayerObjective
{
    None,
    Enemy,
    Health,
    Weapon,
    Ammo,
    Treasure,
    Secret,
    Key,
    Exit
}
