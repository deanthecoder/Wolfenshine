[![Twitter URL](https://img.shields.io/twitter/url/https/twitter.com/deanthecoder.svg?style=social&label=Follow%20%40deanthecoder)](https://twitter.com/deanthecoder) [![GitHub Repo stars](https://img.shields.io/github/stars/deanthecoder/Wolfenshine?style=social&label=Star)](https://github.com/deanthecoder/Wolfenshine/stargazers)

# Wolfenshine

A modern C# reimplementation of Wolfenstein 3D, beginning with a faithful 320×200 software renderer and leaving room for enhanced GPU rendering later.

## Status

Wolfenshine currently loads and decompresses all 60 maps from the original six-episode data, locates the E1M1 player start, and provides arrow-key navigation through a textured 320×160 software-raycast view above the original 320×40 status bar. It uses the original indexed wall art, static world objects, inert direction-aware enemies with player collision, animated weapons, HUD artwork, VGA palette, and walking/running movement rates. Ammo clips and treasure can be collected, with the original ammo limit and treasure scores shown on the HUD. The complete 320×200 native image is presented at the original 4:3 display aspect ratio, accounting for Mode 13h's non-square pixels. Hold Alt with Left/Right to strafe, Shift to run, Command to attack on macOS, use 1–4 to select any weapon, and press Space to operate ordinary sliding doors. Open doors close automatically after the original delay and wait or reopen when obstructed. Ammo is renewed after reaching zero while combat remains a target-free development sandbox. Enemy AI, damage, bosses, locked doors, pushwalls, and wider gameplay are not implemented yet.

In Debug builds, press `I` during play to log a diagnostic snapshot containing the camera, weapon, doors, actors, resolved directional sprites, projection values, and nearby decorations. The key hook and diagnostic path are omitted from Release builds.

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

The original VGA palette is available in id Software's released source tree. Clone that alongside the game data at:

```shell
git clone https://github.com/id-Software/wolf3d.git local/reference/wolf3d-source
```

The `local/` directory is ignored by Git. When present, the build copies the `.WL6` files and `WOLFSRC/OBJ/GAMEPAL.OBJ` into the application's `GameData` output directory. Without the required resources, Wolfenshine still builds and starts, but displays a message listing the missing files and expected location.

## Original source reference

The original Wolfenstein 3D source release is available from the official [id Software Wolf3D repository](https://github.com/id-Software/wolf3d). Wolfenshine links to that repository as a format and behavioral reference; the original C source is not included here.

## Beyond the basics

Once the original game data, rendering, and core behavior are working, possible enhanced-mode experiments include:

- View bob and smoother camera motion.
- Persistent enemy blood splats.
- Improved dynamic and colored lighting.
- Ambient occlusion for added depth around walls and objects.
- Dynamic shadows.
- Spatial 3D sound.

These are ideas rather than compatibility requirements. A faithful rendering path should remain available alongside later visual and audio enhancements.

## Developer notes

### Wolfenstein 3D data files

The `.WL6` suffix identifies data for the full six-episode edition. The shareware episode uses `.WL1`; other releases use related suffixes and may arrange individual resources differently. Multi-byte values in the original data are little-endian.

| File | Purpose |
|---|---|
| `AUDIOHED.WL6` | An offset table locating audio chunks within `AUDIOT.WL6`. |
| `AUDIOT.WL6` | PC-speaker effects, AdLib effects, and music data. Digitized sound samples are stored in `VSWAP.WL6`. |
| `MAPHEAD.WL6` | Contains the RLEW compression tag and a table reserving 100 level-header offsets within `GAMEMAPS.WL6`. Wolfenstein 3D uses the first 60 slots; unused slots within that range contain `0xFFFFFFFF`. |
| `GAMEMAPS.WL6` | Contains the level headers and compressed map planes describing walls, objects, actors, and areas. Map planes use Carmack compression followed by RLEW compression. |
| `VGADICT.WL6` | The Huffman dictionary used to decompress chunks from `VGAGRAPH.WL6`. |
| `VGAHEAD.WL6` | A table of 24-bit offsets locating graphics chunks within `VGAGRAPH.WL6`. |
| `VGAGRAPH.WL6` | Huffman-compressed UI artwork, fonts, tiles, and other screen graphics. Chunk identifiers vary between game versions. |
| `VSWAP.WL6` | A page-oriented container holding wall textures, sprites, and digitized sound samples. |
| `GAMEPAL.OBJ` | The original source release's 16-bit OMF object containing the 256-color VGA palette. This is copied from the ignored local source checkout rather than committed to Wolfenshine. |

`CONFIG.WL6` is generated configuration state rather than a required asset. `WOLF3D.EXE` is useful as a behavioral reference but is not loaded by Wolfenshine.

The current private development data is the full six-episode June 1992 v1.1 release. The released source tree defaults to the later `GOODTIMES` v1.4 configuration, so resource readers must avoid assuming that version-specific chunk identifiers are universal.

#### Map container layout

`MAPHEAD.WL6` begins with a 16-bit RLEW tag followed by 100 little-endian 32-bit offsets. Wolfenstein 3D reads the first 60 map slots; `0xFFFFFFFF` marks a sparse slot within that range.

Each offset locates a 38-byte map header in `GAMEMAPS.WL6`:

| Field | Size | Notes |
|---|---:|---|
| Plane offsets | 3 × 4 bytes | Absolute offsets within `GAMEMAPS.WL6`. |
| Plane lengths | 3 × 2 bytes | Compressed byte lengths. |
| Width and height | 2 × 2 bytes | The original Wolfenstein 3D maps are 64 × 64 tiles. |
| Name | 16 bytes | Null-terminated DOS ASCII. |

The game loads plane 0 (walls and areas) and plane 1 (actors, objects, and level information). Each plane starts with its Carmack-expanded byte length. Carmack expansion produces an RLEW stream whose first word is its final expanded byte length; expanding that stream produces `width × height` 16-bit tile values in row-major order.

#### Wall textures and palette

`VSWAP.WL6` begins with three 16-bit values: the total page count, the first sprite page, and the first digitized-sound page. These are followed by one 32-bit offset and one 16-bit length for every page. Pages before the sprite boundary are 64 × 64 wall textures stored as palette indices in column-major order.

Each ordinary wall tile selects a pair of pages: one for east/west-facing walls and one for north/south-facing walls. Door textures occupy the final eight pages of the wall region and similarly use orientation-specific pairs. Wall faces immediately inside a doorway use the dedicated `DOORWALL + 2/+3` jamb textures from that region.

Wolfenshine retains the textures as 8-bit palette indices. `GAMEPAL.OBJ` contains 256 RGB triplets using the VGA DAC's 0–63 channel range; these are expanded to 8-bit RGB values when the palette loads. The software renderer resolves each texture index through that palette while writing its reusable RGBA framebuffer. Keeping indexed textures as the canonical representation preserves the original data and leaves palette swaps or a future GPU palette lookup straightforward.

The E1M1 ceiling uses palette index `0x1D`; the original VGA clear routine uses `0x19` for the floor. Wolfenshine resolves both through the same loaded palette rather than approximating their RGB colors.

`VGADICT.WL6` contains the Huffman tree used to expand `VGAGRAPH.WL6`, while `VGAHEAD.WL6` supplies its 24-bit chunk offsets. Picture chunk zero expands to a width/height table. Wolfenshine identifies the 320×40 status bar from those dimensions instead of relying on a generated chunk number: it is chunk 95 in the current v1.1 data but chunk 86 in the later GOODTIMES source configuration. Pictures are stored in four VGA planes and converted to row-major palette indices after expansion.

Sprite pages use a column/post format. Each column lists its opaque vertical runs and their palette-index data, so transparency is structural rather than represented by a reserved color. The final 20 sprite pages before the sound boundary contain the four weapons' five animation frames; this allows weapon frames to be located without hard-coding version-specific absolute sprite numbers.

## License

Licensed under the MIT License. See [LICENSE](LICENSE) for details.
