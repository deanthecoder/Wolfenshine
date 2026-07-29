// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Wolfenshine.Game;

namespace Wolfenshine.Rendering;

/// <summary>
/// Projects world-space sprite centers into the software viewport.
/// </summary>
/// <remarks>
/// Callers provide reusable output storage so camera movement creates no garbage.
/// </remarks>
public static class WorldSpriteProjector
{
    private const double ObjectDepthAdjustment = 0.125;

    public static int Project(
        IReadOnlyList<WorldSprite> sprites,
        RaycastCamera camera,
        int viewportWidth,
        int viewportHeight,
        Span<ProjectedWorldSprite> projectedSprites)
        => Project(sprites, camera, viewportWidth, viewportHeight, viewportHeight, projectedSprites);

    public static int Project(
        IReadOnlyList<WorldSprite> sprites,
        RaycastCamera camera,
        int viewportWidth,
        int viewportHeight,
        int projectionHeight,
        Span<ProjectedWorldSprite> projectedSprites)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(viewportWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(viewportHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(projectionHeight);
        if (projectedSprites.Length < sprites.Count)
            throw new ArgumentException("The projected-sprite buffer is too small.", nameof(projectedSprites));

        var determinant = (camera.PlaneX * camera.DirectionY) - (camera.DirectionX * camera.PlaneY);
        if (Math.Abs(determinant) < double.Epsilon)
            throw new ArgumentException("The camera projection plane is degenerate.", nameof(camera));
        var inverseDeterminant = 1.0 / determinant;
        var visibleCount = 0;
        foreach (var sprite in sprites)
        {
            var relativeX = sprite.X - camera.X;
            var relativeY = sprite.Y - camera.Y;
            var cameraX = inverseDeterminant *
                          ((camera.DirectionY * relativeX) - (camera.DirectionX * relativeY));
            var depth = inverseDeterminant *
                        ((-camera.PlaneY * relativeX) + (camera.PlaneX * relativeY));
            depth -= ObjectDepthAdjustment;
            if (depth <= 0.0)
                continue;

            var centerX = (int)Math.Round((viewportWidth * 0.5) * (1.0 + (cameraX / depth)));
            var renderedSize = Math.Max(1, (int)Math.Round(projectionHeight / depth));
            var halfSize = renderedSize / 2;
            if (centerX + halfSize < 0 || centerX - halfSize >= viewportWidth)
                continue;
            projectedSprites[visibleCount++] = new ProjectedWorldSprite(
                ResolveSpriteNumber(sprite, camera),
                depth,
                centerX,
                renderedSize,
                sprite.Brightness,
                CastsGroundShadow: sprite.IsActor,
                GodRayAmount: sprite.GodRayAmount);
        }

        projectedSprites[..visibleCount].Sort(static (left, right) => right.Depth.CompareTo(left.Depth));
        return visibleCount;
    }

    private static int ResolveSpriteNumber(WorldSprite sprite, RaycastCamera camera)
    {
        if (sprite.FacingDirection < 0)
            return sprite.SpriteNumber;
        var toCameraX = camera.X - sprite.X;
        var toCameraY = camera.Y - sprite.Y;
        var viewAngle = Math.Atan2(-toCameraY, toCameraX) * 180.0 / Math.PI;
        var facingAngle = sprite.FacingDirection * 90.0;
        var relativeAngle = NormalizeDegrees(viewAngle - facingAngle + 22.5);
        return sprite.SpriteNumber + (int)(relativeAngle / 45.0);
    }

    private static double NormalizeDegrees(double angle) => ((angle % 360.0) + 360.0) % 360.0;
}
