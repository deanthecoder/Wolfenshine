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
/// Reports that a directory does not contain a complete Wolfenstein 3D data set.
/// </summary>
/// <remarks>
/// Keeping validation failures specific makes startup errors useful without coupling resource loading to the UI.
/// </remarks>
public sealed class WolfensteinDataNotFoundException : Exception
{
    public WolfensteinDataNotFoundException(DirectoryInfo directory, IReadOnlyList<string> missingFileNames)
        : base(CreateMessage(directory, missingFileNames))
    {
        Directory = directory;
        MissingFileNames = missingFileNames;
    }

    public DirectoryInfo Directory { get; }
    public IReadOnlyList<string> MissingFileNames { get; }

    private static string CreateMessage(DirectoryInfo directory, IReadOnlyList<string> missingFileNames) =>
        $"Required Wolfenstein 3D data files were not found in '{directory.FullName}'. " +
        $"Missing or empty: {string.Join(", ", missingFileNames)}.";
}
