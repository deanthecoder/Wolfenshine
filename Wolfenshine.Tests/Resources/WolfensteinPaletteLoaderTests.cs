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
using Wolfenshine.Graphics;
using Wolfenshine.Resources;

namespace Wolfenshine.Tests.Resources;

/// <summary>
/// Verifies extraction of the game palette from its released OMF object.
/// </summary>
/// <remarks>
/// Synthetic OMF data exercises the loader without redistributing id Software's palette bytes.
/// </remarks>
public sealed class WolfensteinPaletteLoaderTests
{
    [Test]
    public void GivenPaletteObjectCheckGamePaletteIsLoaded()
    {
        using var tempDirectory = new TempDirectory();
        DirectoryInfo directory = tempDirectory;
        foreach (var fileName in WolfensteinResources.FileNames.Values)
            File.WriteAllBytes(Path.Combine(directory.FullName, fileName), [1]);
        WritePaletteObject(new FileInfo(Path.Combine(directory.FullName, "GAMEPAL.OBJ")));

        var palette = WolfensteinPaletteLoader.Load(WolfensteinResources.Load(directory));

        Assert.That(palette.GetColor(1), Is.EqualTo(new RgbaColor(255, 170, 85)));
    }

    private static void WritePaletteObject(FileInfo file)
    {
        var palette = new byte[WolfensteinPalette.VgaDataLength];
        palette[3] = 63;
        palette[4] = 42;
        palette[5] = 21;
        using var writer = new BinaryWriter(file.Open(FileMode.Create, FileAccess.Write));
        writer.Write((byte)0xA0);
        writer.Write((ushort)(3 + palette.Length + 1));
        writer.Write((byte)1);
        writer.Write((ushort)0);
        writer.Write(palette);
        writer.Write((byte)0);
    }
}
