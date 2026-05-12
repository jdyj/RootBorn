# Town Tilemap Placement Guide

Date: 2026-05-08

## Scene

Open `Assets/Scenes/Town.unity`.

The playable baseline uses these root objects:

- `TownGrid`
- `TownGrid/TownGroundTilemap`
- `TownGrid/TownDecorationTilemap`
- `TownGrid/TownCollisionTilemap`
- `TownSpawnPoint`
- `TownStreet`
- `TownApartment`
- `TownShop`
- `TownCommunityBoard`

## Tile Assets

Use `Assets/Data/Tiles/GroundTile.asset` for the current baseline floor and path tiles.

Modern town art references are available under:

- `Assets/Modern_Farm_v1.2/`
- `Assets/moderninteriors-win/`
- `Assets/modernuserinterface-win/`
- `Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/`

When adding new gameplay data, keep entity definitions in ScriptableObject assets and register them through `Assets/Data/Registry/GameDataRegistry.asset`.

## Layering

Use this tilemap split:

- `TownGroundTilemap`: base floor, sidewalks, street pavement.
- `TownDecorationTilemap`: non-blocking props, signs, rugs, counters, shop details.
- `TownCollisionTilemap`: blocking guide tiles or collision markers.

Keep the Player and UI outside `TownGrid`.

## Tile Palette Workflow

1. Open `Assets/Scenes/Town.unity`.
2. Open Unity's Tile Palette window.
3. Create or select a Town palette.
4. Add `Assets/Data/Tiles/GroundTile.asset` and any sliced town sprites from the modern or Pixelwood asset folders.
5. Paint base walkable space on `TownGroundTilemap`.
6. Paint shop, apartment, and community detail tiles on `TownDecorationTilemap`.
7. Paint blocking markers on `TownCollisionTilemap` only where the player should not walk.
8. Press Play and run the Town PlayMode smoke tests after changing the layout.

## Verification

After placement changes, run:

```powershell
# Unity Test Runner MCP
tests-run testMode=PlayMode testNamespace=Rootborn.Tests.PlayMode.TownConcept
tests-run testMode=EditMode testNamespace=Rootborn.Tests.EditMode.TownConcept
```

The Town baseline is expected to keep:

- a visible floor or town visual root;
- one `Player` with movement components;
- one `Main Camera` with `CameraFollow`;
- one Canvas with Modern UI panel routing;
- `TownGrid` with layered tilemaps;
- at least two town-life visual cues.
