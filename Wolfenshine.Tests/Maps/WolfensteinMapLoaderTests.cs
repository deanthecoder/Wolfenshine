// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Text;
using DTC.Core;
using NUnit.Framework;
using Wolfenshine.Maps;
using Wolfenshine.Resources;

namespace Wolfenshine.Tests.Maps;

/// <summary>
/// Verifies loading and decompression of Wolfenstein 3D map containers.
/// </summary>
/// <remarks>
/// Synthetic containers exercise the original binary format without requiring commercial game data in the tests.
/// </remarks>
public sealed class WolfensteinMapLoaderTests
{
    private const ushort RlewTag = 0xABCD;

    [Test]
    public void GivenValidMapFilesCheckMapPlanesAreLoaded()
    {
        using var tempDirectory = CreateResources();

        var mapSet = WolfensteinMapLoader.Load(WolfensteinResources.Load(tempDirectory));

        Assert.Multiple(() =>
        {
            Assert.That(mapSet.RlewTag, Is.EqualTo(RlewTag));
            Assert.That(mapSet.Maps, Has.Count.EqualTo(1));
            Assert.That(mapSet.Maps[0].Slot, Is.Zero);
            Assert.That(mapSet.Maps[0].Name, Is.EqualTo("Test Map"));
            Assert.That(mapSet.Maps[0].Width, Is.EqualTo(2));
            Assert.That(mapSet.Maps[0].Height, Is.EqualTo(2));
            Assert.That(mapSet.Maps[0].Walls, Is.EqualTo(new ushort[] { 10, 20, 10, 20 }));
            Assert.That(mapSet.Maps[0].Objects, Is.EqualTo(new ushort[] { 30, 40, 30, 40 }));
        });
    }

    [Test]
    public void GivenMapPlaneOutsideContainerCheckUsefulExceptionIsThrown()
    {
        using var tempDirectory = CreateResources();
        var mapFile = new FileInfo(Path.Combine(tempDirectory.FullName, "GAMEMAPS.WL6"));
        using (var stream = mapFile.Open(FileMode.Open, FileAccess.Write))
        using (var writer = new BinaryWriter(stream))
            writer.Write(uint.MaxValue - 1);

        var exception = Assert.Throws<InvalidDataException>(() =>
            WolfensteinMapLoader.Load(WolfensteinResources.Load(tempDirectory)));

        Assert.That(exception.Message, Does.Contain("map slot 0 plane 0"));
    }

    [Test]
    public void GivenSharewareMapHeaderCheckOnlyTenReservedSlotsAreRead()
    {
        using var tempDirectory = CreateResources(".WL1", sharewareOffsets: true);

        var mapSet = WolfensteinMapLoader.Load(WolfensteinResources.Load(tempDirectory));

        Assert.That(mapSet.Maps, Has.Count.EqualTo(1));
    }

    private static TempDirectory CreateResources(string extension = ".WL6", bool sharewareOffsets = false)
    {
        var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        foreach (var fileName in WolfensteinResources.FileNames.Values)
            File.WriteAllBytes(Path.Combine(directory.FullName, Path.ChangeExtension(fileName, extension)), [1]);

        WriteMapHeader(new FileInfo(Path.Combine(directory.FullName, $"MAPHEAD{extension}")), sharewareOffsets);
        WriteMapData(new FileInfo(Path.Combine(directory.FullName, $"GAMEMAPS{extension}")));
        return tempDirectory;
    }

    private static void WriteMapHeader(FileInfo file, bool sharewareOffsets)
    {
        using var writer = new BinaryWriter(file.Open(FileMode.Create, FileAccess.Write));
        writer.Write(RlewTag);
        writer.Write((uint)0);
        for (var slot = 1; slot < 100; slot++)
            writer.Write(sharewareOffsets && slot >= 10 ? 0U : uint.MaxValue);
    }

    private static void WriteMapData(FileInfo file)
    {
        var wallPlane = CreateNearCompressedPlane(10, 20);
        var objectPlane = CreateFarCompressedPlane(30, 40);
        const int mapHeaderLength = 38;
        var wallOffset = mapHeaderLength;
        var objectOffset = wallOffset + wallPlane.Length;

        using var writer = new BinaryWriter(file.Open(FileMode.Create, FileAccess.Write));
        writer.Write((uint)wallOffset);
        writer.Write((uint)objectOffset);
        writer.Write((uint)0);
        writer.Write((ushort)wallPlane.Length);
        writer.Write((ushort)objectPlane.Length);
        writer.Write((ushort)0);
        writer.Write((ushort)2);
        writer.Write((ushort)2);
        var name = new byte[16];
        Encoding.ASCII.GetBytes("Test Map").CopyTo(name, 0);
        writer.Write(name);
        writer.Write(wallPlane);
        writer.Write(objectPlane);
    }

    private static byte[] CreateNearCompressedPlane(ushort first, ushort second)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)10);
        writer.Write((ushort)8);
        writer.Write(first);
        writer.Write(second);
        writer.Write((ushort)0xA702);
        writer.Write((byte)2);
        return stream.ToArray();
    }

    private static byte[] CreateFarCompressedPlane(ushort first, ushort second)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)10);
        writer.Write((ushort)8);
        writer.Write(first);
        writer.Write(second);
        writer.Write((ushort)0xA802);
        writer.Write((ushort)1);
        return stream.ToArray();
    }
}
