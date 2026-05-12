# Student Life Trait Activity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add repeatable student-life activities whose data-driven choices grow the 10 existing student traits outside career practices.

**Architecture:** Reuse `LifeActivityDefinition`, `LifeActivityEffectBase`, `StudentLifeProgress`, `LifeActivityRunner`, and `StudentLifeNetworkStateBroadcaster`. Add a choice definition ScriptableObject and a board component so each activity exposes multiple choices, each choice applies its own effect array, and request ids remain idempotent per player progress.

**Tech Stack:** Unity 6000.3.13f1, C#, ScriptableObject data, NUnit EditMode tests, Unity PlayMode tests, existing bash CI gate.

---

## File Structure

- Create `Assets/Scripts/Game/StudentLife/LifeChoiceDefinition.cs`
  - ScriptableObject for one selectable lifestyle choice.
  - Stores id, display key, and `LifeActivityEffectBase[]`.
  - No trait/activity/choice id branching.

- Modify `Assets/Scripts/Game/StudentLife/LifeActivityDefinition.cs`
  - Add serialized `LifeChoiceDefinition[] _choices`.
  - Add `Choices`, `GetChoice(int)`, and choice-aware test configuration overload.

- Modify `Assets/Scripts/Game/StudentLife/StudentLifeCore.cs`
  - Add `ChoiceId` and changed trait ids to `LifeActivityResult`.
  - Add `StudentLifeProgress.GetTraitValueById`.
  - Add `LifeActivityRunner.TryPerformChoice`.
  - Preserve existing `TryPerform` behavior for career practices.

- Modify `Assets/Scripts/Game/StudentLife/StudentLifeSceneInteraction.cs`
  - Add `LifeActivityBoard` component.
  - Expose `Activities`, `LastActivityId`, `LastChoiceId`, `LastRequestId`, `LastResultKind`, `ChangedTraitIds`.

- Modify `Assets/Scripts/Network/StudentLife/StudentLifeNetworkStateBroadcaster.cs`
  - Include `ChoiceId` in broadcast payload and duplicate key.
  - Keep server-authority-only publish behavior.

- Modify `Assets/Scripts/Game/Bootstrap/TownStudentLifeRuntimeInstaller.cs`
  - Add a `LifeActivityBoard` object named `LifeActivityBoard`.
  - Bind 8 lifestyle activities with 2-3 choices each.
  - Create all choices/effects via ScriptableObject instances, not branches in execution.

- Modify `Assets/Scripts/Game/Common/GameDataRegistry.cs`
  - Add `LifeChoiceDefinition[] StudentLifeChoices`.
  - Keep existing arrays intact.

- Create tests:
  - `Assets/Tests/EditMode/StudentLife/StudentLifeTraitActivityTests.cs`
  - `Assets/Tests/EditMode/StudentLife/StudentLifeTraitActivityNetworkTests.cs`
  - `Assets/Tests/EditMode/StudentLife/StudentLifeTraitActivitySourceAuditTests.cs`
  - Modify `Assets/Tests/EditMode/StudentLife/StudentLifeRegistryTests.cs`
  - Modify or extend `Assets/Tests/PlayMode/TownConcept/TownStudentLifeInteractionTests.cs`

## Activity Data Matrix

Use this 1st-slice dataset in tests and Town runtime:

| Activity | Category | Choice | Traits |
| --- | --- | --- | --- |
| `activity.morning-routine` | SelfStudy | `choice.pack-timetable` | planning, responsibility |
| `activity.morning-routine` | SelfStudy | `choice.leave-on-time` | discipline, responsibility |
| `activity.morning-routine` | SelfStudy | `choice.help-family-breakfast` | empathy, service-sense |
| `activity.class-time` | School | `choice.focus-notes` | focus, observation |
| `activity.class-time` | School | `choice.prepare-presentation` | planning, calm, creativity |
| `activity.class-time` | School | `choice.help-friend-question` | empathy, service-sense |
| `activity.after-school-club` | Hobby | `choice.art-craft` | creativity, observation |
| `activity.after-school-club` | Hobby | `choice.sports-club` | fitness, discipline |
| `activity.after-school-club` | Hobby | `choice.volunteer-club` | empathy, responsibility, service-sense |
| `activity.house-chores` | Errand | `choice.clean-room` | planning, discipline |
| `activity.house-chores` | Errand | `choice.help-meal-prep` | service-sense, responsibility |
| `activity.house-chores` | Errand | `choice-check-family-mood` | observation, empathy |
| `activity.neighborhood-help` | Errand | `choice-guide-lost-neighbor` | observation, empathy |
| `activity.neighborhood-help` | Errand | `choice-store-errand` | responsibility, service-sense |
| `activity.neighborhood-help` | Errand | `choice-park-exercise` | fitness, discipline |
| `activity.training-practice` | Hobby | `choice-pace-run` | fitness, calm |
| `activity.training-practice` | Hobby | `choice-follow-routine` | discipline, planning |
| `activity.training-practice` | Hobby | `choice-recover-after-mistake` | calm, responsibility |
| `activity.sudden-trouble` | Social | `choice-calm-first-aid` | calm, empathy, observation |
| `activity.sudden-trouble` | Social | `choice-prioritize-schedule` | planning, calm |
| `activity.sudden-trouble` | Social | `choice-choose-conflict-words` | empathy, calm |
| `activity.part-time-shift` | Work | `choice-greet-customers` | service-sense, observation |
| `activity.part-time-shift` | Work | `choice-track-orders` | focus, responsibility |
| `activity.part-time-shift` | Work | `choice-solve-queue-pressure` | calm, service-sense |

Every target trait appears in at least two choices.

---

### Task 1: Choice Definition and Activity Choice Storage

**Files:**
- Create: `Assets/Scripts/Game/StudentLife/LifeChoiceDefinition.cs`
- Modify: `Assets/Scripts/Game/StudentLife/LifeActivityDefinition.cs`
- Test: `Assets/Tests/EditMode/StudentLife/StudentLifeTraitActivityTests.cs`

- [ ] **Step 1: Write the failing choice data test**

Add a new test file through Unity MCP `script-update-or-create`:

```csharp
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentLifeTraitActivityTests
    {
        [Test]
        public void LIFE_TRAIT_ACTIVITY_001_ActivityStoresDataDrivenChoices()
        {
            var focus = CreateTrait("trait.focus");
            var effect = ScriptableObject.CreateInstance<TraitDeltaActivityEffect>();
            effect.ConfigureForTests(focus, 2);
            var choice = ScriptableObject.CreateInstance<LifeChoiceDefinition>();
            choice.ConfigureForTests("choice.focus-notes", "choice.focus-notes", new LifeActivityEffectBase[] { effect });
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();

            activity.ConfigureForTests(
                "activity.class-time",
                "activity.class-time",
                LifeActivityCategory.School,
                20,
                1,
                1,
                0,
                null,
                null,
                new[] { choice });

            Assert.AreEqual(1, activity.Choices.Count);
            Assert.AreSame(choice, activity.GetChoice(0));
            Assert.AreEqual("choice.focus-notes", activity.GetChoice(0).Id);
        }

        private static TraitDefinition CreateTrait(string id)
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            trait.ConfigureForTests(id, id);
            return trait;
        }
    }
}
```

- [ ] **Step 2: Run the failing test**

Run:

```powershell
# Prefer Unity MCP tests-run if Editor is connected:
# tests-run testMode=EditMode testClass=Rootborn.Tests.EditMode.StudentLife.StudentLifeTraitActivityTests
```

Expected: compile failure because `LifeChoiceDefinition`, `Choices`, `GetChoice`, and the extended `ConfigureForTests` overload do not exist.

- [ ] **Step 3: Implement `LifeChoiceDefinition`**

Create `Assets/Scripts/Game/StudentLife/LifeChoiceDefinition.cs` only through Unity MCP `script-update-or-create`:

```csharp
using System;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "LifeChoice_New", menuName = "Rootborn/Student Life/Choice")]
    public sealed class LifeChoiceDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private LifeActivityEffectBase[] _effects = Array.Empty<LifeActivityEffectBase>();

        public IReadOnlyList<LifeActivityEffectBase> Effects => _effects;

        public void ApplyEffects(StudentLifeProgress progress)
        {
            for (int i = 0; i < _effects.Length; i++)
            {
                _effects[i]?.Apply(progress);
            }
        }

        public void ConfigureForTests(string id, string displayNameKey, LifeActivityEffectBase[] effects)
        {
            ConfigureForTests(id, displayNameKey);
            _effects = effects ?? Array.Empty<LifeActivityEffectBase>();
        }
    }
}
```

If the compiler reports `IReadOnlyList<>` missing, add `using System.Collections.Generic;`.

- [ ] **Step 4: Extend `LifeActivityDefinition`**

Modify `Assets/Scripts/Game/StudentLife/LifeActivityDefinition.cs` through Unity MCP `script-update-or-create`:

```csharp
[SerializeField] private LifeChoiceDefinition[] _choices = Array.Empty<LifeChoiceDefinition>();
public IReadOnlyList<LifeChoiceDefinition> Choices => _choices;

public LifeChoiceDefinition GetChoice(int index)
{
    return index >= 0 && index < _choices.Length ? _choices[index] : null;
}
```

Add this overload without removing the existing one:

```csharp
public void ConfigureForTests(
    string id,
    string displayNameKey,
    LifeActivityCategory category,
    int timeCostMinutes,
    int energyCost,
    int focusCost,
    int stressDelta,
    LifeActivityRequirementBase[] requirements,
    LifeActivityEffectBase[] effects,
    LifeChoiceDefinition[] choices)
{
    ConfigureForTests(id, displayNameKey, category, timeCostMinutes, energyCost, focusCost, stressDelta, requirements, effects);
    _choices = choices ?? Array.Empty<LifeChoiceDefinition>();
}
```

Ensure `using System.Collections.Generic;` is present.

- [ ] **Step 5: Run the test**

Run the same EditMode test.

Expected: `LIFE_TRAIT_ACTIVITY_001_ActivityStoresDataDrivenChoices` passes.

- [ ] **Step 6: Commit**

```powershell
git add -- Assets/Scripts/Game/StudentLife/LifeChoiceDefinition.cs Assets/Scripts/Game/StudentLife/LifeActivityDefinition.cs Assets/Tests/EditMode/StudentLife/StudentLifeTraitActivityTests.cs
git commit -m "[FEATURE][TEST] 생활 활동 선택지 데이터 모델 추가"
```

---

### Task 2: Choice Execution, Changed Traits, and Idempotency

**Files:**
- Modify: `Assets/Scripts/Game/StudentLife/StudentLifeCore.cs`
- Modify: `Assets/Tests/EditMode/StudentLife/StudentLifeTraitActivityTests.cs`

- [ ] **Step 1: Add failing choice execution tests**

Append these tests to `StudentLifeTraitActivityTests`:

```csharp
[Test]
public void LIFE_TRAIT_ACTIVITY_004_ChoiceAppliesDifferentTraitCombinations()
{
    var focus = CreateTrait("trait.focus");
    var observation = CreateTrait("trait.observation");
    var planning = CreateTrait("trait.planning");
    var calm = CreateTrait("trait.calm");
    var noteChoice = CreateChoice("choice.focus-notes", Trait(focus, 2), Trait(observation, 1));
    var presentationChoice = CreateChoice("choice.prepare-presentation", Trait(planning, 2), Trait(calm, 1), Trait(creativity, 1));
    var activity = CreateActivity("activity.class-time", LifeActivityCategory.School, noteChoice, presentationChoice);
    var progress = new StudentLifeProgress("slot-a", "player-1", 10, 10);
    var runner = new LifeActivityRunner();

    Assert.IsTrue(runner.TryPerformChoice(activity, noteChoice, progress, "request-notes", out var first));
    Assert.IsTrue(runner.TryPerformChoice(activity, presentationChoice, progress, "request-presentation", out var second));

    Assert.AreEqual(LifeActivityResultKind.Applied, first.Kind);
    Assert.AreEqual("choice.focus-notes", first.ChoiceId);
    CollectionAssert.Contains(first.ChangedTraitIds, "trait.focus");
    CollectionAssert.Contains(first.ChangedTraitIds, "trait.observation");
    Assert.AreEqual(2, progress.GetTraitValue(focus));
    Assert.AreEqual(1, progress.GetTraitValue(observation));
    Assert.AreEqual(2, progress.GetTraitValue(planning));
    Assert.AreEqual(1, progress.GetTraitValue(calm));
    Assert.AreEqual("choice.prepare-presentation", second.ChoiceId);
}

[Test]
public void LIFE_TRAIT_ACTIVITY_007_SameRequestIdAndChoiceDoesNotApplyTwice()
{
    var focus = CreateTrait("trait.focus");
    var choice = CreateChoice("choice.focus-notes", Trait(focus, 2));
    var activity = CreateActivity("activity.class-time", LifeActivityCategory.School, choice);
    var progress = new StudentLifeProgress("slot-a", "player-1", 10, 10);
    var runner = new LifeActivityRunner();

    Assert.IsTrue(runner.TryPerformChoice(activity, choice, progress, "same-choice-request", out var first));
    int afterFirst = progress.GetTraitValue(focus);
    Assert.IsFalse(runner.TryPerformChoice(activity, choice, progress, "same-choice-request", out var second));

    Assert.AreEqual(LifeActivityResultKind.Applied, first.Kind);
    Assert.AreEqual(LifeActivityResultKind.DuplicateRequest, second.Kind);
    Assert.AreEqual(afterFirst, progress.GetTraitValue(focus));
}
```

Add helpers in the same test class:

```csharp
private static LifeActivityDefinition CreateActivity(string id, LifeActivityCategory category, params LifeChoiceDefinition[] choices)
{
    var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
    activity.ConfigureForTests(id, id, category, 20, 1, 1, 0, null, null, choices);
    return activity;
}

private static LifeChoiceDefinition CreateChoice(string id, params LifeActivityEffectBase[] effects)
{
    var choice = ScriptableObject.CreateInstance<LifeChoiceDefinition>();
    choice.ConfigureForTests(id, id, effects);
    return choice;
}

private static TraitDeltaActivityEffect Trait(TraitDefinition trait, int delta)
{
    var effect = ScriptableObject.CreateInstance<TraitDeltaActivityEffect>();
    effect.ConfigureForTests(trait, delta);
    return effect;
}
```

- [ ] **Step 2: Run failing tests**

Expected: compile failure for `TryPerformChoice`, `ChoiceId`, `ChangedTraitIds`, and `GetTraitValueById`.

- [ ] **Step 3: Extend `LifeActivityResult`**

Modify `StudentLifeCore.cs`:

```csharp
public readonly string ChoiceId;
public readonly string[] ChangedTraitIds;
```

Add a constructor overload:

```csharp
public LifeActivityResult(
    LifeActivityResultKind kind,
    string saveSlot,
    string playerId,
    string activityId,
    string requestId,
    string choiceId,
    string[] changedTraitIds)
    : this(kind, saveSlot, playerId, activityId, requestId)
{
    ChoiceId = string.IsNullOrEmpty(choiceId) ? string.Empty : choiceId;
    ChangedTraitIds = changedTraitIds ?? Array.Empty<string>();
}
```

In the existing constructor, initialize:

```csharp
ChoiceId = string.Empty;
ChangedTraitIds = Array.Empty<string>();
```

- [ ] **Step 4: Add trait id helpers to `StudentLifeProgress`**

Add:

```csharp
public int GetTraitValueById(string traitId)
{
    return !string.IsNullOrEmpty(traitId) && _traitValues.TryGetValue(traitId, out int value) ? value : 0;
}

public string[] GetTraitIds()
{
    var result = new string[_traitValues.Count];
    _traitValues.Keys.CopyTo(result, 0);
    return result;
}
```

- [ ] **Step 5: Implement `TryPerformChoice`**

Add to `LifeActivityRunner`:

```csharp
public bool TryPerformChoice(
    LifeActivityDefinition activity,
    LifeChoiceDefinition choice,
    StudentLifeProgress progress,
    string requestId,
    out LifeActivityResult result)
{
    string saveSlot = progress == null ? "default" : progress.SaveSlot;
    string playerId = progress == null ? "player" : progress.PlayerId;
    string activityId = activity == null ? string.Empty : activity.Id;
    string choiceId = choice == null ? string.Empty : choice.Id;
    result = new LifeActivityResult(LifeActivityResultKind.InvalidRequest, saveSlot, playerId, activityId, requestId, choiceId, Array.Empty<string>());

    if (activity == null || choice == null || progress == null || string.IsNullOrEmpty(requestId))
    {
        return false;
    }

    if (progress.HasAppliedRequest(requestId))
    {
        result = new LifeActivityResult(LifeActivityResultKind.DuplicateRequest, progress.SaveSlot, progress.PlayerId, activity.Id, requestId, choice.Id, Array.Empty<string>());
        return false;
    }

    if (!progress.CanSpend(activity.EnergyCost, activity.FocusCost))
    {
        result = new LifeActivityResult(LifeActivityResultKind.InsufficientResources, progress.SaveSlot, progress.PlayerId, activity.Id, requestId, choice.Id, Array.Empty<string>());
        return false;
    }

    if (!activity.HasSatisfiedRequirements(progress))
    {
        result = new LifeActivityResult(LifeActivityResultKind.RequirementFailed, progress.SaveSlot, progress.PlayerId, activity.Id, requestId, choice.Id, Array.Empty<string>());
        return false;
    }

    string[] beforeIds = progress.GetTraitIds();
    int[] beforeValues = new int[beforeIds.Length];
    for (int i = 0; i < beforeIds.Length; i++)
    {
        beforeValues[i] = progress.GetTraitValueById(beforeIds[i]);
    }

    progress.Spend(activity.TimeCostMinutes, activity.EnergyCost, activity.FocusCost, activity.StressDelta);
    activity.ApplyEffects(progress);
    choice.ApplyEffects(progress);
    progress.MarkRequestApplied(requestId);

    string[] afterIds = progress.GetTraitIds();
    var changed = new List<string>();
    for (int i = 0; i < afterIds.Length; i++)
    {
        string id = afterIds[i];
        int before = 0;
        for (int j = 0; j < beforeIds.Length; j++)
        {
            if (beforeIds[j] == id)
            {
                before = beforeValues[j];
                break;
            }
        }

        if (progress.GetTraitValueById(id) != before)
        {
            changed.Add(id);
        }
    }

    result = new LifeActivityResult(LifeActivityResultKind.Applied, progress.SaveSlot, progress.PlayerId, activity.Id, requestId, choice.Id, changed.ToArray());
    return true;
}
```

Ensure `using System.Collections.Generic;` remains present.

- [ ] **Step 6: Run tests**

Expected: Task 1 and Task 2 tests pass.

- [ ] **Step 7: Commit**

```powershell
git add -- Assets/Scripts/Game/StudentLife/StudentLifeCore.cs Assets/Tests/EditMode/StudentLife/StudentLifeTraitActivityTests.cs
git commit -m "[FEATURE][TEST] 생활 활동 선택 실행과 중복 방지 추가"
```

---

### Task 3: Trait Coverage Dataset Tests

**Files:**
- Modify: `Assets/Tests/EditMode/StudentLife/StudentLifeTraitActivityTests.cs`

- [ ] **Step 1: Add failing coverage tests for required scenario IDs**

Append tests that build the matrix from this plan in memory:

```csharp
[Test]
public void LIFE_TRAIT_ACTIVITY_002_EachTraitCanGrowFromAtLeastTwoChoices()
{
    var set = CreateLifestyleSet();

    foreach (var trait in set.RequiredTraits)
    {
        int choices = CountChoicesIncreasingTrait(set.Activities, trait.Id);
        Assert.GreaterOrEqual(choices, 2, trait.Id);
    }
}

[Test]
public void LIFE_TRAIT_ACTIVITY_003_SchoolActivitiesGrowFocusPlanningObservation()
{
    var set = CreateLifestyleSet();

    AssertTraitAvailableInCategory(set.Activities, LifeActivityCategory.School, "trait.focus");
    AssertTraitAvailableInCategory(set.Activities, LifeActivityCategory.School, "trait.planning");
    AssertTraitAvailableInCategory(set.Activities, LifeActivityCategory.School, "trait.observation");
}

[Test]
public void LIFE_TRAIT_ACTIVITY_004_HouseChoresGrowResponsibilityDisciplineEmpathy()
{
    var set = CreateLifestyleSet();
    var house = FindActivity(set.Activities, "activity.house-chores");

    AssertTraitAvailable(house, "trait.responsibility");
    AssertTraitAvailable(house, "trait.discipline");
    AssertTraitAvailable(house, "trait.empathy");
}

[Test]
public void LIFE_TRAIT_ACTIVITY_005_NeighborhoodActivitiesGrowServiceObservationEmpathy()
{
    var set = CreateLifestyleSet();
    var neighborhood = FindActivity(set.Activities, "activity.neighborhood-help");

    AssertTraitAvailable(neighborhood, "trait.service-sense");
    AssertTraitAvailable(neighborhood, "trait.observation");
    AssertTraitAvailable(neighborhood, "trait.empathy");
}

[Test]
public void LIFE_TRAIT_ACTIVITY_006_TrainingActivitiesGrowFitnessDisciplineCalm()
{
    var set = CreateLifestyleSet();
    var training = FindActivity(set.Activities, "activity.training-practice");

    AssertTraitAvailable(training, "trait.fitness");
    AssertTraitAvailable(training, "trait.discipline");
    AssertTraitAvailable(training, "trait.calm");
}
```

- [ ] **Step 2: Add concrete in-memory dataset helpers**

Add helper methods that create the exact activity matrix from the plan. Keep them in tests for coverage first; Task 5 will move the same data shape into Town runtime.

```csharp
private sealed class LifestyleSet
{
    public TraitDefinition[] RequiredTraits;
    public LifeActivityDefinition[] Activities;
}

private static LifestyleSet CreateLifestyleSet()
{
    var focus = CreateTrait("trait.focus");
    var service = CreateTrait("trait.service-sense");
    var creativity = CreateTrait("trait.creativity");
    var planning = CreateTrait("trait.planning");
    var fitness = CreateTrait("trait.fitness");
    var discipline = CreateTrait("trait.discipline");
    var responsibility = CreateTrait("trait.responsibility");
    var calm = CreateTrait("trait.calm");
    var empathy = CreateTrait("trait.empathy");
    var observation = CreateTrait("trait.observation");

    return new LifestyleSet
    {
        RequiredTraits = new[] { focus, service, creativity, planning, fitness, discipline, responsibility, calm, empathy, observation },
        Activities = new[]
        {
            CreateActivity("activity.morning-routine", LifeActivityCategory.SelfStudy,
                CreateChoice("choice.pack-timetable", Trait(planning, 1), Trait(responsibility, 1)),
                CreateChoice("choice.leave-on-time", Trait(discipline, 1), Trait(responsibility, 1)),
                CreateChoice("choice.help-family-breakfast", Trait(empathy, 1), Trait(service, 1))),
            CreateActivity("activity.class-time", LifeActivityCategory.School,
                CreateChoice("choice.focus-notes", Trait(focus, 2), Trait(observation, 1)),
                CreateChoice("choice.prepare-presentation", Trait(planning, 2), Trait(calm, 1), Trait(creativity, 1)),
                CreateChoice("choice.help-friend-question", Trait(empathy, 1), Trait(service, 1))),
            CreateActivity("activity.after-school-club", LifeActivityCategory.Hobby,
                CreateChoice("choice.art-craft", Trait(creativity, 2), Trait(observation, 1)),
                CreateChoice("choice.sports-club", Trait(fitness, 2), Trait(discipline, 1)),
                CreateChoice("choice.volunteer-club", Trait(empathy, 1), Trait(responsibility, 1), Trait(service, 1))),
            CreateActivity("activity.house-chores", LifeActivityCategory.Errand,
                CreateChoice("choice.clean-room", Trait(planning, 1), Trait(discipline, 1)),
                CreateChoice("choice.help-meal-prep", Trait(service, 1), Trait(responsibility, 1)),
                CreateChoice("choice.check-family-mood", Trait(observation, 1), Trait(empathy, 1))),
            CreateActivity("activity.neighborhood-help", LifeActivityCategory.Errand,
                CreateChoice("choice.guide-lost-neighbor", Trait(observation, 1), Trait(empathy, 1)),
                CreateChoice("choice.store-errand", Trait(responsibility, 1), Trait(service, 1)),
                CreateChoice("choice.park-exercise", Trait(fitness, 1), Trait(discipline, 1))),
            CreateActivity("activity.training-practice", LifeActivityCategory.Hobby,
                CreateChoice("choice.pace-run", Trait(fitness, 1), Trait(calm, 1)),
                CreateChoice("choice.follow-routine", Trait(discipline, 1), Trait(planning, 1)),
                CreateChoice("choice.recover-after-mistake", Trait(calm, 1), Trait(responsibility, 1))),
            CreateActivity("activity.sudden-trouble", LifeActivityCategory.Social,
                CreateChoice("choice.calm-first-aid", Trait(calm, 1), Trait(empathy, 1), Trait(observation, 1)),
                CreateChoice("choice.prioritize-schedule", Trait(planning, 1), Trait(calm, 1)),
                CreateChoice("choice.choose-conflict-words", Trait(empathy, 1), Trait(calm, 1))),
            CreateActivity("activity.part-time-shift", LifeActivityCategory.Work,
                CreateChoice("choice.greet-customers", Trait(service, 1), Trait(observation, 1)),
                CreateChoice("choice.track-orders", Trait(focus, 1), Trait(responsibility, 1)),
                CreateChoice("choice.solve-queue-pressure", Trait(calm, 1), Trait(service, 1))),
        }
    };
}
```

Add assertion helpers:

```csharp
private static int CountChoicesIncreasingTrait(LifeActivityDefinition[] activities, string traitId)
{
    int count = 0;
    for (int i = 0; i < activities.Length; i++)
    {
        for (int j = 0; j < activities[i].Choices.Count; j++)
        {
            var progress = new StudentLifeProgress("slot", "player", 99, 99);
            var runner = new LifeActivityRunner();
            var choice = activities[i].GetChoice(j);
            runner.TryPerformChoice(activities[i], choice, progress, activities[i].Id + ":" + choice.Id, out _);
            if (progress.GetTraitValueById(traitId) > 0)
            {
                count++;
            }
        }
    }

    return count;
}

private static void AssertTraitAvailableInCategory(LifeActivityDefinition[] activities, LifeActivityCategory category, string traitId)
{
    for (int i = 0; i < activities.Length; i++)
    {
        if (activities[i].Category == category && ActivityCanIncreaseTrait(activities[i], traitId))
        {
            return;
        }
    }

    Assert.Fail("Trait not available in category: " + traitId + " / " + category);
}

private static void AssertTraitAvailable(LifeActivityDefinition activity, string traitId)
{
    Assert.IsTrue(ActivityCanIncreaseTrait(activity, traitId), traitId);
}

private static bool ActivityCanIncreaseTrait(LifeActivityDefinition activity, string traitId)
{
    for (int i = 0; i < activity.Choices.Count; i++)
    {
        var progress = new StudentLifeProgress("slot", "player", 99, 99);
        var runner = new LifeActivityRunner();
        var choice = activity.GetChoice(i);
        runner.TryPerformChoice(activity, choice, progress, activity.Id + ":" + choice.Id, out _);
        if (progress.GetTraitValueById(traitId) > 0)
        {
            return true;
        }
    }

    return false;
}

private static LifeActivityDefinition FindActivity(LifeActivityDefinition[] activities, string id)
{
    for (int i = 0; i < activities.Length; i++)
    {
        if (activities[i].Id == id)
        {
            return activities[i];
        }
    }

    return null;
}
```

- [ ] **Step 3: Run tests**

Expected: all `LIFE_TRAIT_ACTIVITY_001` through `006` tests pass.

- [ ] **Step 4: Commit**

```powershell
git add -- Assets/Tests/EditMode/StudentLife/StudentLifeTraitActivityTests.cs
git commit -m "[TEST] 생활 활동 성향 커버리지 검증 추가"
```

---

### Task 4: Network Authority and Player Isolation

**Files:**
- Modify: `Assets/Scripts/Network/StudentLife/StudentLifeNetworkStateBroadcaster.cs`
- Create: `Assets/Tests/EditMode/StudentLife/StudentLifeTraitActivityNetworkTests.cs`

- [ ] **Step 1: Add failing network tests**

Create `StudentLifeTraitActivityNetworkTests.cs`:

```csharp
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.Network.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentLifeTraitActivityNetworkTests
    {
        [Test]
        public void LIFE_TRAIT_ACTIVITY_NET_001_ServerAuthorityConfirmsChoiceResult()
        {
            var trait = CreateTrait("trait.focus");
            var choice = CreateChoice("choice.focus-notes", Trait(trait, 1));
            var activity = CreateActivity("activity.class-time", choice);
            var progress = new StudentLifeProgress("slot-a", "player-1", 10, 10);
            var runner = new LifeActivityRunner();
            var broadcaster = new StudentLifeNetworkStateBroadcaster(true, "slot-a");
            broadcaster.RegisterClient(1UL);

            Assert.IsTrue(runner.TryPerformChoice(activity, choice, progress, "request-1", out var result));
            Assert.IsTrue(broadcaster.Publish(result));

            Assert.AreEqual("player-1", broadcaster.LastPlayerId);
            Assert.AreEqual("activity.class-time", broadcaster.LastActivityId);
            Assert.AreEqual("choice.focus-notes", broadcaster.LastChoiceId);
            Assert.AreEqual(1, broadcaster.BroadcastCount);
        }

        [Test]
        public void LIFE_TRAIT_ACTIVITY_NET_002_PlayerOneChoiceDoesNotMutatePlayerTwoProgress()
        {
            var trait = CreateTrait("trait.focus");
            var choice = CreateChoice("choice.focus-notes", Trait(trait, 1));
            var activity = CreateActivity("activity.class-time", choice);
            var playerOne = new StudentLifeProgress("slot-a", "player-1", 10, 10);
            var playerTwo = new StudentLifeProgress("slot-a", "player-2", 10, 10);
            var runner = new LifeActivityRunner();

            Assert.IsTrue(runner.TryPerformChoice(activity, choice, playerOne, "request-p1", out var result));

            Assert.AreEqual("player-1", result.PlayerId);
            Assert.AreEqual(1, playerOne.GetTraitValue(trait));
            Assert.AreEqual(0, playerTwo.GetTraitValue(trait));
        }

        private static TraitDefinition CreateTrait(string id)
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            trait.ConfigureForTests(id, id);
            return trait;
        }

        private static TraitDeltaActivityEffect Trait(TraitDefinition trait, int delta)
        {
            var effect = ScriptableObject.CreateInstance<TraitDeltaActivityEffect>();
            effect.ConfigureForTests(trait, delta);
            return effect;
        }

        private static LifeChoiceDefinition CreateChoice(string id, params LifeActivityEffectBase[] effects)
        {
            var choice = ScriptableObject.CreateInstance<LifeChoiceDefinition>();
            choice.ConfigureForTests(id, id, effects);
            return choice;
        }

        private static LifeActivityDefinition CreateActivity(string id, params LifeChoiceDefinition[] choices)
        {
            var activity = ScriptableObject.CreateInstance<LifeActivityDefinition>();
            activity.ConfigureForTests(id, id, LifeActivityCategory.School, 10, 1, 1, 0, null, null, choices);
            return activity;
        }
    }
}
```

- [ ] **Step 2: Run failing tests**

Expected: compile failure for `LastChoiceId`.

- [ ] **Step 3: Add choice id to broadcaster**

Modify `StudentLifeNetworkStateBroadcaster.cs`:

```csharp
public readonly string ChoiceId;
```

Add `choiceId` parameter to `StudentLifeNetworkStateBroadcast` constructor and assign:

```csharp
ChoiceId = string.IsNullOrEmpty(choiceId) ? string.Empty : choiceId;
```

Add property:

```csharp
public string LastChoiceId { get; private set; }
```

Initialize in constructor:

```csharp
LastChoiceId = string.Empty;
```

Include in duplicate key and state:

```csharp
string key = result.SaveSlot + "|" + result.PlayerId + "|" + result.ActivityId + "|" + result.ChoiceId + "|" + result.RequestId + "|" + result.Kind;
LastChoiceId = result.ChoiceId;
```

Pass it into each broadcast:

```csharp
result.ChoiceId,
```

- [ ] **Step 4: Run tests**

Expected: network tests pass and existing `StudentLifeNetworkTests` still pass.

- [ ] **Step 5: Commit**

```powershell
git add -- Assets/Scripts/Network/StudentLife/StudentLifeNetworkStateBroadcaster.cs Assets/Tests/EditMode/StudentLife/StudentLifeTraitActivityNetworkTests.cs
git commit -m "[NETWORK][TEST] 생활 활동 선택 서버 확정 검증 추가"
```

---

### Task 5: Town LifeActivityBoard Runtime Object

**Files:**
- Modify: `Assets/Scripts/Game/StudentLife/StudentLifeSceneInteraction.cs`
- Modify: `Assets/Scripts/Game/Bootstrap/TownStudentLifeRuntimeInstaller.cs`
- Modify: `Assets/Tests/PlayMode/TownConcept/TownStudentLifeInteractionTests.cs`

- [ ] **Step 1: Add failing PlayMode tests**

Append to `TownStudentLifeInteractionTests`:

```csharp
[UnityTest]
public IEnumerator LIFE_TRAIT_ACTIVITY_PM_001_TownSceneInstallsLifeActivityBoard()
{
    yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
    yield return WaitForLifeActivityBoard();

    var board = GameObject.Find("LifeActivityBoard").GetComponent<LifeActivityBoard>();

    Assert.IsNotNull(board);
    Assert.GreaterOrEqual(board.Activities.Count, 8);
}

[UnityTest]
public IEnumerator LIFE_TRAIT_ACTIVITY_PM_002_TownLifeActivityChoiceChangesProgressAndExposesLastState()
{
    yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
    yield return WaitForLifeActivityBoard();

    var playerProgress = GameObject.Find("Player").GetComponent<StudentLifeProgressComponent>();
    var board = GameObject.Find("LifeActivityBoard").GetComponent<LifeActivityBoard>();

    Assert.IsTrue(board.RunChoice(playerProgress, 1, 0, "town-life-choice-request"));
    yield return null;

    Assert.AreEqual("activity.class-time", board.LastActivityId);
    Assert.AreEqual("choice.focus-notes", board.LastChoiceId);
    Assert.AreEqual("town-life-choice-request", board.LastRequestId);
    Assert.AreEqual(LifeActivityResultKind.Applied, board.LastResultKind);
    CollectionAssert.Contains(board.ChangedTraitIds, "trait.focus");
    Assert.Greater(playerProgress.Progress.GetTraitValueById("trait.focus"), 0);
}

private static IEnumerator WaitForLifeActivityBoard()
{
    for (int i = 0; i < 240; i++)
    {
        var player = GameObject.Find("Player");
        var board = GameObject.Find("LifeActivityBoard");
        if (player != null &&
            player.GetComponent<StudentLifeProgressComponent>() != null &&
            board != null &&
            board.GetComponent<LifeActivityBoard>() != null)
        {
            yield break;
        }

        yield return null;
    }

    Assert.Fail("Town LifeActivityBoard did not install within timeout.");
}
```

- [ ] **Step 2: Run failing PlayMode tests**

Expected: compile failure for `LifeActivityBoard` or runtime failure because object is not installed.

- [ ] **Step 3: Implement `LifeActivityBoard`**

Add to `StudentLifeSceneInteraction.cs`:

```csharp
[DisallowMultipleComponent]
public sealed class LifeActivityBoard : MonoBehaviour
{
    [SerializeField] private LifeActivityDefinition[] _activities = Array.Empty<LifeActivityDefinition>();

    private readonly LifeActivityRunner _runner = new LifeActivityRunner();

    public IReadOnlyList<LifeActivityDefinition> Activities => _activities;
    public IReadOnlyList<string> ChangedTraitIds => _changedTraitIds;
    private readonly List<string> _changedTraitIds = new List<string>();

    public string LastActivityId { get; private set; } = string.Empty;
    public string LastChoiceId { get; private set; } = string.Empty;
    public string LastRequestId { get; private set; } = string.Empty;
    public LifeActivityResultKind LastResultKind { get; private set; } = LifeActivityResultKind.InvalidRequest;

    public void Bind(LifeActivityDefinition[] activities)
    {
        _activities = activities ?? Array.Empty<LifeActivityDefinition>();
        _changedTraitIds.Clear();
        LastActivityId = string.Empty;
        LastChoiceId = string.Empty;
        LastRequestId = string.Empty;
        LastResultKind = LifeActivityResultKind.InvalidRequest;
    }

    public bool RunChoice(StudentLifeProgressComponent progressComponent, int activityIndex, int choiceIndex, string requestId)
    {
        var progress = progressComponent == null ? null : progressComponent.EnsureProgress();
        var activity = GetActivity(activityIndex);
        var choice = activity == null ? null : activity.GetChoice(choiceIndex);
        bool applied = _runner.TryPerformChoice(activity, choice, progress, requestId, out var result);

        LastActivityId = activity == null ? string.Empty : activity.Id;
        LastChoiceId = choice == null ? string.Empty : choice.Id;
        LastRequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId;
        LastResultKind = result.Kind;
        _changedTraitIds.Clear();
        for (int i = 0; i < result.ChangedTraitIds.Length; i++)
        {
            _changedTraitIds.Add(result.ChangedTraitIds[i]);
        }

        return applied;
    }

    private LifeActivityDefinition GetActivity(int index)
    {
        return index >= 0 && index < _activities.Length ? _activities[index] : null;
    }
}
```

Ensure `using System.Collections.Generic;` exists in the file.

- [ ] **Step 4: Install `LifeActivityBoard` in Town runtime**

In `TownStudentLifeRuntimeInstaller.cs`, add:

```csharp
private const string LifeActivityBoardName = "LifeActivityBoard";
```

Call after `EnsureCareerPracticeBoard(root.transform);`:

```csharp
EnsureLifeActivityBoard(root.transform);
```

Add `EnsureLifeActivityBoard` mirroring `EnsureCareerPracticeBoard`, then bind `CreateLifestyleActivities()`.

Create traits with ids exactly:

```csharp
trait.focus
trait.service-sense
trait.creativity
trait.planning
trait.fitness
trait.discipline
trait.responsibility
trait.calm
trait.empathy
trait.observation
```

Create activities and choices using the matrix in this plan. Use helper methods:

```csharp
private static LifeActivityDefinition CreateLifestyleActivity(string id, LifeActivityCategory category, LifeChoiceDefinition[] choices)
private static LifeChoiceDefinition Choice(string id, params LifeActivityEffectBase[] effects)
```

Do not add any `if` or `switch` by trait/activity/choice id.

- [ ] **Step 5: Run PlayMode tests**

Expected: existing Town student-life tests and the new LifeActivityBoard tests pass.

- [ ] **Step 6: Commit**

```powershell
git add -- Assets/Scripts/Game/StudentLife/StudentLifeSceneInteraction.cs Assets/Scripts/Game/Bootstrap/TownStudentLifeRuntimeInstaller.cs Assets/Tests/PlayMode/TownConcept/TownStudentLifeInteractionTests.cs
git commit -m "[FEATURE][TEST] Town 생활 활동 보드 추가"
```

---

### Task 6: Registry and Source Audit

**Files:**
- Modify: `Assets/Scripts/Game/Common/GameDataRegistry.cs`
- Modify: `Assets/Tests/EditMode/StudentLife/StudentLifeRegistryTests.cs`
- Create: `Assets/Tests/EditMode/StudentLife/StudentLifeTraitActivitySourceAuditTests.cs`

- [ ] **Step 1: Add failing registry test**

Modify `StudentLifeRegistryTests`:

```csharp
Assert.IsNotNull(registry.StudentLifeChoices);
```

- [ ] **Step 2: Add failing source audit test**

Create:

```csharp
using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class StudentLifeTraitActivitySourceAuditTests
    {
        [Test]
        public void LIFE_TRAIT_ACTIVITY_008_RuntimeExecutionDoesNotBranchByEntityIds()
        {
            string core = File.ReadAllText("Assets/Scripts/Game/StudentLife/StudentLifeCore.cs");
            string scene = File.ReadAllText("Assets/Scripts/Game/StudentLife/StudentLifeSceneInteraction.cs");

            AssertNoEntityIdBranch(core);
            AssertNoEntityIdBranch(scene);
        }

        private static void AssertNoEntityIdBranch(string source)
        {
            StringAssert.DoesNotContain("traitId ==", source);
            StringAssert.DoesNotContain("activityId ==", source);
            StringAssert.DoesNotContain("choiceId ==", source);
            StringAssert.DoesNotContain("switch (traitId", source);
            StringAssert.DoesNotContain("switch (activityId", source);
            StringAssert.DoesNotContain("switch (choiceId", source);
        }
    }
}
```

- [ ] **Step 3: Implement registry field**

Modify `GameDataRegistry.cs`:

```csharp
[SerializeField] private LifeChoiceDefinition[] _studentLifeChoices = Array.Empty<LifeChoiceDefinition>();
public LifeChoiceDefinition[] StudentLifeChoices => _studentLifeChoices;
```

Place it near other StudentLife arrays.

- [ ] **Step 4: Run EditMode tests**

Expected: registry and source audit tests pass.

- [ ] **Step 5: Run CI branch gate**

Run:

```powershell
bash Scripts/ci/check-no-entity-id-branching.sh
```

Expected:

```text
OK: no entity-id branching in system code.
```

- [ ] **Step 6: Commit**

```powershell
git add -- Assets/Scripts/Game/Common/GameDataRegistry.cs Assets/Tests/EditMode/StudentLife/StudentLifeRegistryTests.cs Assets/Tests/EditMode/StudentLife/StudentLifeTraitActivitySourceAuditTests.cs
git commit -m "[TEST] 생활 활동 데이터 등록과 ID 분기 감사 추가"
```

---

### Task 7: Final Verification

**Files:**
- No code changes expected.

- [ ] **Step 1: Run focused EditMode tests**

Run with Unity MCP:

```text
tests-run testMode=EditMode testNamespace=Rootborn.Tests.EditMode.StudentLife
```

Expected: all StudentLife EditMode tests pass.

- [ ] **Step 2: Run focused PlayMode tests**

Run with Unity MCP:

```text
tests-run testMode=PlayMode testClass=Rootborn.Tests.PlayMode.TownConcept.TownStudentLifeInteractionTests
```

Expected: all Town student-life interaction tests pass.

- [ ] **Step 3: Run CI gate**

Run:

```powershell
bash Scripts/ci/check-no-entity-id-branching.sh
```

Expected:

```text
OK: no entity-id branching in system code.
```

- [ ] **Step 4: Check dirty state**

Run:

```powershell
git status --short
```

Expected: only intentional uncommitted files remain, or clean if all task commits were made.

- [ ] **Step 5: Final report**

Report:

- Implemented activity/choice data model.
- Implemented idempotent choice execution.
- Implemented Town `LifeActivityBoard`.
- Implemented server-authority and player-isolation tests.
- List exact test commands and pass/fail results.
- State commit hashes or that commits were not made.
- State remaining risks: runtime-generated Town data is not yet persisted as project `.asset` instances unless a follow-up asset-generation pass is performed.

## Self-Review

- Spec coverage: all 10 traits, minimum two choices per trait, category tests, duplicate request id, network authority, player isolation, Town board state exposure, and no ID branching are covered by tasks.
- Placeholder scan: no TBD/TODO steps remain.
- Type consistency: `LifeChoiceDefinition`, `LifeActivityRunner.TryPerformChoice`, `LifeActivityResult.ChoiceId`, `LifeActivityResult.ChangedTraitIds`, and `LifeActivityBoard.RunChoice` are introduced before later tasks reference them.
