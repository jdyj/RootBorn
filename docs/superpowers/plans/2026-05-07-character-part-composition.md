# Character Part Composition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build data-driven 16x16 character part selection, saving, loading, and layered player rendering for ROOTBORN.

**Architecture:** Character parts are ScriptableObject definitions registered in `GameDataRegistry`, while save data stores stable definition ids through a serializable `CharacterAppearance` value object. Runtime rendering uses child `SpriteRenderer` layers driven by selected definitions and shared direction/frame state; UI owns selection flow and preview only.

**Tech Stack:** Unity 6000.3, C# asmdefs under `Rootborn.Game` and `Rootborn.UI`, Unity MCP `script-update-or-create` for all `Assets/**/*.cs`, Addressables-backed `ResourceManager`, Unity EditMode/PlayMode tests.

---

## Current Baseline

- `Assets/Scripts/Game/Player/CharacterCustomization.cs` stores numeric body/hair/outfit variants and a facing enum. This does not satisfy stable part ids, eyes/accessory categories, or SO-driven parts.
- `SaveService` serializes `SaveSlotMetadata.Character` and creates a default `CharacterCustomization` for old saves.
- `SaveSlotSelectPanel` has button-driven numeric selection and a color swatch preview, not layered sprite preview.
- `GameDataRegistry` has arrays for crops/tools/resources/knowledge/etc. and direct sprite references for ground/player.
- `PlayerController` owns a single `_renderer`, flips that renderer, and changes it for tool animations.
- `FarmAutoFiller` currently instantiates or wires a player using `Registry.PlayerSprite`.
- Modern Farm generator files exist under `Assets/Modern_Farm_v1.2/Farmer_Generator_Pieces/Character Pieces/{Accessories,Bodies,Eyes,Hairstyles,Outfits}/16x16/`; at least `Body_1.png` is `896x352` and currently `spriteMode: 1`, so importer slicing must be verified and automated.
- Modern Interiors generator files exist under `Assets/moderninteriors-win/2_Characters/Character_Generator/...`; adult and kids folders must be documented, with kids excluded from first runtime wiring unless explicitly chosen later.

## Task 1: Character Part Inventory Document

**Files:**
- Create: `docs/art/character-part-inventory.md`
- Read-only inspect: `Assets/Modern_Farm_v1.2/Farmer_Generator_Pieces/Character Pieces/**/16x16/*.png`
- Read-only inspect: `Assets/moderninteriors-win/2_Characters/Character_Generator/**/16x16/*.png`

- [ ] **Step 1: Gather folder and PNG dimensions**

Run read-only PowerShell using `Get-ChildItem` plus `System.Drawing.Image.FromFile` for the required generator folders. Record category, sample files, texture dimensions, current `spriteMode`, and whether width/height are divisible by 16.

- [ ] **Step 2: Write inventory markdown**

Document the adult Modern Farm categories selected for runtime: `body`, `eyes`, `hair`, `outfit`, `accessory`. Document Modern Interiors and kids folders as investigated but out of first runtime scope unless their slices are later normalized.

- [ ] **Step 3: Verify inventory references are concrete**

Search the document for vague placeholders and confirm it names actual folders, sample files, dimensions, and the slice policy.

## Task 2: RED Tests For Definition And Appearance Data

**Files:**
- Create: `Assets/Tests/EditMode/Family/CharacterPartDefinitionTests.cs`
- Create: `Assets/Tests/EditMode/Family/CharacterAppearanceTests.cs`

- [ ] **Step 1: Write failing tests using the intended API**

Use Unity MCP `script-update-or-create`, not direct filesystem writes. Tests should assert:
- `CharacterPartDefinition` exposes `Id`, `CategoryId`, `DisplayNameKey`, `SheetAddress`, `SubSpriteName`, `LayerOrder`, `IsDefault`.
- invalid definitions fail validation when id/category/sheet/sub-sprite are empty.
- `CharacterAppearance` stores selected part ids, returns a selected id by category, replaces category selections, and serializes through `JsonUtility` without Unity object references.
- fallback selection fills missing categories from default definitions without mutating the original corrupt saved ids.

- [ ] **Step 2: Run targeted EditMode tests to verify RED**

Run `tests_run` for `Rootborn.Tests.EditMode.Family`. Expected: compile or test failure because the new production types do not exist yet.

## Task 3: GREEN Implementation For Definition And Appearance

**Files:**
- Create: `Assets/Scripts/Game/Family/CharacterPartDefinition.cs`
- Create: `Assets/Scripts/Game/Family/CharacterAppearance.cs`
- Modify: `Assets/Scripts/Game/Common/GameDataRegistry.cs`
- Modify: `Assets/Scripts/Game/Managers/DataManager.cs`

- [ ] **Step 1: Add production scripts through Unity MCP**

Use `script-update-or-create`. Keep definitions generic and data-driven: no part-id `if`, `switch`, enum dispatch, or per-part classes. Category is a serialized string id.

- [ ] **Step 2: Add registry lookup**

Add `CharacterParts` to `GameDataRegistry` and `CharacterPartById` / category lookup helpers to `DataManager`. Duplicate ids should warn and the last registered definition should not silently hide validation failures in tests.

- [ ] **Step 3: Run targeted tests to verify GREEN**

Run `tests_run` for the Family EditMode namespace. Expected: pass.

## Task 4: RED/GREEN Save Format Migration

**Files:**
- Modify: `Assets/Tests/EditMode/Save/SaveSlotServiceTests.cs`
- Modify: `Assets/Scripts/Game/Save/SaveSlotMetadata.cs`
- Modify: `Assets/Scripts/Game/Save/SaveService.cs`
- Modify: `Assets/Scripts/Game/Player/CharacterCustomization.cs` or replace its usage with `CharacterAppearance` while preserving old save migration.

- [ ] **Step 1: Write failing save tests**

Tests should prove that metadata round-trips selected part ids, legacy numeric `CharacterCustomization` saves still load with a default appearance, and missing/corrupt appearance data returns a fallback value without deleting the original metadata file.

- [ ] **Step 2: Run save tests to verify RED**

Expected: failure on missing `Appearance` field or fallback API.

- [ ] **Step 3: Implement minimal save migration**

Add a serializable appearance field. Keep old character data readable long enough for migration. Save stable part ids only, never `UnityEngine.Object` references.

- [ ] **Step 4: Run save tests to verify GREEN**

Expected: save tests pass.

## Task 5: Character Part Asset Setup And SO Wiring

**Files:**
- Modify/Create through Unity Editor safe path: importer settings for selected 16x16 sheets
- Create: `Assets/Data/Family/CharacterParts/*.asset`
- Modify: `Assets/Data/Registry/GameDataRegistry.asset`
- Modify: `Assets/AddressableAssetsData/AssetGroups/Data.asset`
- Modify: `Assets/AddressableAssetsData/AssetGroups/Sprites.asset`
- Create or modify editor setup script via MCP if automation is needed: `Assets/Scripts/Editor/Tools/CharacterPartSetup.cs`
- Test: `Assets/Tests/EditMode/Family/CharacterPartRegistryTests.cs`

- [ ] **Step 1: Write failing registry and sprite reference tests**

Tests should assert at least two definitions per runtime category, registry discoverability, valid sheet address/sub-sprite fields, and actual 16x16 slice rects for referenced sprites.

- [ ] **Step 2: Run tests to verify RED**

Expected: fail because assets/registry are not wired yet.

- [ ] **Step 3: Configure slices and create SO assets**

Use Unity MCP/editor APIs. Prefer selected Modern Farm adult 16x16 sheets. Name sub-sprites deterministically from category/file/row/column.

- [ ] **Step 4: Register Addressables and registry entries**

Definitions go under `Assets/Data/Family/CharacterParts/`. Sheets get stable addresses used by definitions. Registry lists all definitions.

- [ ] **Step 5: Run registry/sprite tests to verify GREEN**

Expected: pass.

## Task 6: Layered Runtime Renderer

**Files:**
- Create: `Assets/Scripts/Game/Family/CharacterPartLayer.cs`
- Create: `Assets/Scripts/Game/Family/CharacterPartComposer.cs`
- Modify: `Assets/Scripts/Game/Player/PlayerController.cs`
- Modify: `Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs`
- Modify: `Assets/Scripts/Editor/Tools/PlayerSetup.cs`
- Modify prefab via Unity MCP/editor safe path: `Assets/Prefabs/Player.prefab`
- Test: `Assets/Tests/EditMode/Family/CharacterPartComposerTests.cs`
- Test: relevant PlayMode farm/player scenario test

- [ ] **Step 1: Write failing renderer tests**

Tests should create a player GameObject with child renderers and assert body/eyes/hair/outfit/accessory layer objects exist, sorting orders are deterministic, `flipX` applies to all layers, and applying an appearance sets sprites by category without using a single complete player sprite.

- [ ] **Step 2: Run tests to verify RED**

Expected: fail because composer does not exist or player still uses one renderer.

- [ ] **Step 3: Implement composer and player integration**

Composer resolves selected definitions through registry/data manager, loads sheet sub-sprites through `ResourceManager`, applies shared direction/frame naming, and falls back per category. `PlayerController` delegates flip and frame state to composer while preserving tool interaction behavior.

- [ ] **Step 4: Update prefab/editor setup**

Player prefab must contain named child renderers such as `Part_Body`, `Part_Eyes`, `Part_Hair`, `Part_Outfit`, `Part_Accessory`.

- [ ] **Step 5: Run renderer tests to verify GREEN**

Expected: pass.

## Task 7: Character Selection UI Flow

**Files:**
- Modify: `Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs`
- Optional create: `Assets/Scripts/UI/MainMenu/CharacterPartSelectionView.cs`
- Test: `Assets/Tests/EditMode/Save/SaveSlotSelectPanelTests.cs`
- Test: PlayMode new-slot flow.

- [ ] **Step 1: Write failing UI tests**

Tests should assert category controls for body/eyes/hair/outfit/accessory, next/previous changes selected definition ids, preview has layered image/renderers instead of a color swatch, and new-slot metadata is not saved until confirm.

- [ ] **Step 2: Run tests to verify RED**

Expected: fail on missing categories and preview structure.

- [ ] **Step 3: Implement UI selection model and preview**

Keep visual polish minimal. Separate selection logic from final 16x16 panel styling. Use localization-key-ready labels or keys, and avoid adding new runtime `Resources.Load` dependencies.

- [ ] **Step 4: Run UI tests to verify GREEN**

Expected: pass.

## Task 8: End-To-End Verification

**Files/Commands:**
- `Assets/Tests/EditMode/**`
- `Assets/Tests/PlayMode/**`
- `Scripts/ci/check-no-entity-id-branching.sh`
- Unity screenshot or isolated render of `Player`

- [ ] **Step 1: Run targeted EditMode tests**

Run Family, Save, DataManager, and static gate tests. Expected: pass.

- [ ] **Step 2: Run targeted PlayMode tests**

Run new-slot customization, save/load restore, existing-save fallback, and farm-entry tests. Expected: pass.

- [ ] **Step 3: Run entity-id branching gate**

Run the CI gate. Expected: pass with no part-id branching.

- [ ] **Step 4: Capture visual evidence**

Use Unity screenshot tooling to confirm the Player uses active part renderers and not only a single complete sprite.

- [ ] **Step 5: Completion audit**

Map each goal requirement and scenario (`CHAR-PART-001` through `CHAR-SAVE-001`) to files, assets, tests, command output, and visual evidence. Only mark the goal complete if every item has direct evidence.
