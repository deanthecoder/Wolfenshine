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

namespace Wolfenshine.Tests.Resources;

/// <summary>
/// Verifies validation and lookup of original Wolfenstein 3D data files.
/// </summary>
/// <remarks>
/// Tests use empty stand-in files and never depend on or redistribute the original game data.
/// </remarks>
public sealed class WolfensteinResourcesTests
{
    [Test]
    public void GivenCompleteDataSetCheckFilesCanBeResolved()
    {
        using var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        foreach (var fileName in WolfensteinResources.FileNames.Values)
            File.WriteAllBytes(Path.Combine(directory.FullName, fileName.ToLowerInvariant()), [1]);

        var resources = WolfensteinResources.Load(directory);

        Assert.That(resources.GetFile(WolfensteinResourceKind.MapData).Name, Is.EqualTo("gamemaps.wl6"));
    }

    [Test]
    public void GivenMissingDataCheckUsefulExceptionIsThrown()
    {
        using var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        File.WriteAllBytes(Path.Combine(directory.FullName, "VSWAP.WL6"), [1]);

        var exception = Assert.Throws<WolfensteinDataNotFoundException>(() =>
            WolfensteinResources.Load(directory));

        Assert.That(exception.MissingFileNames, Does.Contain("GAMEMAPS.WL6"));
        Assert.That(exception.Message, Does.Contain(directory.FullName));
    }

    [Test]
    public void GivenEmptyDataFileCheckItIsReportedAsMissing()
    {
        using var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        foreach (var fileName in WolfensteinResources.FileNames.Values)
            File.WriteAllBytes(Path.Combine(directory.FullName, fileName), [1]);
        File.WriteAllBytes(Path.Combine(directory.FullName, "VSWAP.WL6"), []);

        var exception = Assert.Throws<WolfensteinDataNotFoundException>(() =>
            WolfensteinResources.Load(directory));

        Assert.That(exception.MissingFileNames, Is.EqualTo(new[] { "VSWAP.WL6" }));
    }
}
