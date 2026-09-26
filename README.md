# RA2 RPG Unity

A browser-targeted 2D isometric Action RPG prototype inspired by classic RTS presentation.

## Goals

- 2D isometric world
- one directly controlled hero
- click-to-move to the exact clicked world position
- camera locked to the hero
- visible ranged projectiles
- data-driven characters, buildings, loot, quests and maps
- WebGL build served from Docker
- local import pipeline for legally owned Red Alert 2 asset files

## RA2 assets

Original game assets are **not stored in this repository**.

Put your own legally obtained game files in:

```
LocalRA2/
```

That folder is ignored by Git. Generated/imported proprietary assets should also stay local unless you have the rights to redistribute them.

## First run

1. Clone this repository.
2. Open the repository folder directly from Unity Hub.
3. In Unity, use **RA2 RPG > Create Demo Scene**.
4. Press Play.
5. Left- or right-click on the ground: the hero moves to the exact clicked point.
6. The camera follows the hero.

## Planned RA2 importer

The importer layer is intentionally separated from gameplay. It will progressively support:

- MIX archive discovery/extraction
- PAL palettes
- SHP sprites/animations
- TMP isometric terrain
- VXL/HVA vehicles
- INI metadata/rules

## WebGL / Docker

A Docker setup for serving a Unity WebGL build will live under `docker/`.
