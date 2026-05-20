# Milestone HUD Objective Journal Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove `MilestoneHudPanel` as a separate always-visible objective HUD and move milestone guidance into `ObjectiveJournalPanel` Hints rows plus `TrackedObjectiveHud`.

**Architecture:** `StudentDayRuntimeInstaller` keeps creating the day result panel and day-end board, but stops creating milestone HUD UI. Milestone state is read by `ObjectiveJournalRuntimeProvider.MilestoneObjectiveSource` from registry and persisted milestone progress, and PlayMode verifies the real player path through SaveSlot, `Tab`, Hints row selection, Track, outside-school interaction, day-end, reload, and screenshot evidence.

**Tech Stack:** Unity 6000.3.13f1, C#, NUnit EditMode/PlayMode, Unity Input System, Unity MCP `script-update-or-create`/`script-delete`, `git diff --check`, `Scripts/ci/check-no-entity-id-branching.sh`

---

## File Structure

- Modify: `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalUiOwnershipGateTests.cs`
  - Remove `MilestoneHudPanel` type/factory allowlist entries to make a red ownership gate.
- Modify: `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalRuntimeProviderTests.cs`
  - Strengthen milestone source tests for Active, Completed, RewardClaimed, route/recommendation guidance, and Hints category.
- Modify: `Assets/Scripts/UI/Objectives/ObjectiveJournalRuntimeProvider.cs`
  - Ensure milestone source exposes legacy-equivalent progress, state, route/recommendation, and stable key text without ID-specific branching.
- Modify: `Assets/Tests/PlayMode/EndToEnd/OpenEndedMilestoneGrowthE2EScenarioTests.cs`
  - Replace `MilestoneHudPanel` assertions with real Objective Journal Hints row, Track, `TrackedObjectiveHud`, reload, name-based absence checks, and screenshot evidence.
- Modify: `Assets/Scripts/UI/StudentLife/StudentDayRuntimeInstaller.cs`
  - Remove `MilestoneHudPanel.EnsureInScene` and `RefreshMilestoneHud`.
- Modify: `Assets/Scripts/UI/StudentLife/StudentDayEndInteractor.cs`
  - Remove direct `MilestoneHudPanel` refresh after day-end; keep result summary.
- Modify: `Assets/Scripts/UI/Quests/DialoguePanel.cs`
  - Remove `MilestoneHudPanel` hide/show coupling.
- Delete: `Assets/Scripts/UI/StudentLife/MilestoneHudPanel.cs`
  - Delete through Unity MCP after all type references are removed.

## Guardrails

- Do not edit `Assets/**/*.cs` through shell redirection or `apply_patch`; use Unity MCP `script-update-or-create` or `script-delete`.
- Do not add `if (milestone.Id == "...")`, `switch` on IDs, or any entity-specific C# branching.
- Keep `StudentDayResultPanel` milestone summary because it is immediate day-end feedback, not an always-visible objective HUD.
- Do not stage unrelated current working-tree changes:
  - `Assets/AddressableAssetsData/link.xml`
  - `Assets/AddressableAssetsData/link.xml.meta`
  - `production/qa/evidence/registry-provider-town-flow.png`

### Task 1: Red Ownership Gate For Milestone HUD

**Files:**
- Modify: `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalUiOwnershipGateTests.cs`

- [ ] **Step 1: Remove Milestone legacy type allowlist**

Use Unity MCP `script-update-or-create` to remove this entry from `AllowedObjectiveUiTypes`:

```csharp
"Assets/Scripts/UI/StudentLife/MilestoneHudPanel.cs :: MilestoneHudPanel"
```

- [ ] **Step 2: Remove Milestone legacy factory allowlist**

Remove these entries from `AllowedObjectiveUiFactories`:

```csharp
"Assets/Scripts/UI/StudentLife/MilestoneHudPanel.cs :: public static MilestoneHudPanel EnsureInScene(Canvas canvas)",
"Assets/Scripts/UI/StudentLife/MilestoneHudPanel.cs :: var go = new GameObject(\"MilestoneHudPanel\");",
"Assets/Scripts/UI/StudentLife/MilestoneHudPanel.cs :: _root = new GameObject(\"MilestoneHudRoot\", typeof(RectTransform), typeof(ModernUiTileImage));",
"Assets/Scripts/UI/StudentLife/StudentDayRuntimeInstaller.cs :: var hud = MilestoneHudPanel.EnsureInScene(canvas);",
```

- [ ] **Step 3: Run the gate and confirm red**

Run:

```powershell
tests-run -testMode EditMode -testNamespace Rootborn.Tests.EditMode.UI.Objectives -includeMessages true
```

Expected: the ownership gate fails because `MilestoneHudPanel` and `StudentDayRuntimeInstaller` still contain legacy milestone HUD type/factory usages.

### Task 2: Strengthen Milestone Objective Source Tests

**Files:**
- Modify: `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalRuntimeProviderTests.cs`
- Modify: `Assets/Scripts/UI/Objectives/ObjectiveJournalRuntimeProvider.cs`

- [ ] **Step 1: Add failing Active milestone source test**

Add an EditMode test named:

```csharp
MilestoneObjectiveSource_DisplaysActiveStateRoutesAndHintsCategory
```

The test creates:

```csharp
var objective = ScriptableObject.CreateInstance<OutsideSchoolLogMilestoneObjective>();
objective.ConfigureForTests("milestone.objective.outside", "outside-school", 2);
var route = ScriptableObject.CreateInstance<MilestoneRouteDefinition>();
route.ConfigureForTests("milestone.route.outside", objective, "outside.help-neighbor");
var milestone = ScriptableObject.CreateInstance<MilestoneDefinition>();
milestone.ConfigureForTests("milestone.active", "Active Milestone", new MilestoneObjectiveBase[] { objective }, Array.Empty<MilestoneRewardBase>(), new[] { route }, 1);
```

Assert the source output includes:

```csharp
Assert.AreEqual("Hints", items[0].CategoryId);
Assert.AreEqual("Active", items[0].State);
StringAssert.Contains("milestone.objective.outside 0 / 2", items[0].ProgressText);
StringAssert.Contains("outside.help-neighbor", items[0].NextHintText);
```

- [ ] **Step 2: Run the new source test and confirm red**

Run:

```powershell
tests-run -testMode EditMode -testClass Rootborn.Tests.EditMode.UI.Objectives.ObjectiveJournalRuntimeProviderTests -includeMessages true
```

Expected: the new assertion for route/recommendation guidance fails if `NextHintText` only contains the generic fallback.

- [ ] **Step 3: Implement minimal provider source update**

In `ObjectiveJournalRuntimeProvider.MilestoneObjectiveSource`, update `BuildMilestoneHintText` to append route recommendations from `milestone.Routes` when the milestone is active and no reward is pending. Use all non-empty `RecommendedActionId` values and join them with `" | "`.

Required behavior:

```csharp
// Active milestone with routes:
"Use any matching growth route; school is not the only path. Routes: outside.help-neighbor"
```

Do not branch on specific route IDs.

- [ ] **Step 4: Run source tests green**

Run:

```powershell
tests-run -testMode EditMode -testClass Rootborn.Tests.EditMode.UI.Objectives.ObjectiveJournalRuntimeProviderTests -includeMessages true
```

Expected: milestone source tests pass.

### Task 3: Convert Milestone E2E To Objective Journal Flow

**Files:**
- Modify: `Assets/Tests/PlayMode/EndToEnd/OpenEndedMilestoneGrowthE2EScenarioTests.cs`

- [ ] **Step 1: Add Objective Journal dependencies**

Add:

```csharp
using Rootborn.UI.Objectives;
using UnityEngine.InputSystem.LowLevel;
```

Add screenshot path:

```csharp
private const string ScreenshotPath = "Builds/Logs/milestone-objective-journal-e2e.png";
```

- [ ] **Step 2: Replace runtime wait for HUD with Journal/provider/tracked HUD**

`WaitForMilestoneRuntime` must wait for:

```csharp
ObjectiveJournalPanel
ObjectiveJournalRuntimeProvider
TrackedObjectiveHud
```

It must not wait for `MilestoneHudPanel`.

- [ ] **Step 3: Replace initial HUD assertions with real Journal flow**

After runtime setup:

```csharp
yield return PressTab(keyboard);
yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveCategory_Hints"));
StringAssert.Contains("milestone.village-adaptation", runtime.Journal.VisibleText);
StringAssert.Contains("outside.help-neighbor", runtime.Journal.VisibleText);
yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveRow_0"));
yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
Assert.IsTrue(runtime.TrackedHud.IsVisible);
StringAssert.Contains("Milestone", runtime.TrackedHud.VisibleText);
yield return PressTab(keyboard);
```

- [ ] **Step 4: Add Journal wait helper**

Add:

```csharp
private IEnumerator WaitForMilestoneJournalText(Keyboard keyboard, string expected, float timeoutSeconds, string failure)
```

The helper opens Journal with actual `Tab` input, selects `ObjectiveCategory_Hints`, clicks `ObjectiveRow_0`, checks `Journal.VisibleText` or `TrackedHud.VisibleText`, tracks selected row, and closes Journal.

- [ ] **Step 5: Add name-based legacy absence assertion**

Add:

```csharp
private static void AssertNoLegacyMilestoneHudObjects(string message)
{
    Assert.IsNull(GameObject.Find("MilestoneHudPanel"), message + " MilestoneHudPanel exists.");
    Assert.IsNull(GameObject.Find("MilestoneHudRoot"), message + " MilestoneHudRoot exists.");
}
```

Call it after runtime wait and after reload runtime wait.

- [ ] **Step 6: Add screenshot helper and final evidence assertion**

Add:

```csharp
private static void CaptureScreenshot()
{
    Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath));
    if (File.Exists(ScreenshotPath)) File.Delete(ScreenshotPath);
    ScreenCapture.CaptureScreenshot(ScreenshotPath);
}
```

At the end of the E2E, capture and assert the screenshot exists.

- [ ] **Step 7: Run the migrated E2E and confirm red before implementation**

Run:

```powershell
tests-run -testMode PlayMode -testMethod Rootborn.Tests.PlayMode.EndToEnd.OpenEndedMilestoneGrowthE2EScenarioTests.MILESTONE_E2E_001_010_PlayOutsideSchoolMilestoneResultReloadAndDedupe -includeMessages true -includeLogs true -logType Warning
```

Expected before deleting production HUD code: the E2E fails because legacy milestone HUD objects still exist.

### Task 4: Remove Milestone HUD Runtime Coupling

**Files:**
- Modify: `Assets/Scripts/UI/StudentLife/StudentDayRuntimeInstaller.cs`
- Modify: `Assets/Scripts/UI/StudentLife/StudentDayEndInteractor.cs`
- Modify: `Assets/Scripts/UI/Quests/DialoguePanel.cs`

- [ ] **Step 1: Remove StudentDayRuntimeInstaller HUD creation**

Remove:

```csharp
var hud = MilestoneHudPanel.EnsureInScene(canvas);
```

Remove the loop that calls:

```csharp
RefreshMilestoneHud(scene, hud);
```

Delete the `RefreshMilestoneHud` method.

- [ ] **Step 2: Remove StudentDayEndInteractor HUD refresh**

Remove:

```csharp
var hud = UnityEngine.Object.FindFirstObjectByType<MilestoneHudPanel>(FindObjectsInactive.Include);
if (hud != null) hud.Refresh(milestones, milestoneProgress, milestoneApplyResult);
```

Keep milestone progress persistence and `MilestoneDaySummary` for the result panel.

- [ ] **Step 3: Remove DialoguePanel milestone hide/show coupling**

Remove the block that finds `MilestoneHudPanel` and calls `Show()` or `Hide()`.

- [ ] **Step 4: Run compile-focused Objective tests**

Run:

```powershell
tests-run -testMode EditMode -testNamespace Rootborn.Tests.EditMode.UI.Objectives -includeMessages true
```

Expected: compile succeeds, ownership gate still fails only while `MilestoneHudPanel.cs` exists.

### Task 5: Delete MilestoneHudPanel

**Files:**
- Delete: `Assets/Scripts/UI/StudentLife/MilestoneHudPanel.cs`

- [ ] **Step 1: Confirm only name-based references remain**

Run:

```powershell
rg -n "MilestoneHudPanel" Assets/Scripts Assets/Tests -g "*.cs"
```

Expected before deletion: only `MilestoneHudPanel.cs` and name-based E2E cleanup/assertions remain. No production type references remain.

- [ ] **Step 2: Delete with Unity MCP**

Use Unity MCP `script-delete` for:

```text
Assets/Scripts/UI/StudentLife/MilestoneHudPanel.cs
```

- [ ] **Step 3: Run ownership gate green**

Run:

```powershell
tests-run -testMode EditMode -testNamespace Rootborn.Tests.EditMode.UI.Objectives -includeMessages true
```

Expected: objective ownership tests pass.

### Task 6: Full Milestone Verification And Commit

**Files:**
- Verify only

- [ ] **Step 1: Run Objective EditMode tests**

```powershell
tests-run -testMode EditMode -testNamespace Rootborn.Tests.EditMode.UI.Objectives -includeMessages true
```

- [ ] **Step 2: Run Objective PlayMode tests**

```powershell
tests-run -testMode PlayMode -testNamespace Rootborn.Tests.PlayMode.Objectives -includeMessages true -includeLogs true -logType Warning
```

- [ ] **Step 3: Run Milestone E2E**

```powershell
tests-run -testMode PlayMode -testMethod Rootborn.Tests.PlayMode.EndToEnd.OpenEndedMilestoneGrowthE2EScenarioTests.MILESTONE_E2E_001_010_PlayOutsideSchoolMilestoneResultReloadAndDedupe -includeMessages true -includeLogs true -logType Warning
```

- [ ] **Step 4: Verify screenshot evidence**

```powershell
Test-Path Builds/Logs/milestone-objective-journal-e2e.png
```

Expected: `True`

- [ ] **Step 5: Run static gates**

```powershell
git diff --check
C:\Program Files\Git\bin\bash.exe Scripts/ci/check-no-entity-id-branching.sh
```

- [ ] **Step 6: Stage only milestone migration files**

Do not stage unrelated existing changes.

```powershell
git add Assets/Scripts/UI/Objectives/ObjectiveJournalRuntimeProvider.cs `
        Assets/Scripts/UI/Quests/DialoguePanel.cs `
        Assets/Scripts/UI/StudentLife/StudentDayRuntimeInstaller.cs `
        Assets/Scripts/UI/StudentLife/StudentDayEndInteractor.cs `
        Assets/Scripts/UI/StudentLife/MilestoneHudPanel.cs `
        Assets/Scripts/UI/StudentLife/MilestoneHudPanel.cs.meta `
        Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalRuntimeProviderTests.cs `
        Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalUiOwnershipGateTests.cs `
        Assets/Tests/PlayMode/EndToEnd/OpenEndedMilestoneGrowthE2EScenarioTests.cs `
        docs/superpowers/plans/2026-05-20-milestone-hud-objective-journal-migration.md
```

- [ ] **Step 7: Commit**

```powershell
git commit -m "[REFACTOR][UI][TEST] Milestone HUD를 Objective Journal로 이관"
```

## Self-Review

- Spec coverage: This plan implements Phase 1 of `docs/superpowers/specs/2026-05-20-objective-journal-legacy-hud-full-migration-goal.md`.
- Exclusions: Quest Chain and Career Interest migrations remain separate phases after this plan.
- Visual verification: Milestone PlayMode E2E must use actual SaveSlot, keyboard movement, `Tab`, category click, row click, Track click, day-end interaction, reload, and screenshot evidence.
- UI ownership: `MilestoneHudPanel` type/factory and runtime refresh coupling are removed; `TrackedObjectiveHud` remains the only always-visible objective HUD.
- Performance: No polling loop is added to Objective Journal; refresh remains event/open driven.
