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
/// Interprets common enemy markers from map plane one.
/// </summary>
/// <remarks>
/// Difficulty-specific duplicate markers normalize to the same actor definitions used by the original game.
/// </remarks>
public static class WolfensteinActors
{
    private const int GuardStandingSprite = 50;
    private const int GuardWalkingSprite = 58;
    private const int DogWalkingSprite = 99;
    private const int SsStandingSprite = 138;
    private const int SsWalkingSprite = 146;
    private const int MutantStandingSprite = 187;
    private const int MutantWalkingSprite = 195;
    private const int OfficerStandingSprite = 238;
    private const int OfficerWalkingSprite = 246;

    public static IReadOnlyList<WolfensteinActor> FromMap(
        WolfensteinMap map,
        GameDifficulty difficulty = GameDifficulty.Normal)
    {
        ArgumentNullException.ThrowIfNull(map);
        var actors = new List<WolfensteinActor>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (TryCreate(map, x, y, map.GetObject(x, y), difficulty, out var actor))
                    actors.Add(actor);
            }
        }
        return actors;
    }

    private static bool TryCreate(
        WolfensteinMap map,
        int x,
        int y,
        ushort marker,
        GameDifficulty difficulty,
        out WolfensteinActor actor)
    {
        actor = default;
        if (marker is >= 180 and <= 213)
        {
            if (difficulty != GameDifficulty.Hard)
                return false;
            marker -= 72;
        }
        else if (marker is >= 144 and <= 177)
        {
            if (difficulty < GameDifficulty.Normal)
                return false;
            marker -= 36;
        }
        else if (marker is >= 252 and <= 259)
        {
            if (difficulty != GameDifficulty.Hard)
                return false;
            marker -= 36;
        }
        else if (marker is >= 234 and <= 241)
        {
            if (difficulty < GameDifficulty.Normal)
                return false;
            marker -= 18;
        }

        WolfensteinActorType type;
        int direction;
        bool isPatrolling;
        int sprite;
        if (TryMatch(marker, 108, out direction, out isPatrolling))
            (type, sprite) = (WolfensteinActorType.Guard, isPatrolling ? GuardWalkingSprite : GuardStandingSprite);
        else if (TryMatch(marker, 116, out direction, out isPatrolling))
            (type, sprite) = (WolfensteinActorType.Officer, isPatrolling ? OfficerWalkingSprite : OfficerStandingSprite);
        else if (TryMatch(marker, 126, out direction, out isPatrolling))
            (type, sprite) = (WolfensteinActorType.Ss, isPatrolling ? SsWalkingSprite : SsStandingSprite);
        else if (TryMatch(marker, 134, out direction, out isPatrolling))
            (type, sprite) = (WolfensteinActorType.Dog, DogWalkingSprite);
        else if (TryMatch(marker, 216, out direction, out isPatrolling))
            (type, sprite) = (WolfensteinActorType.Mutant, isPatrolling ? MutantWalkingSprite : MutantStandingSprite);
        else
        {
            return false;
        }

        actor = new WolfensteinActor(
            x + 0.5,
            y + 0.5,
            type,
            direction,
            isPatrolling,
            map.GetWall(x, y) == 106,
            sprite);
        return true;
    }

    private static bool TryMatch(ushort marker, int standingMarker, out int direction, out bool isPatrolling)
    {
        var relative = marker - standingMarker;
        isPatrolling = relative is >= 4 and <= 7;
        direction = isPatrolling ? relative - 4 : relative;
        return relative is >= 0 and <= 7;
    }
}
