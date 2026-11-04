# Alley Exploration Crate Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans for the later approved integration. This handoff stages one generation only; the parent launches it after review. No generation, SSH, Unity operation, or asset import has been performed by this preparation task.

**Goal:** Give the existing alley investigation point a recognizable small crate instead of the shared open-school-book sprite, using one new FEVM ComfyUI image and the approved urban oak/navy/ivory/brass pixel treatment.

**Architecture:** Replace only the saved SpriteRenderer sprite at the existing point after generation and review. Retain the interaction SO, choices, progression, transform, depth sorting, and trigger. The Modern crate is a geometry reference only; it must not become the delivered artwork directly.

**Tech Stack:** FEVM ComfyUI Qwen Image Edit 2511; Pillow/NumPy technical matte and pixel export; Unity 2D sprite/Addressables integration by the parent.

## Evidence and exact binding

- Design: `docs/superpowers/specs/2026-05-22-town-modern-interactable-sprites-design.md:143` maps `TownExplorationInteractionPoint` to a crate/object. `docs/superpowers/goals/2026-05-14-exploration-interaction-choice-goal.md:44` describes an old alley crate with careful-open, force-open, record, and leave choices.
- Saved scene: `Assets/Scenes/Town.unity`, path `AuthoredTownBindings/TownExplorationInteractionPoint`, active root GameObject `519262771`. **There is no Visual child**; SpriteRenderer `519262774` is on the root.
- Transform `519262772`: local/world position `(22,2,0)`, identity rotation, scale `(1,1,1)`, no children. Parent transform `257248087` is at the origin with scale one.
- SortingGroup `519262776`: default layer, order `9904`. Renderer: default layer, order `0`, Simple draw mode. GroundDepthSort `519262775`: foot offset `(0,0)`, units `48`, base `10000`. Keep all these fields unchanged.
- BoxCollider2D `519262777`: enabled trigger, saved size `(0.7,0.7)`, offset `(0,0)`. The existing runtime installer uses `max(0.65, interaction.InteractionRadius)` and therefore currently resolves `(1,1)` at runtime. This task must not alter either behavior or authored data.
- Interactor `519262773`: `Rootborn.UI.StudentLife.ExplorationInteractionPointInteractor`, script GUID `f3baf9392ae5db441a166e0654ad5e8a`.
- Bound SO: `Assets/Data/StudentLife/ExplorationInteractions/Exploration_TestAlleyCrate.asset`, GUID `cfd09340758f7bf4080cdd77b51faa94`, fileID `11400000`, ID `exploration.test.alley-crate`, position `(22,2)`, radius `1`, nonrepeatable, four existing choices. It is the first registered exploration interaction at `GameDataRegistry.asset:444`.
- Actual consumer: `ExplorationInteractionPointInteractor.TryInteract` opens `ExplorationChoicePanel`; `Choose` runs existing outcomes and saves exploration/student/clue/encyclopedia progress. This is an existing playable interaction, not proposed challenge artwork. Its current English test display copy is a separate known presentation limitation; do not change catalog555 in this art slice.

## Original geometry and proposed export

Current sprite: `Assets/Art/SchoolPixels/sprites/book_open.png`, GUID `2f47b5b9577c3e34daf0662dcdb20fb1`, fileID `21300000`; canvas `24x14`, `48 PPU`, pivot `(12,7)` / normalized `(0.5,0.5)`, opaque bbox top-left `(0,2)-(24,14)`. PNG SHA256: `ff743510159ece8abd98709f111bdf2b80cc1425f6ceba092ed6b5d37a437dfa`.

The existing opaque floor is `2 - 7/48 = 1.854166667`. Recommend **48x48 at48PPU**, pivot **(24,11)**, native bottom transparent margin **4px**, maximum visible silhouette **40x36**, binary alpha, exact2px clusters, <=31 visible colors. The new floor is `2 + (4-11)/48 = 1.854166667`, preserving the original baseline without moving the root. The wider, taller crate is intentional semantic artwork; keep it within one world unit. A96px/88px-visible counter-sized prop would be unnecessarily large here. Do not copy the usual pivot4 from the cashier/bench: that would move this floor toY2.

Proposed new PNG: `Assets/Art/UrbanStreet/Buildings31/alley_exploration_crate.png`; proposed new address: `sprites/town/comfy31/alley-exploration-crate`. Both are new and not yet created/registered. Only Renderer `519262774.m_Sprite` should change in Town. Preserve the global open-book PNG/meta and its other scene uses. Theme26, catalog555, Care111, all existing gameplay definitions, and character artwork remain outside scope.

## Generation staging

- Reference only: `Assets/Modern_Exteriors_32x32/Modern_Exteriors_Complete_Singles_32x32/ME_Singles_Camping_32x32_Pier_Crates_2.png`,32x32, bbox `(0,4)-(32,32)`, source SHA256 `51a242ef549535fcbcac3ffdba1314757fbbbe8c0020e87300a518470adc6b77`. Its original100PPU metadata is not a delivery contract.
- Generate one isolated connected closed crate: broad warm oak planks, dark navy corner bands, restrained ivory edge highlights, one small brass latch, thin top face, straight horizontal front edges. No contents, characters, text, labels, logos, treasure effects, room, floor or cast shadow. Magenta is only a removable technical backdrop.
- Reuse Comfy214's Qwen2511 graph shape:768x768,20steps,CFG4,Euler/simple,EmptySD3 latent,denoise1.0. New seed `3011019215`; job `town-alley-exploration-crate-v1`; client `farmer-alley-exploration-crate-20261101-v1`. The actual queue number must come from the server response; do not assert215 before submission.
- Prepared files under `output/design-comfy-20261031`: `submit_alley_exploration_crate.py`, `town-alley-exploration-crate-v1-workflow.json`, `fetch_alley_exploration_crate.py`, and `alley-exploration-crate-generation-scope.json`.

## Parent execution and review

- [ ] After reviewing this scope, run `python output/design-comfy-20261031/submit_alley_exploration_crate.py`. It first verifies `whoami; hostname` with the documented strict SSH key options, then prepares only the reference crop/nearest enlargement/magenta padding and submits this one named job. It never cancels queues or changes services/models.
- [ ] Run `python output/design-comfy-20261031/fetch_alley_exploration_crate.py` to fetch only its recorded prompt. Inspect the unmodified raw; reject multiple crates, side rotation, missing closure, clutter or soft unreadable geometry.
- [ ] Derive a dedicated48x48 technical export from `export_store_cashier_counter.py` processing: automatic magenta matte, largest connected source silhouette, BOX color/coverage into a24x24 canvas, fit<=20x18, bottom2 small pixels, <=31colors/no dither, binary alpha, then2x nearest. Center atx12; native alpha must end atY44. No hand-painted or selectively erased motifs.
- [ ] Review at48px and integer zoom. Record actual generation number, prompt ID, source/workflow/export hashes, bbox, colors and floor. Keep unimported until accepted.
- [ ] Parent integration only: protect the current scene and sources, red-check the exact old renderer binding, import the accepted PNG through Unity tools, register the new address, and change only the one SpriteRenderer reference. The initial saved-art patch needs no C# changes; the actual presentation blocker and its separate repair are recorded below.
- [ ] Verify saved semantic scene diff, source/global-book preservation, bundle pixels, first/cold actual walking/front-behind rendering and normal E/close flow. Preserve real exploration state; use a copied suitable save and do not reset or inject completion merely to prove this art. Confirm runtime sprite, floor and existing collider size separately from the authored0.7 size.


## Actual presentation blocker and repair scope

Packed native E reproduced a hidden panel under
LocationNpcCanvas/LocationIdentityPanel/LocationIdentityRoot: activeSelf true,
activeInHierarchy false, four hidden choices, no gameplay-input lease. Evidence
is retained in evidence/alley-exploration-crate-before-panel-fix/. No choice was
selected; all five DTOs and saved-file hashes remained unchanged.

Repair only ExplorationInteractionPointInteractor and ExplorationChoicePanel:
choose an active same-scene root Overlay, prefer the established shared UI,
reuse only its direct child panel, add normal Close/Escape without choosing Leave,
and acquire/release only its own input lease. Escape is consumed in LateUpdate
so the Settings shortcut router sees the lease during Update. Reuse approved
Comfy-themed buttons, the existing close translation and shared Korean font;
use dark panel ink and a transparent root raycast Image. No new catalog/theme
values, domain outcomes or risk rules are included in this presentation repair.

Eight isolated ownership/lifecycle cases first reproduced seven failures. The
saved-art fixture now reads exact serialized YAML instead of activating another
preview scene global light; native play independently checks appearance/depth.
The existing runtime installer assigns renderer order56 and trigger1; saved
renderer order0/trigger0.7 and SortingGroup9904 are preserved. Record these
separate contracts. Independent bundled crate pixels and the actual registry's
exploration/four-choice dependency graph must match authored data.

Acceptance: actual first/cold E, processed Dynamic W with no movement, Escape
without opening Settings, and actual Close pointer; five DTOs and save files
remain unchanged, with no choices or injected progress. The pre-existing Leave
domain mismatch is separate: the design says no change, but current fallback
risk and completion mutate state. Do not substitute Leave for Close.
