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
/// Summarizes the original time, completion ratios, and bonuses for a completed floor.
/// </summary>
public sealed record WolfensteinLevelStats(
    int Floor,
    int ElapsedSeconds,
    int? ParSeconds,
    int KillRatio,
    int SecretRatio,
    int TreasureRatio,
    int TimeBonus,
    int Bonus)
{
    private static readonly int?[] s_parSeconds =
    [
        90, 120, 120, 210, 180, 180, 150, 150, null, null,
        90, 210, 180, 120, 240, 360, 60, 180, null, null,
        90, 90, 150, 150, 210, 150, 120, 360, null, null,
        120, 120, 90, 60, 270, 210, 120, 270, null, null,
        150, 90, 150, 150, 240, 180, 270, 210, null, null,
        390, 240, 270, 360, 300, 330, 330, 510, null, null
    ];

    public string TimeText => FormatTime(ElapsedSeconds);
    public string ParText => ParSeconds is { } seconds ? FormatTime(seconds) : "??:??";
    public string KillText => $"{KillRatio}%";
    public string SecretText => $"{SecretRatio}%";
    public string TreasureText => $"{TreasureRatio}%";
    public string BonusText => Bonus.ToString();

    public static WolfensteinLevelStats Create(
        int mapSlot,
        double elapsedSeconds,
        int killCount,
        int killTotal,
        int secretCount,
        int secretTotal,
        int treasureCount,
        int treasureTotal)
    {
        var elapsed = Math.Min(99 * 60, (int)elapsedSeconds);
        var par = mapSlot >= 0 && mapSlot < s_parSeconds.Length ? s_parSeconds[mapSlot] : null;
        var killRatio = GetRatio(killCount, killTotal);
        var secretRatio = GetRatio(secretCount, secretTotal);
        var treasureRatio = GetRatio(treasureCount, treasureTotal);
        var timeBonus = par is { } parSeconds ? Math.Max(0, parSeconds - elapsed) * 500 : 0;
        var perfectBonuses = new[] { killRatio, secretRatio, treasureRatio }.Count(ratio => ratio == 100) * 10000;
        return new WolfensteinLevelStats(
            (mapSlot % 10) + 1,
            elapsed,
            par,
            killRatio,
            secretRatio,
            treasureRatio,
            timeBonus,
            timeBonus + perfectBonuses);
    }

    private static int GetRatio(int count, int total) => total == 0 ? 0 : count * 100 / total;

    private static string FormatTime(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";
}
