# freeserf3.net

A re-implementation of **The Settlers III** (Siedler 3) in C# using [Silk.NET](https://github.com/dotnet/Silk.NET),
targeting **Windows** and **Android**. Inspired by [freeserf.net](https://github.com/Pyrdacor/freeserf.net)
(Settlers 1) and the reference projects [settlers-remake](https://github.com/paulwedeck/settlers-remake)
(Java) and [X-Settlers](https://github.com/Skarmux/X-Settlers) (C++).

## References

The following reference projects are used for porting game logic, data formats and rendering:

| Project | Role | Local path |
| --- | --- | --- |
| **settlers-remake (new)** | **Primary reference** – most developed branch; use for all ports | `C:\Users\ostermann_d\Downloads\settler3\settlers-remake-master_new` |
| settlers-remake (old) | Secondary reference (older branch) | `C:\Users\ostermann_d\Downloads\settler3\settlers-remake` |
| X-Settlers | C++ reference (original game rendering) | `C:\Users\ostermann_d\Downloads\settler3\x-settlers` |
| freeserf.net | C# architecture reference (Settlers 1) | `C:\git\copilot-worktrees\freeserf.net` |

> **Note:** `settlers-remake-master_new` is significantly more developed than the older
> `settlers-remake` branch and is the **primary reference** for all future porting work.

## License

GPL v3. The Ubisoft Connect download code is ported from freeserf.net (GPL v3).
The data format readers are ported from settlers-remake (MIT).

## Game data

The original Settlers 3 game data is **copyrighted** and is **not** bundled with this project.
You must own the game (e.g. "The Settlers 3 - History Edition" on Ubisoft Connect).

The game data can be obtained in two ways:

1. **Local folder** – point the app to your Settlers 3 installation (e.g. `D:\thesettlers3`).
2. **Ubisoft Connect download** – log in with your Ubisoft account (Windows and Android);
   the app downloads the required data files (GFX, SND, Map, Theme) for product ID `11784`.

## Project structure

| Project | Description |
| --- | --- |
| `Freeserf3.Core` | Game logic, data format loaders, render/audio/UI interfaces |
| `Freeserf3.Renderer` | Silk.NET OpenGL / OpenGL ES implementation |
| `Freeserf3.Ubisoft` | Ubisoft Connect login + game data download |
| `Freeserf3` | Desktop host (Windows, GLFW) |
| `Freeserf3.Android` | Android host (SDL, OpenGL ES 3.0) |
| `Freeserf3.Test` | Unit tests |
| `Silk.NET.Window` | Cross-platform window abstraction (from freeserf.net) |

## Milestones

- **M1** – Map viewer MVP: data loaders (GFX/MAP/SND), isometric terrain rendering on Windows & Android
- **M1.5** – Ubisoft Connect download (Windows & Android)
- **M2** – Game logic core: map data model & geometry, deterministic simulation (game/player/inventory), buildings & production chains, movables & transport (pathfinding, flags, roads), rendering integration (buildings/settlers/roads/flags, minimap), desktop interaction (select, build, road, flag) — **done**
- **M3** – AI & extended logic (army/construction/economy, trading, missions)
- **M4** – Full game (UI/menus, audio, multiplayer)

## Build

Requires the .NET 10 SDK.

```sh
dotnet build Freeserf3/Freeserf3.csproj -c Release
```

Android (requires the `android` workload):

```sh
dotnet build Freeserf3.Android/Freeserf3.Android.csproj -c Release
```
