# Urban Molding UI Application Plan

> **For agentic workers:** Use superpowers:subagent-driven-development. Root exclusively mutates Unity, imports staged scripts and runs tests; workers stage outside Assets. Keep the open farmer workspace and unrelated changes intact.

**Goal:** Apply the user-approved ComfyUI v5 molding skin to the existing game UI and verify real input and Game View.

**Architecture:** A shared Addressables-loaded theme supplies three sprites from the approved 256px atlas. ModernUiTileImage retains its legacy fallback but renders one sliced child Image when the new theme is available. Root buttons keep their hit target and use the skin graphic for state transitions; decoration remains non-raycasting. Existing data, save slots and menu navigation remain authoritative.

**Tech Stack:** Unity 6000.3, uGUI, Addressables, ScriptableObject, EditMode/PlayMode tests, native UI input.

## 1. Assets and persistent registration

- [x] Add failing `UrbanMoldingAssetTests` verifying atlas, three Sprite rect/border/pivot values, theme refs and local Addressables entries.
- [x] Copy approved `output/ui-design-comfy-v5/skin/urban-molding-atlas.png` to `Assets/Art/UI/UrbanMolding/urban-molding-atlas.png` only; do not import duplicate standalone PNGs.
- [x] Stage/import `Assets/Scripts/UI/Modern/UrbanUiThemeDefinition.cs` and an Editor installer. Define panel/button/slot Sprite properties, theme address `data/ui/urban-molding`, atlas address `sprites/ui/urban-molding/atlas`.
- [x] Editor importer uses Point, no mipmaps, no compression, no read/write, FullRect, 16 PPU, SpriteMultiple; convert top-left rect y by `256-y-height`. Rects: panel=(8,152,96,96), button=(112,224,96,24), slot=(112,160,48,48); borders 18/6/6; pivot .5/.5.
- [x] Save theme to `Assets/Data/UI/UrbanMoldingTheme.asset`; register theme in Data and atlas in Sprites, Local only. Verify saved refs and asset tests.

## 2. Shared runtime rendering

- [x] Stage behavioral tests first: one Image per background; reused across resize/reopen; slot and button choose correct sprite; child never blocks clicks; disabled/destroyed listeners do not leak; fallback when theme absent.
- [x] Add `UrbanUiThemeRuntime`: one DDOL owner loads through `Managers.Resource.LoadAsync<UrbanUiThemeDefinition>` after bootstrap; publishes readiness; rejects late completion after destruction; owner releases address once. No per-frame loads or UI scans.
- [x] Extend `ModernUiTileImage` to subscribe to readiness and render/reuse `[UrbanSkin]` as first sibling with `Image.Type.Sliced`; keep corners at native 2x using pixelsPerUnitMultiplier=.5. Disable old generated tiles before deferred destruction. Rebuild only on explicit change/readiness, not Update.
- [x] Tests and existing Modern UI regression classes must pass. No entity-ID branching.

## 3. Visible surfaces and input

- [x] Inspect Inventory, DialoguePanel, StudentDayResultPanel factories; connect their plain button/slot skins where the common path cannot cover them. Keep text/icons foreground and readable against ivory.
- [x] Prefer already-staged, previously RED-verified day-result polish only where needed to prevent clipping/leaked text. Retest before applying.
- [x] Preserve existing user journeys. Hosting interactive canvas requires GraphicRaycaster; decoration has raycastTarget=false; modal lifecycle releases gameplay input.
- [x] Update AGENTS.md and UI standards with this user-approved explicit theme exception; leave legacy recipe requirement as fallback.

## 4. Verification

- [x] Targeted tests RED then GREEN, relevant regressions, console error inspection.
- [x] Native Boot -> Single -> isolated save load -> inventory open/close; NPC dialogue; house bed -> day result -> close/next day as feasible. Capture Game View and inspect actual sprite/material/child counts. Never substitute internal Show calls for journey verification.
- [x] Record serialized asset references separately from runtime evidence. Check theme-ready reopen does not recreate background objects.
- [x] Report what was verified; do not claim FPS or build performance without profiling/build evidence. No broad commits/reset in the shared checkout.

Staging/evidence: `output/urban-ui-integration/`; Unity helper: `output/full-design-20261027/qa_step.py` (stage path relative to that helper output root).
