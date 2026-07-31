// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Wolfenshine.Resources;

/// <summary>
/// Resolves the writable per-user Wolfenstein 3D data directory used by the application.
/// </summary>
/// <remarks>
/// Development builds can still populate a read-only fallback beside the executable from the ignored local data folder.
/// </remarks>
public static class WolfensteinResourceLocator
{
    public const string DataDirectoryName = "GameData";

    public static DirectoryInfo GetDefaultDirectory() =>
        WolfenshineUserData.GetDirectory().CreateSubdirectory(DataDirectoryName);

    public static WolfensteinResources LoadDefault()
    {
        var userDirectory = GetDefaultDirectory();
        if (ContainsGameDataCandidate(userDirectory))
            return WolfensteinResources.Load(userDirectory);

        var bundledDirectory = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, DataDirectoryName));
        if (ContainsGameDataCandidate(bundledDirectory))
            return WolfensteinResources.Load(bundledDirectory);

        return WolfensteinResources.Load(userDirectory);
    }

    private static bool ContainsGameDataCandidate(DirectoryInfo directory) =>
        directory.Exists && directory.EnumerateFiles().Any(file =>
            file.Extension.Equals(".WL1", StringComparison.OrdinalIgnoreCase) ||
            file.Extension.Equals(".WL6", StringComparison.OrdinalIgnoreCase));
}
