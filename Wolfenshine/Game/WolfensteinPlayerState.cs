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
/// Captures player values that persist between completed levels.
/// </summary>
/// <remarks>
/// Level-local actors, doors, pickups, and statistics are intentionally excluded.
/// </remarks>
public readonly record struct WolfensteinPlayerState(
    int Health,
    int Ammo,
    int Lives,
    int Score,
    PlayerWeapon Weapon,
    PlayerWeapon ChosenWeapon,
    PlayerWeapon BestWeapon);
