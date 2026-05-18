# SPUM Character Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate SPUM character visuals into ROOTBORN through Addressables, ScriptableObject appearance data, Player/NPC visual adapters, and a pre-entry character creator without changing Player physics or interaction ownership.

**Architecture:** SPUM source output is migrated into ROOTBORN-managed Addressables assets and SO catalogs. Runtime systems talk to `ICharacterVisualView`; Pixelwood and SPUM visuals are alternate implementations behind the same boundary. Save slots store stable appearance snapshots, not Unity object references.

**Tech Stack:** Unity 6000.3.13f1, C#, Unity Addressables, Unity Input System, Unity Test Framework EditMode/PlayMode, ROOTBORN `script-update-or-create` MCP workflow.

---

## Ground Rules

- Do not edit `Assets/**/*.cs` with shell writes, `cat`, `echo`, or `apply_patch`. Use Unity MCP `script-update-or-create`.
- Do not run Unity Editor, Unity MCP scene/script tools, or package import until the user explicitly approves that execution.
- Do not copy SPUM files into the project until the user approves the migration step.
- Write failing tests first for every implementation task.
- PlayMode tests must use actual keyboard, mouse, trigger, and UI click paths.
- Keep entity behavior data-driven. No `if (npcId == ...)`, `switch(toolId)`, or SPUM prefab-name branching.

## File Structure

Create or modify these files during implementation:

- Create: `Assets/Scripts/Game/Characters/CharacterAppearanceDefinition.cs`
- Create: `Assets/Scripts/Game/Characters/CharacterAppearanceSnapshot.cs`
- Create: `Assets/Scripts/Game/Characters/ICharacterVisualView.cs`
- Create: `Assets/Scripts/Game/Characters/CharacterVisualAction.cs`
- Create: `Assets/Scripts/Game/Characters/PixelwoodCharacterVisualView.cs`
- Create: `Assets/Scripts/Game/Characters/PlayerCharacterVisualAdapter.cs`
- Create: `Assets/Scripts/Game/Characters/NpcCharacterVisualAdapter.cs`
- Create: `Assets/Scripts/Game/Characters/Spum/SpumAppearanceDefinition.cs`
- Create: `Assets/Scripts/Game/Characters/Spum/SpumPartCatalogDefinition.cs`
- Create: `Assets/Scripts/Game/Characters/Spum/SpumPartDefinition.cs`
- Create: `Assets/Scripts/Game/Characters/Spum/SpumCharacterCreatorPresetDefinition.cs`
- Create: `Assets/Scripts/Game/Characters/Spum/SpumCharacterVisualView.cs`
- Create: `Assets/Scripts/Game/Tools/ToolVisualMappingDefinition.cs`
- Modify: `Assets/Scripts/Game/Common/GameDataRegistry.cs`
- Modify: `Assets/Scripts/Game/Dialogue/NpcDefinition.cs`
- Modify: `Assets/Scripts/Game/Save/SaveSlotMetadata.cs`
- Modify: `Assets/Scripts/Game/Bootstrap/TownPlayableBaselineRuntimeInstaller.cs`
- Modify: `Assets/Scripts/Game/Player/PlayerController.cs`
- Create: `Assets/Scripts/UI/MainMenu/SpumCharacterCreatorPanel.cs`
- Create: `Assets/Scripts/UI/MainMenu/SpumCharacterCreatorPreview.cs`
- Modify: `Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs`
- Create: `Assets/Scripts/Editor/Tools/SpumAddressablesMigrationService.cs`
- Create: `Assets/Scripts/Editor/Tools/SpumAddressablesMigrationWindow.cs`
- Create tests under `Assets/Tests/EditMode/Characters/Spum/`
- Create tests under `Assets/Tests/EditMode/UI/Spum/`
- Create tests under `Assets/Tests/PlayMode/EndToEnd/`

## Verification Commands

Use MCP `tests-run` for Unity tests when available. CLI fallback:

```cmd
"<UnityPath>" -batchmode -nographics -projectPath "c:\Users\jdyj\farmer" -runTests -testPlatform editmode -testResults "Builds/Logs/spum-editmode-results.xml" -logFile "Builds/Logs/spum-editmode.log"
```

```cmd
"<UnityPath>" -batchmode -nographics -projectPath "c:\Users\jdyj\farmer" -runTests -testPlatform playmode -testResults "Builds/Logs/spum-playmode-results.xml" -logFile "Builds/Logs/spum-playmode.log"
```

Run the entity branching gate after code changes:

```powershell
bash Scripts/ci/check-no-entity-id-branching.sh
```

## Task 1: Runtime Resources Gate

**Files:**
- Create: `Assets/Tests/EditMode/Characters/Spum/SpumResourcesUsageGateTests.cs`

- [ ] **Step 1: Write the failing test**

Create a source scan test that enumerates `Assets/Scripts/Game`, `Assets/Scripts/UI`, and `Assets/Scripts/Network`, excludes existing accepted builtin font calls if the project keeps them, and fails on new `Resources.Load`, `Resources.LoadAll`, or `Resources.LoadAsync`.

- [ ] **Step 2: Run the test and verify it fails or reports current known exceptions**

Run EditMode test filtered to `SpumResourcesUsageGateTests`. Expected before implementation: fail if unapproved runtime `Resources.Load*` is present, or pass with an explicit allowlist documenting existing non-SPUM built-in font use.

- [ ] **Step 3: Add or tighten the allowlist**

Allow only `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` if still needed by current UI tests. Do not allow SPUM runtime loading.

- [ ] **Step 4: Run the test again**

Expected: PASS and output lists zero SPUM runtime `Resources.Load*` uses.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Tests/EditMode/Characters/Spum/SpumResourcesUsageGateTests.cs
git commit -m "[TEST] SPUM 런타임 Resources 사용 금지 게이트 추가"
```

## Task 2: Appearance Data Model

**Files:**
- Create tests: `Assets/Tests/EditMode/Characters/Spum/SpumAppearanceDefinitionTests.cs`
- Create: `Assets/Scripts/Game/Characters/CharacterAppearanceDefinition.cs`
- Create: `Assets/Scripts/Game/Characters/CharacterAppearanceSnapshot.cs`
- Create: `Assets/Scripts/Game/Characters/Spum/SpumAppearanceDefinition.cs`
- Create: `Assets/Scripts/Game/Characters/Spum/SpumPartCatalogDefinition.cs`
- Create: `Assets/Scripts/Game/Characters/Spum/SpumPartDefinition.cs`
- Create: `Assets/Scripts/Game/Characters/Spum/SpumCharacterCreatorPresetDefinition.cs`

- [ ] **Step 1: Write failing tests**

Cover:

- snapshot serializes `schemaVersion`, `visualKind`, `appearanceDefinitionId`, `catalogId`, and selected parts.
- catalog resolves selected part by stable id.
- missing saved part falls back to category default.
- missing category returns empty result without changing the original snapshot.
- preset produces defaults for body/skin/eyes/hair/outfit when catalog contains them.

- [ ] **Step 2: Run the tests**

Expected: FAIL because the model types do not exist.

- [ ] **Step 3: Implement minimal SO and snapshot types**

Use sealed classes, `[SerializeField] private` fields, read-only public properties, and test configuration methods only where existing project tests require them.

- [ ] **Step 4: Run targeted EditMode tests**

Expected: PASS for `SpumAppearanceDefinitionTests`.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/Game/Characters Assets/Tests/EditMode/Characters/Spum
git commit -m "[FEATURE][TEST] SPUM 외관 데이터 모델 추가"
```

## Task 3: Registry and Save Metadata

**Files:**
- Create tests: `Assets/Tests/EditMode/Characters/Spum/SpumAppearanceRegistryTests.cs`
- Modify: `Assets/Scripts/Game/Common/GameDataRegistry.cs`
- Modify: `Assets/Scripts/Game/Save/SaveSlotMetadata.cs`

- [ ] **Step 1: Write failing tests**

Verify `GameDataRegistry` exposes character appearance definitions, SPUM catalogs, creator presets, and tool visual mappings. Verify `SaveSlotMetadata` round-trips `CharacterAppearanceSnapshot` through `JsonUtility`.

- [ ] **Step 2: Run targeted tests**

Expected: FAIL due to missing registry fields and metadata field.

- [ ] **Step 3: Implement registry arrays and save metadata field**

Add arrays only. Do not load by Resources. Preserve existing `CharacterAppearance Appearance` for migration and rollback.

- [ ] **Step 4: Run tests**

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/Game/Common/GameDataRegistry.cs Assets/Scripts/Game/Save/SaveSlotMetadata.cs Assets/Tests/EditMode/Characters/Spum/SpumAppearanceRegistryTests.cs
git commit -m "[FEATURE][TEST] SPUM 외관 레지스트리와 저장 스냅샷 추가"
```

## Task 4: Visual View Boundary

**Files:**
- Create tests: `Assets/Tests/EditMode/Characters/Spum/CharacterVisualViewBoundaryTests.cs`
- Create: `Assets/Scripts/Game/Characters/ICharacterVisualView.cs`
- Create: `Assets/Scripts/Game/Characters/CharacterVisualAction.cs`
- Create: `Assets/Scripts/Game/Characters/PixelwoodCharacterVisualView.cs`
- Create: `Assets/Scripts/Game/Characters/PlayerCharacterVisualAdapter.cs`

- [ ] **Step 1: Write failing tests**

Verify Player-facing adapter can call `SetMotion`, `SetFlipX`, `PlayAction`, and `ApplyEquippedToolVisual` without referencing a SPUM namespace. Add a source test that `PlayerController.cs` does not contain `SpumCharacterVisualView` or `SPUM_Prefabs`.

- [ ] **Step 2: Run tests**

Expected: FAIL because interfaces/adapters do not exist.

- [ ] **Step 3: Implement boundary and Pixelwood adapter**

Wrap existing `CharacterPartComposer`/`CharacterPartAnimator`. Keep behavior equivalent: motion/facing calls reach `CharacterPartAnimator.SetMotion`; attack calls `PlayClip` when mapping provides a Pixelwood clip.

- [ ] **Step 4: Run existing character part tests plus new tests**

Run `CharacterPartComposerTests`, `CharacterPartAnimatorTests`, and `CharacterVisualViewBoundaryTests`. Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/Game/Characters Assets/Tests/EditMode/Characters/Spum/CharacterVisualViewBoundaryTests.cs
git commit -m "[FEATURE][TEST] 캐릭터 비주얼 어댑터 경계 추가"
```

## Task 5: Tool Visual Mapping

**Files:**
- Create tests: `Assets/Tests/EditMode/Characters/Spum/SpumToolVisualMappingTests.cs`
- Create: `Assets/Scripts/Game/Tools/ToolVisualMappingDefinition.cs`
- Modify: `Assets/Scripts/Game/Common/GameDataRegistry.cs`

- [ ] **Step 1: Write failing tests**

Verify mappings are found by `ToolDefinition` reference, not tool id switch. Verify missing mapping returns a null/empty mapping. Verify StoneAxe and StonePickaxe fixture assets can be assigned mappings without code branching.

- [ ] **Step 2: Run tests**

Expected: FAIL because mapping type is missing.

- [ ] **Step 3: Implement mapping SO and registry lookup helper**

The helper loops registry array and compares `ToolDefinition` object references. It must not switch on `tool.Id`.

- [ ] **Step 4: Run tests and branching gate**

Expected: targeted tests PASS; `check-no-entity-id-branching.sh` PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/Game/Tools/ToolVisualMappingDefinition.cs Assets/Scripts/Game/Common/GameDataRegistry.cs Assets/Tests/EditMode/Characters/Spum/SpumToolVisualMappingTests.cs
git commit -m "[FEATURE][TEST] SPUM 도구 비주얼 매핑 추가"
```

## Task 6: SPUM Visual View

**Files:**
- Create tests: `Assets/Tests/EditMode/Characters/Spum/SpumVisualAdapterMotionTests.cs`
- Create: `Assets/Scripts/Game/Characters/Spum/SpumCharacterVisualView.cs`

- [ ] **Step 1: Write failing tests**

Verify:

- zero input maps to IDLE.
- non-zero input maps to MOVE.
- side facing calls visual-only flip.
- attack action maps to configured ATTACK clip index.
- visual scale defaults to `32f / 49f`.

- [ ] **Step 2: Run tests**

Expected: FAIL because `SpumCharacterVisualView` does not exist.

- [ ] **Step 3: Implement minimal view**

Do not depend on SPUM source runtime classes if they require `Resources.Load*`. Use Unity `Animator`, `AnimatorOverrideController`, serialized clip references, and child SpriteRenderer transforms from migrated prefabs.

- [ ] **Step 4: Run tests**

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/Game/Characters/Spum/SpumCharacterVisualView.cs Assets/Tests/EditMode/Characters/Spum/SpumVisualAdapterMotionTests.cs
git commit -m "[FEATURE][TEST] SPUM 비주얼 상태 어댑터 추가"
```

## Task 7: Player Integration

**Files:**
- Create tests: `Assets/Tests/PlayMode/EndToEnd/SpumPlayerVisualMoveE2ETests.cs`
- Create tests: `Assets/Tests/PlayMode/EndToEnd/SpumPlayerAttackGatherE2ETests.cs`
- Modify: `Assets/Scripts/Game/Player/PlayerController.cs`
- Modify: `Assets/Scripts/Game/Bootstrap/TownPlayableBaselineRuntimeInstaller.cs`

- [ ] **Step 1: Write failing PlayMode tests**

Use actual keyboard input to move the Player and assert the SPUM view enters MOVE and flips when moving right. Use actual mouse left click to attack and assert `GatherInteractor` interaction count increments once.

- [ ] **Step 2: Run PlayMode tests**

Expected: FAIL because Player is not wired to `ICharacterVisualView`.

- [ ] **Step 3: Implement adapter calls**

`PlayerController` should call a serialized or discovered `PlayerCharacterVisualAdapter`. Keep existing `CharacterPartAnimator` path until Pixelwood adapter fully replaces it. Do not reference `SpumCharacterVisualView` directly.

- [ ] **Step 4: Run targeted PlayMode tests and existing character part PlayMode tests**

Expected: PASS for new SPUM tests and `CharacterPartCompositionPlayModeTests`.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/Game/Player/PlayerController.cs Assets/Scripts/Game/Bootstrap/TownPlayableBaselineRuntimeInstaller.cs Assets/Tests/PlayMode/EndToEnd/SpumPlayerVisualMoveE2ETests.cs Assets/Tests/PlayMode/EndToEnd/SpumPlayerAttackGatherE2ETests.cs
git commit -m "[FEATURE][TEST] Player SPUM 비주얼 어댑터 연결"
```

## Task 8: NPC Integration

**Files:**
- Create tests: `Assets/Tests/EditMode/Characters/Spum/SpumNpcAppearanceDefinitionTests.cs`
- Create tests: `Assets/Tests/PlayMode/EndToEnd/SpumNpcDialogueE2ETests.cs`
- Modify: `Assets/Scripts/Game/Dialogue/NpcDefinition.cs`
- Create: `Assets/Scripts/Game/Characters/NpcCharacterVisualAdapter.cs`

- [ ] **Step 1: Write failing tests**

Verify `NpcDefinition` exposes `CharacterAppearanceDefinition`. PlayMode spawns an NPC from definition, opens dialogue via actual interaction input, and confirms visual child remains separate from collider/dialogue root.

- [ ] **Step 2: Run tests**

Expected: FAIL because NPC appearance field and adapter are absent.

- [ ] **Step 3: Implement NPC appearance field and adapter**

Keep existing world texture fallback. Do not branch by NPC id.

- [ ] **Step 4: Run tests**

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/Game/Dialogue/NpcDefinition.cs Assets/Scripts/Game/Characters/NpcCharacterVisualAdapter.cs Assets/Tests/EditMode/Characters/Spum/SpumNpcAppearanceDefinitionTests.cs Assets/Tests/PlayMode/EndToEnd/SpumNpcDialogueE2ETests.cs
git commit -m "[FEATURE][TEST] NPC SPUM 외관 어댑터 추가"
```

## Task 9: Addressables Migration Tool

**Files:**
- Create tests: `Assets/Tests/EditMode/Characters/Spum/SpumAddressablesMigrationTests.cs`
- Create: `Assets/Scripts/Editor/Tools/SpumAddressablesMigrationService.cs`
- Create: `Assets/Scripts/Editor/Tools/SpumAddressablesMigrationWindow.cs`

- [ ] **Step 1: Ask for approval**

This step requires Unity Editor execution and later SPUM file copy. Ask the user before using Editor/MCP tools or importing/copying SPUM assets.

- [ ] **Step 2: Write failing EditMode tests**

Use temporary test assets, not real SPUM package files. Verify migration service registers prefab/clip/sprite in `CharacterVisuals`, creates catalog/part/appearance SOs, and rejects output paths outside approved ROOTBORN asset folders.

- [ ] **Step 3: Run tests**

Expected: FAIL because service/window are absent.

- [ ] **Step 4: Implement migration service**

Service takes explicit source asset refs and destination root. Window is a thin UI wrapper. Do not generate C# files. Use Unity AssetDatabase APIs in Editor code only.

- [ ] **Step 5: Run tests**

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add -- Assets/Scripts/Editor/Tools/SpumAddressablesMigrationService.cs Assets/Scripts/Editor/Tools/SpumAddressablesMigrationWindow.cs Assets/Tests/EditMode/Characters/Spum/SpumAddressablesMigrationTests.cs
git commit -m "[FEATURE][TEST] SPUM Addressables 이관 도구 추가"
```

## Task 10: Character Creator UI Data and Style

**Files:**
- Create tests: `Assets/Tests/EditMode/UI/Spum/SpumCharacterCreatorPanelTests.cs`
- Create tests: `Assets/Tests/EditMode/UI/Spum/SpumCharacterCreatorStyleTests.cs`
- Create: `Assets/Scripts/UI/MainMenu/SpumCharacterCreatorPanel.cs`
- Create: `Assets/Scripts/UI/MainMenu/SpumCharacterCreatorPreview.cs`

- [ ] **Step 1: Write failing EditMode tests**

Verify panel builds category tabs for Body/Skin, Eye, Hair, Outfit/Cloth, Accessory/Back/Helmet, and weapon preview if catalog supplies it. Verify common panels use `ModernUiTileImage` and `ModernUiRecipes.CommonPanel`. Verify no nested card containers are created.

- [ ] **Step 2: Run tests**

Expected: FAIL because panel does not exist.

- [ ] **Step 3: Implement UI panel**

Use 1920x1080 Canvas Scaler assumptions, short text labels, icon+text buttons where useful, and a grid that can page when category part count exceeds the visible cell budget.

- [ ] **Step 4: Run tests**

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/UI/MainMenu/SpumCharacterCreatorPanel.cs Assets/Scripts/UI/MainMenu/SpumCharacterCreatorPreview.cs Assets/Tests/EditMode/UI/Spum
git commit -m "[UI][TEST] SPUM 캐릭터 생성 패널 추가"
```

## Task 11: SaveSlot New Game Flow

**Files:**
- Create tests: `Assets/Tests/PlayMode/EndToEnd/SpumCharacterCreatorNewGameE2ETests.cs`
- Modify: `Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs`

- [ ] **Step 1: Write failing PlayMode tests**

From MainMenu/SaveSlot, click `NewGameButton`, click category next/grid part controls, click random, click confirm, then assert save metadata contains SPUM snapshot and Town Player visual uses the selected values.

- [ ] **Step 2: Run PlayMode tests**

Expected: FAIL because New Game still creates slot immediately.

- [ ] **Step 3: Change New Game flow**

`SaveSlotSelectPanel` opens `SpumCharacterCreatorPanel` for empty slot creation. Confirm callback creates metadata and loads Town. Cancel returns to slot cards.

- [ ] **Step 4: Run tests**

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs Assets/Tests/PlayMode/EndToEnd/SpumCharacterCreatorNewGameE2ETests.cs
git commit -m "[UI][TEST] New Game SPUM 커스터마이징 흐름 연결"
```

## Task 12: Save/Load Persistence

**Files:**
- Create tests: `Assets/Tests/PlayMode/EndToEnd/SpumAppearanceSaveLoadE2ETests.cs`
- Modify: `Assets/Scripts/Game/Save/SaveService.cs`
- Modify: `Assets/Scripts/Game/Bootstrap/TownPlayableBaselineRuntimeInstaller.cs`

- [ ] **Step 1: Write failing PlayMode test**

Create a slot through UI, enter Town, return/load same metadata, and assert SPUM appearance snapshot resolves to the same visual parts after load.

- [ ] **Step 2: Run test**

Expected: FAIL if save/load does not persist new snapshot.

- [ ] **Step 3: Implement persistence**

Ensure `SaveService` writes and reads `CharacterAppearanceSnapshot`. Keep existing Pixelwood `CharacterAppearance` migration field.

- [ ] **Step 4: Run test**

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/Game/Save/SaveService.cs Assets/Scripts/Game/Bootstrap/TownPlayableBaselineRuntimeInstaller.cs Assets/Tests/PlayMode/EndToEnd/SpumAppearanceSaveLoadE2ETests.cs
git commit -m "[FEATURE][TEST] SPUM 외관 저장 로드 유지"
```

## Task 13: Performance and Visual Verification

**Files:**
- Create tests: `Assets/Tests/PlayMode/EndToEnd/SpumVisualScaleComparisonTests.cs`
- Create docs: `docs/superpowers/audits/2026-05-14-spum-visual-scale-performance.md`

- [ ] **Step 1: Ask for approval**

Visual screenshot/performance verification requires Unity Editor or PlayMode execution. Ask the user before running it.

- [ ] **Step 2: Write PlayMode comparison test**

Spawn Pixelwood reference and SPUM visual at known positions. Capture bounds or screenshot evidence and assert SPUM visual height is within the approved tolerance.

- [ ] **Step 3: Add performance probe**

Measure renderer count, Animator count, `CharacterVisuals` preload time, and memory delta for Player + representative NPC count.

- [ ] **Step 4: Run PlayMode verification**

Expected: PASS within documented tolerance. If not, update SO scale defaults and rerun.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Tests/PlayMode/EndToEnd/SpumVisualScaleComparisonTests.cs docs/superpowers/audits/2026-05-14-spum-visual-scale-performance.md
git commit -m "[TEST][DOCS] SPUM 비주얼 크기와 성능 검증 추가"
```

## Final Verification

- [ ] Run targeted EditMode SPUM tests.
- [ ] Run targeted PlayMode SPUM E2E tests.
- [ ] Run existing `CharacterPartCompositionPlayModeTests`.
- [ ] Run `bash Scripts/ci/check-no-entity-id-branching.sh`.
- [ ] Confirm `rg -n "Resources\.Load|Resources\.LoadAll|Resources\.LoadAsync" Assets/Scripts` has no new SPUM runtime usage.
- [ ] Confirm no code references `npcId`, `toolId`, SPUM prefab name, or SPUM `_code` for branching.
- [ ] Confirm UI Style2 tests cover `SpumCharacterCreatorPanel`.
- [ ] Document Unity Editor actions and SPUM copied asset paths in the PR.
