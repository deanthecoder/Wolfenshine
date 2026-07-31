// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.IO.Compression;
using DTC.Core;

namespace Wolfenshine.Resources;

/// <summary>
/// Installs the recognized Wolfenstein 3D shareware resources from their downloadable archive.
/// </summary>
public static class WolfensteinDataInstaller
{
    public const string SharewareArchiveFileName = "w3d-box.zip";
    public const string SharewareDownloadUrl =
        "https://www.dosgamesarchive.com/file.php?id=557";

    /// <summary>
    /// Extracts only the eight expected WL1 resource files, regardless of their directory inside the ZIP.
    /// </summary>
    public static void InstallSharewareArchive(FileInfo archive, DirectoryInfo targetDirectory)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(targetDirectory);
        if (!archive.Exists)
            throw new FileNotFoundException("The dropped shareware archive was not found.", archive.FullName);
        if (!archive.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Drop the w3d-box.zip shareware archive here.");

        var expectedNames = WolfensteinResources.FileNames.Values
            .Select(fileName => Path.ChangeExtension(fileName, ".WL1"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        using var zip = ZipFile.OpenRead(archive.FullName);
        var entries = zip.Entries
            .Where(entry => expectedNames.Contains(Path.GetFileName(entry.FullName)) && entry.Length > 0)
            .GroupBy(entry => Path.GetFileName(entry.FullName), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var missingNames = expectedNames.Where(fileName => !entries.ContainsKey(fileName)).ToArray();
        if (missingNames.Length > 0)
        {
            throw new InvalidDataException(
                $"The ZIP is not the expected Wolfenstein 3D shareware archive. Missing: " +
                $"{string.Join(", ", missingNames)}.");
        }

        targetDirectory.Create();
        foreach (var fileName in expectedNames)
        {
            var destinationPath = Path.Combine(targetDirectory.FullName, fileName);
            using var source = entries[fileName].Open();
            using var destination = File.Create(destinationPath);
            source.CopyTo(destination);
        }
        Logger.Instance.Info(
            $"Installed {expectedNames.Count} Wolfenstein 3D shareware files from {archive.FullName}.");
    }
}
