# Quest Chain HUD Objective Journal Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove `QuestChainHudWidget` and route quest-chain objective status through `ObjectiveJournalPanel` plus the single `TrackedObjectiveHud`.

**Architecture:** `QuestChainLogPanel` remains a user-opened/detail management panel because it owns accept, track, filter, and reward claim actions. The compact always-visible quest-chain HUD is removed; quest-chain rows are built by `ObjectiveJournalRuntimeProvider` from runtime `QuestChainLogPanel` state, while `QuestChainRuntimeInstaller` only refreshes the common provider after state changes.

**Tech Stack:** Unity 6000.3.13f1, C#, NUnit EditMode/PlayMode tests, Unity MCP `script-update-or-create` for all `Assets/**/*.cs` edits.

---

## Files

- Modify: `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalUiOwnershipGateTests.cs`
  - Remove `QuestChainHudWidget` from the reviewed objective UI allowlist.
- Modify: `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalRuntimeProviderTests.cs`
  - Add coverage for `QuestChainObjectiveSource` rows, progress text, tracked state, and next-action hint.
- Modify: `Assets/Tests/EditMode/UI/QuestChainLogPanelTests.cs`
  - Replace the legacy HUD widget test with an Objective Journal source test or delete it if covered by the Objective namespace test.
- Modify: `Assets/Tests/PlayMode/EndToEnd/CareerInterestQuestChainStateE2EScenarioTests.cs`
  - Reproduce the player flow through `QuestChainLogPanel`, then open `ObjectiveJournalPanel` with `Tab`, select `Quests`, track a quest-chain row, verify `TrackedObjectiveHud`, save/load, and capture Game View screenshot.
- Modify: `Assets/Scripts/UI/Objectives/ObjectiveJournalRuntimeProvider.cs`
  - Add `QuestChainObjectiveSource` to `BuildSources()`.
  - Source reads `QuestChainLogPanel` runtime state and builds `ObjectiveJournalItem[]`.
- Modify: `Assets/Scripts/UI/Quests/QuestChainLogPanel.cs`
  - Expose read-only `Chains`, `Log`, and a non-mutating selection summary if needed by the source.
- Modify: `Assets/Scripts/UI/Quests/QuestChainRuntimeInstaller.cs`
  - Remove direct `_journal.SetItems("Quests", ...)` and direct `_trackedHud.Show(...)`.
  - Keep `QuestChainLogPanel` setup, but call `ObjectiveJournalRuntimeProvider.RefreshNow()` after quest-chain state changes.
- Delete: `Assets/Scripts/UI/Quests/QuestChainHudWidget.cs`
- Delete: `Assets/Scripts/UI/Quests/QuestChainHudWidget.cs.meta`

## Task 1: Ownership Gate Red Test

**Files:**
- Modify: `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalUiOwnershipGateTests.cs`

- [ ] **Step 1: Remove QuestChainHudWidget allowlist entries**

Use `script-update-or-create` to remove these exact entries:

```csharp
"Assets/Scripts/UI/Quests/QuestChainHudWidget.cs :: QuestChainHudWidget",
"Assets/Scripts/UI/Quests/QuestChainHudWidget.cs :: public static QuestChainHudWidget EnsureInScene(Canvas canvas)",
"Assets/Scripts/UI/Quests/QuestChainHudWidget.cs :: var go = new GameObject(\"QuestChainHudWidget\", typeof(RectTransform));",
"Assets/Scripts/UI/Quests/QuestChainHudWidget.cs :: _root = new GameObject(\"QuestChainHudRoot\", typeof(RectTransform), typeof(ModernUiTileImage));",
```

- [ ] **Step 2: Run the ownership gate and verify RED**

Run:

```text
tests_run testMode=EditMode testNamespace=Rootborn.Tests.EditMode.UI.Objectives
```

Expected: FAIL. The failure must name `Assets/Scripts/UI/Quests/QuestChainHudWidget.cs :: QuestChainHudWidget` and its factories.

## Task 2: Runtime Source Red Test

**Files:**
- Modify: `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalRuntimeProviderTests.cs`
- Modify: `Assets/Scripts/UI/Quests/QuestChainLogPanel.cs`

- [ ] **Step 1: Add a failing QuestChain source test**

Add this test to `ObjectiveJournalRuntimeProviderTests`:

```csharp
[Test]
public void QuestChainObjectiveSource_DisplaysTrackedChainProgressAndNextAction()
{
    var objective = ScriptableObject.CreateInstance<KeyedQuestObjective>();
    objective.Configure("questchain.objective.help", 2, "help");
    var chain = ScriptableObject.CreateInstance<QuestChainDefinition>();
    chain.ConfigureForTests(
        "questchain.source.help",
        "career.helper",
        new[] { objective },
        Array.Empty<QuestRewardBase>(),
        7);

    var canvasGo = new GameObject("QuestChainSourceCanvas", typeof(RectTransform), typeof(Canvas));
    try
    {
        var log = new QuestChainLog(new ScriptableObject[] { chain });
        Assert.IsTrue(log.Accept(chain));
        Assert.IsTrue(log.Track(chain));
        log.RecordEvent(new QuestEvent(QuestEventKind.LocationActivity, "help-1"));

        var panel = QuestChainLogPanel.EnsureInScene(canvasGo.GetComponent<Canvas>());
        panel.Bind(new[] { chain }, log, null, default);

        var items = BuildItemsFromSource("QuestChainObjectiveSource", null, null);

        Assert.AreEqual(1, items.Length);
        Assert.AreEqual("Quests", items[0].CategoryId);
        Assert.AreEqual("Tracked", items[0].State);
        Assert.IsTrue(items[0].IsTracked);
        StringAssert.Contains("questchain.objective.help 1 / 2", items[0].ProgressText);
        StringAssert.Contains("questchain.objective.help", items[0].NextHintText);
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(canvasGo);
        UnityEngine.Object.DestroyImmediate(objective);
        UnityEngine.Object.DestroyImmediate(chain);
    }
}
```

- [ ] **Step 2: Run the test and verify RED**

Run:

```text
tests_run testMode=EditMode testClass=Rootborn.Tests.EditMode.UI.Objectives.ObjectiveJournalRuntimeProviderTests
```

Expected: FAIL because `QuestChainObjectiveSource` does not exist yet.

## Task 3: Move QuestChain Objective Rows Into Provider

**Files:**
- Modify: `Assets/Scripts/UI/Objectives/ObjectiveJournalRuntimeProvider.cs`
- Modify: `Assets/Scripts/UI/Quests/QuestChainLogPanel.cs`

- [ ] **Step 1: Expose read-only QuestChainLogPanel state**

Add these public properties to `QuestChainLogPanel`:

```csharp
public QuestChainDefinition[] Chains => _chains;
public QuestChainLog Log => _log;
```

- [ ] **Step 2: Register the QuestChain source**

Add this line to `ObjectiveJournalRuntimeProvider.BuildSources()` after `QuestLogObjectiveSource`:

```csharp
yield return new QuestChainObjectiveSource();
```

- [ ] **Step 3: Add the QuestChainObjectiveSource nested type**

Add this nested source to `ObjectiveJournalRuntimeProvider`:

```csharp
private sealed class QuestChainObjectiveSource : IObjectiveJournalSource
{
    private static readonly ObjectiveJournalCategory CategoryValue = new ObjectiveJournalCategory("Quests", "Quests");

    public string SourceId => "quest-chain-log";
    public ObjectiveJournalCategory Category => CategoryValue;

    public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
    {
        var panel = FindFirstObjectByType<QuestChainLogPanel>(FindObjectsInactive.Include);
        var chains = panel != null ? panel.Chains : null;
        var log = panel != null ? panel.Log : null;
        if (chains == null || log == null)
        {
            return Array.Empty<ObjectiveJournalItem>();
        }

        var items = new List<ObjectiveJournalItem>();
        for (int i = 0; i < chains.Length; i++)
        {
            var chain = chains[i];
            if (chain == null || string.IsNullOrEmpty(chain.Id))
            {
                continue;
            }

            var state = log.GetState(chain);
            items.Add(new ObjectiveJournalItem(
                "questchain." + chain.Id,
                Category.CategoryId,
                chain.Id,
                chain.RelatedCareerInterestId,
                state.ToString(),
                BuildQuestChainObjectiveText(log, chain),
                BuildQuestChainHintText(log, chain, state),
                log.IsTracked(chain),
                chain.SortPriority));
        }

        return items.ToArray();
    }
}
```

- [ ] **Step 4: Add helper methods beside the source**

Add these helpers in the same nested class:

```csharp
private static string BuildQuestChainObjectiveText(QuestChainLog log, QuestChainDefinition chain)
{
    var step = chain != null && chain.Steps != null && chain.Steps.Length > 0 ? chain.Steps[0] : null;
    var objectives = step != null ? step.Objectives : null;
    if (objectives == null || objectives.Length == 0)
    {
        return chain != null ? log.GetState(chain).ToString() : string.Empty;
    }

    string text = string.Empty;
    for (int i = 0; i < objectives.Length; i++)
    {
        var objective = objectives[i];
        if (objective == null)
        {
            continue;
        }

        string line = SafeText(objective.DisplayKey, objective.GetType().Name) + " " + log.GetObjectiveProgress(chain, i) + " / " + objective.RequiredCount;
        text += string.IsNullOrEmpty(text) ? line : " | " + line;
    }

    return string.IsNullOrEmpty(text) ? log.GetState(chain).ToString() : text;
}

private static string BuildQuestChainHintText(QuestChainLog log, QuestChainDefinition chain, QuestChainState state)
{
    if (state == QuestChainState.Completed)
    {
        return "Claim rewards or review the next route.";
    }

    if (state == QuestChainState.Blocked)
    {
        return log.GetBlockedSummary(chain);
    }

    var step = chain != null && chain.Steps != null && chain.Steps.Length > 0 ? chain.Steps[0] : null;
    var objective = step != null && step.Objectives != null && step.Objectives.Length > 0 ? step.Objectives[0] : null;
    return objective != null ? "Next: " + SafeText(objective.DisplayKey, objective.GetType().Name) : "Open quest chain log.";
}
```

- [ ] **Step 5: Run the source test and verify GREEN**

Run:

```text
tests_run testMode=EditMode testClass=Rootborn.Tests.EditMode.UI.Objectives.ObjectiveJournalRuntimeProviderTests
```

Expected: PASS.

## Task 4: Remove Direct Journal Mutation From Runtime Installer

**Files:**
- Modify: `Assets/Scripts/UI/Quests/QuestChainRuntimeInstaller.cs`

- [ ] **Step 1: Replace direct journal fields**

Replace:

```csharp
private ObjectiveJournalPanel _journal;
private TrackedObjectiveHud _trackedHud;
```

with:

```csharp
private ObjectiveJournalRuntimeProvider _objectiveProvider;
```

- [ ] **Step 2: Bind the common provider instead of directly setting quest-chain rows**

Replace installer setup:

```csharp
_journal = canvas.GetComponentInChildren<ObjectiveJournalPanel>(true);
_trackedHud = canvas.GetComponentInChildren<TrackedObjectiveHud>(true);
if (_journal != null) _journal.BindTrackedHud(_trackedHud);
```

with:

```csharp
_objectiveProvider = canvas.GetComponent<ObjectiveJournalRuntimeProvider>();
```

- [ ] **Step 3: Replace RefreshObjectiveJournal implementation**

Replace the whole method body with:

```csharp
private void RefreshObjectiveJournal()
{
    if (_objectiveProvider != null)
    {
        _objectiveProvider.RefreshNow();
    }
}
```

- [ ] **Step 4: Remove unused direct journal helpers/imports**

Remove direct `ObjectiveJournalPanel`, `TrackedObjectiveHud`, and `ObjectiveJournalItem` usage from `QuestChainRuntimeInstaller`. Keep `using Rootborn.UI.Objectives;` only if `ObjectiveJournalRuntimeProvider` still requires it.

- [ ] **Step 5: Run objective and quest-chain edit tests**

Run:

```text
tests_run testMode=EditMode testNamespace=Rootborn.Tests.EditMode.UI.Objectives
tests_run testMode=EditMode testClass=Rootborn.Tests.EditMode.UI.QuestChainLogPanelTests
```

Expected: Objective namespace PASS except the ownership RED remains until widget deletion; QuestChainLogPanel tests PASS after their legacy HUD assertion is removed or migrated.

## Task 5: Delete QuestChainHudWidget and Update Legacy Tests

**Files:**
- Delete: `Assets/Scripts/UI/Quests/QuestChainHudWidget.cs`
- Delete: `Assets/Scripts/UI/Quests/QuestChainHudWidget.cs.meta`
- Modify: `Assets/Tests/EditMode/UI/QuestChainLogPanelTests.cs`

- [ ] **Step 1: Remove the legacy HUD widget test**

Delete or replace this test:

```csharp
public void QUEST_CHAIN_EDIT_010_HudWidgetShowsOnlyOneTrackedChainCurrentObjective()
```

Do not leave any `QuestChainHudWidget` type references in tests except optional string-based absence assertions in PlayMode E2E.

- [ ] **Step 2: Delete the widget through Unity MCP**

Run:

```text
script-delete files=Assets/Scripts/UI/Quests/QuestChainHudWidget.cs
```

Expected: script and `.meta` are deleted, Unity recompiles successfully.

- [ ] **Step 3: Verify no production references remain**

Run:

```powershell
rg -n "QuestChainHudWidget|QuestChainHudRoot" Assets/Scripts Assets/Tests -g "*.cs"
```

Expected: no production references. Test-only absence assertions are acceptable if they do not reference the type.

## Task 6: PlayMode User Flow Verification

**Files:**
- Modify: `Assets/Tests/PlayMode/EndToEnd/CareerInterestQuestChainStateE2EScenarioTests.cs`

- [ ] **Step 1: Add player-facing Objective Journal assertions**

In `QUEST_CHAIN_E2E_001_014_SaveSlotMovementUiAcceptTrackProgressResultReload`, after accepting and tracking through the existing `QuestChainLogPanel` buttons:

```csharp
yield return PressTab();
yield return ClickButton(FindButton("ObjectiveCategory_Quests"));
yield return WaitForTextContains<ObjectiveJournalPanel>("questchain.test.career-interest");
yield return ClickButton(FindButton("ObjectiveRow_0"));
yield return ClickButton(FindButton("ObjectiveTrackButton"));
AssertNoLegacyQuestChainHudObjects();
AssertTrackedHudContains("questchain");
```

- [ ] **Step 2: Add screenshot evidence**

At the point where the Quest category and tracked HUD are visible, capture:

```csharp
ScreenCapture.CaptureScreenshot("Builds/Logs/quest-chain-objective-journal-e2e.png");
```

Then wait until the file exists before completing the test.

- [ ] **Step 3: Verify save/load restores Journal tracking**

After the existing scene reload path, open the Journal with `Tab`, select `Quests`, and assert the reloaded row still shows the claimed or tracked state and no duplicate reward claim is possible.

- [ ] **Step 4: Run the QuestChain E2E test**

Run:

```text
tests_run testMode=PlayMode testMethod=Rootborn.Tests.PlayMode.EndToEnd.CareerInterestQuestChainStateE2EScenarioTests.QUEST_CHAIN_E2E_001_014_SaveSlotMovementUiAcceptTrackProgressResultReload
```

Expected: PASS, with `Builds/Logs/quest-chain-objective-journal-e2e.png` present.

## Task 7: Final Phase 2 Gates and Commit

**Files:**
- All modified files from this plan.

- [ ] **Step 1: Run static gates**

Run:

```powershell
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

Expected: `diff --check` returns only acceptable line-ending warnings or no output; entity branching check returns `OK`.

- [ ] **Step 2: Commit only QuestChain phase files**

Do not stage unrelated dirty files such as `Assets/AddressableAssetsData/link.xml*` or `production/qa/evidence/registry-provider-town-flow.png`.

Run:

```powershell
git add docs/superpowers/plans/2026-05-20-quest-chain-hud-objective-journal-migration.md `
        Assets/Scripts/UI/Objectives/ObjectiveJournalRuntimeProvider.cs `
        Assets/Scripts/UI/Quests/QuestChainRuntimeInstaller.cs `
        Assets/Scripts/UI/Quests/QuestChainLogPanel.cs `
        Assets/Scripts/UI/Quests/QuestChainHudWidget.cs `
        Assets/Scripts/UI/Quests/QuestChainHudWidget.cs.meta `
        Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalRuntimeProviderTests.cs `
        Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalUiOwnershipGateTests.cs `
        Assets/Tests/EditMode/UI/QuestChainLogPanelTests.cs `
        Assets/Tests/PlayMode/EndToEnd/CareerInterestQuestChainStateE2EScenarioTests.cs
git commit -m "[REFACTOR][UI][TEST] Quest Chain HUD를 Objective Journal로 이관"
```

Expected: one focused Phase 2 commit.

## Self-Review

- Spec coverage: This plan removes the legacy quest-chain compact HUD, keeps the interactive quest-chain log panel classified as user-opened detail UI, routes quest-chain rows through Objective Journal, and verifies PlayMode player flow with screenshot evidence.
- Placeholder scan: No TBD/TODO placeholders remain. Every task has specific files, commands, and expected results.
- Type consistency: `QuestChainObjectiveSource`, `QuestChainLogPanel.Chains`, and `QuestChainLogPanel.Log` are introduced before use by runtime provider tests and implementation.
