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
    public void GivenWeaponSelectionCheckAllWeaponsAreImmediatelyAvailable()
    {
        var session = CreateSession();

        session.Update(0.0, new PlayerInput(false, false, false, false, WeaponSelection: PlayerWeapon.Chaingun));

        Assert.That(session.Weapon, Is.EqualTo(PlayerWeapon.Chaingun));
        Assert.That(session.WeaponFrame, Is.Zero);
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
    public void GivenSandboxAmmoReachesZeroCheckItIsRenewed()
    {
        var session = CreateSession();
        for (var shot = 0; shot < 8; shot++)
        {
            session.Update(0.0, new PlayerInput(false, false, false, false, Attack: true));
            session.Update(0.4, new PlayerInput(false, false, false, false, Attack: true));
            session.Update(0.0, default);
        }

        Assert.That(session.Ammo, Is.EqualTo(99));
    }

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
}
