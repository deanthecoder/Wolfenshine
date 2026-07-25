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
    private const double MovementSpeed = 3.0;
    private const double RotationSpeed = Math.PI * 2.0 / 3.0;
    private const double PlayerRadius = 0.2;
    private const double MaximumMovementStep = 0.1;

    public GameSession(WolfensteinMap map, RaycastCamera camera)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(camera);
        Map = map;
        Camera = camera;
    }

    public WolfensteinMap Map { get; }
    public RaycastCamera Camera { get; private set; }

    public bool Update(double elapsedSeconds, PlayerInput input)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0.0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (elapsedSeconds == 0.0)
            return false;

        var turn = (input.TurnRight ? 1.0 : 0.0) - (input.TurnLeft ? 1.0 : 0.0);
        var movement = (input.MoveForward ? 1.0 : 0.0) - (input.MoveBackward ? 1.0 : 0.0);
        if (turn == 0.0 && movement == 0.0)
            return false;

        var x = Camera.X;
        var y = Camera.Y;
        var directionX = Camera.DirectionX;
        var directionY = Camera.DirectionY;
        var planeX = Camera.PlaneX;
        var planeY = Camera.PlaneY;

        if (turn != 0.0)
        {
            var angle = turn * RotationSpeed * elapsedSeconds;
            (directionX, directionY) = Rotate(directionX, directionY, angle);
            (planeX, planeY) = Rotate(planeX, planeY, angle);
        }

        if (movement != 0.0)
        {
            var distance = movement * MovementSpeed * elapsedSeconds;
            var stepCount = Math.Max(1, (int)Math.Ceiling(Math.Abs(distance) / MaximumMovementStep));
            var stepDistance = distance / stepCount;
            for (var step = 0; step < stepCount; step++)
            {
                // Resolve each axis independently so the player slides naturally along nearby walls.
                var nextX = x + (directionX * stepDistance);
                if (CanOccupy(nextX, y))
                    x = nextX;
                var nextY = y + (directionY * stepDistance);
                if (CanOccupy(x, nextY))
                    y = nextY;
            }
        }

        Camera = new RaycastCamera(x, y, directionX, directionY, planeX, planeY);
        return true;
    }

    private bool CanOccupy(double x, double y) =>
        !Map.IsSolid((int)Math.Floor(x - PlayerRadius), (int)Math.Floor(y - PlayerRadius)) &&
        !Map.IsSolid((int)Math.Floor(x + PlayerRadius), (int)Math.Floor(y - PlayerRadius)) &&
        !Map.IsSolid((int)Math.Floor(x - PlayerRadius), (int)Math.Floor(y + PlayerRadius)) &&
        !Map.IsSolid((int)Math.Floor(x + PlayerRadius), (int)Math.Floor(y + PlayerRadius));

    private static (double X, double Y) Rotate(double x, double y, double angle)
    {
        var cosine = Math.Cos(angle);
        var sine = Math.Sin(angle);
        return ((x * cosine) - (y * sine), (x * sine) + (y * cosine));
    }
}
