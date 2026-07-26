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
        28 => WolfensteinPickupType.AmmoClip,
        31 => WolfensteinPickupType.Cross,
        32 => WolfensteinPickupType.Chalice,
        33 => WolfensteinPickupType.Bible,
        34 => WolfensteinPickupType.Crown,
        _ => WolfensteinPickupType.None
    };
}
