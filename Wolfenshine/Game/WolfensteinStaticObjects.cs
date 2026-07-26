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

namespace Wolfenshine.Game;

/// <summary>
/// Interprets static decoration and pickup markers from map plane one.
/// </summary>
/// <remarks>
/// Full Wolfenstein 3D markers 23 through 70 map to SPR_STAT_0 through SPR_STAT_47.
/// </remarks>
public static class WolfensteinStaticObjects
{
    private const ushort FirstMarker = 23;
    private const ushort LastMarker = 70;
    private const int FirstStaticSprite = 2;

    // These indices correspond to entries carrying the original `block` flag in statinfo[].
    private static readonly HashSet<ushort> BlockingMarkers =
    [
        24, 25, 26, 28, 30, 31, 33, 34, 35, 36, 39, 40, 41, 45, 58, 59, 60, 62, 63, 68, 69
    ];

    public static IReadOnlyList<WorldSprite> FromMap(WolfensteinMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var objects = new List<WorldSprite>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var marker = map.GetObject(x, y);
                if (marker is < FirstMarker or > LastMarker)
                    continue;
                objects.Add(new WorldSprite(
                    x + 0.5,
                    y + 0.5,
                    FirstStaticSprite + marker - FirstMarker));
            }
        }
        return objects;
    }

    /// <summary>
    /// Determines whether a plane-one marker represents solid scenery.
    /// </summary>
    public static bool BlocksMovement(ushort marker) => BlockingMarkers.Contains(marker);

    /// <summary>
    /// Identifies ammo and treasure represented by a static sprite.
    /// </summary>
    public static WolfensteinPickupType GetPickupType(int spriteNumber) => spriteNumber switch
    {
        8 => WolfensteinPickupType.DogFood,
        22 => WolfensteinPickupType.GoldKey,
        23 => WolfensteinPickupType.SilverKey,
        26 => WolfensteinPickupType.Food,
        27 => WolfensteinPickupType.FirstAid,
        28 => WolfensteinPickupType.AmmoClip,
        29 => WolfensteinPickupType.MachineGun,
        30 => WolfensteinPickupType.Chaingun,
        31 => WolfensteinPickupType.Cross,
        32 => WolfensteinPickupType.Chalice,
        33 => WolfensteinPickupType.Bible,
        34 => WolfensteinPickupType.Crown,
        35 => WolfensteinPickupType.FullHeal,
        _ => WolfensteinPickupType.None
    };

    /// <summary>
    /// Gets the upward and downward light emitted by a static sprite.
    /// </summary>
    /// <remarks>
    /// Directional brightness keeps the shader independent of Wolfenstein-specific object types.
    /// </remarks>
    public static (float Upward, float Downward) GetLightBrightness(int spriteNumber) => spriteNumber switch
    {
        5 => (0.90f, 0.80f), // Floor lamp.
        6 => (0.10f, 1.00f), // Chandelier.
        16 => (0.55f, 1.00f), // Green ceiling light.
        31 or 32 or 33 or 34 => (0.30f, 0.40f), // Treasure glow.
        _ => (0.0f, 0.0f)
    };

    /// <summary>
    /// Gets the upward and downward light radius of a static sprite in map tiles.
    /// </summary>
    public static (float Upward, float Downward) GetLightRadii(int spriteNumber) => spriteNumber switch
    {
        5 or 6 => (2.75f, 2.75f), // Floor lamp or chandelier.
        16 => (1.10f, 2.75f), // Green ceiling light with a narrow upward shine.
        31 or 32 or 33 or 34 => (1.25f, 1.25f), // Localized treasure glow.
        _ => (0.0f, 0.0f)
    };
}
