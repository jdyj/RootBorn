# Campaign HUD Objective Journal Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `CampaignHudPanel` 상시 HUD를 제거하고 Campaign 진행 표시, row 선택, tracking, reload restore를 `ObjectiveJournalPanel`과 `TrackedObjectiveHud`로 이관한다.

**Architecture:** Campaign 진행 상태와 저장 책임은 `CampaignRuntimeInstaller`에 남기고, 플레이어에게 보이는 objective 정보는 `ObjectiveJournalRuntimeProvider`의 Campaign source가 소유한다. Town E2E는 실제 SaveSlot 진입, 키보드 이동, 상호작용, `Tab` 입력, Campaign category click, row click, Track click, reload 흐름으로 검증한다. Objective UI ownership gate는 Campaign legacy type/factory allowlist를 제거해 회귀를 막는다.

**Tech Stack:** Unity 6000.3.13f1, C#, NUnit EditMode/PlayMode, Unity Input System, Unity MCP `script-update-or-create`/`script-delete`, `git diff --check`, `Scripts/ci/check-no-entity-id-branching.sh`

---

## File Structure

- Modify: `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalUiOwnershipGateTests.cs`
  - Campaign legacy type/factory allowlist를 제거해 red gate를 만든다.
- Modify: `Assets/Tests/PlayMode/EndToEnd/FirstThreeDaysCampaignE2EScenarioTests.cs`
  - `CampaignHudPanel` 탐색을 제거하고 Objective Journal 사용자 흐름으로 campaign 진행을 검증한다.
- Modify: `Assets/Scripts/UI/StudentLife/CampaignRuntimeInstaller.cs`
  - Campaign progress 설치, apply, persistence만 유지하고 별도 campaign canvas/HUD 생성과 refresh 의존을 제거한다.
- Modify: `Assets/Scripts/UI/Quests/DialoguePanel.cs`
  - dialogue open/close 때 `CampaignHudPanel` hide/show 의존을 제거한다.
- Delete: `Assets/Scripts/UI/StudentLife/CampaignHudPanel.cs`
  - 모든 compile-time 참조가 제거된 뒤 Unity MCP `script-delete`로 삭제한다.
- Verify only: `Assets/Scripts/UI/Objectives/ObjectiveJournalRuntimeProvider.cs`
  - Campaign source가 current day, selected route, completed objective, next guide를 이미 제공하는지 확인한다. 누락 시 이 파일 안의 `CampaignObjectiveSource`만 보강한다.

## Guardrails

- `Assets/**/*.cs`는 shell, apply_patch, echo, PowerShell redirection으로 쓰지 않는다. C# 수정과 삭제는 Unity MCP `script-update-or-create`/`script-delete`를 사용한다.
- Campaign/campaign day/route/objective ID별 `if`/`switch` 분기를 추가하지 않는다. 표시와 진행은 `CampaignDefinition`, `CampaignDayDefinition`, `CampaignProgress` 데이터에서 계산한다.
- PlayMode 검증은 실제 사용자 경로를 재현한다. 내부 메서드 직접 호출만으로 완료 처리하지 않는다.
- 화면 변경 검증은 Game View screenshot 파일을 남긴다.

### Task 1: Red Gate For Campaign Legacy HUD Ownership

**Files:**
- Modify: `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalUiOwnershipGateTests.cs`

- [ ] **Step 1: Remove Campaign legacy type allowlist**

Use Unity MCP `script-update-or-create` to rewrite `ObjectiveJournalUiOwnershipGateTests.cs` with these Campaign entries removed from `AllowedObjectiveUiTypes`:

```csharp
"Assets/Scripts/UI/StudentLife/CampaignHudPanel.cs :: CampaignHudPanel",
```

Leave these non-Campaign entries in place:

```csharp
"Assets/Scripts/UI/Objectives/ObjectiveJournalPanel.cs :: ObjectiveJournalPanel",
"Assets/Scripts/UI/Objectives/TrackedObjectiveHud.cs :: TrackedObjectiveHud",
"Assets/Scripts/UI/Quests/QuestLogPanel.cs :: QuestLogPanel",
"Assets/Scripts/UI/Quests/QuestChainLogPanel.cs :: QuestChainLogPanel",
"Assets/Scripts/UI/Quests/QuestChainHudWidget.cs :: QuestChainHudWidget",
"Assets/Scripts/UI/StudentLife/CareerInterestPanel.cs :: CareerInterestPanel",
"Assets/Scripts/UI/StudentLife/CareerInterestHudWidget.cs :: CareerInterestHudWidget",
"Assets/Scripts/UI/StudentLife/MilestoneHudPanel.cs :: MilestoneHudPanel"
```

- [ ] **Step 2: Remove Campaign legacy factory allowlist**

Remove only these Campaign entries from `AllowedObjectiveUiFactories`:

```csharp
"Assets/Scripts/UI/StudentLife/CampaignHudPanel.cs :: public static CampaignHudPanel EnsureInScene(Canvas canvas)",
"Assets/Scripts/UI/StudentLife/CampaignHudPanel.cs :: var go = new GameObject(\"CampaignHudPanel\");",
"Assets/Scripts/UI/StudentLife/CampaignHudPanel.cs :: _root = new GameObject(\"CampaignHudRoot\", typeof(RectTransform), typeof(ModernUiTileImage));",
"Assets/Scripts/UI/StudentLife/CampaignRuntimeInstaller.cs :: _hud = CampaignHudPanel.EnsureInScene(canvas);",
"Assets/Scripts/UI/StudentLife/CampaignRuntimeInstaller.cs :: var canvasGo = new GameObject(\"[CampaignCanvas]\", typeof(RectTransform));",
```

Keep `StudentDayEndInteractor` result summary entries because day result summary is immediate feedback, not always-visible objective HUD:

```csharp
"Assets/Scripts/UI/StudentLife/StudentDayEndInteractor.cs :: go = new GameObject(\"CampaignResultSummary\", typeof(RectTransform), typeof(Text));",
```

- [ ] **Step 3: Run the gate and confirm red**

Run:

```powershell
tests-run -testMode EditMode -testNamespace Rootborn.Tests.EditMode.UI.Objectives -includeMessages true
```

Expected: `OBJECTIVE_UI_GATE_001_ObjectiveLikeHudPanelTypesStayInsideReviewedOwners` and `OBJECTIVE_UI_GATE_002_ObjectiveLikeAlwaysVisibleFactoriesStayReviewed` fail because `CampaignHudPanel` and `CampaignRuntimeInstaller` still contain the removed Campaign HUD usages.

### Task 2: Convert Campaign E2E To Objective Journal Flow

**Files:**
- Modify: `Assets/Tests/PlayMode/EndToEnd/FirstThreeDaysCampaignE2EScenarioTests.cs`

- [ ] **Step 1: Add Objective Journal namespaces and screenshot path**

Add the Objective namespace and screenshot path:

```csharp
using Rootborn.UI.Objectives;
using UnityEngine.InputSystem.LowLevel;
```

Inside the test class:

```csharp
private const string ScreenshotPath = "Builds/Logs/campaign-objective-journal-e2e.png";
```

- [ ] **Step 2: Replace first HUD assertions with actual Journal open/click/track flow**

Replace the initial `CampaignHudPanel` lookup block:

```csharp
var hud = Object.FindFirstObjectByType<CampaignHudPanel>(FindObjectsInactive.Include);
AssertPanelContains(hud.transform, "Town adaptation", "CAMPAIGN-E2E-002 failed: day one HUD omitted today's direction.");
AssertPanelContains(hud.transform, "Library path", "CAMPAIGN-E2E-002 failed: day one HUD omitted alternate route.");
```

with:

```csharp
var runtime = FindRuntime();
yield return PressTab(keyboard);
Assert.IsTrue(runtime.Journal.IsVisible, "CAMPAIGN-E2E-002 failed: actual Tab input did not open Objective Journal.");
yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveCategory_Campaign"));
StringAssert.Contains("Campaign", runtime.Journal.VisibleText, "CAMPAIGN-E2E-002 failed: Campaign category did not display campaign entries.");
StringAssert.Contains("Town adaptation", runtime.Journal.VisibleText, "CAMPAIGN-E2E-002 failed: Campaign Journal omitted today's direction.");
StringAssert.Contains("Library path", runtime.Journal.VisibleText, "CAMPAIGN-E2E-002 failed: Campaign Journal omitted alternate route.");
yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveRow_0"));
yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
Assert.IsTrue(runtime.TrackedHud.IsVisible, "CAMPAIGN-E2E-002 failed: Track click did not update TrackedObjectiveHud.");
StringAssert.Contains("Campaign", runtime.TrackedHud.VisibleText, "CAMPAIGN-E2E-002 failed: tracked HUD did not show campaign text.");
yield return PressTab(keyboard);
Assert.IsFalse(runtime.Journal.IsVisible, "CAMPAIGN-E2E-002 failed: second Tab should close Objective Journal before movement.");
```

- [ ] **Step 3: Replace progress waits that used HUD text**

Replace `WaitForHudText` calls with a helper that opens Journal, selects Campaign, checks text, then closes Journal. For example:

```csharp
yield return WaitForCampaignJournalText(keyboard, "campaign.objective.visit-town", 3f, "CAMPAIGN-E2E-003 failed: actual location interaction did not advance day one campaign objective.");
```

Use the same helper for:

```csharp
"Find growth"
"campaign.objective.school-free-growth"
"Connect future"
"campaign.objective.career-hint"
```

- [ ] **Step 4: Add Objective Journal runtime wait**

Replace `WaitForCampaignRuntime` internals so it waits for player, campaign progress, `ObjectiveJournalPanel`, `ObjectiveJournalRuntimeProvider`, `TrackedObjectiveHud`, and `EventSystem`:

```csharp
private static IEnumerator WaitForCampaignRuntime(float timeoutSeconds)
{
    float elapsed = 0f;
    string lastState = string.Empty;
    while (elapsed < timeoutSeconds)
    {
        var player = GameObject.Find("Player");
        var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
        var provider = Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
        var trackedHud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
        lastState = "scene=" + SceneManager.GetActiveScene().name
            + " player=" + (player != null)
            + " controller=" + (player != null && player.GetComponent<PlayerController>() != null)
            + " router=" + (player != null && player.GetComponent<PlayerInteractionRouter>() != null)
            + " student=" + (player != null && player.GetComponent<StudentLifeProgressComponent>() != null)
            + " campaign=" + (player != null && player.GetComponent<CampaignProgressComponent>() != null)
            + " journal=" + (journal != null)
            + " provider=" + (provider != null)
            + " trackedHud=" + (trackedHud != null)
            + " eventSystem=" + (EventSystem.current != null);
        if (player != null
            && player.GetComponent<PlayerController>() != null
            && player.GetComponent<PlayerInteractionRouter>() != null
            && player.GetComponent<StudentLifeProgressComponent>() != null
            && player.GetComponent<CampaignProgressComponent>() != null
            && journal != null
            && provider != null
            && trackedHud != null
            && EventSystem.current != null)
        {
            yield break;
        }

        elapsed += Time.unscaledDeltaTime;
        yield return null;
    }

    Assert.Fail("CAMPAIGN-E2E runtime did not expose player, campaign progress, Objective Journal, tracked HUD, and UI event system within timeout. Last state: " + lastState);
}
```

- [ ] **Step 5: Add actual Tab input helper**

Add:

```csharp
private IEnumerator PressTab(Keyboard keyboard)
{
    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
    InputSystem.Update();
    yield return null;
    yield return new WaitForFixedUpdate();
    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
    InputSystem.Update();
    yield return null;
}
```

- [ ] **Step 6: Add Campaign Journal assertion helper**

Add:

```csharp
private IEnumerator WaitForCampaignJournalText(Keyboard keyboard, string expected, float timeoutSeconds, string failure)
{
    float elapsed = 0f;
    while (elapsed < timeoutSeconds)
    {
        var runtime = FindRuntime();
        if (!runtime.Journal.IsVisible)
        {
            yield return PressTab(keyboard);
        }

        yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveCategory_Campaign"));
        yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveRow_0"));
        if (runtime.Journal.VisibleText.Contains(expected) || runtime.TrackedHud.VisibleText.Contains(expected))
        {
            yield return ClickButton(FindButton(runtime.Journal.transform, "ObjectiveTrackButton"));
            if (runtime.Journal.IsVisible)
            {
                yield return PressTab(keyboard);
            }
            yield break;
        }

        if (runtime.Journal.IsVisible)
        {
            yield return PressTab(keyboard);
        }

        elapsed += Time.unscaledDeltaTime;
        yield return null;
    }

    Assert.Fail(failure);
}
```

- [ ] **Step 7: Extend `RuntimeState`**

Replace the existing `RuntimeState` with:

```csharp
private readonly struct RuntimeState
{
    public readonly GameObject Player;
    public readonly PlayerInteractionRouter Router;
    public readonly StudentDayEndInteractor DayEnd;
    public readonly ObjectiveJournalPanel Journal;
    public readonly TrackedObjectiveHud TrackedHud;

    public RuntimeState(GameObject player, PlayerInteractionRouter router, StudentDayEndInteractor dayEnd, ObjectiveJournalPanel journal, TrackedObjectiveHud trackedHud)
    {
        Player = player;
        Router = router;
        DayEnd = dayEnd;
        Journal = journal;
        TrackedHud = trackedHud;
    }
}
```

Update `FindRuntime()`:

```csharp
private static RuntimeState FindRuntime()
{
    var player = GameObject.Find("Player");
    var dayEnd = GameObject.Find("StudentDayEndBoard");
    var journal = Object.FindFirstObjectByType<ObjectiveJournalPanel>(FindObjectsInactive.Include);
    var trackedHud = Object.FindFirstObjectByType<TrackedObjectiveHud>(FindObjectsInactive.Include);
    Assert.IsNotNull(player);
    Assert.IsNotNull(dayEnd);
    Assert.IsNotNull(journal);
    Assert.IsNotNull(trackedHud);
    return new RuntimeState(player, player.GetComponent<PlayerInteractionRouter>(), dayEnd.GetComponent<StudentDayEndInteractor>(), journal, trackedHud);
}
```

- [ ] **Step 8: Replace reload HUD assertion**

Replace:

```csharp
var reloadedHud = Object.FindFirstObjectByType<CampaignHudPanel>(FindObjectsInactive.Include);
AssertPanelContains(reloadedHud.transform, "campaign.objective.visit-town", "CAMPAIGN-E2E-009 failed: campaign objective did not survive reload.");
```

with:

```csharp
var reloaded = FindRuntime();
yield return PressTab(keyboard);
yield return ClickButton(FindButton(reloaded.Journal.transform, "ObjectiveCategory_Campaign"));
yield return ClickButton(FindButton(reloaded.Journal.transform, "ObjectiveRow_0"));
StringAssert.Contains("campaign.objective.visit-town", reloaded.Journal.VisibleText, "CAMPAIGN-E2E-009 failed: campaign objective did not survive reload in Objective Journal.");
Assert.IsTrue(reloaded.TrackedHud.IsVisible, "CAMPAIGN-E2E-009 failed: tracked campaign objective was not restored after reload and Journal refresh.");
StringAssert.Contains("Campaign", reloaded.TrackedHud.VisibleText, "CAMPAIGN-E2E-009 failed: restored tracked HUD did not show campaign text.");
CaptureScreenshot();
for (int i = 0; i < 5; i++) yield return null;
Assert.IsTrue(File.Exists(ScreenshotPath), "CAMPAIGN-E2E-011 failed: Game View screenshot evidence was not written.");
```

- [ ] **Step 9: Add screenshot helper**

Add:

```csharp
private static void CaptureScreenshot()
{
    Directory.CreateDirectory(Path.GetDirectoryName(ScreenshotPath));
    if (File.Exists(ScreenshotPath)) File.Delete(ScreenshotPath);
    ScreenCapture.CaptureScreenshot(ScreenshotPath);
}
```

- [ ] **Step 10: Run Campaign E2E and confirm red or partial red**

Run:

```powershell
tests-run -testMode PlayMode -testClass Rootborn.Tests.PlayMode.EndToEnd.FirstThreeDaysCampaignE2EScenarioTests -includeMessages true -includeLogs true -logType Error
```

Expected before implementation: compile may fail from `CampaignHudPanel` references still present in production code after later deletion is not done, or the E2E may pass while the ownership gate remains red. Continue to Task 3 because the red ownership gate is the migration blocker.

### Task 3: Remove Campaign HUD Creation From Runtime Installer

**Files:**
- Modify: `Assets/Scripts/UI/StudentLife/CampaignRuntimeInstaller.cs`

- [ ] **Step 1: Remove HUD field and canvas creation from runner startup**

Remove:

```csharp
private CampaignHudPanel _hud;
```

Remove this block from `Runner.Start()`:

```csharp
var canvas = EnsureCanvas(_scene);
EnsureEventSystem(_scene);
_hud = CampaignHudPanel.EnsureInScene(canvas);
```

Replace it with:

```csharp
EnsureEventSystem(_scene);
```

- [ ] **Step 2: Replace `Refresh()` with state sync only**

Replace the current `Refresh()` method:

```csharp
private void Refresh()
{
    if (_hud == null) return;
    if (_student != null && _campaignProgress != null) _campaignProgress.Progress.SetCurrentDayNumber(_student.EnsureProgress().CurrentDay);
    _hud.Refresh(CampaignSummaryBuilder.BuildHudSummary(_campaign, _campaignProgress != null ? _campaignProgress.Progress : null));
}
```

with:

```csharp
private void Refresh()
{
    if (_student != null && _campaignProgress != null)
    {
        _campaignProgress.Progress.SetCurrentDayNumber(_student.EnsureProgress().CurrentDay);
    }
}
```

Rationale: `ModernUiPanelInputRouter.ToggleObjectiveJournalPanel()` calls `ObjectiveJournalRuntimeProvider.RefreshNow()` immediately before showing the Journal. This keeps CampaignRuntimeInstaller free of a runtime `FindFirstObjectByType` dependency and avoids per-frame polling.

- [ ] **Step 3: Remove unused canvas helper and imports**

Delete `EnsureCanvas(Scene scene)` from `CampaignRuntimeInstaller.cs` because the installer no longer creates `[CampaignCanvas]`.

Remove unused imports that become unnecessary:

```csharp
using UnityEngine.UI;
```

Keep these imports because the installer still creates an EventSystem if needed:

```csharp
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
```

- [ ] **Step 4: Run targeted compile/test**

Run:

```powershell
tests-run -testMode EditMode -testNamespace Rootborn.Tests.EditMode.UI.Objectives -includeMessages true
```

Expected: the gate still fails only if `CampaignHudPanel.cs` or another Campaign HUD factory remains. It should no longer report `_hud = CampaignHudPanel.EnsureInScene(canvas);` or `[CampaignCanvas]`.

### Task 4: Remove DialoguePanel Campaign HUD Hide/Show Coupling

**Files:**
- Modify: `Assets/Scripts/UI/Quests/DialoguePanel.cs`

- [ ] **Step 1: Remove Campaign hide/show block**

In `SetBackgroundHudsVisible(bool visible)`, delete only this Campaign block:

```csharp
var campaign = UnityEngine.Object.FindFirstObjectByType<CampaignHudPanel>(FindObjectsInactive.Include);
if (campaign != null)
{
    if (visible) campaign.Show(); else campaign.Hide();
}
```

Keep the Milestone block until the Milestone migration plan removes it:

```csharp
var milestone = UnityEngine.Object.FindFirstObjectByType<MilestoneHudPanel>(FindObjectsInactive.Include);
if (milestone != null)
{
    if (visible) milestone.Show(); else milestone.Hide();
}
```

- [ ] **Step 2: Run compile-focused Objective tests**

Run:

```powershell
tests-run -testMode EditMode -testNamespace Rootborn.Tests.EditMode.UI.Objectives -includeMessages true
```

Expected: no compile error from `DialoguePanel` after Campaign HUD removal.

### Task 5: Delete CampaignHudPanel Script

**Files:**
- Delete: `Assets/Scripts/UI/StudentLife/CampaignHudPanel.cs`

- [ ] **Step 1: Confirm no compile-time references remain**

Run:

```powershell
rg -n "CampaignHudPanel" Assets/Scripts Assets/Tests -g "*.cs"
```

Expected remaining matches before deletion: only `Assets/Scripts/UI/StudentLife/CampaignHudPanel.cs` and the failing ownership gate message if Task 1 is not fully applied. No production or test file should reference the type.

- [ ] **Step 2: Delete with Unity MCP**

Use Unity MCP `script-delete`:

```text
Assets/Scripts/UI/StudentLife/CampaignHudPanel.cs
```

- [ ] **Step 3: Refresh and run gate**

Run:

```powershell
tests-run -testMode EditMode -testNamespace Rootborn.Tests.EditMode.UI.Objectives -includeMessages true
```

Expected: `Rootborn.Tests.EditMode.UI.Objectives` passes. The Campaign legacy type/factory must not appear in failure output.

### Task 6: Verify Campaign Source Coverage

**Files:**
- Verify/Modify: `Assets/Scripts/UI/Objectives/ObjectiveJournalRuntimeProvider.cs`
- Test: `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalRuntimeProviderTests.cs`

- [ ] **Step 1: Run existing Campaign source test**

Run:

```powershell
tests-run -testMode EditMode -testClass Rootborn.Tests.EditMode.UI.Objectives.ObjectiveJournalRuntimeProviderTests -includeMessages true
```

Expected: `CampaignObjectiveSource_DisplaysCurrentDaySelectedRouteCompletedObjectiveAndNextGuide` passes.

- [ ] **Step 2: If the test does not cover tracked HUD text, add this assertion to the existing Campaign test**

Inside `CampaignObjectiveSource_DisplaysCurrentDaySelectedRouteCompletedObjectiveAndNextGuide`, after `var items = BuildItemsFromSource("CampaignObjectiveSource", player, null);`, assert the `ObjectiveJournalItem` contains the same information the legacy HUD showed:

```csharp
Assert.AreEqual("Campaign", items[0].CategoryId);
StringAssert.Contains("Campaign day 2", items[0].ProgressText);
StringAssert.Contains("Library route", items[0].ProgressText);
StringAssert.Contains("visit-library", items[0].ProgressText);
StringAssert.Contains("talk-mentor", items[0].ProgressText);
StringAssert.Contains("Talk to a mentor", items[0].NextActionText);
```

Use the actual objective keys configured in that test fixture. Do not add ID-specific production branches to satisfy the assertion.

- [ ] **Step 3: Run the source test again**

Run:

```powershell
tests-run -testMode EditMode -testClass Rootborn.Tests.EditMode.UI.Objectives.ObjectiveJournalRuntimeProviderTests -includeMessages true
```

Expected: pass.

### Task 7: Full Campaign PlayMode Verification

**Files:**
- Test: `Assets/Tests/PlayMode/EndToEnd/FirstThreeDaysCampaignE2EScenarioTests.cs`

- [ ] **Step 1: Run the migrated Campaign E2E**

Run:

```powershell
tests-run -testMode PlayMode -testClass Rootborn.Tests.PlayMode.EndToEnd.FirstThreeDaysCampaignE2EScenarioTests -includeMessages true -includeLogs true -logType Error
```

Expected: pass.

- [ ] **Step 2: Verify screenshot evidence exists**

Run:

```powershell
Test-Path Builds/Logs/campaign-objective-journal-e2e.png
```

Expected:

```text
True
```

- [ ] **Step 3: Confirm legacy HUD is absent during PlayMode flow**

Add this assertion after `WaitForCampaignRuntime(10f)` and after reload `WaitForCampaignRuntime(10f)`:

```csharp
Assert.IsNull(Object.FindFirstObjectByType<CampaignHudPanel>(FindObjectsInactive.Include), "CAMPAIGN-E2E-011 failed: CampaignHudPanel must not exist after migration.");
```

Because `CampaignHudPanel` is deleted in Task 5, replace the type assertion with a name-based assertion:

```csharp
Assert.IsNull(GameObject.Find("CampaignHudPanel"), "CAMPAIGN-E2E-011 failed: CampaignHudPanel GameObject must not exist after migration.");
Assert.IsNull(GameObject.Find("CampaignHudRoot"), "CAMPAIGN-E2E-011 failed: CampaignHudRoot GameObject must not exist after migration.");
Assert.IsNull(GameObject.Find("[CampaignCanvas]"), "CAMPAIGN-E2E-011 failed: legacy Campaign canvas must not exist after migration.");
```

Run the E2E again and expect pass.

### Task 8: Regression Suite And Static Checks

**Files:**
- Verify only

- [ ] **Step 1: Run Objective EditMode tests**

Run:

```powershell
tests-run -testMode EditMode -testNamespace Rootborn.Tests.EditMode.UI.Objectives -includeMessages true
```

Expected: pass.

- [ ] **Step 2: Run Objective PlayMode tests**

Run:

```powershell
tests-run -testMode PlayMode -testNamespace Rootborn.Tests.PlayMode.Objectives -includeMessages true -includeLogs true -logType Error
```

Expected: pass.

- [ ] **Step 3: Run Town Objective Journal E2E**

Run:

```powershell
tests-run -testMode PlayMode -testClass Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalTownE2EScenarioTests -includeMessages true -includeLogs true -logType Error
```

Expected: pass and `Builds/Logs/objective-journal-town-e2e.png` exists.

- [ ] **Step 4: Run Campaign E2E**

Run:

```powershell
tests-run -testMode PlayMode -testClass Rootborn.Tests.PlayMode.EndToEnd.FirstThreeDaysCampaignE2EScenarioTests -includeMessages true -includeLogs true -logType Error
```

Expected: pass and `Builds/Logs/campaign-objective-journal-e2e.png` exists.

- [ ] **Step 5: Run entity branching gate**

Run:

```powershell
C:\Program Files\Git\bin\bash.exe Scripts/ci/check-no-entity-id-branching.sh
```

Expected: pass.

- [ ] **Step 6: Run whitespace check**

Run:

```powershell
git diff --check
```

Expected: no whitespace errors. CRLF warnings are acceptable only if they are warnings from unchanged Unity-generated line endings.

### Task 9: Commit

**Files:**
- Modified/deleted files from Tasks 1-8 only

- [ ] **Step 1: Inspect working tree**

Run:

```powershell
git status --short
```

Expected: Campaign migration files are listed. Existing unrelated deletions under `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta` must remain unstaged.

- [ ] **Step 2: Stage only Campaign migration files**

Run:

```powershell
git add Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalUiOwnershipGateTests.cs Assets/Tests/PlayMode/EndToEnd/FirstThreeDaysCampaignE2EScenarioTests.cs Assets/Scripts/UI/StudentLife/CampaignRuntimeInstaller.cs Assets/Scripts/UI/Quests/DialoguePanel.cs Assets/Scripts/UI/StudentLife/CampaignHudPanel.cs Assets/Scripts/UI/StudentLife/CampaignHudPanel.cs.meta
```

If the `.meta` file was already removed by Unity MCP, `git add -u Assets/Scripts/UI/StudentLife` is acceptable after confirming it stages only Campaign HUD removal.

- [ ] **Step 3: Commit with Korean prefix**

Run:

```powershell
git commit -m "[REFACTOR][UI][TEST] Campaign HUD를 Objective Journal로 이관"
```

Expected: commit succeeds.

## Self-Review

- Spec coverage: This plan implements the Campaign HUD section of `docs/superpowers/specs/2026-05-19-objective-journal-legacy-hud-migration-design.md`. Milestone, Quest Chain, and Career Interest are intentionally excluded and should each receive a separate implementation plan.
- UI ownership: `CampaignHudPanel` type/factory and `[CampaignCanvas]` creation are removed; `TrackedObjectiveHud` remains the only always-visible objective HUD.
- Player-facing verification: The E2E uses SaveSlot UI, keyboard movement, interaction key, `Tab`, category click, row click, Track click, reload, and Game View screenshot.
- Performance: No `Update()` polling, per-frame source scans, or runtime string-building loops are added. Journal refresh remains tied to opening the panel.
