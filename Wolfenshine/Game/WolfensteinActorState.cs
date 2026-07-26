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
/// Tracks the combat and animation state of one map-spawned enemy.
/// </summary>
/// <remarks>
/// Map actor definitions remain immutable while this runtime state advances through the original death sprites.
/// </remarks>
public sealed class WolfensteinActorState
{
    private const double OriginalTicksPerSecond = 70.0;
    private readonly int[] m_deathSprites;
    private readonly double m_deathFrameDuration;
    private double m_animationTime;
    private int m_deathFrame;
    private bool m_isHurt;

    public WolfensteinActorState(WolfensteinActor actor)
    {
        Actor = actor;
        (HitPoints, Score, m_deathFrameDuration, m_deathSprites) = actor.Type switch
        {
            WolfensteinActorType.Guard => (25, 100, 15.0 / OriginalTicksPerSecond, new[] { 91, 92, 93, 95 }),
            WolfensteinActorType.Officer => (50, 400, 11.0 / OriginalTicksPerSecond, new[] { 279, 280, 281, 283, 284 }),
            WolfensteinActorType.Ss => (100, 500, 15.0 / OriginalTicksPerSecond, new[] { 179, 180, 181, 183 }),
            WolfensteinActorType.Dog => (1, 200, 15.0 / OriginalTicksPerSecond, new[] { 131, 132, 133, 134 }),
            WolfensteinActorType.Mutant => (55, 700, 7.0 / OriginalTicksPerSecond, new[] { 228, 229, 230, 232, 233 }),
            _ => throw new ArgumentOutOfRangeException(nameof(actor))
        };
        CurrentSpriteNumber = actor.BaseSpriteNumber;
    }

    public WolfensteinActor Actor { get; }
    public int HitPoints { get; private set; }
    public int Score { get; }
    public int CurrentSpriteNumber { get; private set; }
    public bool IsDead => HitPoints == 0;
    public bool IsHurt => m_isHurt;
    public bool IsDeathAnimationComplete => IsDead && m_deathFrame == m_deathSprites.Length - 1;

    public WorldSprite ToWorldSprite() => new(
        Actor.X,
        Actor.Y,
        CurrentSpriteNumber,
        IsDead ? -1 : Actor.Direction);

    public bool Damage(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        if (IsDead)
            return false;
        HitPoints = Math.Max(0, HitPoints - amount);
        if (!IsDead)
        {
            m_isHurt = true;
            m_animationTime = 0.0;
            CurrentSpriteNumber = Actor.Type switch
            {
                WolfensteinActorType.Guard => 90,
                WolfensteinActorType.Officer => 278,
                WolfensteinActorType.Ss => 178,
                WolfensteinActorType.Mutant => 227,
                _ => Actor.BaseSpriteNumber
            };
            return true;
        }
        m_isHurt = false;
        m_deathFrame = 0;
        m_animationTime = 0.0;
        CurrentSpriteNumber = m_deathSprites[0];
        return true;
    }

    public bool Update(double elapsedSeconds)
    {
        if (m_isHurt)
        {
            m_animationTime += elapsedSeconds;
            if (m_animationTime < 10.0 / OriginalTicksPerSecond)
                return false;
            m_isHurt = false;
            m_animationTime = 0.0;
            CurrentSpriteNumber = Actor.BaseSpriteNumber;
            return true;
        }
        if (!IsDead || IsDeathAnimationComplete)
            return false;
        m_animationTime += elapsedSeconds;
        var changed = false;
        while (m_animationTime >= m_deathFrameDuration && !IsDeathAnimationComplete)
        {
            m_animationTime -= m_deathFrameDuration;
            CurrentSpriteNumber = m_deathSprites[++m_deathFrame];
            changed = true;
        }
        return changed;
    }
}
