# Career Interest HUD Objective Journal Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the always-visible `CareerInterestHudWidget` and surface career interest guidance through `ObjectiveJournalPanel` with `TrackedObjectiveHud` as the only compact tracked objective HUD.

**Architecture:** `CareerInterestProgressComponent` remains the runtime owner of selected interest state. `ObjectiveJournalRuntimeProvider` adds a `CareerInterestObjectiveSource` in the `Hints` category that reads the player components, builds `CareerInterestSummary` rows, and marks the selected interest as tracked. `CareerInterestRuntimeInstaller` still installs the interactive `CareerInterestPanel`, but refreshes the Objective Journal provider instead of creating its own HUD.

**Tech Stack:** Unity 6000.3.13f1, C#, NUnit EditMode/PlayMode tests, Unity MCP `script-update-or-create`/`script-delete`, Objective Journal UI.

---

### Task 1: RED Ownership And Source Tests

**Files:**
- Modify: `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalUiOwnershipGateTests.cs`
- Modify: `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalRuntimeProviderTests.cs`

- [ ] **Step 1: Remove legacy Career Interest HUD allowlist entries**

Remove these allowlist rows from `ObjectiveJournalUiOwnershipGateTests.cs`:

```csharp
"Assets/Scripts/UI/StudentLife/CareerInterestHudWidget.cs :: CareerInterestHudWidget"
"Assets/Scripts/UI/StudentLife/CareerInterestRuntimeInstaller.cs :: _hud = CareerInterestHudWidget.EnsureInScene(canvas);"
"Assets/Scripts/UI/StudentLife/CareerInterestHudWidget.cs :: public static CareerInterestHudWidget EnsureInScene(Canvas canvas)"
```

- [ ] **Step 2: Add failing provider test**

Add this test to `ObjectiveJournalRuntimeProviderTests.cs`:

```csharp
[Test]
public void CareerInterestObjectiveSource_DisplaysSelectedInterestRecommendationsInHintsCategory()
{
    var candidate = ScriptableObject.CreateInstance<CareerCandidateDefinition>();
    var recommendation = ScriptableObject.CreateInstance<CareerInterestStaticRecommendation>();
    var interest = ScriptableObject.CreateInstance<CareerInterestDefinition>();
    var player = new GameObject("CareerInterestObjectivePlayer");

    try
    {
        candidate.ConfigureForTests("candidate.learning", "candidate.learning", "desc.candidate.learning", null, null, new[] { "candidate.route.study" }, null, null, null, 1, 2, 3);
        recommendation.ConfigureForTests(new[] { "action.visit-library" });
        interest.ConfigureForTests("interest.learning", "Learning Interest", "desc.interest.learning", candidate, null, null, null, null, new CareerInterestRecommendationBase[] { recommendation }, null);

        var student = player.AddComponent<StudentLifeProgressComponent>();
        student.ConfigureForTests("career-interest-slot", "player", 10, 10, 0, 8 * 60);
        var candidates = player.AddComponent<CareerCandidateProgressComponent>();
        candidates.Bind(new[] { candidate }, Array.Empty<CareerHintDefinition>());
        var interests = player.AddComponent<CareerInterestProgressComponent>();
        interests.Bind(new[] { interest });
        Assert.IsTrue(interests.Progress.TrySelect(interest, candidates.Progress, student.Progress, student.Progress.CurrentDay, "test-select").Applied);

        var items = BuildItemsFromSource("CareerInterestObjectiveSource", player, null);

        Assert.AreEqual(1, items.Length);
        Assert.AreEqual("Hints", items[0].CategoryId);
        Assert.AreEqual("Selected", items[0].State);
        Assert.IsTrue(items[0].IsTracked);
        StringAssert.Contains("interest.learning", items[0].StableKey);
        StringAssert.Contains("action.visit-library", items[0].NextHintText);
        StringAssert.Contains("candidate.route.study", items[0].NextHintText);
    }
    finally
    {
        UnityEngine.Object.DestroyImmediate(player);
        UnityEngine.Object.DestroyImmediate(candidate);
        UnityEngine.Object.DestroyImmediate(recommendation);
        UnityEngine.Object.DestroyImmediate(interest);
    }
}
```

- [ ] **Step 3: Run RED tests**

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
```

Expected: FAIL. The ownership gate should report `CareerInterestHudWidget`, and the source test should fail because `CareerInterestObjectiveSource` does not exist yet.

### Task 2: Objective Journal Source And Runtime Installer

**Files:**
- Modify: `Assets/Scripts/UI/Objectives/ObjectiveJournalRuntimeProvider.cs`
- Modify: `Assets/Scripts/UI/StudentLife/CareerInterestRuntimeInstaller.cs`
- Delete: `Assets/Scripts/UI/StudentLife/CareerInterestHudWidget.cs`

- [ ] **Step 1: Add source registration**

In `ObjectiveJournalRuntimeProvider.BuildSources()`, add:

```csharp
yield return new CareerInterestObjectiveSource();
```

- [ ] **Step 2: Implement `CareerInterestObjectiveSource`**

Add a nested source that:

```csharp
private sealed class CareerInterestObjectiveSource : IObjectiveJournalSource
{
    private static readonly ObjectiveJournalCategory CategoryValue = new ObjectiveJournalCategory("Hints", "Hints");

    public string SourceId => "career-interests";
    public ObjectiveJournalCategory Category => CategoryValue;

    public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
    {
        var interestsComponent = context.Player != null ? context.Player.GetComponent<CareerInterestProgressComponent>() : null;
        var interests = interestsComponent != null ? interestsComponent.Interests : null;
        if (interestsComponent == null || interests == null || interests.Length == 0)
        {
            return Array.Empty<ObjectiveJournalItem>();
        }

        var student = context.Player.GetComponent<StudentLifeProgressComponent>()?.Progress;
        var candidates = context.Player.GetComponent<CareerCandidateProgressComponent>()?.Progress;
        var progress = interestsComponent.EnsureProgress();
        int day = student != null ? student.CurrentDay : 0;
        var items = new List<ObjectiveJournalItem>();

        for (int i = 0; i < interests.Length; i++)
        {
            var interest = interests[i];
            if (interest == null || string.IsNullOrEmpty(interest.Id))
            {
                continue;
            }

            var summary = CareerInterestSummaryBuilder.Build(interest, progress, candidates, student, day);
            bool selected = progress.CurrentInterestId == interest.Id;
            items.Add(new ObjectiveJournalItem(
                "career-interest." + interest.Id,
                Category.CategoryId,
                summary.DisplayName,
                summary.Description,
                summary.State.ToString(),
                BuildCareerInterestProgressText(summary, progress),
                BuildCareerInterestHintText(summary),
                selected,
                selected ? 10 : 40 + i));
        }

        return items.ToArray();
    }
}
```

Add helpers in the same class:

```csharp
private static string BuildCareerInterestProgressText(CareerInterestSummary summary, CareerInterestProgress progress)
private static string BuildCareerInterestHintText(CareerInterestSummary summary)
private static string JoinNonEmpty(string[] values, string separator)
```

`BuildCareerInterestHintText` must include both selected recommendations and candidate recommendations returned by `CareerInterestSummaryBuilder`.

- [ ] **Step 3: Replace runtime HUD refresh with provider refresh**

In `CareerInterestRuntimeInstaller.cs`:

```csharp
using Rootborn.Game.Player;
using Rootborn.UI.Modern;
using Rootborn.UI.Objectives;
```

Replace `_hud` with:

```csharp
private ObjectiveJournalRuntimeProvider _objectiveProvider;
```

After `var canvas = EnsureCanvas();`, install/bind common objective UI:

```csharp
ModernUiPanelAutoInstaller.InstallOnCanvas(canvas, player.GetComponent<PlayerInventory>());
_objectiveProvider = canvas.GetComponent<ObjectiveJournalRuntimeProvider>();
```

Replace `RefreshHud` with:

```csharp
private void RefreshObjectiveJournal()
{
    if (_objectiveProvider != null)
    {
        _objectiveProvider.RefreshNow();
    }
}
```

Subscribe `SelectionChanged` to `RefreshObjectiveJournal`, call `RefreshObjectiveJournal()` after panel open, and remove all calls to `CareerInterestHudWidget.EnsureInScene`.

- [ ] **Step 4: Delete legacy HUD script**

Run:

```powershell
script_delete files="Assets/Scripts/UI/StudentLife/CareerInterestHudWidget.cs"
```

- [ ] **Step 5: Run GREEN EditMode tests**

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
```

Expected: PASS.

### Task 3: Player-Facing PlayMode Migration

**Files:**
- Modify: `Assets/Tests/PlayMode/EndToEnd/CareerInterestSelectionLoopE2EScenarioTests.cs`

- [ ] **Step 1: Remove direct legacy HUD setup**

Remove:

```csharp
var interestHud = CareerInterestHudWidget.EnsureInScene(EnsureCanvas());
```

Remove manual `interestHud.Refresh(...)` assertions and replace them with Objective Journal assertions.

- [ ] **Step 2: Open Objective Journal through player input**

After selecting `interest.learning`, press Tab and click the Hints category:

```csharp
yield return PressTabKey(keyboard);
var provider = Object.FindFirstObjectByType<ObjectiveJournalRuntimeProvider>(FindObjectsInactive.Include);
Assert.IsNotNull(provider, "CAREER-INTEREST-E2E-007 failed: Objective Journal provider missing.");
yield return ClickButton(FindButton(provider.Panel.transform, "ObjectiveCategory_Hints"));
StringAssert.Contains("interest.learning", provider.Panel.VisibleText);
StringAssert.Contains("action.visit-library", provider.Panel.VisibleText);
```

- [ ] **Step 3: Track selected interest from Objective Journal**

Click the row containing `career-interest.interest.learning`, click `ObjectiveTrackButton`, and assert `TrackedObjectiveHud.VisibleText` contains `interest.learning` and `action.visit-library`.

- [ ] **Step 4: Add visible evidence and legacy absence checks**

Capture a Game View screenshot to:

```text
Builds/Logs/career-interest-objective-journal-e2e.png
```

Assert no runtime objects named `CareerInterestHudWidget` or `CareerInterestHudRoot` exist before the first reload and after the final reload.

- [ ] **Step 5: Run Career Interest PlayMode scenario**

Run:

```powershell
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.CareerInterestSelectionLoopE2EScenarioTests.CAREER_INTEREST_E2E_001_013_SaveSlotMovementLocationUiInterestSelectionQuestResultReloadAndDedupe
```

Expected: PASS and the screenshot file exists with non-zero length.

### Task 4: Static Gates And Commit

**Files:**
- Verify all touched files.

- [ ] **Step 1: Search for legacy Career Interest HUD references**

Run:

```powershell
rg -n "CareerInterestHudWidget|CareerInterestHudRoot" Assets/Scripts Assets/Tests -g "*.cs"
```

Expected: only absence assertions in tests, or no matches.

- [ ] **Step 2: Run static gates**

Run:

```powershell
git diff --check
C:\Program Files\Git\bin\bash.exe Scripts/ci/check-no-entity-id-branching.sh
```

Expected: no errors; line-ending warnings are acceptable only if they are warnings, not failures.

- [ ] **Step 3: Commit Phase 3**

Stage only Phase 3 files, excluding unrelated dirty files:

```powershell
git add docs/superpowers/plans/2026-05-20-career-interest-hud-objective-journal-migration.md
git add Assets/Scripts/UI/Objectives/ObjectiveJournalRuntimeProvider.cs
git add Assets/Scripts/UI/StudentLife/CareerInterestRuntimeInstaller.cs
git add Assets/Scripts/UI/StudentLife/CareerInterestHudWidget.cs.meta
git add Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalUiOwnershipGateTests.cs
git add Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalRuntimeProviderTests.cs
git add Assets/Tests/PlayMode/EndToEnd/CareerInterestSelectionLoopE2EScenarioTests.cs
git commit -m "[REFACTOR][UI][TEST] Career Interest HUD를 Objective Journal로 이관"
```

If `script_delete` removes the `.cs.meta`, stage the deleted `.cs` and `.meta` paths together.

### Task 5: Phase 4 Legacy HUD Audit Handoff

**Files:**
- Audit only unless violations are found.

- [ ] **Step 1: Search every migrated legacy HUD**

Run:

```powershell
rg -n "CampaignHudPanel|MilestoneHudPanel|QuestChainHudWidget|CareerInterestHudWidget|CampaignHudRoot|MilestoneHudRoot|QuestChainHudRoot|CareerInterestHudRoot" Assets/Scripts Assets/Tests docs -g "*.cs" -g "*.md"
```

Expected: no production references; docs may mention historical migration names.

- [ ] **Step 2: Run migrated Objective Journal scenarios**

Run the Objective Journal EditMode namespace and the migrated E2E PlayMode methods for campaign, milestone, quest chain, and career interest. Each visible E2E must leave a screenshot under `Builds/Logs/`.

- [ ] **Step 3: Commit audit updates if any**

If audit changes files, commit with:

```powershell
git commit -m "[TEST][CHORE] Objective Journal legacy HUD 이관 감사"
```

If no files changed, record the audit in the final response and do not create an empty commit.
