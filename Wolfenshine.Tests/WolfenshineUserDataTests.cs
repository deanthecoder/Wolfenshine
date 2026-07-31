// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core;
using NUnit.Framework;
using Wolfenshine.Resources;

namespace Wolfenshine.Tests;

/// <summary>
/// Verifies that the reset option removes only the intended per-user state.
/// </summary>
public sealed class WolfenshineUserDataTests
{
    [Test]
    public void GivenSavedStateCheckResetRemovesPreferencesAndGameDataOnly()
    {
        using var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        var settingsPath = Path.Combine(directory.FullName, WolfenshineUserData.SettingsFileName);
        File.WriteAllText(settingsPath, "{}");
        var gameDataDirectory = directory.CreateSubdirectory(WolfensteinResourceLocator.DataDirectoryName);
        File.WriteAllText(Path.Combine(gameDataDirectory.FullName, "VSWAP.WL1"), "game data");
        var logPath = Path.Combine(directory.FullName, "log.txt");
        File.WriteAllText(logPath, "retain me");

        WolfenshineUserData.Reset(directory);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(settingsPath), Is.False);
            Assert.That(gameDataDirectory.Exists, Is.False);
            Assert.That(File.Exists(logPath), Is.True);
        });
    }
}
