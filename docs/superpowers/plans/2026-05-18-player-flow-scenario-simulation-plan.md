# Player Flow Scenario Simulation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Strengthen the player-facing scenario simulations in `B -> C -> A` order: Town core flow first, House/Interiors flow second, then full scenario coverage audit.

**Architecture:** Reuse the existing PlayMode E2E tests that already execute real SaveSlot, keyboard movement, prompt input, UI clicks, save/load, and dedupe checks. Add scenario ID coverage, stricter evidence paths, House before/after reload screenshots, and a written audit that maps actual tests to the direct visual verification gate.

**Tech Stack:** Unity 6000.3, C#, NUnit EditMode/PlayMode, Unity Input System, Tilemap runtime probes, Rootborn asmdefs, Unity MCP `tests-run`. All `Assets/**/*.cs` changes must be made through Unity MCP `script-update-or-create`, not direct disk writes.

---

## Files

- Modify via Unity MCP: `Assets/Tests/PlayMode/Scenarios/ScenarioId.cs`
  - Add scenario IDs for `TOWN-FLOW-*`, `HOUSE-FLOW-*`, and `COVERAGE-*`.
- Create via Unity MCP: `Assets/Tests/EditMode/Scenarios/ScenarioCoverageMappingTests.cs`
  - Verifies new scenario IDs exist and are mapped to real test/audit files.
- Modify via Unity MCP: `Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs`
  - Add `TOWN-FLOW-*` scenario naming and write evidence to the required `production/qa/evidence/town-core-flow-*.png` paths.
- Modify via Unity MCP: `Assets/Tests/PlayMode/EndToEnd/HousePlacementSaveLoadE2EScenarioTests.cs`
  - Add `HOUSE-FLOW-*` scenario naming, before-reload screenshot evidence, after-reload screenshot evidence, and runtime probe paths under `production/qa/evidence`.
- Create: `docs/superpowers/audits/2026-05-18-player-flow-scenario-coverage-audit.md`
  - Documents code/test counts, player-flow tests, fixture/source-audit tests, `ScenarioId.cs` mapping, visual gate status, remaining gaps.

## Commands

- Targeted EditMode:
  ```powershell
  # Unity MCP tests-run:
  # testMode=EditMode
  # testAssembly=Rootborn.Tests.EditMode
  # testClass=Rootborn.Tests.EditMode.Scenarios.ScenarioCoverageMappingTests
  ```
- Targeted Town PlayMode:
  ```powershell
  # Unity MCP tests-run:
  # testMode=PlayMode
  # testAssembly=Rootborn.Tests.PlayMode
  # testClass=Rootborn.Tests.PlayMode.EndToEnd.IntegratedVerticalSliceFoundationE2ETests
  ```
- Targeted House PlayMode:
  ```powershell
  # Unity MCP tests-run:
  # testMode=PlayMode
  # testAssembly=Rootborn.Tests.PlayMode
  # testClass=Rootborn.Tests.PlayMode.EndToEnd.HousePlacementSaveLoadE2EScenarioTests
  ```
- Entity branching gate:
  ```powershell
  & "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh
  ```

---

### Task 1: Scenario ID Coverage Contract

**Files:**
- Modify: `Assets/Tests/PlayMode/Scenarios/ScenarioId.cs`
- Create: `Assets/Tests/EditMode/Scenarios/ScenarioCoverageMappingTests.cs`

- [ ] **Step 1: Write the failing EditMode mapping test**

Use Unity MCP `script-update-or-create` to create `Assets/Tests/EditMode/Scenarios/ScenarioCoverageMappingTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using Rootborn.Tests.PlayMode.Scenarios;

namespace Rootborn.Tests.EditMode.Scenarios
{
    public sealed class ScenarioCoverageMappingTests
    {
        [Test]
        public void COVERAGE_001_NewPlayerFlowScenarioIdsAreDeclared()
        {
            Assert.AreEqual("TOWN-FLOW-001", ScenarioId.TOWN_FLOW_001);
            Assert.AreEqual("TOWN-FLOW-002", ScenarioId.TOWN_FLOW_002);
            Assert.AreEqual("TOWN-FLOW-003", ScenarioId.TOWN_FLOW_003);
            Assert.AreEqual("HOUSE-FLOW-001", ScenarioId.HOUSE_FLOW_001);
            Assert.AreEqual("HOUSE-FLOW-002", ScenarioId.HOUSE_FLOW_002);
            Assert.AreEqual("HOUSE-FLOW-003", ScenarioId.HOUSE_FLOW_003);
            Assert.AreEqual("COVERAGE-001", ScenarioId.COVERAGE_001);
        }

        [Test]
        public void COVERAGE_002_TownAndHouseScenarioIdsMapToConcretePlayModeTests()
        {
            string town = File.ReadAllText("Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs");
            string house = File.ReadAllText("Assets/Tests/PlayMode/EndToEnd/HousePlacementSaveLoadE2EScenarioTests.cs");

            StringAssert.Contains(ScenarioId.TOWN_FLOW_001, town);
            StringAssert.Contains(ScenarioId.TOWN_FLOW_002, town);
            StringAssert.Contains(ScenarioId.TOWN_FLOW_003, town);
            StringAssert.Contains(ScenarioId.HOUSE_FLOW_001, house);
            StringAssert.Contains(ScenarioId.HOUSE_FLOW_002, house);
            StringAssert.Contains(ScenarioId.HOUSE_FLOW_003, house);
        }

        [Test]
        public void COVERAGE_003_AuditDocumentMapsPlayerFlowVerificationGate()
        {
            const string auditPath = "docs/superpowers/audits/2026-05-18-player-flow-scenario-coverage-audit.md";
            Assert.IsTrue(File.Exists(auditPath), "The player-flow scenario coverage audit must be written before completion.");
            string audit = File.ReadAllText(auditPath);

            StringAssert.Contains("Town Core Player Flow", audit);
            StringAssert.Contains("House/Interiors Player Flow", audit);
            StringAssert.Contains("Direct Visual Play Verification Gate", audit);
            StringAssert.Contains(ScenarioId.TOWN_FLOW_001, audit);
            StringAssert.Contains(ScenarioId.HOUSE_FLOW_001, audit);
            StringAssert.Contains(ScenarioId.COVERAGE_001, audit);
        }
    }
}
```

- [ ] **Step 2: Run the targeted EditMode test and verify RED**

Run Unity MCP `tests-run` for `Rootborn.Tests.EditMode.Scenarios.ScenarioCoverageMappingTests`.

Expected result: compile failure because `ScenarioId.TOWN_FLOW_001` and related constants do not exist yet.

- [ ] **Step 3: Add the scenario ID constants**

Use Unity MCP `script-update-or-create` to update `Assets/Tests/PlayMode/Scenarios/ScenarioId.cs` so it contains the existing constants plus these new constants before the pending comment:

```csharp
public const string TOWN_FLOW_001 = "TOWN-FLOW-001";
public const string TOWN_FLOW_002 = "TOWN-FLOW-002";
public const string TOWN_FLOW_003 = "TOWN-FLOW-003";
public const string HOUSE_FLOW_001 = "HOUSE-FLOW-001";
public const string HOUSE_FLOW_002 = "HOUSE-FLOW-002";
public const string HOUSE_FLOW_003 = "HOUSE-FLOW-003";
public const string COVERAGE_001 = "COVERAGE-001";
public const string COVERAGE_002 = "COVERAGE-002";
public const string COVERAGE_003 = "COVERAGE-003";
```

- [ ] **Step 4: Run the targeted EditMode test and verify expected partial RED**

Run Unity MCP `tests-run` for `ScenarioCoverageMappingTests`.

Expected result: test compiles, `COVERAGE_001_NewPlayerFlowScenarioIdsAreDeclared` passes, and the test class still fails because the PlayMode test source and audit document do not yet contain the new IDs.

- [ ] **Step 5: Commit this task**

```powershell
git add -- Assets/Tests/PlayMode/Scenarios/ScenarioId.cs Assets/Tests/PlayMode/Scenarios/ScenarioId.cs.meta Assets/Tests/EditMode/Scenarios/ScenarioCoverageMappingTests.cs Assets/Tests/EditMode/Scenarios/ScenarioCoverageMappingTests.cs.meta
git commit -m "[TEST] 플레이 흐름 시나리오 ID 매핑 계약 추가"
```

---

### Task 2: Phase B Town Core Player Flow Evidence

**Files:**
- Modify: `Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs`
- Test: `Assets/Tests/EditMode/Scenarios/ScenarioCoverageMappingTests.cs`

- [ ] **Step 1: Add failing source assertions for required Town evidence paths**

Use Unity MCP `script-update-or-create` to append this test to `ScenarioCoverageMappingTests`:

```csharp
[Test]
public void COVERAGE_004_TownCoreFlowWritesRequiredEvidencePaths()
{
    string town = File.ReadAllText("Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs");

    StringAssert.Contains("town-core-flow-objective-journal.png", town);
    StringAssert.Contains("town-core-flow-day-result.png", town);
    StringAssert.Contains("production", town);
    StringAssert.Contains("qa", town);
    StringAssert.Contains("evidence", town);
}
```

- [ ] **Step 2: Run the targeted EditMode test and verify RED**

Run Unity MCP `tests-run` for `ScenarioCoverageMappingTests`.

Expected result: failure because the Town E2E currently writes `integrated-vertical-slice-objective-journal.png` and `integrated-vertical-slice-day-result.png`, not the required `town-core-flow-*` evidence names.

- [ ] **Step 3: Update the Town PlayMode test scenario name**

Use Unity MCP `script-update-or-create` to modify `IntegratedVerticalSliceFoundationE2ETests.cs`.

Add this using directive:

```csharp
using Rootborn.Tests.PlayMode.Scenarios;
```

Rename the test method from:

```csharp
public IEnumerator VERTICAL_E2E_001_010_NewGameTownUsageObjectiveJournalDayResultHouseMotivationReloadAndDedupe()
```

to:

```csharp
public IEnumerator TOWN_FLOW_001_003_NewGameTownObjectiveJournalDayResultReloadAndDedupe()
```

- [ ] **Step 4: Add explicit scenario ID assertions to the Town test**

Inside the renamed test method, immediately after `var keyboard = InputSystem.AddDevice<Keyboard>();`, add:

```csharp
Assert.AreEqual("TOWN-FLOW-001", ScenarioId.TOWN_FLOW_001);
Assert.AreEqual("TOWN-FLOW-002", ScenarioId.TOWN_FLOW_002);
Assert.AreEqual("TOWN-FLOW-003", ScenarioId.TOWN_FLOW_003);
```

- [ ] **Step 5: Change the Town screenshot evidence names**

In `IntegratedVerticalSliceFoundationE2ETests.cs`, replace:

```csharp
yield return CaptureGameViewEvidence("integrated-vertical-slice-objective-journal.png");
```

with:

```csharp
yield return CaptureGameViewEvidence("town-core-flow-objective-journal.png");
```

Replace:

```csharp
yield return CaptureGameViewEvidence("integrated-vertical-slice-day-result.png");
```

with:

```csharp
yield return CaptureGameViewEvidence("town-core-flow-day-result.png");
```

- [ ] **Step 6: Run the targeted EditMode test and verify the Town source checks pass**

Run Unity MCP `tests-run` for `ScenarioCoverageMappingTests`.

Expected result: `COVERAGE_002_TownAndHouseScenarioIdsMapToConcretePlayModeTests` still fails only for House IDs and audit document content, while `COVERAGE_004_TownCoreFlowWritesRequiredEvidencePaths` passes.

- [ ] **Step 7: Run the targeted Town PlayMode test and verify GREEN**

Run Unity MCP `tests-run` for `Rootborn.Tests.PlayMode.EndToEnd.IntegratedVerticalSliceFoundationE2ETests`.

Expected result: the test passes and writes:

```text
production/qa/evidence/town-core-flow-objective-journal.png
production/qa/evidence/town-core-flow-day-result.png
```

- [ ] **Step 8: Commit this task**

```powershell
git add -- Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs.meta Assets/Tests/EditMode/Scenarios/ScenarioCoverageMappingTests.cs Assets/Tests/EditMode/Scenarios/ScenarioCoverageMappingTests.cs.meta production/qa/evidence/town-core-flow-objective-journal.png production/qa/evidence/town-core-flow-day-result.png
git commit -m "[TEST] Town 핵심 플레이 흐름 증거 경로 보강"
```

---

### Task 3: Phase C House/Interiors Before and After Reload Evidence

**Files:**
- Modify: `Assets/Tests/PlayMode/EndToEnd/HousePlacementSaveLoadE2EScenarioTests.cs`
- Test: `Assets/Tests/EditMode/Scenarios/ScenarioCoverageMappingTests.cs`

- [ ] **Step 1: Add failing source assertions for required House evidence paths**

Use Unity MCP `script-update-or-create` to append this test to `ScenarioCoverageMappingTests`:

```csharp
[Test]
public void COVERAGE_005_HouseFlowWritesBeforeAndAfterReloadEvidencePaths()
{
    string house = File.ReadAllText("Assets/Tests/PlayMode/EndToEnd/HousePlacementSaveLoadE2EScenarioTests.cs");

    StringAssert.Contains("house-interior-placement-before-reload.png", house);
    StringAssert.Contains("house-interior-placement-after-reload.png", house);
    StringAssert.Contains("house-interior-placement-before-reload-probe.txt", house);
    StringAssert.Contains("house-interior-placement-after-reload-probe.txt", house);
}
```

- [ ] **Step 2: Run the targeted EditMode test and verify RED**

Run Unity MCP `tests-run` for `ScenarioCoverageMappingTests`.

Expected result: failure because `HousePlacementSaveLoadE2EScenarioTests.cs` currently writes only the old `Builds/Logs/house-placement-save-load-e2e/restored-house-placement-*` artifacts.

- [ ] **Step 3: Add the scenario namespace to House E2E**

Use Unity MCP `script-update-or-create` to add this using directive to `HousePlacementSaveLoadE2EScenarioTests.cs`:

```csharp
using Rootborn.Tests.PlayMode.Scenarios;
```

- [ ] **Step 4: Rename the House E2E test method**

Rename:

```csharp
public IEnumerator HOUSE_PLACEMENT_E2E_001_NewGamePlaceSaveExitLoadRestoresHouseFurniture()
```

to:

```csharp
public IEnumerator HOUSE_FLOW_001_003_NewGamePlaceSaveExitLoadRestoresHouseFurnitureWithVisualEvidence()
```

- [ ] **Step 5: Add explicit scenario ID assertions to the House test**

Inside the renamed test method, before `yield return OpenCreatorFromMainMenu();`, add:

```csharp
Assert.AreEqual("HOUSE-FLOW-001", ScenarioId.HOUSE_FLOW_001);
Assert.AreEqual("HOUSE-FLOW-002", ScenarioId.HOUSE_FLOW_002);
Assert.AreEqual("HOUSE-FLOW-003", ScenarioId.HOUSE_FLOW_003);
```

- [ ] **Step 6: Capture before-reload evidence after save**

After this existing assertion:

```csharp
StringAssert.Contains(placedTileName, File.ReadAllText(layoutPath), "The saved layout JSON should identify the same furniture tile placed by the player-facing world click.");
```

add:

```csharp
yield return CaptureVisualEvidence(
    "house-interior-placement-before-reload.png",
    "house-interior-placement-before-reload-probe.txt",
    furnitureTilemap,
    occupancyTilemap,
    placedCell,
    placedTileName);
```

- [ ] **Step 7: Replace the after-reload evidence call**

Replace:

```csharp
yield return CaptureVisualEvidence(restoredFurnitureTilemap, restoredOccupancyTilemap, restoredCell, restoredTileName);
```

with:

```csharp
yield return CaptureVisualEvidence(
    "house-interior-placement-after-reload.png",
    "house-interior-placement-after-reload-probe.txt",
    restoredFurnitureTilemap,
    restoredOccupancyTilemap,
    restoredCell,
    restoredTileName);
```

- [ ] **Step 8: Change the evidence helper signature and output directory**

Replace the existing helper signature:

```csharp
private static IEnumerator CaptureVisualEvidence(Tilemap furnitureTilemap, Tilemap occupancyTilemap, Vector3Int restoredCell, string restoredTileName)
```

with:

```csharp
private static IEnumerator CaptureVisualEvidence(string screenshotFileName, string probeFileName, Tilemap furnitureTilemap, Tilemap occupancyTilemap, Vector3Int restoredCell, string restoredTileName)
```

Inside the helper, replace:

```csharp
string evidenceDirectory = Path.Combine("Builds", "Logs", "house-placement-save-load-e2e");
Directory.CreateDirectory(evidenceDirectory);
string screenshotPath = Path.Combine(evidenceDirectory, "restored-house-placement-game-view.png");
string probePath = Path.Combine(evidenceDirectory, "restored-house-placement-probe.txt");
```

with:

```csharp
string evidenceDirectory = Path.Combine(Application.dataPath, "..", "production", "qa", "evidence");
Directory.CreateDirectory(evidenceDirectory);
string screenshotPath = Path.Combine(evidenceDirectory, screenshotFileName);
string probePath = Path.Combine(evidenceDirectory, probeFileName);
```

- [ ] **Step 9: Run the targeted EditMode test and verify the House source checks pass**

Run Unity MCP `tests-run` for `ScenarioCoverageMappingTests`.

Expected result: the source checks pass except the audit document check, which remains red until Task 4.

- [ ] **Step 10: Run the targeted House PlayMode test and verify GREEN**

Run Unity MCP `tests-run` for `Rootborn.Tests.PlayMode.EndToEnd.HousePlacementSaveLoadE2EScenarioTests`.

Expected result: the test passes and writes:

```text
production/qa/evidence/house-interior-placement-before-reload.png
production/qa/evidence/house-interior-placement-before-reload-probe.txt
production/qa/evidence/house-interior-placement-after-reload.png
production/qa/evidence/house-interior-placement-after-reload-probe.txt
```

- [ ] **Step 11: Commit this task**

```powershell
git add -- Assets/Tests/PlayMode/EndToEnd/HousePlacementSaveLoadE2EScenarioTests.cs Assets/Tests/PlayMode/EndToEnd/HousePlacementSaveLoadE2EScenarioTests.cs.meta Assets/Tests/EditMode/Scenarios/ScenarioCoverageMappingTests.cs Assets/Tests/EditMode/Scenarios/ScenarioCoverageMappingTests.cs.meta production/qa/evidence/house-interior-placement-before-reload.png production/qa/evidence/house-interior-placement-before-reload-probe.txt production/qa/evidence/house-interior-placement-after-reload.png production/qa/evidence/house-interior-placement-after-reload-probe.txt
git commit -m "[TEST] House 배치 전후 시각 증거 검증 보강"
```

---

### Task 4: Phase A Scenario Coverage Audit Document

**Files:**
- Create: `docs/superpowers/audits/2026-05-18-player-flow-scenario-coverage-audit.md`
- Test: `Assets/Tests/EditMode/Scenarios/ScenarioCoverageMappingTests.cs`

- [ ] **Step 1: Confirm the audit test is RED**

Run Unity MCP `tests-run` for `ScenarioCoverageMappingTests`.

Expected result: only `COVERAGE_003_AuditDocumentMapsPlayerFlowVerificationGate` fails because the audit document is missing.

- [ ] **Step 2: Write the audit document**

Create `docs/superpowers/audits/2026-05-18-player-flow-scenario-coverage-audit.md` with this content, updating the verification result lines only after Task 2 and Task 3 PlayMode runs have produced actual results:

```markdown
# Player Flow Scenario Coverage Audit

Date: 2026-05-18

## Scope

This audit maps player-facing scenario coverage for the `B -> C -> A` implementation order:

- `B`: Town Core Player Flow
- `C`: House/Interiors Player Flow
- `A`: Scenario Coverage Audit

## Repository Counts

- `Assets/Scripts` C# files: 527
- `Assets/Tests/EditMode` C# tests: 209
- `Assets/Tests/PlayMode` C# tests: 90
- Largest runtime domains: `StudentLife`, `Quests`, `DiscoveryClues`, `WorldState`, `Housing`, `Interiors`

## Town Core Player Flow

Scenario IDs:

- `TOWN-FLOW-001`: SaveSlot New Game, character confirmation, Town entry, player-controlled movement and interaction.
- `TOWN-FLOW-002`: Objective Journal and DayResult reflect at least two changed gameplay domains.
- `TOWN-FLOW-003`: Save/load and duplicate interaction checks preserve state without double progress or double reward.

Mapped PlayMode test:

- `Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs`

Direct Visual Play Verification Gate:

- Actual SaveSlot UI click: covered.
- Actual keyboard movement: covered.
- Actual prompt/input path: covered.
- Actual UI button click: covered.
- Runtime state inspection: covered.
- Save/load state inspection: covered.
- Duplicate-action prevention: covered.
- Game View screenshot evidence:
  - `production/qa/evidence/town-core-flow-objective-journal.png`
  - `production/qa/evidence/town-core-flow-day-result.png`

## House/Interiors Player Flow

Scenario IDs:

- `HOUSE-FLOW-001`: New Game to House entry, palette selection, player-facing mouse placement.
- `HOUSE-FLOW-002`: Runtime Tilemap, occupancy, overlay, selected tile name, and stale sample/debug tilemap checks.
- `HOUSE-FLOW-003`: Save/load restores visual furniture state and occupancy without duplicates.

Mapped PlayMode test:

- `Assets/Tests/PlayMode/EndToEnd/HousePlacementSaveLoadE2EScenarioTests.cs`

Direct Visual Play Verification Gate:

- Actual SaveSlot UI click: covered.
- Actual House scene entry: covered.
- Actual palette UI click: covered.
- Actual world mouse placement: covered.
- Runtime Tilemap state inspection: covered.
- Runtime overlay state inspection: covered.
- Save/load state inspection: covered.
- Stale sample/debug tilemap inspection: covered.
- Game View or Camera screenshot evidence:
  - `production/qa/evidence/house-interior-placement-before-reload.png`
  - `production/qa/evidence/house-interior-placement-after-reload.png`
- Runtime probe evidence:
  - `production/qa/evidence/house-interior-placement-before-reload-probe.txt`
  - `production/qa/evidence/house-interior-placement-after-reload-probe.txt`

## Fixture and Source-Audit Tests

These tests are useful but are not sufficient by themselves for user-facing completion:

- EditMode source audit tests that read `.cs` files.
- Registry asset tests that verify ScriptableObject wiring.
- UI surface tests that instantiate panels without SaveSlot/Town/House entry.
- DirectValidation source tests that inspect autoplay code.

They may support Phase B and Phase C, but completion requires the PlayMode paths listed above.

## ScenarioId Mapping

- `TOWN-FLOW-001` -> `IntegratedVerticalSliceFoundationE2ETests`
- `TOWN-FLOW-002` -> `IntegratedVerticalSliceFoundationE2ETests`
- `TOWN-FLOW-003` -> `IntegratedVerticalSliceFoundationE2ETests`
- `HOUSE-FLOW-001` -> `HousePlacementSaveLoadE2EScenarioTests`
- `HOUSE-FLOW-002` -> `HousePlacementSaveLoadE2EScenarioTests`
- `HOUSE-FLOW-003` -> `HousePlacementSaveLoadE2EScenarioTests`
- `COVERAGE-001` -> `ScenarioCoverageMappingTests`
- `COVERAGE-002` -> `ScenarioCoverageMappingTests`
- `COVERAGE-003` -> `ScenarioCoverageMappingTests`

## Remaining Gaps

- Host/client multiplayer validation remains outside this slice.
- Free-form House wall/floor editing beyond the covered furniture placement path remains outside this slice.
- Existing historical tests that use internal setup remain useful but should not be reported as direct visual verification unless paired with a player-flow PlayMode path.

## Verification Results

- EditMode `ScenarioCoverageMappingTests`: PASS after audit creation.
- PlayMode `IntegratedVerticalSliceFoundationE2ETests`: PASS after Town evidence path update.
- PlayMode `HousePlacementSaveLoadE2EScenarioTests`: PASS after before/after reload evidence update.
- Entity branching gate: PASS after final verification.
```

- [ ] **Step 3: Run the targeted EditMode test and verify GREEN**

Run Unity MCP `tests-run` for `ScenarioCoverageMappingTests`.

Expected result: all tests in `ScenarioCoverageMappingTests` pass.

- [ ] **Step 4: Commit this task**

```powershell
git add -- docs/superpowers/audits/2026-05-18-player-flow-scenario-coverage-audit.md Assets/Tests/EditMode/Scenarios/ScenarioCoverageMappingTests.cs Assets/Tests/EditMode/Scenarios/ScenarioCoverageMappingTests.cs.meta
git commit -m "[DOCS][TEST] 플레이 흐름 시나리오 커버리지 감사 추가"
```

---

### Task 5: Final Verification and Evidence Review

**Files:**
- Modify if needed: `docs/superpowers/audits/2026-05-18-player-flow-scenario-coverage-audit.md`

- [ ] **Step 1: Run targeted EditMode verification**

Run Unity MCP `tests-run`:

```text
testMode=EditMode
testAssembly=Rootborn.Tests.EditMode
testClass=Rootborn.Tests.EditMode.Scenarios.ScenarioCoverageMappingTests
```

Expected result: all tests pass.

- [ ] **Step 2: Run targeted Town PlayMode verification**

Run Unity MCP `tests-run`:

```text
testMode=PlayMode
testAssembly=Rootborn.Tests.PlayMode
testClass=Rootborn.Tests.PlayMode.EndToEnd.IntegratedVerticalSliceFoundationE2ETests
```

Expected result: all tests pass and both Town screenshots exist under `production/qa/evidence`.

- [ ] **Step 3: Run targeted House PlayMode verification**

Run Unity MCP `tests-run`:

```text
testMode=PlayMode
testAssembly=Rootborn.Tests.PlayMode
testClass=Rootborn.Tests.PlayMode.EndToEnd.HousePlacementSaveLoadE2EScenarioTests
```

Expected result: all tests pass and both House screenshots plus both probe files exist under `production/qa/evidence`.

- [ ] **Step 4: Run the entity branching gate**

Run:

```powershell
& "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh
```

Expected result:

```text
OK: no entity-id branching in system code.
```

- [ ] **Step 5: Inspect evidence files**

Run:

```powershell
Get-ChildItem production\qa\evidence\town-core-flow-*.png,production\qa\evidence\house-interior-placement-* | Select-Object Name,Length
```

Expected result: all expected files exist and each `Length` is greater than `0`.

- [ ] **Step 6: Update the audit if verification differs**

If any targeted command is not run or fails, edit `docs/superpowers/audits/2026-05-18-player-flow-scenario-coverage-audit.md` so `Verification Results` states the exact unverified or failing path.

- [ ] **Step 7: Commit final verification notes if changed**

```powershell
git add -- docs/superpowers/audits/2026-05-18-player-flow-scenario-coverage-audit.md
git commit -m "[DOCS] 플레이 흐름 검증 결과 갱신"
```

Skip this commit only if the audit already matches the actual verification result and no file changed.

