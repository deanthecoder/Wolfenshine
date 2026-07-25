// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Wolfenshine.Game;

/// <summary>
/// Describes one map-spawned enemy before behavior and combat are applied.
/// </summary>
/// <remarks>
/// Direction uses the map's cardinal order: east, north, west, then south.
/// </remarks>
public readonly record struct WolfensteinActor(
    double X,
    double Y,
    WolfensteinActorType Type,
    int Direction,
    bool IsPatrolling,
    bool IsAmbush,
    int BaseSpriteNumber)
{
    public WorldSprite ToWorldSprite() => new(X, Y, BaseSpriteNumber, Direction);
}
