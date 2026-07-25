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
using Wolfenshine.Graphics;

namespace Wolfenshine.Resources;

/// <summary>
/// Extracts the original VGA game palette from id Software's released GAMEPAL object file.
/// </summary>
/// <remarks>
/// The source object remains local and ignored while Wolfenshine consumes its standard 16-bit OMF data record.
/// </remarks>
public static class WolfensteinPaletteLoader
{
    private const byte LeDataRecord = 0xA0;
    private const int LeDataHeaderLength = 3;

    public static WolfensteinPalette Load(WolfensteinResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var file = resources.GetFile(WolfensteinResourceKind.PaletteSource);
        Logger.Instance.Info($"Loading Wolfenstein 3D palette from {file.Name}.");

        using var reader = new BinaryReader(file.OpenRead());
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            if (reader.BaseStream.Length - reader.BaseStream.Position < 3)
                throw new InvalidDataException("GAMEPAL.OBJ ends inside an OMF record header.");
            var recordType = reader.ReadByte();
            var recordLength = reader.ReadUInt16();
            if (recordLength == 0 || reader.BaseStream.Position + recordLength > reader.BaseStream.Length)
                throw new InvalidDataException("GAMEPAL.OBJ contains an invalid OMF record length.");

            var payloadLength = recordLength - 1;
            var payload = reader.ReadBytes(payloadLength);
            reader.ReadByte(); // OMF record checksum.
            if (recordType != LeDataRecord || payload.Length < LeDataHeaderLength + WolfensteinPalette.VgaDataLength)
                continue;

            var paletteData = payload.AsSpan(LeDataHeaderLength, WolfensteinPalette.VgaDataLength);
            if (paletteData.ContainsAnyExceptInRange((byte)0, (byte)63))
                continue;
            Logger.Instance.Info("Loaded 256 colors from the original VGA game palette.");
            return WolfensteinPalette.FromVgaDac(paletteData);
        }

        throw new InvalidDataException("GAMEPAL.OBJ does not contain a 256-color VGA palette record.");
    }
}
