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
using NUnit.Framework;
using Wolfenshine.Resources;

namespace Wolfenshine.Tests.Resources;

/// <summary>
/// Verifies safe installation of the downloadable shareware archive.
/// </summary>
public sealed class WolfensteinDataInstallerTests
{
    [Test]
    public void GivenSharewareArchiveCheckOnlyExpectedFilesAreInstalled()
    {
        using var tempDirectory = new TempDirectory();
        DirectoryInfo root = tempDirectory;
        var archive = new FileInfo(Path.Combine(root.FullName, "download.zip"));
        CreateArchive(archive, includeAllFiles: true);
        var target = new DirectoryInfo(Path.Combine(root.FullName, "GameData"));

        WolfensteinDataInstaller.InstallSharewareArchive(archive, target);

        var installedNames = target.GetFiles().Select(file => file.Name).Order().ToArray();
        var expectedNames = WolfensteinResources.FileNames.Values
            .Select(fileName => Path.ChangeExtension(fileName, ".WL1"))
            .Order()
            .ToArray();
        Assert.That(installedNames, Is.EqualTo(expectedNames));
        Assert.That(File.Exists(Path.Combine(root.FullName, "unwanted.txt")), Is.False);
    }

    [Test]
    public void GivenIncompleteArchiveCheckInstallationIsRejected()
    {
        using var tempDirectory = new TempDirectory();
        DirectoryInfo root = tempDirectory;
        var archive = new FileInfo(Path.Combine(root.FullName, "download.zip"));
        CreateArchive(archive, includeAllFiles: false);
        var target = new DirectoryInfo(Path.Combine(root.FullName, "GameData"));

        var exception = Assert.Throws<InvalidDataException>(() =>
            WolfensteinDataInstaller.InstallSharewareArchive(archive, target));

        Assert.That(exception.Message, Does.Contain("Missing"));
        Assert.That(target.Exists, Is.False);
    }

    private static void CreateArchive(FileInfo archive, bool includeAllFiles)
    {
        using var zip = ZipFile.Open(archive.FullName, ZipArchiveMode.Create);
        var names = WolfensteinResources.FileNames.Values
            .Select(fileName => Path.ChangeExtension(fileName, ".WL1"))
            .ToArray();
        if (!includeAllFiles)
            names = names.Skip(1).ToArray();
        foreach (var fileName in names)
        {
            var entry = zip.CreateEntry($"WOLF3D/{fileName}");
            using var writer = new StreamWriter(entry.Open());
            writer.Write(fileName);
        }
        var unwantedEntry = zip.CreateEntry("../unwanted.txt");
        using var unwantedWriter = new StreamWriter(unwantedEntry.Open());
        unwantedWriter.Write("This file must never be extracted.");
    }
}
