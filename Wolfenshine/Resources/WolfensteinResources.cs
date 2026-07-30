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
    private const string FullExtension = ".WL6";
    private const string SharewareExtension = ".WL1";
    private static readonly IReadOnlyDictionary<WolfensteinResourceKind, string> s_fullFileNames =
        CreateFileNames(FullExtension);
    private static readonly IReadOnlyDictionary<WolfensteinResourceKind, string> s_sharewareFileNames =
        CreateFileNames(SharewareExtension);
    private readonly IReadOnlyDictionary<WolfensteinResourceKind, FileInfo> m_files;

    private WolfensteinResources(
        DirectoryInfo directory,
        WolfensteinDataEdition edition,
        IReadOnlyDictionary<WolfensteinResourceKind, FileInfo> files)
    {
        Directory = directory;
        Edition = edition;
        m_files = files;
    }

    public DirectoryInfo Directory { get; }
    public WolfensteinDataEdition Edition { get; }
    public int MapSlotCount => Edition == WolfensteinDataEdition.Shareware ? 10 : 60;

    /// <summary>
    /// Returns the traditional full-edition filenames retained by synthetic tests and diagnostics.
    /// </summary>
    public static IReadOnlyDictionary<WolfensteinResourceKind, string> FileNames => s_fullFileNames;

    public static WolfensteinResources Load(DirectoryInfo directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        Logger.Instance.Info($"Loading Wolfenstein 3D resources from {directory.FullName}.");

        var availableFiles = directory.Exists
            ? directory.EnumerateFiles().ToDictionary(file => file.Name, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
        var fullMissing = GetMissingFiles(s_fullFileNames, availableFiles);
        var sharewareMissing = GetMissingFiles(s_sharewareFileNames, availableFiles);
        WolfensteinDataEdition edition;
        IReadOnlyDictionary<WolfensteinResourceKind, string> fileNames;
        if (fullMissing.Length == 0)
        {
            edition = WolfensteinDataEdition.Full;
            fileNames = s_fullFileNames;
        }
        else if (sharewareMissing.Length == 0)
        {
            edition = WolfensteinDataEdition.Shareware;
            fileNames = s_sharewareFileNames;
        }
        else
        {
            var hasSharewareFiles = availableFiles.Keys.Any(fileName =>
                fileName.EndsWith(SharewareExtension, StringComparison.OrdinalIgnoreCase));
            var missingFileNames = hasSharewareFiles ? sharewareMissing : fullMissing;
            Logger.Instance.Warn(
                $"Wolfenstein 3D resource validation failed. Missing or empty: {string.Join(", ", missingFileNames)}.");
            throw new WolfensteinDataNotFoundException(directory, missingFileNames);
        }

        var files = fileNames.ToDictionary(
            pair => pair.Key,
            pair => availableFiles[pair.Value]);
        foreach (var (kind, file) in files)
            Logger.Instance.Info($"Loaded {kind} from {file.Name} ({file.Length:N0} bytes).");
        Logger.Instance.Info(
            $"Wolfenstein 3D {edition.ToString().ToLowerInvariant()} resource validation completed with {files.Count} files.");
        return new WolfensteinResources(directory, edition, files);
    }

    public FileInfo GetFile(WolfensteinResourceKind kind) => m_files[kind];

    public Stream OpenRead(WolfensteinResourceKind kind) => GetFile(kind).OpenRead();

    private static IReadOnlyDictionary<WolfensteinResourceKind, string> CreateFileNames(string extension) =>
        new Dictionary<WolfensteinResourceKind, string>
        {
            [WolfensteinResourceKind.AudioHeader] = $"AUDIOHED{extension}",
            [WolfensteinResourceKind.AudioData] = $"AUDIOT{extension}",
            [WolfensteinResourceKind.MapHeader] = $"MAPHEAD{extension}",
            [WolfensteinResourceKind.MapData] = $"GAMEMAPS{extension}",
            [WolfensteinResourceKind.GraphicsDictionary] = $"VGADICT{extension}",
            [WolfensteinResourceKind.GraphicsHeader] = $"VGAHEAD{extension}",
            [WolfensteinResourceKind.GraphicsData] = $"VGAGRAPH{extension}",
            [WolfensteinResourceKind.SwapData] = $"VSWAP{extension}"
        };

    private static string[] GetMissingFiles(
        IReadOnlyDictionary<WolfensteinResourceKind, string> fileNames,
        IReadOnlyDictionary<string, FileInfo> availableFiles) =>
        fileNames.Values
            .Where(fileName => !availableFiles.TryGetValue(fileName, out var file) || file.Length == 0)
            .ToArray();
}
