// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Reflection;
using DTC.Core.Extensions;
using Wolfenshine.Resources;

namespace Wolfenshine;

/// <summary>
/// Resolves and resets the writable files owned by the current Wolfenshine user.
/// </summary>
public static class WolfenshineUserData
{
    public const string SettingsFileName = "wolfenshine-settings.json";

    public static DirectoryInfo GetDirectory() =>
        (Assembly.GetEntryAssembly() ?? typeof(WolfenshineUserData).Assembly).GetAppSettingsPath();

    /// <summary>
    /// Deletes saved preferences and installed game data while retaining unrelated files such as the log.
    /// </summary>
    public static void Reset(DirectoryInfo directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var settingsFile = new FileInfo(Path.Combine(directory.FullName, SettingsFileName));
        if (settingsFile.Exists)
            settingsFile.Delete();
        var gameDataDirectory = new DirectoryInfo(
            Path.Combine(directory.FullName, WolfensteinResourceLocator.DataDirectoryName));
        if (gameDataDirectory.Exists)
            gameDataDirectory.Delete(recursive: true);
    }
}
