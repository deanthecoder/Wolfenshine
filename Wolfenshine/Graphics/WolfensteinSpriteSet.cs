// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Wolfenshine.Graphics;

/// <summary>
/// Provides sprite-number lookup within VSWAP's decoded sprite region.
/// </summary>
/// <remarks>
/// Sprite numbers are relative to the VSWAP sprite boundary, matching the identifiers used by the original game.
/// </remarks>
public sealed class WolfensteinSpriteSet
{
    private const int WeaponSpriteCount = 20;
    private const int PistolReadyWeaponOffset = 5;
    private readonly IReadOnlyList<WolfensteinSprite> m_sprites;

    public WolfensteinSpriteSet(IReadOnlyList<WolfensteinSprite> sprites)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        if (sprites.Count < WeaponSpriteCount)
            throw new ArgumentException("The sprite set does not contain the expected weapon frames.", nameof(sprites));
        m_sprites = sprites;
    }

    public int Count => m_sprites.Count;
    public WolfensteinSprite PistolReady => Get(Count - WeaponSpriteCount + PistolReadyWeaponOffset);

    public WolfensteinSprite Get(int spriteNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(spriteNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(spriteNumber, Count);
        return m_sprites[spriteNumber];
    }
}
