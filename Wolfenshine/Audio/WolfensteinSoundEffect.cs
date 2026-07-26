// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Wolfenshine.Audio;

/// <summary>
/// Identifies the original effects currently used by Wolfenshine gameplay.
/// </summary>
/// <remarks>
/// Values match the sound identifiers generated for the retail WL6 audio resources.
/// </remarks>
public enum WolfensteinSoundEffect
{
    AttackGatling = 11,
    OpenDoor = 18,
    CloseDoor = 19,
    AttackKnife = 23,
    AttackPistol = 24,
    AttackMachineGun = 26,
    GetMachineGun = 30,
    GetAmmo = 31,
    Health = 33,
    FirstAid = 34,
    BonusCross = 35,
    BonusChalice = 36,
    BonusBible = 37,
    GetGatling = 38,
    ExtraLife = 44,
    BonusCrown = 45,
    GuardFire = 58,
    SsFire = 60,
    DogAttack = 68
}
