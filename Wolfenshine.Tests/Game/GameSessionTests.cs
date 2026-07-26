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
/// Verifies time-based player movement, turning, and collision.
/// </summary>
/// <remarks>
/// Game updates use synthetic maps so navigation behavior remains deterministic and renderer-independent.
/// </remarks>
public sealed class GameSessionTests
{
    [Test]
    public void GivenForwardInputCheckPlayerMovesInFacingDirection()
    {
        var session = CreateSession();

        var changed = session.Update(0.1, new PlayerInput(true, false, false, false));

        Assert.That(changed, Is.True);
        Assert.That(session.Camera.X, Is.EqualTo(2.5).Within(0.0001));
        Assert.That(session.Camera.Y, Is.EqualTo(1.93924).Within(0.0001));
    }

    [Test]
    public void GivenRunInputCheckPlayerMovesAtTwiceWalkingSpeed()
    {
        var walkingSession = CreateSession();
        var runningSession = CreateSession();

        walkingSession.Update(0.1, new PlayerInput(true, false, false, false));
        runningSession.Update(0.1, new PlayerInput(true, false, false, false, false, true));

        var walkingDistance = 2.5 - walkingSession.Camera.Y;
        var runningDistance = 2.5 - runningSession.Camera.Y;
        Assert.That(runningDistance, Is.EqualTo(walkingDistance * 2.0).Within(0.0001));
    }

    [Test]
    public void GivenBackwardInputCheckOriginalReducedSpeedIsUsed()
    {
        var session = CreateSession();

        session.Update(0.1, new PlayerInput(false, true, false, false));

        Assert.That(session.Camera.Y, Is.EqualTo(2.87384).Within(0.0001));
    }

    [Test]
    public void GivenUnownedWeaponSelectionCheckCurrentWeaponIsUnchanged()
    {
        var session = CreateSession();

        session.Update(0.0, new PlayerInput(false, false, false, false, WeaponSelection: PlayerWeapon.Chaingun));

        Assert.That(session.Weapon, Is.EqualTo(PlayerWeapon.Pistol));
        Assert.That(session.WeaponFrame, Is.Zero);
    }

    [TestCase(50, PlayerWeapon.MachineGun)]
    [TestCase(51, PlayerWeapon.Chaingun)]
    public void GivenWeaponPickupCheckItIsOwnedEquippedAndAddsSixRounds(int marker, PlayerWeapon weapon)
    {
        var session = CreateSessionWithObject((ushort)marker);

        session.Update(0.2, new PlayerInput(true, false, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(session.BestWeapon, Is.EqualTo(weapon));
            Assert.That(session.Weapon, Is.EqualTo(weapon));
            Assert.That(session.Ammo, Is.EqualTo(14));
            Assert.That(session.StaticObjects, Is.Empty);
        });
    }

    [Test]
    public void GivenPistolAttackCheckOriginalFramesAdvanceAndAmmoIsConsumed()
    {
        var session = CreateSession();
        session.Update(0.0, new PlayerInput(false, false, false, false, Attack: true));

        session.Update(6.0 / 70.0, new PlayerInput(false, false, false, false, Attack: true));
        Assert.That(session.WeaponFrame, Is.EqualTo(2));

        session.Update(6.0 / 70.0, new PlayerInput(false, false, false, false, Attack: true));
        Assert.Multiple(() =>
        {
            Assert.That(session.WeaponFrame, Is.EqualTo(3));
            Assert.That(session.Ammo, Is.EqualTo(7));
        });
    }

    [Test]
    public void GivenAmmoReachesZeroCheckItRemainsEmpty()
    {
        var session = CreateSession();
        for (var shot = 0; shot < 8; shot++)
        {
            session.Update(0.0, new PlayerInput(false, false, false, false, Attack: true));
            session.Update(0.4, new PlayerInput(false, false, false, false, Attack: true));
            session.Update(0.0, default);
        }

        session.Update(0.0, new PlayerInput(false, false, false, false, Attack: true));
        session.Update(0.4, new PlayerInput(false, false, false, false, Attack: true));

        Assert.That(session.Ammo, Is.Zero);
    }

    [TestCase(PlayerWeapon.Pistol)]
    public void GivenFinalRoundAndHeldAttackCheckGunStopsAndReturnsToKnife(PlayerWeapon weapon)
    {
        var session = CreateSession();
        FireShots(session, 7);
        session.Update(0.0, new PlayerInput(false, false, false, false, WeaponSelection: weapon));

        session.Update(0.0, new PlayerInput(false, false, false, false, Attack: true));
        session.Update(1.0, new PlayerInput(false, false, false, false, Attack: true));

        Assert.Multiple(() =>
        {
            Assert.That(session.Ammo, Is.Zero);
            Assert.That(session.IsAttacking, Is.False);
            Assert.That(session.Weapon, Is.EqualTo(PlayerWeapon.Knife));
            Assert.That(session.WeaponFrame, Is.Zero);
        });
    }

    [Test]
    public void GivenEmptyGunCheckAttackUsesKnifeWithoutConsumingAmmo()
    {
        var session = CreateSession();
        FireShots(session, 8);
        session.Update(0.0, new PlayerInput(
            false,
            false,
            false,
            false,
            Attack: true,
            WeaponSelection: PlayerWeapon.Pistol));

        Assert.Multiple(() =>
        {
            Assert.That(session.Ammo, Is.Zero);
            Assert.That(session.Weapon, Is.EqualTo(PlayerWeapon.Knife));
            Assert.That(session.IsAttacking, Is.True);
        });
    }

    [Test]
    public void GivenAmmoCollectedAfterExhaustionCheckPreviouslyChosenGunIsRestored()
    {
        var session = CreateSessionWithObject(49);
        FireShots(session, 7);
        session.Update(0.0, new PlayerInput(
            false,
            false,
            false,
            false,
            WeaponSelection: PlayerWeapon.Pistol));
        session.Update(0.0, new PlayerInput(false, false, false, false, Attack: true));
        session.Update(0.5, new PlayerInput(false, false, false, false, Attack: true));
        session.Update(0.0, default);
        Assert.That(session.Weapon, Is.EqualTo(PlayerWeapon.Knife));

        session.Update(0.2, new PlayerInput(true, false, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(session.Ammo, Is.EqualTo(8));
            Assert.That(session.Weapon, Is.EqualTo(PlayerWeapon.Pistol));
            Assert.That(session.WeaponFrame, Is.Zero);
        });
    }

#if DEBUG
    [Test]
    public void GivenDebugReloadCheckAmmoAndHealthAreMaximized()
    {
        var session = CreateSession();
        FireShots(session, 3);

        var changed = session.ReloadDebugState();

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That(session.Ammo, Is.EqualTo(99));
            Assert.That(session.Health, Is.EqualTo(100));
            Assert.That(session.BestWeapon, Is.EqualTo(PlayerWeapon.Chaingun));
        });

        session.Update(0.0, new PlayerInput(
            false,
            false,
            false,
            false,
            WeaponSelection: PlayerWeapon.Chaingun));
        Assert.That(session.Weapon, Is.EqualTo(PlayerWeapon.Chaingun));
    }
#endif

    [Test]
    public void GivenRightTurnCheckPlayerRotatesClockwise()
    {
        var session = CreateSession();

        session.Update(36.0 / 49.0, new PlayerInput(false, false, false, true));

        Assert.That(session.Camera.DirectionX, Is.EqualTo(1.0).Within(0.0001));
        Assert.That(session.Camera.DirectionY, Is.EqualTo(0.0).Within(0.0001));
    }

    [Test]
    public void GivenStrafeAndRightInputCheckPlayerMovesSidewaysWithoutTurning()
    {
        var session = CreateSession();

        session.Update(0.1, new PlayerInput(false, false, false, true, Strafe: true));

        Assert.Multiple(() =>
        {
            Assert.That(session.Camera.X, Is.EqualTo(3.06076).Within(0.0001));
            Assert.That(session.Camera.Y, Is.EqualTo(2.5).Within(0.0001));
            Assert.That(session.Camera.DirectionX, Is.EqualTo(0.0).Within(0.0001));
            Assert.That(session.Camera.DirectionY, Is.EqualTo(-1.0).Within(0.0001));
        });
    }

    [Test]
    public void GivenLongMovementCheckPlayerCannotCrossWall()
    {
        var session = CreateSession();

        session.Update(1.0, new PlayerInput(true, false, false, false));

        Assert.That(session.Camera.Y, Is.GreaterThanOrEqualTo(1.19));
        Assert.That(session.Camera.Y, Is.LessThan(1.31));
    }

    [Test]
    public void GivenInertActorAheadCheckPlayerCannotWalkThroughIt()
    {
        var session = CreateSessionWithActor(new WolfensteinActor(
            2.5, 1.5, WolfensteinActorType.Guard, 0, false, false, 50));

        session.Update(0.5, new PlayerInput(true, false, false, false));

        Assert.That(session.Camera.Y, Is.EqualTo(2.5).Within(0.0001));
    }

    [Test]
    public void GivenGunShotAtVisibleGuardCheckItDiesScoresAndStopsBlocking()
    {
        var session = CreateSessionWithActor(new WolfensteinActor(
            2.5, 1.5, WolfensteinActorType.Guard, 0, false, false, 50));

        session.Update(0.0, new PlayerInput(false, false, false, false, Attack: true));
        session.Update(12.0 / 70.0, new PlayerInput(false, false, false, false, Attack: true));

        Assert.Multiple(() =>
        {
            Assert.That(session.Score, Is.EqualTo(100));
            Assert.That(session.ActorSprites[0].SpriteNumber, Is.EqualTo(91));
            Assert.That(session.StaticObjects, Has.One.Matches<WorldSprite>(item =>
                item.SpriteNumber == 28 && item.X == 2.5 && item.Y == 1.5));
        });

        session.Update(45.0 / 70.0, default);
        session.Update(0.5, new PlayerInput(true, false, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(session.ActorSprites[0].SpriteNumber, Is.EqualTo(95));
            Assert.That(session.Camera.Y, Is.LessThan(2.0));
        });
    }

    [Test]
    public void GivenBlockingDecorationAheadCheckPlayerCannotWalkThroughItsTile()
    {
        var session = CreateSessionWithObject(31);

        session.Update(1.0, new PlayerInput(true, false, false, false));

        Assert.That(session.Camera.Y, Is.EqualTo(2.2).Within(0.11));
    }

    [Test]
    public void GivenAmmoClipCheckEightRoundsAreAddedAndPickupIsRemoved()
    {
        var session = CreateSessionWithObject(49);

        session.Update(0.2, new PlayerInput(true, false, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(session.Ammo, Is.EqualTo(16));
            Assert.That(session.StaticObjects, Is.Empty);
        });
    }

    [Test]
    public void GivenFullAmmoCheckClipRemainsAvailable()
    {
        var session = CreateSessionWithObject(49);
#if DEBUG
        session.ReloadDebugState();
#else
        Assert.Ignore("Debug reload supplies the full-ammo test state.");
#endif

        session.Update(0.2, new PlayerInput(true, false, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(session.Ammo, Is.EqualTo(99));
            Assert.That(session.StaticObjects, Has.Count.EqualTo(1));
        });
    }

    [TestCase(52, 100)]
    [TestCase(53, 500)]
    [TestCase(54, 1000)]
    [TestCase(55, 5000)]
    public void GivenTreasureCheckOriginalScoreIsAwardedAndPickupIsRemoved(int marker, int expectedScore)
    {
        var session = CreateSessionWithObject((ushort)marker);

        session.Update(0.2, new PlayerInput(true, false, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(session.Score, Is.EqualTo(expectedScore));
            Assert.That(session.TreasureCount, Is.EqualTo(1));
            Assert.That(session.StaticObjects, Is.Empty);
        });
    }

    [Test]
    public void GivenNoInputCheckCameraIsUnchanged()
    {
        var session = CreateSession();
        var originalCamera = session.Camera;

        var changed = session.Update(0.1, default);

        Assert.That(changed, Is.False);
        Assert.That(session.Camera, Is.SameAs(originalCamera));
    }

    [Test]
    public void GivenUseInputCheckDoorAheadOpensAndBecomesPassable()
    {
        var session = CreateDoorSession();

        session.Update(0.01, new PlayerInput(false, false, false, false, true));
        session.Update(1.0, new PlayerInput(false, false, false, false, true));
        session.Update(0.5, new PlayerInput(true, false, false, false));

        Assert.That(session.Doors.Items, Has.Count.EqualTo(1));
        Assert.That(session.Doors.Items[0].IsFullyOpen, Is.True);
        Assert.That(session.Camera.Y, Is.LessThan(2.2));
    }

    [Test]
    public void GivenPlayerInDoorwayCheckAutomaticClosingWaitsUntilPlayerLeaves()
    {
        var session = CreateDoorSession();
        session.Update(0.01, new PlayerInput(false, false, false, false, true));
        session.Update(1.0, default);
        session.Update(0.2, new PlayerInput(true, false, false, false));

        session.Update(5.0, default);

        Assert.That(session.Doors.Items[0].IsClosing, Is.False);
        Assert.That(session.Doors.Items[0].IsFullyOpen, Is.True);
    }

    [Test]
    public void GivenPushwallAheadCheckUseActivatesItAndCountsSecret()
    {
        var session = CreatePushWallSession();

        session.Update(0.0, new PlayerInput(false, false, false, false, true));
        session.Update(256.0 / 70.0, default);

        Assert.Multiple(() =>
        {
            Assert.That(session.SecretCount, Is.EqualTo(1));
            Assert.That(session.SecretTotal, Is.EqualTo(1));
            Assert.That(session.PushWalls.Items, Has.Count.EqualTo(1));
            Assert.That(session.PushWalls.Items[0].Distance, Is.EqualTo(2.0));
            Assert.That(session.PushWalls.Items[0].IsMoving, Is.False);
        });
    }

    private static GameSession CreateSession()
    {
        const int size = 5;
        var walls = Enumerable.Repeat((ushort)107, size * size).ToArray();
        for (var i = 0; i < size; i++)
        {
            walls[i] = 1;
            walls[((size - 1) * size) + i] = 1;
            walls[i * size] = 1;
            walls[(i * size) + size - 1] = 1;
        }

        var objects = new ushort[size * size];
        objects[(2 * size) + 2] = 19;
        var map = new WolfensteinMap(0, "Test Map", size, size, walls, objects);
        return new GameSession(map, RaycastCamera.FromPlayerStart(map));
    }

    private static GameSession CreateDoorSession()
    {
        const int size = 5;
        var walls = Enumerable.Repeat((ushort)107, size * size).ToArray();
        for (var i = 0; i < size; i++)
        {
            walls[i] = 1;
            walls[((size - 1) * size) + i] = 1;
            walls[i * size] = 1;
            walls[(i * size) + size - 1] = 1;
        }

        walls[(2 * size) + 1] = 1;
        walls[(2 * size) + 2] = 91;
        walls[(2 * size) + 3] = 1;
        var objects = new ushort[size * size];
        objects[(3 * size) + 2] = 19;
        var map = new WolfensteinMap(0, "Door Map", size, size, walls, objects);
        return new GameSession(map, RaycastCamera.FromPlayerStart(map));
    }

    private static GameSession CreateSessionWithActor(WolfensteinActor actor)
    {
        const int size = 5;
        var walls = Enumerable.Repeat((ushort)107, size * size).ToArray();
        for (var i = 0; i < size; i++)
        {
            walls[i] = 1;
            walls[((size - 1) * size) + i] = 1;
            walls[i * size] = 1;
            walls[(i * size) + size - 1] = 1;
        }

        var objects = new ushort[size * size];
        objects[(2 * size) + 2] = 19;
        var map = new WolfensteinMap(0, "Actor Collision", size, size, walls, objects);
        return new GameSession(map, RaycastCamera.FromPlayerStart(map), new[] { actor });
    }

    private static GameSession CreatePushWallSession()
    {
        const int size = 7;
        var walls = Enumerable.Repeat((ushort)107, size * size).ToArray();
        for (var index = 0; index < size; index++)
        {
            walls[index] = 1;
            walls[((size - 1) * size) + index] = 1;
            walls[index * size] = 1;
            walls[(index * size) + size - 1] = 1;
        }
        walls[(4 * size) + 3] = 2;
        var objects = new ushort[size * size];
        objects[(5 * size) + 3] = 19;
        objects[(4 * size) + 3] = 98;
        var map = new WolfensteinMap(0, "Pushwall Session", size, size, walls, objects);
        return new GameSession(map, RaycastCamera.FromPlayerStart(map));
    }

    private static GameSession CreateSessionWithObject(ushort marker)
    {
        const int size = 5;
        var walls = Enumerable.Repeat((ushort)107, size * size).ToArray();
        for (var i = 0; i < size; i++)
        {
            walls[i] = 1;
            walls[((size - 1) * size) + i] = 1;
            walls[i * size] = 1;
            walls[(i * size) + size - 1] = 1;
        }

        var objects = new ushort[size * size];
        objects[(2 * size) + 2] = 19;
        objects[(1 * size) + 2] = marker;
        var map = new WolfensteinMap(0, "Decoration Collision", size, size, walls, objects);
        return new GameSession(map, RaycastCamera.FromPlayerStart(map));
    }

    private static void FireShots(GameSession session, int count)
    {
        for (var shot = 0; shot < count; shot++)
        {
            session.Update(0.0, new PlayerInput(false, false, false, false, Attack: true));
            session.Update(0.4, new PlayerInput(false, false, false, false, Attack: true));
            session.Update(0.0, default);
        }
    }
}
