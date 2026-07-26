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
using Wolfenshine.Audio;
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
            Assert.That(session.FacePictureIndex, Is.EqualTo(
                weapon == PlayerWeapon.Chaingun ? 22 : 0));
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
        var sounds = session.DrainSoundEvents();
        Assert.Multiple(() =>
        {
            Assert.That(session.WeaponFrame, Is.EqualTo(3));
            Assert.That(session.Ammo, Is.EqualTo(7));
            Assert.That(sounds, Has.Some.Matches<WolfensteinSoundEvent>(sound =>
                sound.Effect == WolfensteinSoundEffect.AttackPistol && sound.X == null && sound.Y == null));
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
        var sounds = session.DrainSoundEvents();

        Assert.Multiple(() =>
        {
            Assert.That(session.Camera.Y, Is.GreaterThanOrEqualTo(1.19));
            Assert.That(session.Camera.Y, Is.LessThan(1.31));
            Assert.That(sounds, Has.One.Matches<WolfensteinSoundEvent>(sound =>
                sound.Effect == WolfensteinSoundEffect.HitWall));
        });
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
        var sounds = session.DrainSoundEvents();

        Assert.Multiple(() =>
        {
            Assert.That(session.Score, Is.EqualTo(100));
            Assert.That(session.ActorSprites[0].SpriteNumber, Is.EqualTo(91));
            Assert.That(session.StaticObjects, Has.One.Matches<WorldSprite>(item =>
                item.SpriteNumber == 28 && item.X == 2.5 && item.Y == 1.5));
            Assert.That(sounds, Has.One.Matches<WolfensteinSoundEvent>(sound =>
                sound.Effect is WolfensteinSoundEffect.GuardDeath1 or WolfensteinSoundEffect.GuardDeath2 or
                    WolfensteinSoundEffect.GuardDeath3 or WolfensteinSoundEffect.GuardDeath4 or
                    WolfensteinSoundEffect.GuardDeath5 or WolfensteinSoundEffect.GuardDeath7 or
                    WolfensteinSoundEffect.GuardDeath8 or WolfensteinSoundEffect.GuardDeath9 &&
                sound.X == 2.5 && sound.Y == 1.5));
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
    public void GivenCloseUnawareSsCheckPistolDamageDoesNotAlwaysKillIt()
    {
        var session = CreateSessionWithActor(new WolfensteinActor(
            2.5, 1.5, WolfensteinActorType.Ss, 0, false, false, 138));

        session.Update(0.0, new PlayerInput(false, false, false, false, Attack: true));
        session.Update(12.0 / 70.0, new PlayerInput(false, false, false, false, Attack: true));

        Assert.Multiple(() =>
        {
            Assert.That(session.Actors[0].IsDead, Is.False);
            Assert.That(session.Actors[0].HitPoints, Is.GreaterThan(0));
            Assert.That(session.Actors[0].HitPoints, Is.LessThan(100));
        });
    }

    [Test]
    public void GivenGuardMovedFromSpawnCheckAmmoDropsAtDeathPosition()
    {
        var session = CreateSessionWithActor(new WolfensteinActor(
            2.5, 1.5, WolfensteinActorType.Guard, 0, false, false, 50));
        session.Actors[0].MoveTo(2.5, 1.8, 3, 0.1);

        session.Update(0.0, new PlayerInput(false, false, false, false, Attack: true));
        session.Update(12.0 / 70.0, new PlayerInput(false, false, false, false, Attack: true));

        Assert.That(session.StaticObjects, Has.One.Matches<WorldSprite>(item =>
            item.SpriteNumber == 28 && item.X == 2.5 && item.Y == 1.8));
    }

    [Test]
    public void GivenGuardFacingPlayerCheckItBecomesAlertedBySight()
    {
        var session = CreateSessionWithActor(new WolfensteinActor(
            2.5, 1.5, WolfensteinActorType.Guard, 3, false, false, 50));

        session.Update(0.0, default);

        Assert.That(session.Actors[0].Behavior, Is.Not.EqualTo(WolfensteinActorBehavior.Dormant));
    }

    [Test]
    public void GivenGunfireBehindGuardCheckSoundAlertsIt()
    {
        var session = CreateSessionWithActor(new WolfensteinActor(
            2.5, 4.5, WolfensteinActorType.Guard, 3, false, false, 50));

        session.Update(0.0, new PlayerInput(false, false, false, false, Attack: true));
        session.Update(12.0 / 70.0, new PlayerInput(false, false, false, false, Attack: true));
        var sounds = session.DrainSoundEvents();

        Assert.Multiple(() =>
        {
            Assert.That(session.Actors[0].Behavior, Is.Not.EqualTo(WolfensteinActorBehavior.Dormant));
            Assert.That(sounds, Has.One.Matches<WolfensteinSoundEvent>(sound =>
                sound.Effect == WolfensteinSoundEffect.GuardAlert && sound.X != null && sound.Y != null));
        });
    }

    [Test]
    public void GivenGunfireAndClosedDoorWithinSameAreaCheckSoundAlertsGuard()
    {
        const int size = 7;
        var walls = Enumerable.Repeat((ushort)107, size * size).ToArray();
        for (var index = 0; index < size; index++)
        {
            walls[index] = 1;
            walls[((size - 1) * size) + index] = 1;
            walls[index * size] = 1;
            walls[(index * size) + size - 1] = 1;
            walls[(3 * size) + index] = 1;
        }
        walls[(3 * size) + 3] = 90;
        var objects = new ushort[size * size];
        objects[(2 * size) + 3] = 19;
        var map = new WolfensteinMap(0, "Shared Area Door", size, size, walls, objects);
        var actor = new WolfensteinActor(3.5, 4.5, WolfensteinActorType.Guard, 3, false, false, 50);
        var session = new GameSession(map, RaycastCamera.FromPlayerStart(map), new[] { actor });

        session.Update(0.0, new PlayerInput(false, false, false, false, Attack: true));
        session.Update(12.0 / 70.0, new PlayerInput(false, false, false, false, Attack: true));

        Assert.That(session.Actors[0].Behavior, Is.Not.EqualTo(WolfensteinActorBehavior.Dormant));
    }

    [Test]
    public void GivenGunfireBehindAmbushGuardCheckItStillRequiresSight()
    {
        var session = CreateSessionWithActor(new WolfensteinActor(
            2.5, 4.5, WolfensteinActorType.Guard, 3, false, true, 50));

        session.Update(0.0, new PlayerInput(false, false, false, false, Attack: true));
        session.Update(12.0 / 70.0, new PlayerInput(false, false, false, false, Attack: true));

        Assert.That(session.Actors[0].Behavior, Is.EqualTo(WolfensteinActorBehavior.Dormant));
    }

    [Test]
    public void GivenAlertGuardFiresCheckPlayerTakesDamage()
    {
        var session = CreateSessionWithActor(new WolfensteinActor(
            2.5, 1.5, WolfensteinActorType.Guard, 3, false, false, 50));

        session.Update(0.0, default);
        for (var attack = 0; attack < 8 && session.Health == 100; attack++)
            session.Update(1.5, default);
        var sounds = session.DrainSoundEvents();

        Assert.Multiple(() =>
        {
            Assert.That(session.Health, Is.LessThan(100));
            Assert.That(sounds, Has.Some.Matches<WolfensteinSoundEvent>(sound =>
                sound.Effect == WolfensteinSoundEffect.GuardFire && sound.X == 2.5 && sound.Y == 1.5));
        });
    }

    [Test]
    public void GivenBabyDifficultyCheckIncomingDamageIsQuarterStrength()
    {
        var actor = new WolfensteinActor(
            2.5, 1.5, WolfensteinActorType.Guard, 3, false, false, 50);
        var normalSession = CreateSessionWithActor(actor);
        var babySession = CreateSessionWithActor(actor, GameDifficulty.Baby);

        normalSession.Update(0.0, default);
        babySession.Update(0.0, default);
        for (var attack = 0; attack < 8 && normalSession.Health == 100; attack++)
        {
            normalSession.Update(1.5, default);
            babySession.Update(1.5, default);
        }

        var normalDamage = 100 - normalSession.Health;
        var babyDamage = 100 - babySession.Health;
        Assert.Multiple(() =>
        {
            Assert.That(normalDamage, Is.GreaterThan(0));
            Assert.That(babyDamage, Is.EqualTo(normalDamage >> 2));
        });
    }

    [TestCase(WolfensteinActorType.Officer, 238, 26, 287)]
    [TestCase(WolfensteinActorType.Ss, 138, 40, 186)]
    [TestCase(WolfensteinActorType.Mutant, 187, 6, 235)]
    public void GivenOtherSoldierAtCloseRangeCheckItsFirstAttackFrameUsesExpectedAnimation(
        WolfensteinActorType type,
        int baseSprite,
        int attackTicks,
        int expectedSprite)
    {
        var session = CreateSessionWithActor(new WolfensteinActor(
            2.5, 1.5, type, 3, false, false, baseSprite));

        session.Update(0.0, default);
        session.Update(attackTicks / 70.0, default);

        Assert.Multiple(() =>
        {
            Assert.That(session.Health, Is.InRange(0, 100));
            Assert.That(session.Actors[0].CurrentSpriteNumber, Is.EqualTo(expectedSprite));
        });
    }

    [Test]
    public void GivenSsAttackCheckFourRoundBurstCanDamagePlayerRepeatedly()
    {
        var session = CreateSessionWithActor(new WolfensteinActor(
            2.5, 1.5, WolfensteinActorType.Ss, 3, false, false, 138));
        session.Update(0.0, default);

        foreach (var ticks in new[] { 20, 20, 10, 10, 10, 10, 10, 10 })
            session.Update(ticks / 70.0, default);

        Assert.That(session.Health, Is.LessThan(100));
    }

    [Test]
    public void GivenDogAtCloseRangeCheckJumpAnimationBitesPlayerAndReturnsToChase()
    {
        var session = CreateSessionWithActor(new WolfensteinActor(
            2.5, 1.5, WolfensteinActorType.Dog, 3, false, false, 99));

        session.Update(0.0, default);
        Assert.That(session.Actors[0].CurrentSpriteNumber, Is.EqualTo(135));

        session.Update(20.0 / 70.0, default);
        Assert.That(session.Actors[0].CurrentSpriteNumber, Is.EqualTo(137));

        session.Update(40.0 / 70.0, default);
        Assert.Multiple(() =>
        {
            Assert.That(session.Actors[0].Behavior, Is.EqualTo(WolfensteinActorBehavior.Chasing));
            Assert.That(session.Actors[0].CurrentSpriteNumber, Is.EqualTo(99));
        });
    }

    [Test]
    public void GivenChasingGuardAtCloseRangeCheckItCannotOverlapPlayer()
    {
        var session = CreateSessionWithActor(new WolfensteinActor(
            2.5, 1.5, WolfensteinActorType.Guard, 3, false, false, 50));
        var guard = session.Actors[0];
        guard.Alert();
        guard.AttackCooldown = 10.0;

        session.Update(2.0, default);

        Assert.That(Math.Abs(guard.Y - session.Camera.Y), Is.GreaterThanOrEqualTo(1.0));

        session.Update(0.2, new PlayerInput(false, true, false, false));
        Assert.That(session.Camera.Y, Is.GreaterThan(2.5));
    }

    [Test]
    public void GivenAlertGuardBehindClosedDoorCheckItOpensDoorToChasePlayer()
    {
        const int width = 7;
        const int height = 5;
        var walls = Enumerable.Repeat((ushort)1, width * height).ToArray();
        for (var x = 1; x <= 5; x++)
            walls[(2 * width) + x] = 107;
        walls[(2 * width) + 3] = 90;
        var map = new WolfensteinMap(0, "Guard Door", width, height, walls, new ushort[width * height]);
        var actor = new WolfensteinActor(5.5, 2.5, WolfensteinActorType.Guard, 2, false, false, 50);
        var session = new GameSession(
            map,
            new RaycastCamera(1.5, 2.5, 1.0, 0.0, 0.0, 0.66),
            new[] { actor });
        session.Actors[0].Alert();

        session.Update(1.0, default);
        session.Update(0.1, default);
        var sounds = session.DrainSoundEvents();

        Assert.Multiple(() =>
        {
            Assert.That(session.Doors.Items[0].IsOpening, Is.True);
            Assert.That(sounds, Has.One.Matches<WolfensteinSoundEvent>(sound =>
                sound.Effect == WolfensteinSoundEffect.OpenDoor && sound.X == 3.5 && sound.Y == 2.5));
        });
    }

    [Test]
    public void GivenAdjacentDogsChasingSidewaysCheckTheyDoNotDeadlockEachOther()
    {
        const int width = 6;
        const int height = 5;
        var walls = Enumerable.Repeat((ushort)1, width * height).ToArray();
        for (var y = 1; y <= 3; y++)
        {
            for (var x = 1; x <= 4; x++)
                walls[(y * width) + x] = 107;
        }
        var map = new WolfensteinMap(0, "Adjacent Dogs", width, height, walls, new ushort[width * height]);
        WolfensteinActor[] actors =
        [
            new(4.5, 2.5, WolfensteinActorType.Dog, 2, false, false, 99),
            new(4.5, 1.5, WolfensteinActorType.Dog, 2, false, false, 99)
        ];
        var session = new GameSession(
            map,
            new RaycastCamera(1.5, 2.5, 1.0, 0.0, 0.0, 0.66),
            actors);
        foreach (var dog in session.Actors)
        {
            dog.Alert();
            dog.AttackCooldown = 10.0;
        }

        session.Update(0.1, default);

        Assert.Multiple(() =>
        {
            Assert.That(session.Actors[0].X, Is.LessThan(4.5));
            Assert.That(session.Actors[1].X, Is.LessThan(4.5));
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
        var sounds = session.DrainSoundEvents();

        Assert.Multiple(() =>
        {
            Assert.That(session.Ammo, Is.EqualTo(16));
            Assert.That(session.StaticObjects, Is.Empty);
            Assert.That(sounds, Has.One.Matches<WolfensteinSoundEvent>(sound =>
                sound.Effect == WolfensteinSoundEffect.GetAmmo));
        });
    }

    [TestCase(43, true, false)]
    [TestCase(44, false, true)]
    public void GivenKeyPickupCheckMatchingLevelKeyIsGrantedAndPickupIsRemoved(
        int marker,
        bool expectedGoldKey,
        bool expectedSilverKey)
    {
        var session = CreateSessionWithObject((ushort)marker);

        session.Update(0.2, new PlayerInput(true, false, false, false));
        var sounds = session.DrainSoundEvents();

        Assert.Multiple(() =>
        {
            Assert.That(session.HasGoldKey, Is.EqualTo(expectedGoldKey));
            Assert.That(session.HasSilverKey, Is.EqualTo(expectedSilverKey));
            Assert.That(session.StaticObjects, Is.Empty);
            Assert.That(sounds, Has.One.Matches<WolfensteinSoundEvent>(sound =>
                sound.Effect == WolfensteinSoundEffect.GetKey));
        });
    }

    [TestCase(47)]
    [TestCase(48)]
    public void GivenHealthPickupAfterDogBiteCheckHealthIsRestoredAndPickupRemoved(int marker)
    {
        var session = CreateSessionWithObjectAndDog((ushort)marker);
        InflictDogBite(session);
        var damagedHealth = session.Health;

        session.Update(0.2, new PlayerInput(true, false, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(session.Health, Is.EqualTo(Math.Min(100, damagedHealth + 10)));
            Assert.That(session.StaticObjects, Is.Empty);
        });
    }

    [Test]
    public void GivenDogFoodAfterDogBiteCheckFourHealthIsRestoredAndPickupRemoved()
    {
        var session = CreateSessionWithObjectAndDog(29);
        InflictDogBite(session);
        var damagedHealth = session.Health;

        session.Update(0.2, new PlayerInput(true, false, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(session.Health, Is.EqualTo(Math.Min(100, damagedHealth + 4)));
            Assert.That(session.StaticObjects, Is.Empty);
        });
    }

    [Test]
    public void GivenFullHealPickupCheckHealthAmmoLifeAndTreasureCountAreGranted()
    {
        var session = CreateSessionWithObjectAndDog(56);
        session.Update(0.0, default);
        session.Update(20.0 / 70.0, default);

        session.Update(0.2, new PlayerInput(true, false, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(session.Health, Is.EqualTo(100));
            Assert.That(session.Ammo, Is.EqualTo(33));
            Assert.That(session.Lives, Is.EqualTo(4));
            Assert.That(session.TreasureCount, Is.EqualTo(1));
            Assert.That(session.StaticObjects, Is.Empty);
        });
    }

    [Test]
    public void GivenFullHealthCheckFoodRemainsForLater()
    {
        var session = CreateSessionWithObject(47);

        session.Update(0.2, new PlayerInput(true, false, false, false));

        Assert.That(session.StaticObjects, Has.Count.EqualTo(1));
    }

    [Test]
    public void GivenPlayerDeathCheckLifeIsConsumedAndLevelStateRestarts()
    {
        var session = CreateSessionWithObjectAndDog(47);
        InflictFatalDogBites(session);
        var sounds = session.DrainSoundEvents();

        Assert.Multiple(() =>
        {
            Assert.That(session.IsDying, Is.True);
            Assert.That(session.FacePictureIndex, Is.EqualTo(21));
            Assert.That(session.Lives, Is.EqualTo(3));
            Assert.That(session.DamageFlash, Is.GreaterThan(0.0));
            Assert.That(sounds, Has.One.Matches<WolfensteinSoundEvent>(sound =>
                sound.Effect == WolfensteinSoundEffect.PlayerDeath));
        });

        session.Update(1.5, new PlayerInput(true, false, false, false));

        Assert.Multiple(() =>
        {
            Assert.That(session.IsDying, Is.False);
            Assert.That(session.IsGameOver, Is.False);
            Assert.That(session.Lives, Is.EqualTo(2));
            Assert.That(session.Health, Is.EqualTo(100));
            Assert.That(session.Ammo, Is.EqualTo(8));
            Assert.That(session.Weapon, Is.EqualTo(PlayerWeapon.Pistol));
            Assert.That(session.Camera.X, Is.EqualTo(2.5));
            Assert.That(session.Camera.Y, Is.EqualTo(2.5));
            Assert.That(session.StaticObjects, Has.Count.EqualTo(1));
            Assert.That(session.Actors[0].Behavior, Is.EqualTo(WolfensteinActorBehavior.Dormant));
        });
    }

    [Test]
    public void GivenFinalLifeIsLostCheckGameOverIsRaised()
    {
        var session = CreateSessionWithObjectAndDog(47);
        for (var life = 0; life < 3; life++)
        {
            InflictFatalDogBites(session);
            session.Update(1.5, default);
        }

        Assert.Multiple(() =>
        {
            Assert.That(session.Lives, Is.Zero);
            Assert.That(session.IsGameOver, Is.True);
        });
    }

    [Test]
    public void GivenPlayerDeathCheckRedFadeAndOriginalMaximumDelayAreUsed()
    {
        var session = CreateSessionWithObjectAndDog(47);
        InflictFatalDogBites(session);

        session.Update(0.5, default);
        Assert.That(session.DeathFade, Is.EqualTo(0.5).Within(0.0001));

        session.Update((100.0 / 70.0) - 0.501, default);
        Assert.That(session.IsDying, Is.True);

        session.Update(0.0011, default);
        Assert.That(session.IsDying, Is.False);
        Assert.That(session.Lives, Is.EqualTo(2));
    }

    [Test]
    public void GivenRedDeathFadeIsCompleteCheckInputSkipsRemainingDelay()
    {
        var session = CreateSessionWithObjectAndDog(47);
        InflictFatalDogBites(session);
        session.Update(1.0, default);

        session.Update(0.0, new PlayerInput(true, false, false, false));

        Assert.That(session.IsDying, Is.False);
        Assert.That(session.Lives, Is.EqualTo(2));
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
        var sounds = session.DrainSoundEvents();
        session.Update(1.0, new PlayerInput(false, false, false, false, true));
        session.Update(0.5, new PlayerInput(true, false, false, false));

        Assert.That(session.Doors.Items, Has.Count.EqualTo(1));
        Assert.That(session.Doors.Items[0].IsFullyOpen, Is.True);
        Assert.That(session.Camera.Y, Is.LessThan(2.2));
        Assert.That(sounds, Has.One.Matches<WolfensteinSoundEvent>(sound =>
            sound.Effect == WolfensteinSoundEffect.OpenDoor && sound.X != null && sound.Y != null));
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
        var sounds = session.DrainSoundEvents();
        session.Update(256.0 / 70.0, default);

        Assert.Multiple(() =>
        {
            Assert.That(session.SecretCount, Is.EqualTo(1));
            Assert.That(session.SecretTotal, Is.EqualTo(1));
            Assert.That(session.PushWalls.Items, Has.Count.EqualTo(1));
            Assert.That(session.PushWalls.Items[0].Distance, Is.EqualTo(2.0));
            Assert.That(session.PushWalls.Items[0].IsMoving, Is.False);
            Assert.That(sounds, Has.One.Matches<WolfensteinSoundEvent>(sound =>
                sound.Effect == WolfensteinSoundEffect.PushWall));
        });
    }

    [Test]
    public void GivenElevatorSwitchUsedFromSideCheckLevelFadesToCompletion()
    {
        var session = CreateElevatorSession(directionX: 1.0, directionY: 0.0, switchX: 3, switchY: 2);

        session.Update(0.0, new PlayerInput(false, false, false, false, Use: true));
        var sounds = session.DrainSoundEvents();
        Assert.That(sounds, Has.One.Matches<WolfensteinSoundEvent>(sound =>
            sound.Effect == WolfensteinSoundEffect.LevelDone));
        Span<WallColumn> columns = stackalloc WallColumn[1];
        Raycaster.Cast(
            session.Map,
            session.Doors,
            session.PushWalls,
            session.ElevatorSwitch,
            session.Camera,
            columns);
        var renderedSwitchTile = columns[0].Tile;
        session.Update(0.25, default);

        Assert.Multiple(() =>
        {
            Assert.That(session.IsCompletingLevel, Is.True);
            Assert.That(session.IsReadyForNextLevel, Is.False);
            Assert.That(session.LevelFade, Is.EqualTo(0.5).Within(0.0001));
            Assert.That(session.ElevatorSwitch, Is.Not.Null);
            Assert.That(renderedSwitchTile, Is.EqualTo(22));
        });

        session.Update(0.25, default);
        Assert.That(session.IsReadyForNextLevel, Is.True);
        Assert.That(session.LevelFade, Is.EqualTo(1.0));
    }

    [Test]
    public void GivenElevatorSwitchUsedVerticallyCheckItDoesNotCompleteLevel()
    {
        var session = CreateElevatorSession(directionX: 0.0, directionY: -1.0, switchX: 2, switchY: 1);

        session.Update(0.0, new PlayerInput(false, false, false, false, Use: true));

        Assert.That(session.IsCompletingLevel, Is.False);
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

    private static GameSession CreateElevatorSession(
        double directionX,
        double directionY,
        int switchX,
        int switchY)
    {
        const int size = 5;
        var walls = Enumerable.Repeat((ushort)140, size * size).ToArray();
        for (var index = 0; index < size; index++)
        {
            walls[index] = 1;
            walls[((size - 1) * size) + index] = 1;
            walls[index * size] = 1;
            walls[(index * size) + size - 1] = 1;
        }
        walls[(switchY * size) + switchX] = 21;
        var map = new WolfensteinMap(0, "Elevator", size, size, walls, new ushort[size * size]);
        var planeX = -directionY * 0.66;
        var planeY = directionX * 0.66;
        return new GameSession(
            map,
            new RaycastCamera(2.5, 2.5, directionX, directionY, planeX, planeY));
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

    private static GameSession CreateSessionWithActor(
        WolfensteinActor actor,
        GameDifficulty difficulty = GameDifficulty.Normal)
    {
        const int size = 7;
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
        return new GameSession(map, RaycastCamera.FromPlayerStart(map), new[] { actor }, difficulty);
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

    private static GameSession CreateSessionWithObjectAndDog(ushort marker)
    {
        const int size = 5;
        var walls = Enumerable.Repeat((ushort)107, size * size).ToArray();
        for (var index = 0; index < size; index++)
        {
            walls[index] = 1;
            walls[((size - 1) * size) + index] = 1;
            walls[index * size] = 1;
            walls[(index * size) + size - 1] = 1;
        }
        var objects = new ushort[size * size];
        objects[(2 * size) + 2] = 19;
        objects[(1 * size) + 2] = marker;
        var map = new WolfensteinMap(0, "Health Pickup", size, size, walls, objects);
        WolfensteinActor[] actors =
        [
            new(3.6, 2.5, WolfensteinActorType.Dog, 2, false, false, 99)
        ];
        return new GameSession(map, RaycastCamera.FromPlayerStart(map), actors);
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

    private static void InflictFatalDogBites(GameSession session)
    {
        for (var update = 0; update < 100 && session.Health > 0; update++)
            session.Update(1.0, default);
        Assert.That(session.Health, Is.Zero, "The test dog did not kill the player in time.");
    }

    private static void InflictDogBite(GameSession session)
    {
        for (var update = 0; update < 20 && session.Health == 100; update++)
            session.Update(1.0, default);
        Assert.That(session.Health, Is.LessThan(100), "The test dog did not bite the player in time.");
    }
}
