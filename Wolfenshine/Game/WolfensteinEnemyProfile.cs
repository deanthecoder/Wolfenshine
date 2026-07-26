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
/// Describes combat constants for one ordinary enemy at a selected difficulty.
/// </summary>
/// <remarks>
/// Centralizing these values keeps difficulty-dependent tuning out of movement and animation code.
/// </remarks>
public sealed record WolfensteinEnemyProfile(
    int HitPoints,
    int Score,
    double ChaseSpeed,
    double AttackCooldown,
    int AttackDamage,
    IReadOnlyList<int> AttackSprites,
    IReadOnlyList<int> AttackFrameTicks,
    IReadOnlySet<int> FiringFrames)
{
    public static WolfensteinEnemyProfile Create(WolfensteinActorType type, GameDifficulty difficulty)
    {
        var mutantHealth = difficulty switch
        {
            GameDifficulty.Baby => 45,
            GameDifficulty.Hard => 65,
            _ => 55
        };
        return type switch
        {
            WolfensteinActorType.Guard => new(25, 100, 1.64, 1.0, 5,
                [96, 97, 98], [20, 20, 20], new HashSet<int> { 1 }),
            WolfensteinActorType.Officer => new(50, 400, 2.73, 0.7, 5,
                [285, 286, 287], [6, 20, 10], new HashSet<int> { 1 }),
            WolfensteinActorType.Ss => new(100, 500, 2.19, 1.2, 4,
                [184, 185, 186, 185, 186, 185, 186, 185, 186],
                [20, 20, 10, 10, 10, 10, 10, 10, 10],
                new HashSet<int> { 1, 3, 5, 7 }),
            WolfensteinActorType.Dog => new(1, 200, 3.2, 0.5, 10,
                [135, 136, 137, 135, 99], [10, 10, 10, 10, 10], new HashSet<int> { 1 }),
            WolfensteinActorType.Mutant => new(mutantHealth, 700, 1.64, 0.8, 6,
                [234, 235, 236, 237], [6, 20, 10, 20], new HashSet<int> { 0, 2 }),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}
