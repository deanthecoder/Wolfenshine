// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using NUnit.Framework;
using Wolfenshine.Game;
using Wolfenshine.Maps;
using Wolfenshine.Rendering;

namespace Wolfenshine.Tests.Game;

/// <summary>
/// Verifies attract-mode decisions through the same input path used during normal play.
/// </summary>
public sealed class AutoPlayerControllerTests
{
    [Test]
    public void GivenClosedDoorCheckLongRouteCanPlanThroughIt()
    {
        var map = CreateCorridorMap();
        var session = new GameSession(map, RaycastCamera.FromPlayerStart(map));
        NavigationRoutePoint[] target = [new(5, 2)];

        var openAreaRoute = AutoPlayerRoutePlanner.FindNearest(session, target, allowClosedDoors: false);
        var longRoute = AutoPlayerRoutePlanner.FindNearest(session, target, allowClosedDoors: true);

        Assert.Multiple(() =>
        {
            Assert.That(openAreaRoute, Is.Empty);
            Assert.That(longRoute, Has.Some.EqualTo(new NavigationRoutePoint(3, 2)));
            Assert.That(longRoute[^1], Is.EqualTo(target[0]));
        });
    }

    [Test]
    public void GivenClearInsideCornerCheckSquareBendIsRemoved()
    {
        var map = CreateOpenMap(6, 5, 1, 1, playerMarker: 20);
        var session = new GameSession(map, RaycastCamera.FromPlayerStart(map));
        NavigationRoutePoint[] route = [new(1, 1), new(2, 1), new(2, 2)];

        var smoothed = AutoPlayerRoutePlanner.SmoothCorners(session, route);

        Assert.That(smoothed, Is.EqualTo(new[] { route[0], route[2] }));
    }

    [Test]
    public void GivenBlockedInsideCornerCheckSquareBendIsRetained()
    {
        const int width = 6;
        const int height = 5;
        var walls = CreateEnclosedWalls(width, height);
        walls[(2 * width) + 1] = 1;
        var objects = new ushort[width * height];
        objects[(1 * width) + 1] = 20;
        var map = new WolfensteinMap(0, "Blocked corner", width, height, walls, objects);
        var session = new GameSession(map, RaycastCamera.FromPlayerStart(map));
        NavigationRoutePoint[] route = [new(1, 1), new(2, 1), new(2, 2)];

        var smoothed = AutoPlayerRoutePlanner.SmoothCorners(session, route);

        Assert.That(smoothed, Is.EqualTo(route));
    }

    [Test]
    public void GivenStraightCorridorCheckLookAheadTargetsDoorAtItsEnd()
    {
        var map = CreateCorridorMap();
        var session = new GameSession(map, RaycastCamera.FromPlayerStart(map));
        NavigationRoutePoint[] route =
        [
            new(1, 2),
            new(2, 2),
            new(3, 2),
            new(4, 2),
            new(5, 2)
        ];

        var lookAhead = AutoPlayerRoutePlanner.FindStraightLookAhead(session, route, 1);

        Assert.That(lookAhead, Is.EqualTo(2));
        Assert.That(route[lookAhead], Is.EqualTo(new NavigationRoutePoint(3, 2)));
    }

    [Test]
    public void GivenStraightOpenRouteCheckLookAheadTargetsItsLastPoint()
    {
        var map = CreateOpenMap(7, 5, 1, 2, playerMarker: 20);
        var session = new GameSession(map, RaycastCamera.FromPlayerStart(map));
        NavigationRoutePoint[] route = [new(1, 2), new(2, 2), new(3, 2), new(4, 2), new(5, 2)];

        var lookAhead = AutoPlayerRoutePlanner.FindStraightLookAhead(session, route, 1);

        Assert.That(lookAhead, Is.EqualTo(route.Length - 1));
    }

    [Test]
    public void GivenVisibleEnemyCheckAutoPlayerAimsAndFires()
    {
        var map = CreateOpenMap(7, 5, 2, 2, playerMarker: 20);
        var actor = new WolfensteinActor(4.5, 2.5, WolfensteinActorType.Guard, 2, false, false, 58);
        var session = new GameSession(map, RaycastCamera.FromPlayerStart(map), [actor]);
        var controller = new AutoPlayerController();

        var input = controller.Update(session, 1.0 / 60.0);

        Assert.Multiple(() =>
        {
            Assert.That(controller.Objective, Is.EqualTo(AutoPlayerObjective.Enemy));
            Assert.That(input.Attack, Is.True);
            Assert.That(input.WeaponSelection, Is.EqualTo(PlayerWeapon.Pistol));
        });
    }

    [Test]
    public void GivenCombatStartsCheckAutoPlayerFiresABurstBeforeStrafing()
    {
        var map = CreateOpenMap(7, 5, 2, 2, playerMarker: 20);
        var actor = new WolfensteinActor(4.5, 2.5, WolfensteinActorType.Guard, 2, false, false, 58);
        var session = new GameSession(map, RaycastCamera.FromPlayerStart(map), [actor]);
        var controller = new AutoPlayerController();

        var openingInputs = Enumerable.Range(0, 40)
            .Select(_ => controller.Update(session, 1.0 / 60.0))
            .ToArray();
        var laterInputs = Enumerable.Range(0, 30)
            .Select(_ => controller.Update(session, 1.0 / 60.0))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(openingInputs, Has.Some.Property(nameof(PlayerInput.Attack)).True);
            Assert.That(openingInputs, Has.None.Property(nameof(PlayerInput.Strafe)).True);
            Assert.That(laterInputs, Has.Some.Property(nameof(PlayerInput.Strafe)).True);
        });
    }

    [Test]
    public void GivenConfirmedHitCheckAutoPlayerKeepsFiringInsteadOfStrafing()
    {
        var map = CreateOpenMap(7, 5, 2, 2, playerMarker: 20);
        var actor = new WolfensteinActor(4.5, 2.5, WolfensteinActorType.Guard, 2, false, false, 58);
        var session = new GameSession(map, RaycastCamera.FromPlayerStart(map), [actor]);
        var controller = new AutoPlayerController();

        for (var update = 0; update < 48; update++)
            controller.Update(session, 1.0 / 60.0);
        session.Actors[0].Damage(1);
        var inputsAfterHit = Enumerable.Range(0, 24)
            .Select(_ => controller.Update(session, 1.0 / 60.0))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(inputsAfterHit, Has.Some.Property(nameof(PlayerInput.Attack)).True);
            Assert.That(inputsAfterHit, Has.None.Property(nameof(PlayerInput.Strafe)).True);
        });
    }

    [Test]
    public void GivenDoorAndExitCheckAutoPlayerCompletesLevelUsingNormalInputs()
    {
        var map = CreateCorridorMap();
        var session = new GameSession(map, RaycastCamera.FromPlayerStart(map));
        var controller = new AutoPlayerController();

        for (var update = 0; update < 3600 && !session.IsCompletingLevel; update++)
        {
            var input = controller.Update(session, 1.0 / 60.0);
            session.Update(1.0 / 60.0, input, useMomentumMovement: true);
        }

        Assert.Multiple(() =>
        {
            Assert.That(session.Doors.Get(3, 2).OpenAmount, Is.GreaterThan(0.0));
            Assert.That(session.Camera.X, Is.GreaterThan(3.5));
            Assert.That(session.IsCompletingLevel, Is.True);
        });
    }

    [Test]
    public void GivenRouteRefreshBetweenTileCentersCheckAutoPlayerDoesNotTurnBack()
    {
        var map = CreateCorridorMap();
        var camera = new RaycastCamera(1.8, 2.5, 1.0, 0.0, 0.0, 0.66);
        var session = new GameSession(map, camera);
        var controller = new AutoPlayerController();

        var input = controller.Update(session, 1.0 / 60.0);

        Assert.Multiple(() =>
        {
            Assert.That(input.MoveForward, Is.True);
            Assert.That(input.TurnLeft, Is.False);
            Assert.That(input.TurnRight, Is.False);
        });
    }

    [Test]
    public void GivenNearbyFoodCheckItIsChosenBeforeTheExit()
    {
        var map = CreateOpenMap(7, 5, 2, 2, playerMarker: 20, objectX: 3, objectY: 2, objectMarker: 47);
        var state = new WolfensteinPlayerState(
            50,
            8,
            3,
            0,
            PlayerWeapon.Pistol,
            PlayerWeapon.Pistol,
            PlayerWeapon.Pistol);
        var session = new GameSession(map, RaycastCamera.FromPlayerStart(map), playerState: state);
        var controller = new AutoPlayerController();

        controller.Update(session, 1.0 / 60.0);

        Assert.That(controller.Objective, Is.EqualTo(AutoPlayerObjective.Health));
    }

    [Test]
    public void GivenNearbyTreasureCheckItIsChosenBeforeTheExit()
    {
        var map = CreateOpenMap(7, 5, 2, 2, playerMarker: 20, objectX: 3, objectY: 2, objectMarker: 52);
        var session = new GameSession(map, RaycastCamera.FromPlayerStart(map));
        var controller = new AutoPlayerController();

        controller.Update(session, 1.0 / 60.0);

        Assert.That(controller.Objective, Is.EqualTo(AutoPlayerObjective.Treasure));
    }

    [Test]
    public void GivenFullAmmoAndWeaponUpgradeCheckWeaponIsStillChosen()
    {
        var map = CreateOpenMap(7, 5, 2, 2, playerMarker: 20, objectX: 3, objectY: 2, objectMarker: 50);
        var state = new WolfensteinPlayerState(
            100,
            99,
            3,
            0,
            PlayerWeapon.Pistol,
            PlayerWeapon.Pistol,
            PlayerWeapon.Pistol);
        var session = new GameSession(map, RaycastCamera.FromPlayerStart(map), playerState: state);
        var controller = new AutoPlayerController();

        controller.Update(session, 1.0 / 60.0);

        Assert.That(controller.Objective, Is.EqualTo(AutoPlayerObjective.Weapon));
    }

    [Test]
    public void GivenVisibleEnemyCheckAutoPlayerKillsItThroughWeaponInput()
    {
        var map = CreateOpenMap(7, 5, 2, 2, playerMarker: 20);
        var actor = new WolfensteinActor(4.5, 2.5, WolfensteinActorType.Guard, 2, false, false, 58);
        var session = new GameSession(map, RaycastCamera.FromPlayerStart(map), [actor], GameDifficulty.Baby);
        var controller = new AutoPlayerController();

        for (var update = 0; update < 1800 && !session.Actors[0].IsDead; update++)
        {
            var input = controller.Update(session, 1.0 / 60.0);
            session.Update(1.0 / 60.0, input, useMomentumMovement: true);
        }

        Assert.Multiple(() =>
        {
            Assert.That(session.Actors[0].IsDead, Is.True);
            Assert.That(session.KillCount, Is.EqualTo(1));
            Assert.That(session.Ammo, Is.LessThan(8));
        });
    }

    [Test]
    public void GivenAccessibleSecretCheckAutoPlayerPushesItAndWaitsForCompletion()
    {
        const int width = 7;
        const int height = 5;
        var walls = CreateEnclosedWalls(width, height);
        walls[(2 * width) + 3] = 1;
        var objects = new ushort[width * height];
        objects[(2 * width) + 2] = 20;
        objects[(2 * width) + 3] = 98;
        var map = new WolfensteinMap(0, "Auto secret", width, height, walls, objects);
        var session = new GameSession(map, RaycastCamera.FromPlayerStart(map));
        var controller = new AutoPlayerController();

        for (var update = 0; update < 600 &&
             (session.PushWalls.Items.Count == 0 || session.PushWalls.Items[0].IsMoving); update++)
        {
            var input = controller.Update(session, 1.0 / 60.0);
            session.Update(1.0 / 60.0, input, useMomentumMovement: true);
        }

        Assert.Multiple(() =>
        {
            Assert.That(session.SecretCount, Is.EqualTo(1));
            Assert.That(session.PushWalls.Items, Has.Count.EqualTo(1));
            Assert.That(session.PushWalls.Items[0].IsMoving, Is.False);
        });
    }

    private static WolfensteinMap CreateCorridorMap()
    {
        const int width = 8;
        const int height = 5;
        var walls = CreateEnclosedWalls(width, height);
        for (var y = 1; y < height - 1; y++)
            walls[(y * width) + 3] = 1;
        walls[(2 * width) + 3] = 90;
        walls[(2 * width) + 6] = 21;
        var objects = new ushort[width * height];
        objects[(2 * width) + 1] = 20;
        return new WolfensteinMap(0, "Auto corridor", width, height, walls, objects);
    }

    private static WolfensteinMap CreateOpenMap(
        int width,
        int height,
        int playerX,
        int playerY,
        ushort playerMarker,
        int objectX = -1,
        int objectY = -1,
        ushort objectMarker = 0)
    {
        var walls = CreateEnclosedWalls(width, height);
        var objects = new ushort[width * height];
        objects[(playerY * width) + playerX] = playerMarker;
        if (objectX >= 0 && objectY >= 0)
            objects[(objectY * width) + objectX] = objectMarker;
        return new WolfensteinMap(0, "Auto room", width, height, walls, objects);
    }

    private static ushort[] CreateEnclosedWalls(int width, int height)
    {
        var walls = Enumerable.Repeat((ushort)107, width * height).ToArray();
        for (var x = 0; x < width; x++)
        {
            walls[x] = 1;
            walls[((height - 1) * width) + x] = 1;
        }
        for (var y = 0; y < height; y++)
        {
            walls[y * width] = 1;
            walls[(y * width) + width - 1] = 1;
        }
        return walls;
    }
}
