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
/// Provides the original fixed Wolfenstein 3D VGA game palette.
/// </summary>
/// <remarks>
/// Keeping these 768 VGA-DAC values with the compatible engine avoids requiring a compiler object
/// file that was never part of the retail or shareware game-data packages.
/// </remarks>
public static class WolfensteinPaletteLoader
{
    private const string PaletteDataBase64 =
        """
            AAAAAAAqACoAACoqKgAAKgAqKhUAKioqFRUVFRU/FT8VFT8/PxUVPxU/Pz8VPz8/Ozs7Nzc3NDQ0MDAwLS0tKioqJiYmIyMj
            Hx8fHBwcGRkZFRUVEhISDg4OCwsLCAgIPwAAOwAAOAAANQAAMgAALwAALAAAKQAAJgAAIgAAHwAAHAAAGQAAFgAAEwAAEAAA
            PzY2Py4uPycnPx8fPxcXPxAQPwgIPwAAPyoXPyYQPyIIPx4AORsAMxgALRUAJxMAPz82Pz8uPz8nPz8fPz4XPz0QPz0IPz0A
            OTYAMzEALSsAJycAISEAHBsAFhUAEBAAND8XMT8QLT8IKD8AJDkAIDMAHS0AGCcANj82Lz8uJz8nID8fGD8XED8QCD8IAD8A
            AD8AADsAADgAADUAATIAAS8AASwAASkAASYAASIAAR8AARwAARkAARYAARMAARAANj8/Lj8/Jz8/Hz8+Fz8/ED8/CD8/AD8/
            ADk5ADMzAC0tACcnACEhABwcABYWABAQFy8/ECw/CCo/ACc/ACM5AB8zABstABcnNjY/Li8/Jyc/HyA/Fxg/EBA/CAk/AAE/
            AAA/AAA7AAA4AAA1AAAyAAAvAAAsAAApAAAmAAAiAAAfAAAcAAAZAAAWAAATAAAQCgoKPzgNPzUJPzMGPzACPy0ALQg/KgA/
            JgA5IAAzHQAtGAAnFAAhEQAcDQAWCgAQPzY/Py4/Pyc/Px8/Pxc/PxA/Pwg/PwA/OAA5MgAzLQAtJwAnIQAhGwAcFgAWEAAQ
            Pzo3Pzg0PzYxPzUvPzMsPzEpPy8nPy4kPywgPykcPycYPCUXOiMWNyIVNCAUMh8TLx4SLRwRKhoQKBkPJxgOJBcNIhYMIBQL
            HRMKGxIJFxAIFQ8HEg4GEAwGDgsFCggDGAAZABkZABgYAAAHAAALDAkEEgASFAAUAAANBwcHExMTFxcXEBAQDAwMDQ0NNj09
            Ljo6Jzc3HTIyEjAwCC0tCCwsACkpACYmACMjACEhAB8fAB4eAB0dABwcABsbJgAi
        """;

    public static WolfensteinPalette Load()
    {
        var paletteData = Convert.FromBase64String(PaletteDataBase64);
        if (paletteData.Length != WolfensteinPalette.VgaDataLength)
            throw new InvalidDataException("The embedded Wolfenstein 3D palette has an invalid length.");
        Logger.Instance.Info("Loaded 256 colors from the embedded original VGA game palette.");
        return WolfensteinPalette.FromVgaDac(paletteData);
    }
}
