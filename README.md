[![Twitter URL](https://img.shields.io/twitter/url/https/twitter.com/deanthecoder.svg?style=social&label=Follow%20%40deanthecoder)](https://twitter.com/deanthecoder) [![GitHub Repo stars](https://img.shields.io/github/stars/deanthecoder/Wolfenshine?style=social&label=Star)](https://github.com/deanthecoder/Wolfenshine/stargazers)

# Wolfenshine

A modern C# reimplementation of Wolfenstein 3D, beginning with a faithful 320×200 software renderer and leaving room for enhanced GPU rendering later.

## Status

Wolfenshine is in its earliest development stage. The repository currently contains a runnable Avalonia desktop shell, an MVVM foundation, and an NUnit test project.

## Building

Clone the repository and its submodules, then build the solution:

```shell
git clone --recurse-submodules https://github.com/deanthecoder/Wolfenshine.git
cd Wolfenshine
dotnet build Wolfenshine.slnx
```

Wolfenshine does not include the commercial Wolfenstein 3D data. During development, place a legitimately obtained six-episode data set in:

```text
local/game-data/wolf3d/
```

The `local/` directory is ignored by Git. When the `.WL6` files are present, the build copies them into the application's `GameData` output directory. Without them, Wolfenshine still builds and starts, but displays a message listing the missing files and expected location.

## Developer notes

### Wolfenstein 3D data files

The `.WL6` suffix identifies data for the full six-episode edition. The shareware episode uses `.WL1`; other releases use related suffixes and may arrange individual resources differently. Multi-byte values in the original data are little-endian.

| File | Purpose |
|---|---|
| `AUDIOHED.WL6` | An offset table locating audio chunks within `AUDIOT.WL6`. |
| `AUDIOT.WL6` | PC-speaker effects, AdLib effects, and music data. Digitized sound samples are stored in `VSWAP.WL6`. |
| `MAPHEAD.WL6` | Contains the RLEW compression tag and offsets to level headers within `GAMEMAPS.WL6`. |
| `GAMEMAPS.WL6` | Contains the level headers and compressed map planes describing walls, objects, actors, and areas. Map planes use Carmack compression followed by RLEW compression. |
| `VGADICT.WL6` | The Huffman dictionary used to decompress chunks from `VGAGRAPH.WL6`. |
| `VGAHEAD.WL6` | A table of 24-bit offsets locating graphics chunks within `VGAGRAPH.WL6`. |
| `VGAGRAPH.WL6` | Huffman-compressed UI artwork, fonts, tiles, and other screen graphics. Chunk identifiers vary between game versions. |
| `VSWAP.WL6` | A page-oriented container holding wall textures, sprites, and digitized sound samples. |

`CONFIG.WL6` is generated configuration state rather than a required asset. `WOLF3D.EXE` is useful as a behavioural reference but is not loaded by Wolfenshine.

The current private development data is the full six-episode June 1992 v1.1 release. The released source tree defaults to the later `GOODTIMES` v1.4 configuration, so resource readers must avoid assuming that version-specific chunk identifiers are universal.

## License

Licensed under the MIT License. See [LICENSE](LICENSE) for details.
