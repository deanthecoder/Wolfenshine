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
/// Resolves the conventional Wolfenstein 3D data directory used by the application.
/// </summary>
/// <remarks>
/// Development builds populate this directory from the ignored local data folder when it is available.
/// </remarks>
public static class WolfensteinResourceLocator
{
    public const string DataDirectoryName = "GameData";

    public static DirectoryInfo GetDefaultDirectory() =>
        new(Path.Combine(AppContext.BaseDirectory, DataDirectoryName));

    public static WolfensteinResources LoadDefault() =>
        WolfensteinResources.Load(GetDefaultDirectory());
}
