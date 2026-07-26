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
    private double m_walkAnimationTime;
    private double m_shootAnimationTime;
    private int m_shootFrame;

    public WolfensteinActorState(WolfensteinActor actor, GameDifficulty difficulty = GameDifficulty.Normal)
    {
        Actor = actor;
        Profile = WolfensteinEnemyProfile.Create(actor.Type, difficulty);
        (m_deathFrameDuration, m_deathSprites) = actor.Type switch
        {
            WolfensteinActorType.Guard => (15.0 / OriginalTicksPerSecond, new[] { 91, 92, 93, 95 }),
            WolfensteinActorType.Officer => (11.0 / OriginalTicksPerSecond, new[] { 279, 280, 281, 283, 284 }),
            WolfensteinActorType.Ss => (15.0 / OriginalTicksPerSecond, new[] { 179, 180, 181, 183 }),
            WolfensteinActorType.Dog => (15.0 / OriginalTicksPerSecond, new[] { 131, 132, 133, 134 }),
            WolfensteinActorType.Mutant => (7.0 / OriginalTicksPerSecond, new[] { 228, 229, 230, 232, 233 }),
            _ => throw new ArgumentOutOfRangeException(nameof(actor))
        };
        HitPoints = Profile.HitPoints;
        CurrentSpriteNumber = actor.BaseSpriteNumber;
        X = actor.X;
        Y = actor.Y;
        Direction = actor.Direction;
    }

    public WolfensteinActor Actor { get; }
    public WolfensteinEnemyProfile Profile { get; }
    public int HitPoints { get; private set; }
    public int Score => Profile.Score;
    public int CurrentSpriteNumber { get; private set; }
    public double X { get; private set; }
    public double Y { get; private set; }
    public int Direction { get; private set; }
    public WolfensteinActorBehavior Behavior { get; private set; }
    public double AttackCooldown { get; set; }
    public (int X, int Y)? PathTarget { get; private set; }
    public bool IsDead => HitPoints == 0;
    public bool IsHurt => m_isHurt;
    public bool IsDeathAnimationComplete => IsDead && m_deathFrame == m_deathSprites.Length - 1;

    public WorldSprite ToWorldSprite() => new(
        X,
        Y,
        CurrentSpriteNumber,
        IsDead || Behavior == WolfensteinActorBehavior.Shooting ? -1 : Direction);

    public bool Alert()
    {
        if (IsDead || Behavior != WolfensteinActorBehavior.Dormant)
            return false;
        Behavior = WolfensteinActorBehavior.Chasing;
        CurrentSpriteNumber = GetWalkingSprite(0);
        return true;
    }

    public void MoveTo(double x, double y, int direction, double elapsedSeconds)
    {
        X = x;
        Y = y;
        Direction = direction;
        m_walkAnimationTime += elapsedSeconds;
        var frame = (int)(m_walkAnimationTime / (10.0 / OriginalTicksPerSecond)) % 4;
        CurrentSpriteNumber = GetWalkingSprite(frame);
    }

    public void SetPathTarget(int x, int y) => PathTarget = (x, y);

    public void ClearPathTarget() => PathTarget = null;

    public bool BeginShooting()
    {
        if (IsDead || Behavior == WolfensteinActorBehavior.Shooting)
            return false;
        Behavior = WolfensteinActorBehavior.Shooting;
        m_shootAnimationTime = 0.0;
        m_shootFrame = 0;
        CurrentSpriteNumber = Profile.AttackSprites[0];
        return true;
    }

    public bool UpdateShooting(double elapsedSeconds, out bool fired)
    {
        fired = false;
        if (Behavior != WolfensteinActorBehavior.Shooting)
            return false;
        m_shootAnimationTime += elapsedSeconds;
        var changed = false;
        while (Behavior == WolfensteinActorBehavior.Shooting &&
               m_shootAnimationTime >= Profile.AttackFrameTicks[m_shootFrame] / OriginalTicksPerSecond)
        {
            m_shootAnimationTime -= Profile.AttackFrameTicks[m_shootFrame] / OriginalTicksPerSecond;
            m_shootFrame++;
            if (Profile.FiringFrames.Contains(m_shootFrame))
                fired = true;
            if (m_shootFrame >= Profile.AttackSprites.Count)
            {
                Behavior = WolfensteinActorBehavior.Chasing;
                CurrentSpriteNumber = GetWalkingSprite(0);
            }
            else
            {
                CurrentSpriteNumber = Profile.AttackSprites[m_shootFrame];
            }
            changed = true;
        }
        return changed;
    }

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
        Behavior = WolfensteinActorBehavior.Dead;
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

    private int GetWalkingSprite(int frame) => Actor.Type switch
    {
        WolfensteinActorType.Guard => 58 + (frame * 8),
        WolfensteinActorType.Officer => 246 + (frame * 8),
        WolfensteinActorType.Ss => 146 + (frame * 8),
        WolfensteinActorType.Dog => 99 + (frame * 8),
        WolfensteinActorType.Mutant => 195 + (frame * 8),
        _ => Actor.BaseSpriteNumber
    };

}
