[![Twitter URL](https://img.shields.io/twitter/url/https/twitter.com/deanthecoder.svg?style=social&label=Follow%20%40deanthecoder)](https://twitter.com/deanthecoder) [![GitHub Repo stars](https://img.shields.io/github/stars/deanthecoder/Wolfenshine?style=social&label=Star)](https://github.com/deanthecoder/Wolfenshine/stargazers)

# Wolfenshine

**Wolfenstein 3D rebuilt in modern C#—faithful when you want it, shiny when you don't.**

Wolfenshine is a playable, cross-platform reimplementation of the 1992 classic. It reads the maps, artwork, music, and sounds from your own copy of Wolfenstein 3D, then brings them to life through a clean .NET engine written from scratch.

At its heart is an authentic 320×200 software-rendered experience. Press F2 and that same game becomes something new: colored lights spill across rooms, muzzle flashes illuminate enemies, fog gathers in the distance, and bright doorways cast descending shafts into the darkness.

![Wolfenshine's authentic software-rendered mode](img/gameplay.png)

## Then press F2

The enhanced renderer builds on that classic foundation without replacing its pixel art. Rooms gain their own atmosphere: lights cast color, treasure glows, enemies move through illumination, and damage changes the character of the whole screen.

<table>
  <tr>
    <td><img src="img/enhanced-green-lighting.png" alt="Green ceiling lights illuminating a blue-stone corridor"></td>
    <td><img src="img/enhanced-enemy-lighting.png" alt="A guard illuminated beside a bright doorway"></td>
  </tr>
  <tr>
    <td><img src="img/enhanced-treasure-lighting.png" alt="Treasure casting warm light around a hidden room"></td>
    <td><img src="img/enhanced-damage-feedback.png" alt="Directional blood and damage feedback during combat"></td>
  </tr>
  <tr>
    <td><img src="img/enhanced-pickup-spotlight.png" alt="A machine-gun pickup beneath a focused overhead spotlight"></td>
    <td><img src="img/enhanced-water-caustics.png" alt="A filled well projecting animated cyan caustics onto the ceiling"></td>
  </tr>
  <tr>
    <td><img src="img/enhanced-exit-room-god-rays.png" alt="An illuminated exit room casting god rays into a dark corridor"></td>
    <td><img src="img/enhanced-low-health.png" alt="Low-health damage effects beneath enhanced chandelier lighting"></td>
  </tr>
</table>

## Classic underneath. Modern light on top.

Wolfenshine keeps the original-style renderer and enhanced renderer side by side. Switch between them instantly during play; the world, AI, input, and game state never change underneath you.

![A 50/50 comparison of Wolfenshine's enhanced and authentic renderers](img/renderer-comparison.png)

### Authentic mode

- Original 320×200 composition presented at the correct 4:3 aspect ratio.
- Original artwork, wall textures, sprites, and screen composition.
- Familiar movement, weapon animation, HUD, menus, pause plaque, and intermission screen.
- A reusable C# software framebuffer with the classic raycast presentation intact.

### Enhanced mode

- True 16:10 gameplay expands the horizontal view without stretching the original artwork; wider monitors use black side bars.
- Colored, directional ceiling lights, chandeliers, lamps, treasure glow, and muzzle flashes.
- Atmospheric doorway shafts that descend from bright rooms into darker ones, illuminate objects, and fade naturally with the viewing direction.
- Focused overhead spotlights draw attention to uncollected weapons, keys, and full-heal pickups.
- Filled wells and puddles project animated ceiling caustics, using an algorithm adapted from a [Shadertoy shader](https://www.shadertoy.com/view/XtKfRG).
- Geometry-aware room ambience, doorway light spill, distance shading, and subtle fog.
- Generated wall relief with material-specific specular response.
- Bloom, ambient occlusion, enemy shadows, view bob, weapon sway, and momentum-based movement.
- Dynamic enemy illumination plus directional damage tint and accumulating blood at the screen edges.

The original pixel art remains at the center of the presentation. Enhanced effects respond to it rather than replacing it with a different asset pack.

![A guard moving through ceiling light and being illuminated by muzzle flashes](img/enemy-lighting.webp)

## A path through the castle

Hold `Tab` in enhanced mode and Wolfenshine projects a flowing trail directly onto the floor. Three luminous chevrons cross each tile, bend smoothly around corners, and fade away when the key is released.

![The enhanced navigation guide flowing across the floor toward the next objective](img/enhanced-navigation-guide.png)

The guide follows gameplay rules rather than drawing a straight line through the map. It first leads to the nearest obtainable gold or silver key, allowing ordinary doors while respecting locks. Once no reachable key remains, it continues to the elevator. Unopened secret walls remain secret; moving one can reveal a newly valid route.

Because the trail is part of the rendered world, walls and doors hide what lies beyond them naturally. Each press captures a stable route for the complete glow and fade, while the next press reflects newly collected keys, opened passages, and shifted secret walls.

## More than a rendering demo

Wolfenshine includes the game systems that make the renderer worth exploring:

- All 60 maps from the six-episode edition load directly from the original data.
- Guards, officers, SS soldiers, mutants, and dogs see, hear, chase, attack, take damage, animate, and die.
- Four weapons, ammunition, health, treasure, keys, scoring, pickups, and enemy drops.
- Sliding and locked doors, secret pushwalls, elevators, death, respawning, level progression, and end-of-level statistics.
- Difficulty-dependent enemy placement, health, behavior, and incoming damage.
- Spatial digitized and AdLib sound through OpenAL, plus original IMF music rendered through OPL emulation.
- Original title, episode and difficulty selection, HUD, animated face, pause display, and intermission presentation.
- An autonomous attract mode starts after 30 seconds at episode selection and plays through the real input system.

Use `W`/`S` to move, `A`/`D` to turn, and `Q`/`E` to strafe. The original-style arrow controls and Alt-strafing remain available too.

![Difficulty selection](img/difficulty-selection.png)

## Play Wolfenshine

Download the installer for your platform from [GitHub Releases](https://github.com/deanthecoder/Wolfenshine/releases):

- Windows: `Wolfenshine-<version>-win-x64.exe`
- Apple Silicon Mac: `Wolfenshine-<version>-osx-arm64.dmg`
- Intel Mac: `Wolfenshine-<version>-osx-x64.dmg`

Wolfenshine needs either the free Wolfenstein 3D shareware data or a legitimate copy of the full six-episode game. It does not distribute id Software's copyrighted game assets. Start the installed app and its setup screen will guide you through adding the free shareware episode.

### Build from source

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), then clone the repository and its submodules:

```shell
git clone --recurse-submodules https://github.com/deanthecoder/Wolfenshine.git
cd Wolfenshine
```

### Quick setup with the free shareware episode

1. Run Wolfenshine without game data. Its setup screen will appear automatically.
2. Click **Download Free Shareware**.
3. When `w3d-box.zip` finishes downloading, drag the unopened ZIP onto the Wolfenshine window.
4. Wolfenshine installs the shareware data and starts immediately.

The shareware edition contains the complete first episode: eight regular levels, its boss level, and its secret level.

When running from a source checkout, you can instead place the unopened ZIP in the repository's Git-ignored data folder before building:

```text
local/game-data/
```

You can also [download `w3d-box.zip` from DOS Games Archive](https://www.dosgamesarchive.com/file/wolfenstein-3d/w3d-box) yourself. Keep its original filename and do not extract it. From the repository root, run:

```bash
dotnet run --project Wolfenshine/Wolfenshine.csproj
```

The build finds the archive automatically. Private game data is never committed to this repository.

### Use the full six-episode game

To play all six episodes, copy these eight files from a legitimate full-game installation into Wolfenshine's per-user `GameData` folder:

```text
AUDIOHED.WL6
AUDIOT.WL6
GAMEMAPS.WL6
MAPHEAD.WL6
VGADICT.WL6
VGAGRAPH.WL6
VGAHEAD.WL6
VSWAP.WL6
```

| Platform | Per-user data folder |
|---|---|
| Windows | `%APPDATA%\Wolfenshine\GameData` |
| macOS | `~/Library/Application Support/Wolfenshine/GameData` |
| Linux | `~/.config/Wolfenshine/GameData` |

Restart Wolfenshine after copying the files. When running from a source checkout, you may instead place them in `local/game-data/` before building. When both complete editions are available, Wolfenshine prefers the full `.WL6` data set.

Wolfenshine supports the original `.WL1` shareware and `.WL6` full-game data sets.

### Run it

```shell
dotnet run --project Wolfenshine/Wolfenshine.csproj
```

To clear saved preferences and remove game data installed through Wolfenshine, start it with `--reset`:

```shell
dotnet run --project Wolfenshine/Wolfenshine.csproj -- --reset
```

Packaged builds use `Wolfenshine --reset`. The option deliberately removes the current user's Wolfenshine settings and installed `GameData`; a packaged app then opens its normal setup screen. It does not touch repository-local development files or the development-build fallback.

![Starting an episode](img/episode-start.png)

### Controls

| Action | Key |
|---|---|
| Move and turn | Arrow keys |
| Run | Shift |
| Strafe | Alt + Left/Right |
| Use doors, switches, and secret walls | Space |
| Fire | Control / Command |
| Select an owned weapon | 1–4 |
| Pause or resume | P |
| Toggle authentic/enhanced rendering | F2 |
| Toggle FPS counter | F3 |
| Toggle fullscreen | Alt/Option+Enter; F11 on Windows/Linux |
| End current game | Escape, followed by Y to confirm or N/Escape to return |
| Show navigation guide | Hold Tab |

## Why build Wolfenstein 3D again?

Because the original game is an unusually good laboratory.

Its compact grid world is understandable enough to rebuild, yet rich enough to explore raycasting, binary formats, sprite projection, game AI, spatial audio, OPL emulation, software rendering, and modern shader effects in one coherent project. C# makes those systems approachable without turning Wolfenshine into a mechanical line-for-line translation of 1990s C.

The result is both a game and a guided tour through how one of PC gaming's foundations works.

## Developer notes

### Relationship to the original source

id Software's [original Wolfenstein 3D source release](https://github.com/id-Software/wolf3d) is an invaluable historical and behavioral reference. Wolfenshine does not contain, compile, or require that C source. Its implementation is independently written in modern C#; we consult the released source to understand original gameplay rules and verify that Wolfenshine behaves similarly.

### Debug shortcuts

Debug builds provide a few development conveniences that are omitted from Release builds:

| Key | Action |
|---|---|
| `I` | Log the player's position and facing angle, weapon, doors, actors, sprite resolution, projection values, and nearby decorations. |
| `C` | On macOS, save a timestamped screenshot of the current window to `~/Downloads`. |
| `Shift+C` | Capture authentic and enhanced frames and rebuild `img/renderer-comparison.png` with a diagonal split. |
| `R` | Restore health and ammunition and unlock all weapons. |
| `M` | Toggle the textured map overview. |
| `F5` | Save the current level, position, and direction as a quick test location. |
| `L` | Restart at the saved test location. |
| `<` / `>` | Restart at the previous or next level, wrapping at either end of the available maps. |
| `F4` | Start attract mode on the episode screen, or toggle autopilot during a normal game. |

The Shift+C comparison capture keeps its clean source images in the Git-ignored `local/screenshots/` directory and overwrites the tracked README comparison image only after both renderer captures succeed.

## License

Licensed under the MIT License. See [LICENSE](LICENSE) for details.

### Third-party libraries

Wolfenshine uses [NukedOPL3Sharp](https://github.com/codengine/NukedOPL3Sharp) by Stefan Hueg, a C# port of nukeykt's Nuked-OPL3 emulator, to render the original AdLib sound effects and IMF music. NukedOPL3Sharp is distributed under the [GNU Lesser General Public License 2.1](https://github.com/codengine/NukedOPL3Sharp/blob/master/LICENSE); that license applies to the library rather than Wolfenshine's MIT-licensed code.
