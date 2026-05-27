# Generated Tilemap + UI Kit

## Where files are
- Tile sprites: `Assets/ArtGenerated/Tilemap`
- UI sprites: `Assets/ArtGenerated/UI`
- Tile assets (auto-created): `Assets/ArtGenerated/Tiles`
- Installer script: `Assets/Editor/FarmVisualKitInstaller.cs`

## How to build in Unity
1. Open Unity and wait for scripts to compile.
2. In top menu run `Tools > FarmSim > Generated Art > Setup Import Settings`.
3. Run `Tools > FarmSim > Generated Art > Build Demo Tilemap`.
4. Run `Tools > FarmSim > Generated Art > Build Demo UI`.

## What you get
- `GeneratedGrid` with 3 layers (`Ground`, `Decor`, `Water`) and painted farm-style map.
- `GeneratedUICanvas` with shop window, top panel, money block, and two styled buttons.

## Notes
- Existing scene objects are not deleted.
- If `GeneratedUICanvas` already exists, its children are rebuilt to apply new style cleanly.
- You can safely rename created objects after generation.
