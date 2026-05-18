# Integrated Vertical Slice Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a player-facing vertical slice from New Game through Town interaction, multi-domain progress, objective guidance, day-result guidance, House/next-life motivation, save/load, and dedupe verification.

**Architecture:** Add a small data-driven `VerticalSliceSummary` layer that reads existing progress systems and produces `ObjectiveJournalItem` rows plus day-result guidance text. Reuse existing Town, Quest, WorldStateUsage, StudentLife, Objective Journal, SaveSlot, and House data instead of creating a new campaign framework.

**Tech Stack:** Unity 6000.3, C#, NUnit EditMode/PlayMode, Unity Input System, ScriptableObject registry data, Rootborn asmdefs. All `Assets/**/*.cs` changes must be made through Unity MCP `script-update-or-create`, not shell writes.

---

## Files

- Create via Unity MCP: `Assets/Scripts/Game/VerticalSlice/VerticalSliceSummary.cs`
  - Immutable summary model for next objective, changed domains, day-result guide, and follow-up motivation.
- Create via Unity MCP: `Assets/Scripts/Game/VerticalSlice/VerticalSliceSummaryBuilder.cs`
  - Data-driven builder that reads `StudentLifeProgress`, `QuestLog`, `WorldStateUsageProgress`, `EncyclopediaProgress`, career hints, and optional House registry data.
- Modify via Unity MCP: `Assets/Scripts/UI/Objectives/ObjectiveJournalPanel.cs`
  - Add a public method to accept vertical slice summary items without replacing existing category behavior.
- Modify via Unity MCP: `Assets/Scripts/UI/StudentLife/StudentDayResultPanel.cs`
  - Add a test-visible next-guide text accessor and a method to append vertical slice guide text.
- Create via Unity MCP: `Assets/Tests/EditMode/VerticalSlice/VerticalSliceSummaryTests.cs`
  - Covers `VERTICAL-EDIT-001` through `VERTICAL-EDIT-008`.
- Create via Unity MCP: `Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs`
  - Covers `VERTICAL-E2E-001` through `VERTICAL-E2E-010`.
- Do not modify `Assets/Data/Registry/GameDataRegistry.asset` in this first implementation pass. The first slice reads existing registered data and runtime progress only.

## Commands

- Targeted EditMode:
  ```powershell
  # Required implementation-session command through Unity MCP:
  # tests-run mode=EditMode assembly=Rootborn.Tests.EditMode namespace=Rootborn.Tests.EditMode.VerticalSlice
  ```
- Targeted PlayMode:
  ```powershell
  # Required implementation-session command through Unity MCP:
  # tests-run mode=PlayMode assembly=Rootborn.Tests.PlayMode class=Rootborn.Tests.PlayMode.EndToEnd.IntegratedVerticalSliceFoundationE2ETests
  ```
- Entity branching gate:
  ```powershell
  & "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh
  ```

---

### Task 1: Vertical Slice Summary Model

**Files:**
- Create: `Assets/Scripts/Game/VerticalSlice/VerticalSliceSummary.cs`
- Test: `Assets/Tests/EditMode/VerticalSlice/VerticalSliceSummaryTests.cs`

- [ ] **Step 1: Write the failing EditMode tests**

Use Unity MCP `script-update-or-create` to create `Assets/Tests/EditMode/VerticalSlice/VerticalSliceSummaryTests.cs` with a first test class containing these tests:

```csharp
using NUnit.Framework;
using Rootborn.Game.VerticalSlice;

namespace Rootborn.Tests.EditMode.VerticalSlice
{
    public sealed class VerticalSliceSummaryTests
    {
        [Test]
        public void VERTICAL_EDIT_003_SummaryBuildsObjectiveJournalRowsForNextAction()
        {
            var summary = new VerticalSliceSummary(
                "vertical.day-one",
                new[] { "StudentLife", "Quest" },
                "Talk to the guide",
                "Review the town board or visit the library",
                "House: interior route can become a later reward",
                "Day 1 changed StudentLife and Quest progress");

            var items = summary.ToObjectiveJournalItems();

            Assert.AreEqual(1, items.Length);
            Assert.AreEqual("Goals", items[0].CategoryId);
            StringAssert.Contains("Talk to the guide", items[0].Title);
            StringAssert.Contains("library", items[0].NextHintText);
        }

        [Test]
        public void VERTICAL_EDIT_004_SummaryCarriesDayResultAndHouseMotivation()
        {
            var summary = new VerticalSliceSummary(
                "vertical.day-one",
                new[] { "DiscoveryClue", "Career" },
                "Inspect archive table",
                "Open Objective Journal",
                "House: save money for a larger study room",
                "Clue and career hint unlocked");

            StringAssert.Contains("Clue", summary.DayResultGuideText);
            StringAssert.Contains("House", summary.FollowUpMotivationText);
        }
    }
}
```

- [ ] **Step 2: Run the targeted EditMode test and verify RED**

Run the `VerticalSliceSummaryTests` test class through Unity MCP `tests-run` in EditMode.

Expected result: compile/test failure because `Rootborn.Game.VerticalSlice.VerticalSliceSummary` does not exist.

- [ ] **Step 3: Implement the minimal summary model**

Use Unity MCP `script-update-or-create` to create `Assets/Scripts/Game/VerticalSlice/VerticalSliceSummary.cs`:

```csharp
using System;
using Rootborn.UI.Objectives;

namespace Rootborn.Game.VerticalSlice
{
    public readonly struct VerticalSliceSummary
    {
        public readonly string StableKey;
        public readonly string[] ChangedDomainIds;
        public readonly string NextObjectiveText;
        public readonly string NextActionText;
        public readonly string FollowUpMotivationText;
        public readonly string DayResultGuideText;

        public VerticalSliceSummary(string stableKey, string[] changedDomainIds, string nextObjectiveText, string nextActionText, string followUpMotivationText, string dayResultGuideText)
        {
            StableKey = string.IsNullOrEmpty(stableKey) ? "vertical.slice" : stableKey;
            ChangedDomainIds = changedDomainIds ?? Array.Empty<string>();
            NextObjectiveText = nextObjectiveText ?? string.Empty;
            NextActionText = nextActionText ?? string.Empty;
            FollowUpMotivationText = followUpMotivationText ?? string.Empty;
            DayResultGuideText = dayResultGuideText ?? string.Empty;
        }

        public ObjectiveJournalItem[] ToObjectiveJournalItems()
        {
            return new[]
            {
                new ObjectiveJournalItem(
                    StableKey,
                    "Goals",
                    string.IsNullOrEmpty(NextObjectiveText) ? "Next objective" : NextObjectiveText,
                    DayResultGuideText,
                    ChangedDomainIds.Length >= 2 ? "Linked" : "Started",
                    ChangedDomainIds.Length + " domains changed",
                    string.IsNullOrEmpty(NextActionText) ? FollowUpMotivationText : NextActionText,
                    false,
                    0)
            };
        }
    }
}
```

- [ ] **Step 4: Run the targeted EditMode test and verify GREEN**

Expected result: `VerticalSliceSummaryTests` passes.

- [ ] **Step 5: Commit this task**

```powershell
git add -- Assets/Scripts/Game/VerticalSlice/VerticalSliceSummary.cs Assets/Scripts/Game/VerticalSlice/VerticalSliceSummary.cs.meta Assets/Tests/EditMode/VerticalSlice/VerticalSliceSummaryTests.cs Assets/Tests/EditMode/VerticalSlice/VerticalSliceSummaryTests.cs.meta
git commit -m "[FEATURE][TEST] 통합 세로 슬라이스 요약 모델 추가"
```

---

### Task 2: Data-Driven Summary Builder

**Files:**
- Create: `Assets/Scripts/Game/VerticalSlice/VerticalSliceSummaryBuilder.cs`
- Modify: `Assets/Tests/EditMode/VerticalSlice/VerticalSliceSummaryTests.cs`

- [ ] **Step 1: Add failing tests for connected domains, dedupe, and no entity-id branching**

Append these tests to `VerticalSliceSummaryTests`:

```csharp
[Test]
public void VERTICAL_EDIT_002_005_006_BuilderSummarizesTwoDomainsAndIsIdempotent()
{
    var progress = new Rootborn.Game.StudentLife.StudentLifeProgress("slot", "player", 10, 10);
    progress.RecordActivityCompleted("location-activity:self-study", new[] { "growth.focus:+1" });
    progress.UnlockCareerHint(CreateCareer("career.learning"));

    var summary = VerticalSliceSummaryBuilder.Build(progress, questChanged: true, clueChanged: false, worldStateChanged: false, houseMotivation: "House: prepare a study room");
    var duplicate = VerticalSliceSummaryBuilder.Build(progress, questChanged: true, clueChanged: false, worldStateChanged: false, houseMotivation: "House: prepare a study room");

    CollectionAssert.Contains(summary.ChangedDomainIds, "StudentLife");
    CollectionAssert.Contains(summary.ChangedDomainIds, "Quest");
    StringAssert.Contains("House", summary.FollowUpMotivationText);
    Assert.AreEqual(summary.StableKey, duplicate.StableKey);
}

[Test]
public void VERTICAL_EDIT_008_BuilderDoesNotBranchByKnownEntityIds()
{
    var source = System.IO.File.ReadAllText("Assets/Scripts/Game/VerticalSlice/VerticalSliceSummaryBuilder.cs");
    StringAssert.DoesNotContain("career.learning", source);
    StringAssert.DoesNotContain("location-activity:self-study", source);
    StringAssert.DoesNotContain("if (activityId", source);
    StringAssert.DoesNotContain("switch", source);
}

private static Rootborn.Game.StudentLife.CareerDefinition CreateCareer(string id)
{
    var career = UnityEngine.ScriptableObject.CreateInstance<Rootborn.Game.StudentLife.CareerDefinition>();
    career.ConfigureForTests(id, id);
    return career;
}
```

- [ ] **Step 2: Run the targeted EditMode test and verify RED**

Expected result: failure because `VerticalSliceSummaryBuilder` does not exist.

- [ ] **Step 3: Implement the builder without entity-id branching**

Use Unity MCP `script-update-or-create` to create `Assets/Scripts/Game/VerticalSlice/VerticalSliceSummaryBuilder.cs`:

```csharp
using System.Collections.Generic;
using Rootborn.Game.StudentLife;

namespace Rootborn.Game.VerticalSlice
{
    public static class VerticalSliceSummaryBuilder
    {
        public static VerticalSliceSummary Build(StudentLifeProgress progress, bool questChanged, bool clueChanged, bool worldStateChanged, string houseMotivation)
        {
            var domains = new List<string>(4);
            if (progress != null && (progress.GetTodayActivityIds().Length > 0 || progress.GetCareerHintIds().Length > 0))
            {
                domains.Add("StudentLife");
            }

            if (questChanged) domains.Add("Quest");
            if (clueChanged) domains.Add("DiscoveryClue");
            if (worldStateChanged) domains.Add("WorldState");

            string nextObjective = domains.Count >= 2 ? "Choose the next town objective" : "Start a town activity";
            string nextAction = domains.Count >= 2 ? "Open Objective Journal and pick a follow-up route" : "Talk, study, help, or inspect a town object";
            string dayGuide = domains.Count >= 2 ? "Today connected " + string.Join(", ", domains) : "Start the first vertical slice route";
            string followUp = string.IsNullOrEmpty(houseMotivation) ? "House: visit later when a route points there" : houseMotivation;

            return new VerticalSliceSummary("vertical.slice.foundation", domains.ToArray(), nextObjective, nextAction, followUp, dayGuide);
        }
    }
}
```

- [ ] **Step 4: Run targeted EditMode tests and verify GREEN**

Expected result: all `VerticalSliceSummaryTests` pass.

- [ ] **Step 5: Commit this task**

```powershell
git add -- Assets/Scripts/Game/VerticalSlice/VerticalSliceSummaryBuilder.cs Assets/Scripts/Game/VerticalSlice/VerticalSliceSummaryBuilder.cs.meta Assets/Tests/EditMode/VerticalSlice/VerticalSliceSummaryTests.cs
git commit -m "[FEATURE][TEST] 통합 세로 슬라이스 진행 요약 빌더 추가"
```

---

### Task 3: UI Surfaces for Objective Journal and Day Result

**Files:**
- Modify: `Assets/Scripts/UI/Objectives/ObjectiveJournalPanel.cs`
- Modify: `Assets/Scripts/UI/StudentLife/StudentDayResultPanel.cs`
- Modify: `Assets/Tests/EditMode/VerticalSlice/VerticalSliceSummaryTests.cs`

- [ ] **Step 1: Add failing UI binding tests**

Append tests:

```csharp
[Test]
public void VERTICAL_EDIT_003_004_UiSurfacesShowVerticalSliceGuidance()
{
    var panelGo = new UnityEngine.GameObject("ObjectiveJournalPanel", typeof(UnityEngine.RectTransform));
    var dayGo = new UnityEngine.GameObject("StudentDayResultPanel", typeof(UnityEngine.RectTransform));
    try
    {
        var journal = panelGo.AddComponent<Rootborn.UI.Objectives.ObjectiveJournalPanel>();
        var dayResult = dayGo.AddComponent<Rootborn.UI.StudentLife.StudentDayResultPanel>();
        var summary = new VerticalSliceSummary("vertical.slice.foundation", new[] { "StudentLife", "Quest" }, "Choose the next town objective", "Visit the library", "House: prepare a study room", "Today connected StudentLife, Quest");

        journal.SetVerticalSliceSummary(summary);
        journal.Show();
        dayResult.AppendVerticalSliceGuide(summary);

        StringAssert.Contains("Choose the next town objective", journal.VisibleText);
        StringAssert.Contains("House", dayResult.NextGuideTextForTests);
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(panelGo);
        UnityEngine.Object.DestroyImmediate(dayGo);
    }
}
```

- [ ] **Step 2: Run the targeted EditMode test and verify RED**

Expected result: failure because `SetVerticalSliceSummary`, `AppendVerticalSliceGuide`, and `NextGuideTextForTests` do not exist.

- [ ] **Step 3: Add minimal UI methods**

Modify `ObjectiveJournalPanel` via Unity MCP:

```csharp
public void SetVerticalSliceSummary(Rootborn.Game.VerticalSlice.VerticalSliceSummary summary)
{
    SetItems("Goals", summary.ToObjectiveJournalItems());
}
```

Modify `StudentDayResultPanel` via Unity MCP:

```csharp
public string NextGuideTextForTests => _nextGuide != null ? _nextGuide.text : string.Empty;

public void AppendVerticalSliceGuide(Rootborn.Game.VerticalSlice.VerticalSliceSummary summary)
{
    BuildIfNeeded();
    string prefix = _nextGuide != null && !string.IsNullOrEmpty(_nextGuide.text) ? _nextGuide.text + "\n" : string.Empty;
    _nextGuide.text = prefix + summary.FollowUpMotivationText;
}
```

- [ ] **Step 4: Run targeted EditMode tests and verify GREEN**

Expected result: `VerticalSliceSummaryTests` passes.

- [ ] **Step 5: Commit this task**

```powershell
git add -- Assets/Scripts/UI/Objectives/ObjectiveJournalPanel.cs Assets/Scripts/UI/StudentLife/StudentDayResultPanel.cs Assets/Tests/EditMode/VerticalSlice/VerticalSliceSummaryTests.cs
git commit -m "[UI][TEST] 통합 세로 슬라이스 목표 UI 연결"
```

---

### Task 4: Player-Facing Integrated E2E

**Files:**
- Create: `Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs`

- [ ] **Step 1: Write failing PlayMode E2E**

Use Unity MCP `script-update-or-create` to create the E2E class. Base it on `WorldStateUsageLoopE2EScenarioTests`, but add Objective Journal and vertical summary checks.

The test method must be named:

```csharp
[UnityTest]
public IEnumerator VERTICAL_E2E_001_010_NewGameTownUsageObjectiveJournalDayResultHouseMotivationReloadAndDedupe()
```

Core assertions:

```csharp
// Start through save slot UI.
// Confirm SPUM if character creator appears.
// Walk to GuideNpc with keyboard input.
// Interact through E key and click actual dialogue choice button.
// Complete a gather/world-state usage route through keyboard movement and E key.
// Build VerticalSliceSummary from real StudentLife progress and quest/world usage state.
// Feed it into ObjectiveJournalPanel.SetVerticalSliceSummary(summary).
// Open Objective Journal through real Tab input or router toggle if Tab fixture is unstable.
// Assert journal.VisibleText contains "Choose the next town objective".
// Open day result through actual day-end board interaction.
// AppendVerticalSliceGuide(summary).
// Assert resultPanel.NextGuideTextForTests contains "House".
// Save/load through SaveSlot UI.
// Assert reloaded StudentLife/career hint/quest/world usage state is still present.
// Reuse the same one-shot usage and assert count remains 1.
```

- [ ] **Step 2: Run the PlayMode E2E and verify RED**

Expected result before Task 3 is complete: compile failure or missing UI method failure.

Expected result after Task 3 but before E2E helper completion: behavioral failure at one of the explicit vertical checks.

- [ ] **Step 3: Complete the E2E using existing helpers**

Copy helper patterns from `WorldStateUsageLoopE2EScenarioTests`:

- `StartNewTownFromSaveSlotUi`
- `WaitForTownRuntime`
- `WalkPlayerWithKeyboardTo`
- `PressInteractKey`
- `ClickButton`
- `FindButton`
- `FindFirstChoiceButton`

Do not call domain completion APIs directly for player-facing milestones. Direct calls are allowed only for reading persistence state after the player flow has executed.

- [ ] **Step 4: Capture direct visual evidence**

Use the Unity screenshot skill/tool after Objective Journal and day result are visible. Capture either Game View or Camera output and record the screenshot tool result in the verification audit. If the screenshot tool fails, record the exact tool failure and do not mark the visual gate complete.

- [ ] **Step 5: Run targeted PlayMode and verify GREEN**

Expected result: the E2E passes and covers `VERTICAL-E2E-001` through `VERTICAL-E2E-010`.

- [ ] **Step 6: Commit this task**

```powershell
git add -- Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs Assets/Tests/PlayMode/EndToEnd/IntegratedVerticalSliceFoundationE2ETests.cs.meta
git commit -m "[TEST] 통합 세로 슬라이스 플레이 검증 추가"
```

---

### Task 5: Final Verification and Completion Audit

**Files:**
- Create: `docs/superpowers/audits/2026-05-18-integrated-vertical-slice-foundation-verification.md`

- [ ] **Step 1: Run targeted EditMode tests**

Expected: `VerticalSliceSummaryTests` passes with 0 failures.

- [ ] **Step 2: Run targeted PlayMode E2E**

Expected: `IntegratedVerticalSliceFoundationE2ETests` passes with 0 failures.

- [ ] **Step 3: Run entity branching gate**

```powershell
& "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh
```

Expected: `OK: no entity-id branching in system code.`

- [ ] **Step 4: Perform direct visual verification**

Use actual PlayMode flow. Confirm PASS/FAIL:

- New Game was started through UI click.
- SPUM confirm was completed through UI click if the creator appeared.
- Town movement used input, not direct transform assignment.
- Interaction used prompt/input/button path.
- At least two domains changed.
- Objective Journal or tracked HUD showed the next action.
- Day result showed change and next motivation.
- House/Interior or next life goal appeared as follow-up motivation.
- Save/load restored state.
- Repeating the same result did not duplicate rewards or progress.
- Game View or Camera screenshot was captured or inspected.
- Unity Console had no related Error/Exception.

- [ ] **Step 5: Write verification audit**

Create `docs/superpowers/audits/2026-05-18-integrated-vertical-slice-foundation-verification.md` with concrete results filled in for every row:

```markdown
# Integrated Vertical Slice Foundation Verification

## Tests

- EditMode VerticalSliceSummaryTests: PASS/FAIL, command, result.
- PlayMode IntegratedVerticalSliceFoundationE2ETests: PASS/FAIL, command, result.
- Entity branching gate: PASS/FAIL, command, result.

## Direct Visual Verification

- New Game UI: PASS/FAIL.
- SPUM confirm UI: PASS/FAIL.
- Input movement: PASS/FAIL.
- Prompt/input/button interaction: PASS/FAIL.
- Two-domain progress: PASS/FAIL.
- Objective UI next action: PASS/FAIL.
- Day result next motivation: PASS/FAIL.
- House/Interior or next life follow-up: PASS/FAIL.
- Save/load restore: PASS/FAIL.
- Dedupe: PASS/FAIL.
- Screenshot/Game View evidence: PASS/FAIL.
- Console errors: PASS/FAIL.

## Files Changed

- C# runtime files:
- C# test files:
- Asset files:

## Performance Notes

- Registry scan behavior:
- Dirty refresh trigger:
- Remaining optimization work:

## Multiplayer Expansion Risk

- Host+Client verification status:
- Personal/shared state risk:
```

- [ ] **Step 6: Completion audit**

Map each requirement in `docs/superpowers/goals/2026-05-18-integrated-vertical-slice-foundation-goal.md` to evidence from tests, screenshot, audit, and code inspection. Do not mark complete if any item is missing.
