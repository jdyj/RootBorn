# School Pixels v4 Implementation Plan

**Goal:** Extend the user-approved v3 art with eight matching classroom objects, preserve all approved PNGs, and provide a concrete path to real interaction testing.

**Authorization:** The user approved the existing appearance and explicitly requested more pixel art in that style, plus investigation of testing options. Continue within that approved visual design. No new approval gate or style redesign is needed.

**Architecture:** Use the unchanged v3 native-pixel module as a baseline; add a separate v4 drawing module. Produce all images on FEVM CPU, then inspect downloaded PNGs. Keep the existing project scenes unchanged. Package both original and new art, pivots, floor footprints, sockets, a composed classroom, 48px tilemap exports and an offline review HTML.

**Tech Stack:** Existing FEVM Docker image, Pillow, integer drawing coordinates, exact v3 palette; Python acceptance checks; Node syntax/data checks.

- [x] Confirm live FEVM SSH and inspect approved source, repository placement constraints and current UI connection state.
- [x] Add output checks for baseline pixel preservation, new object count, palette/alpha, pivots, parented books and tilemap variants; see expected failure before implementing.
- [x] Add teacher desk, lectern, lockers, noticeboard, upright books, clock, potted plant and waste basket using the approved outline and material ramps.
- [x] Compose and inspect the expanded classroom and contact sheet. Inspect the pivot/support overlay as well.
- [x] Preserve native pivots and export transparent-padding variants for the actual 48 PPU importer.
- [x] Document browser and Unity testing prerequisites from actual local configuration and official documentation. Distinguish offline validation from real UI/PlayMode tests.
- [x] Run acceptance, deterministic regeneration, embedded-image/syntax and downloaded hash checks; provide final ZIP and review images.

**Test access findings:** Browser inventory currently empty. Unity process not running. Project includes Unity MCP 0.81.0 and `.mcp.json` entry `ai-game-developer` at `http://localhost:22394/`, but this thread exposes no Unity MCP tools. Do not claim real UI or PlayMode execution. Prior preview-server denial is not addressed by weakening permissions or exposing a network server.

**Further diagnosis:** `UserSettings/AI-Game-Developer-Config.json` also specifies port 22394, whereas the user's active Codex configuration specifies port 29101. Both local ports refused TCP connections. Added an inactive project-scoped configuration example and a precise seven-scenario browser test procedure plus Unity PlayMode/save/reopen steps. No active MCP configuration or Unity scene was changed.

**Result:** 20 native sprites (8 new), 4 retained tiles, 11 Tilemap-compatible padded variants. Seven automated acceptance checks pass; all 16 approved PNGs are byte-identical. All 45 regenerated artifacts are deterministic. Offline HTML syntax and 21 embedded image bytes pass validation. All 62 downloaded manifest files match. ZIP: 63 files / 40 PNGs; SHA256 `e4e18ed4d3be183c867c7cd9c5a208ad1521b56a3c2d445c7157d7120f843fe0`.
