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

namespace Wolfenshine.Resources;

/// <summary>
/// Provides validated access to the original Wolfenstein 3D resource containers.
/// </summary>
/// <remarks>
/// Format-specific readers can request files by purpose without knowing their DOS filenames or installation path.
/// </remarks>
public sealed class WolfensteinResources
{
    private static readonly IReadOnlyDictionary<WolfensteinResourceKind, string> s_fileNames =
        new Dictionary<WolfensteinResourceKind, string>
        {
            [WolfensteinResourceKind.AudioHeader] = "AUDIOHED.WL6",
            [WolfensteinResourceKind.AudioData] = "AUDIOT.WL6",
            [WolfensteinResourceKind.MapHeader] = "MAPHEAD.WL6",
            [WolfensteinResourceKind.MapData] = "GAMEMAPS.WL6",
            [WolfensteinResourceKind.GraphicsDictionary] = "VGADICT.WL6",
            [WolfensteinResourceKind.GraphicsHeader] = "VGAHEAD.WL6",
            [WolfensteinResourceKind.GraphicsData] = "VGAGRAPH.WL6",
            [WolfensteinResourceKind.SwapData] = "VSWAP.WL6",
            [WolfensteinResourceKind.PaletteSource] = "GAMEPAL.OBJ"
        };

    private readonly IReadOnlyDictionary<WolfensteinResourceKind, FileInfo> m_files;

    private WolfensteinResources(
        DirectoryInfo directory,
        IReadOnlyDictionary<WolfensteinResourceKind, FileInfo> files)
    {
        Directory = directory;
        m_files = files;
    }

    public DirectoryInfo Directory { get; }
    public static IReadOnlyDictionary<WolfensteinResourceKind, string> FileNames => s_fileNames;

    public static WolfensteinResources Load(DirectoryInfo directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        Logger.Instance.Info($"Loading Wolfenstein 3D resources from {directory.FullName}.");

        var availableFiles = directory.Exists
            ? directory.EnumerateFiles().ToDictionary(file => file.Name, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
        var missingFileNames = s_fileNames.Values
            .Where(fileName => !availableFiles.TryGetValue(fileName, out var file) || file.Length == 0)
            .ToArray();
        if (missingFileNames.Length > 0)
        {
            Logger.Instance.Warn(
                $"Wolfenstein 3D resource validation failed. Missing or empty: {string.Join(", ", missingFileNames)}.");
            throw new WolfensteinDataNotFoundException(directory, missingFileNames);
        }

        var files = s_fileNames.ToDictionary(
            pair => pair.Key,
            pair => availableFiles[pair.Value]);
        foreach (var (kind, file) in files)
            Logger.Instance.Info($"Loaded {kind} from {file.Name} ({file.Length:N0} bytes).");
        Logger.Instance.Info($"Wolfenstein 3D resource validation completed with {files.Count} files.");
        return new WolfensteinResources(directory, files);
    }

    public FileInfo GetFile(WolfensteinResourceKind kind) => m_files[kind];

    public Stream OpenRead(WolfensteinResourceKind kind) => GetFile(kind).OpenRead();
}
