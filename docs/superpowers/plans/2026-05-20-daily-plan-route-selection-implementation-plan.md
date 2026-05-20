# Daily Plan Route Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first player-facing Daily Plan route slice so a player can select a data-defined Town route, complete a matching activity, see it in Objective Journal and day result UI, then reload without duplicated completion.

**Architecture:** Add a thin `Rootborn.Game.StudentLife` Daily Plan domain over existing `LocationActivityDefinition` data, persist it in a dedicated `daily-plan-progress.json`, and surface guidance through `ObjectiveJournalPanel`. Town access is provided by a runtime-installed board and bounded selection panel; no new always-visible objective HUD is introduced.

**Tech Stack:** Unity 6000.3.13f1, C#, ScriptableObject data, Unity MCP script tools, NUnit EditMode/PlayMode tests, existing Objective Journal and Student Life systems.

---

## Execution Rules

- Use `superpowers:test-driven-development` before production implementation.
- Use `superpowers:systematic-debugging` for any failed test or unexpected PlayMode behavior.
- Use `superpowers:verification-before-completion` before claiming the feature complete.
- Edit every `Assets/**/*.cs` file through Unity MCP `script-update-or-create` or `script-delete`; do not write C# files directly from shell.
- Keep unrelated dirty files unstaged and untouched:
  - `Assets/AddressableAssetsData/link.xml`
  - `Assets/AddressableAssetsData/link.xml.meta`
  - `production/qa/evidence/registry-provider-town-flow.png`
- Use real player flow for visible verification: PlayMode, keyboard movement, interaction router, UI button selection, Tab Objective Journal, day-end interaction, Game View screenshot.

## File Structure

Create:

- `Assets/Scripts/Game/StudentLife/DailyPlanRouteDefinition.cs` - route and route-set ScriptableObject definitions plus requirement/outcome strategies.
- `Assets/Scripts/Game/StudentLife/DailyPlanProgress.cs` - serializable save data, progress aggregate, persistence, runner, and completion result.
- `Assets/Scripts/Game/StudentLife/DailyPlanProgressComponent.cs` - scene/runtime bridge that loads, saves, and publishes progress changes.
- `Assets/Scripts/UI/StudentLife/DailyPlanPanel.cs` - bounded route selection UI.
- `Assets/Scripts/UI/StudentLife/DailyPlanBoardInteractor.cs` - player-facing Town board prompt `[E] Plan Day`.
- `Assets/Scripts/UI/StudentLife/DailyPlanRuntimeInstaller.cs` - idempotent Town installer for board and panel.
- `Assets/Scripts/UI/Objectives/Sources/DailyPlanObjectiveSource.cs` - Objective Journal `Goals` source with stable keys.
- `Assets/Tests/EditMode/StudentLife/DailyPlanRouteRunnerTests.cs`
- `Assets/Tests/EditMode/StudentLife/DailyPlanRegistryTests.cs`
- `Assets/Tests/EditMode/UI/Objectives/DailyPlanObjectiveSourceTests.cs`
- `Assets/Tests/EditMode/UI/StudentLife/DailyPlanPanelTests.cs`
- `Assets/Tests/PlayMode/EndToEnd/DailyPlanRouteSelectionE2EScenarioTests.cs`

Modify:

- `Assets/Scripts/Game/Common/GameDataRegistry.cs` - expose `DailyPlanRouteSetDefinition[]`.
- `Assets/Data/Registry/GameDataRegistry.asset` - register the default Daily Plan route set.
- `Assets/Scripts/UI/Objectives/Sources/ObjectiveJournalSourceCatalog.cs` - add `DailyPlanObjectiveSource`.
- `Assets/Scripts/UI/StudentLife/StudentDayResultPanel.cs` - include compact Daily Plan summary.

Create assets:

- `Assets/Data/StudentLife/DailyPlans/DefaultDailyPlanRouteSet.asset`
- `Assets/Data/StudentLife/DailyPlans/LibrarySelfStudyRoute.asset`
- `Assets/Data/StudentLife/DailyPlans/TownSquareHelpRoute.asset`
- `Assets/Data/StudentLife/DailyPlans/WorkshopShiftRoute.asset`

## Data And API Decisions

- Daily Plan progress uses a dedicated save-slot file: `daily-plan-progress.json`.
- Route matching is data-based:
  - explicit `MatchingActivities` object reference match, or
  - `TargetLocation` object reference plus `LocationGrowthRoute` match.
- Production code must not branch on route id, activity id, location id, or career id.
- First Objective Journal integration uses a focused `DailyPlanObjectiveSource` with source id `daily-plan`.
- First Town entry point is a new runtime-installed `DailyPlanBoardInteractor`.

## Task 1: Domain Tests First

**Files:**
- Create: `Assets/Tests/EditMode/StudentLife/DailyPlanRouteRunnerTests.cs`
- Later create: `Assets/Scripts/Game/StudentLife/DailyPlanRouteDefinition.cs`
- Later create: `Assets/Scripts/Game/StudentLife/DailyPlanProgress.cs`

- [ ] **Step 1: Create failing EditMode tests**

Use Unity MCP `script-update-or-create` for `Assets/Tests/EditMode/StudentLife/DailyPlanRouteRunnerTests.cs`:

```csharp
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyPlanRouteRunnerTests
    {
        [Test]
        public void RouteSet_ReturnsAvailableRoutesFromData()
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests("library", "location.library");
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            route.ConfigureForTests(
                "daily.library.self-study",
                "daily.route.library",
                location,
                LocationGrowthRoute.SelfStudy,
                System.Array.Empty<LocationActivityDefinition>(),
                System.Array.Empty<DailyPlanRouteRequirementBase>(),
                System.Array.Empty<DailyPlanRouteOutcomeBase>(),
                "objective.daily.library.title",
                "objective.daily.library.detail",
                "objective.daily.library.next");
            var set = ScriptableObject.CreateInstance<DailyPlanRouteSetDefinition>();
            set.ConfigureForTests("daily.default", "daily.default", new[] { route }, 1);

            var progress = new StudentLifeProgress("slot-a", "player-a");
            var runner = new DailyPlanRouteRunner();

            var routes = runner.GetAvailableRoutes(new[] { set }, progress);

            Assert.That(routes, Is.EquivalentTo(new[] { route }));
        }

        [Test]
        public void SelectRoute_PersistsSelectedRouteForSameDay()
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests("library", "location.library");
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            route.ConfigureForTests(
                "daily.library.self-study",
                "daily.route.library",
                location,
                LocationGrowthRoute.SelfStudy,
                System.Array.Empty<LocationActivityDefinition>(),
                System.Array.Empty<DailyPlanRouteRequirementBase>(),
                System.Array.Empty<DailyPlanRouteOutcomeBase>(),
                "objective.daily.library.title",
                "objective.daily.library.detail",
                "objective.daily.library.next");
            var student = new StudentLifeProgress("slot-a", "player-a");
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();

            var selected = runner.TrySelectRoute(daily, route, student);

            Assert.That(selected, Is.True);
            Assert.That(daily.SelectedRouteId, Is.EqualTo("daily.library.self-study"));
            Assert.That(daily.CurrentDay, Is.EqualTo(1));
        }

        [Test]
        public void SelectSameRouteTwice_IsNoOp()
        {
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            route.ConfigureForTests(
                "daily.town.help",
                "daily.route.help",
                null,
                LocationGrowthRoute.SocialHelp,
                System.Array.Empty<LocationActivityDefinition>(),
                System.Array.Empty<DailyPlanRouteRequirementBase>(),
                System.Array.Empty<DailyPlanRouteOutcomeBase>(),
                "objective.daily.help.title",
                "objective.daily.help.detail",
                "objective.daily.help.next");
            var student = new StudentLifeProgress("slot-a", "player-a");
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();

            Assert.That(runner.TrySelectRoute(daily, route, student), Is.True);
            Assert.That(runner.TrySelectRoute(daily, route, student), Is.False);
            Assert.That(daily.SelectedRouteId, Is.EqualTo("daily.town.help"));
        }

        [Test]
        public void MatchingActivity_CompletesSelectedRoute()
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests("library", "location.library");
            var activity = ScriptableObject.CreateInstance<LocationActivityDefinition>();
            activity.ConfigureForTests(
                "activity.library.read",
                "activity.library.read",
                location,
                LocationGrowthRoute.SelfStudy,
                60,
                0,
                0,
                0,
                System.Array.Empty<LocationActivityRequirementBase>(),
                System.Array.Empty<LocationActivityOutcomeBase>());
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            route.ConfigureForTests(
                "daily.library.self-study",
                "daily.route.library",
                location,
                LocationGrowthRoute.SelfStudy,
                System.Array.Empty<LocationActivityDefinition>(),
                System.Array.Empty<DailyPlanRouteRequirementBase>(),
                System.Array.Empty<DailyPlanRouteOutcomeBase>(),
                "objective.daily.library.title",
                "objective.daily.library.detail",
                "objective.daily.library.next");
            var set = ScriptableObject.CreateInstance<DailyPlanRouteSetDefinition>();
            set.ConfigureForTests("daily.default", "daily.default", new[] { route }, 1);
            var student = new StudentLifeProgress("slot-a", "player-a");
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();

            runner.TrySelectRoute(daily, route, student);
            var completed = runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-1", out var result);

            Assert.That(completed, Is.True);
            Assert.That(result.Route, Is.SameAs(route));
            Assert.That(result.Activity, Is.SameAs(activity));
            Assert.That(daily.CompletedRouteId, Is.EqualTo("daily.library.self-study"));
            Assert.That(daily.MatchedActivityId, Is.EqualTo("activity.library.read"));
        }

        [Test]
        public void NonMatchingActivity_DoesNotCompleteSelectedRoute()
        {
            var library = ScriptableObject.CreateInstance<LocationDefinition>();
            library.ConfigureForTests("library", "location.library");
            var workshop = ScriptableObject.CreateInstance<LocationDefinition>();
            workshop.ConfigureForTests("workshop", "location.workshop");
            var activity = ScriptableObject.CreateInstance<LocationActivityDefinition>();
            activity.ConfigureForTests(
                "activity.workshop.shift",
                "activity.workshop.shift",
                workshop,
                LocationGrowthRoute.Work,
                60,
                0,
                0,
                0,
                System.Array.Empty<LocationActivityRequirementBase>(),
                System.Array.Empty<LocationActivityOutcomeBase>());
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            route.ConfigureForTests(
                "daily.library.self-study",
                "daily.route.library",
                library,
                LocationGrowthRoute.SelfStudy,
                System.Array.Empty<LocationActivityDefinition>(),
                System.Array.Empty<DailyPlanRouteRequirementBase>(),
                System.Array.Empty<DailyPlanRouteOutcomeBase>(),
                "objective.daily.library.title",
                "objective.daily.library.detail",
                "objective.daily.library.next");
            var set = ScriptableObject.CreateInstance<DailyPlanRouteSetDefinition>();
            set.ConfigureForTests("daily.default", "daily.default", new[] { route }, 1);
            var student = new StudentLifeProgress("slot-a", "player-a");
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();

            runner.TrySelectRoute(daily, route, student);
            var completed = runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-1", out var result);

            Assert.That(completed, Is.False);
            Assert.That(result.IsCompleted, Is.False);
            Assert.That(daily.CompletedRouteId, Is.Empty);
        }

        [Test]
        public void CompletionWithSameRequestKey_DoesNotDuplicate()
        {
            var location = ScriptableObject.CreateInstance<LocationDefinition>();
            location.ConfigureForTests("library", "location.library");
            var activity = ScriptableObject.CreateInstance<LocationActivityDefinition>();
            activity.ConfigureForTests(
                "activity.library.read",
                "activity.library.read",
                location,
                LocationGrowthRoute.SelfStudy,
                60,
                0,
                0,
                0,
                System.Array.Empty<LocationActivityRequirementBase>(),
                System.Array.Empty<LocationActivityOutcomeBase>());
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            route.ConfigureForTests(
                "daily.library.self-study",
                "daily.route.library",
                location,
                LocationGrowthRoute.SelfStudy,
                System.Array.Empty<LocationActivityDefinition>(),
                System.Array.Empty<DailyPlanRouteRequirementBase>(),
                System.Array.Empty<DailyPlanRouteOutcomeBase>(),
                "objective.daily.library.title",
                "objective.daily.library.detail",
                "objective.daily.library.next");
            var set = ScriptableObject.CreateInstance<DailyPlanRouteSetDefinition>();
            set.ConfigureForTests("daily.default", "daily.default", new[] { route }, 1);
            var student = new StudentLifeProgress("slot-a", "player-a");
            var daily = new DailyPlanProgress("slot-a", "player-a", 1);
            var runner = new DailyPlanRouteRunner();

            runner.TrySelectRoute(daily, route, student);
            Assert.That(runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-1", out _), Is.True);
            Assert.That(runner.TryRecordActivity(daily, new[] { set }, activity, student, "request-1", out _), Is.False);

            Assert.That(daily.AppliedCompletionKeys, Is.EquivalentTo(new[] { "request-1" }));
        }
    }
}
```

- [ ] **Step 2: Run the failing tests**

Run in Unity MCP:

```text
tests_run EditMode testClass DailyPlanRouteRunnerTests includeMessages=true
```

Expected: FAIL because `DailyPlanRouteDefinition`, `DailyPlanRouteSetDefinition`, `DailyPlanRouteRunner`, `DailyPlanProgress`, `DailyPlanRouteRequirementBase`, `DailyPlanRouteOutcomeBase`, and `DailyPlanCompletionResult` do not exist.

## Task 2: Domain Implementation

**Files:**
- Create: `Assets/Scripts/Game/StudentLife/DailyPlanRouteDefinition.cs`
- Create: `Assets/Scripts/Game/StudentLife/DailyPlanProgress.cs`
- Test: `Assets/Tests/EditMode/StudentLife/DailyPlanRouteRunnerTests.cs`

- [ ] **Step 1: Add route ScriptableObject definitions**

Use Unity MCP `script-update-or-create` for `Assets/Scripts/Game/StudentLife/DailyPlanRouteDefinition.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(menuName = "Rootborn/Student Life/Daily Plan Route", fileName = "DailyPlanRouteDefinition")]
    public sealed class DailyPlanRouteDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private LocationDefinition _targetLocation;
        [SerializeField] private LocationGrowthRoute _growthRoute;
        [SerializeField] private LocationActivityDefinition[] _matchingActivities = Array.Empty<LocationActivityDefinition>();
        [SerializeField] private DailyPlanRouteRequirementBase[] _requirements = Array.Empty<DailyPlanRouteRequirementBase>();
        [SerializeField] private DailyPlanRouteOutcomeBase[] _outcomes = Array.Empty<DailyPlanRouteOutcomeBase>();
        [SerializeField] private string _objectiveTitleKey = string.Empty;
        [SerializeField] private string _objectiveDetailKey = string.Empty;
        [SerializeField] private string _nextHintKey = string.Empty;

        public LocationDefinition TargetLocation => _targetLocation;
        public LocationGrowthRoute GrowthRoute => _growthRoute;
        public IReadOnlyList<LocationActivityDefinition> MatchingActivities => _matchingActivities;
        public IReadOnlyList<DailyPlanRouteRequirementBase> Requirements => _requirements;
        public IReadOnlyList<DailyPlanRouteOutcomeBase> Outcomes => _outcomes;
        public string ObjectiveTitleKey => _objectiveTitleKey;
        public string ObjectiveDetailKey => _objectiveDetailKey;
        public string NextHintKey => _nextHintKey;

        public bool IsAvailable(StudentLifeProgress progress)
        {
            if (progress == null)
            {
                return false;
            }

            for (var i = 0; i < _requirements.Length; i++)
            {
                if (_requirements[i] != null && !_requirements[i].IsSatisfied(progress))
                {
                    return false;
                }
            }

            return true;
        }

        public bool Matches(LocationActivityDefinition activity)
        {
            if (activity == null)
            {
                return false;
            }

            for (var i = 0; i < _matchingActivities.Length; i++)
            {
                if (_matchingActivities[i] == activity)
                {
                    return true;
                }
            }

            return _targetLocation != null
                && activity.Location == _targetLocation
                && activity.GrowthRoute == _growthRoute;
        }

        public string[] ApplyOutcomes(StudentLifeProgress progress)
        {
            if (progress == null)
            {
                return Array.Empty<string>();
            }

            var logs = new List<string>();
            for (var i = 0; i < _outcomes.Length; i++)
            {
                if (_outcomes[i] == null)
                {
                    continue;
                }

                var result = _outcomes[i].Apply(progress);
                if (!string.IsNullOrWhiteSpace(result))
                {
                    logs.Add(result);
                }
            }

            return logs.ToArray();
        }

        public void ConfigureForTests(
            string id,
            string displayNameKey,
            LocationDefinition targetLocation,
            LocationGrowthRoute growthRoute,
            LocationActivityDefinition[] matchingActivities,
            DailyPlanRouteRequirementBase[] requirements,
            DailyPlanRouteOutcomeBase[] outcomes,
            string objectiveTitleKey,
            string objectiveDetailKey,
            string nextHintKey)
        {
            ConfigureForTests(id, displayNameKey);
            _targetLocation = targetLocation;
            _growthRoute = growthRoute;
            _matchingActivities = matchingActivities ?? Array.Empty<LocationActivityDefinition>();
            _requirements = requirements ?? Array.Empty<DailyPlanRouteRequirementBase>();
            _outcomes = outcomes ?? Array.Empty<DailyPlanRouteOutcomeBase>();
            _objectiveTitleKey = objectiveTitleKey ?? string.Empty;
            _objectiveDetailKey = objectiveDetailKey ?? string.Empty;
            _nextHintKey = nextHintKey ?? string.Empty;
        }
    }

    [CreateAssetMenu(menuName = "Rootborn/Student Life/Daily Plan Route Set", fileName = "DailyPlanRouteSetDefinition")]
    public sealed class DailyPlanRouteSetDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private DailyPlanRouteDefinition[] _routes = Array.Empty<DailyPlanRouteDefinition>();
        [SerializeField] private int _maxSelectionsPerDay = 1;

        public IReadOnlyList<DailyPlanRouteDefinition> Routes => _routes;
        public int MaxSelectionsPerDay => Mathf.Max(1, _maxSelectionsPerDay);

        public DailyPlanRouteDefinition[] GetAvailableRoutes(StudentLifeProgress progress)
        {
            if (progress == null)
            {
                return Array.Empty<DailyPlanRouteDefinition>();
            }

            var routes = new List<DailyPlanRouteDefinition>();
            for (var i = 0; i < _routes.Length; i++)
            {
                if (_routes[i] != null && _routes[i].IsAvailable(progress))
                {
                    routes.Add(_routes[i]);
                }
            }

            return routes.ToArray();
        }

        public void ConfigureForTests(string id, string displayNameKey, DailyPlanRouteDefinition[] routes, int maxSelectionsPerDay)
        {
            ConfigureForTests(id, displayNameKey);
            _routes = routes ?? Array.Empty<DailyPlanRouteDefinition>();
            _maxSelectionsPerDay = Mathf.Max(1, maxSelectionsPerDay);
        }
    }

    public abstract class DailyPlanRouteRequirementBase : ScriptableObject
    {
        public abstract bool IsSatisfied(StudentLifeProgress progress);
    }

    public abstract class DailyPlanRouteOutcomeBase : ScriptableObject
    {
        public abstract string Apply(StudentLifeProgress progress);
    }
}
```

- [ ] **Step 2: Add progress, persistence, and runner**

Use Unity MCP `script-update-or-create` for `Assets/Scripts/Game/StudentLife/DailyPlanProgress.cs`:

```csharp
using System;
using System.Collections.Generic;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [Serializable]
    public sealed class DailyPlanProgressSaveData
    {
        public string saveSlot = string.Empty;
        public string playerId = string.Empty;
        public int currentDay = 1;
        public string selectedRouteId = string.Empty;
        public string completedRouteId = string.Empty;
        public string matchedActivityId = string.Empty;
        public string[] appliedCompletionKeys = Array.Empty<string>();
        public string lastNextHintKey = string.Empty;
    }

    public sealed class DailyPlanProgress
    {
        private readonly HashSet<string> _appliedCompletionKeys = new HashSet<string>(StringComparer.Ordinal);

        public DailyPlanProgress(string saveSlot, string playerId, int currentDay)
        {
            SaveSlot = saveSlot ?? string.Empty;
            PlayerId = playerId ?? string.Empty;
            CurrentDay = Mathf.Max(1, currentDay);
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public int CurrentDay { get; private set; }
        public string SelectedRouteId { get; private set; } = string.Empty;
        public string CompletedRouteId { get; private set; } = string.Empty;
        public string MatchedActivityId { get; private set; } = string.Empty;
        public string LastNextHintKey { get; private set; } = string.Empty;
        public IReadOnlyCollection<string> AppliedCompletionKeys => _appliedCompletionKeys;

        public bool SelectRoute(DailyPlanRouteDefinition route)
        {
            if (route == null || string.IsNullOrWhiteSpace(route.Id))
            {
                return false;
            }

            if (SelectedRouteId == route.Id)
            {
                return false;
            }

            SelectedRouteId = route.Id;
            CompletedRouteId = string.Empty;
            MatchedActivityId = string.Empty;
            LastNextHintKey = route.NextHintKey ?? string.Empty;
            _appliedCompletionKeys.Clear();
            return true;
        }

        public bool CompleteRoute(DailyPlanRouteDefinition route, LocationActivityDefinition activity, string requestKey)
        {
            if (route == null || activity == null || string.IsNullOrWhiteSpace(requestKey))
            {
                return false;
            }

            if (SelectedRouteId != route.Id || _appliedCompletionKeys.Contains(requestKey))
            {
                return false;
            }

            CompletedRouteId = route.Id;
            MatchedActivityId = activity.Id ?? string.Empty;
            LastNextHintKey = route.NextHintKey ?? string.Empty;
            _appliedCompletionKeys.Add(requestKey);
            return true;
        }

        public DailyPlanProgressSaveData ToSaveData()
        {
            return new DailyPlanProgressSaveData
            {
                saveSlot = SaveSlot,
                playerId = PlayerId,
                currentDay = CurrentDay,
                selectedRouteId = SelectedRouteId,
                completedRouteId = CompletedRouteId,
                matchedActivityId = MatchedActivityId,
                appliedCompletionKeys = new List<string>(_appliedCompletionKeys).ToArray(),
                lastNextHintKey = LastNextHintKey
            };
        }

        public static DailyPlanProgress FromSaveData(DailyPlanProgressSaveData data, string fallbackSaveSlot, string fallbackPlayerId, int fallbackDay)
        {
            var progress = new DailyPlanProgress(
                string.IsNullOrWhiteSpace(data?.saveSlot) ? fallbackSaveSlot : data.saveSlot,
                string.IsNullOrWhiteSpace(data?.playerId) ? fallbackPlayerId : data.playerId,
                data == null || data.currentDay <= 0 ? fallbackDay : data.currentDay);

            if (data == null)
            {
                return progress;
            }

            progress.SelectedRouteId = data.selectedRouteId ?? string.Empty;
            progress.CompletedRouteId = data.completedRouteId ?? string.Empty;
            progress.MatchedActivityId = data.matchedActivityId ?? string.Empty;
            progress.LastNextHintKey = data.lastNextHintKey ?? string.Empty;

            if (data.appliedCompletionKeys != null)
            {
                for (var i = 0; i < data.appliedCompletionKeys.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(data.appliedCompletionKeys[i]))
                    {
                        progress._appliedCompletionKeys.Add(data.appliedCompletionKeys[i]);
                    }
                }
            }

            return progress;
        }
    }

    public readonly struct DailyPlanCompletionResult
    {
        public DailyPlanCompletionResult(bool isCompleted, DailyPlanRouteDefinition route, LocationActivityDefinition activity, string[] resultLogs)
        {
            IsCompleted = isCompleted;
            Route = route;
            Activity = activity;
            ResultLogs = resultLogs ?? Array.Empty<string>();
        }

        public bool IsCompleted { get; }
        public DailyPlanRouteDefinition Route { get; }
        public LocationActivityDefinition Activity { get; }
        public string[] ResultLogs { get; }

        public static DailyPlanCompletionResult Empty => new DailyPlanCompletionResult(false, null, null, Array.Empty<string>());
    }

    public sealed class DailyPlanProgressPersistence
    {
        public const string FileName = "daily-plan-progress.json";

        public DailyPlanProgress LoadOrCreate(string saveSlot, string playerId, int currentDay)
        {
            var service = new SaveService(saveSlot);
            var json = service.ReadJson(FileName);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new DailyPlanProgress(saveSlot, playerId, currentDay);
            }

            var data = JsonUtility.FromJson<DailyPlanProgressSaveData>(json);
            return DailyPlanProgress.FromSaveData(data, saveSlot, playerId, currentDay);
        }

        public void Save(DailyPlanProgress progress)
        {
            if (progress == null)
            {
                return;
            }

            var service = new SaveService(progress.SaveSlot);
            service.WriteJson(FileName, JsonUtility.ToJson(progress.ToSaveData(), true));
        }
    }

    public sealed class DailyPlanRouteRunner
    {
        public DailyPlanRouteDefinition[] GetAvailableRoutes(DailyPlanRouteSetDefinition[] routeSets, StudentLifeProgress progress)
        {
            if (routeSets == null || progress == null)
            {
                return Array.Empty<DailyPlanRouteDefinition>();
            }

            var routes = new List<DailyPlanRouteDefinition>();
            for (var i = 0; i < routeSets.Length; i++)
            {
                if (routeSets[i] == null)
                {
                    continue;
                }

                routes.AddRange(routeSets[i].GetAvailableRoutes(progress));
            }

            return routes.ToArray();
        }

        public bool TrySelectRoute(DailyPlanProgress dailyProgress, DailyPlanRouteDefinition route, StudentLifeProgress studentProgress)
        {
            if (dailyProgress == null || route == null || !route.IsAvailable(studentProgress))
            {
                return false;
            }

            return dailyProgress.SelectRoute(route);
        }

        public bool TryRecordActivity(
            DailyPlanProgress dailyProgress,
            DailyPlanRouteSetDefinition[] routeSets,
            LocationActivityDefinition activity,
            StudentLifeProgress studentProgress,
            string requestKey,
            out DailyPlanCompletionResult result)
        {
            result = DailyPlanCompletionResult.Empty;
            if (dailyProgress == null || routeSets == null || activity == null || string.IsNullOrWhiteSpace(dailyProgress.SelectedRouteId))
            {
                return false;
            }

            var selectedRoute = FindRoute(routeSets, dailyProgress.SelectedRouteId);
            if (selectedRoute == null || !selectedRoute.Matches(activity))
            {
                return false;
            }

            if (!dailyProgress.CompleteRoute(selectedRoute, activity, requestKey))
            {
                return false;
            }

            var logs = selectedRoute.ApplyOutcomes(studentProgress);
            result = new DailyPlanCompletionResult(true, selectedRoute, activity, logs);
            return true;
        }

        public DailyPlanRouteDefinition FindRoute(DailyPlanRouteSetDefinition[] routeSets, string routeId)
        {
            if (routeSets == null || string.IsNullOrWhiteSpace(routeId))
            {
                return null;
            }

            for (var i = 0; i < routeSets.Length; i++)
            {
                var set = routeSets[i];
                if (set == null)
                {
                    continue;
                }

                for (var routeIndex = 0; routeIndex < set.Routes.Count; routeIndex++)
                {
                    var route = set.Routes[routeIndex];
                    if (route != null && route.Id == routeId)
                    {
                        return route;
                    }
                }
            }

            return null;
        }
    }
}
```

- [ ] **Step 3: Run domain tests**

Run:

```text
tests_run EditMode testClass DailyPlanRouteRunnerTests includeMessages=true
```

Expected: PASS for `DailyPlanRouteRunnerTests`.

- [ ] **Step 4: Commit domain slice**

```powershell
git add Assets/Scripts/Game/StudentLife/DailyPlanRouteDefinition.cs Assets/Scripts/Game/StudentLife/DailyPlanProgress.cs Assets/Tests/EditMode/StudentLife/DailyPlanRouteRunnerTests.cs
git commit -m "[FEATURE][TEST] Daily Plan domain model add"
```

## Task 3: Registry And Data Assets

**Files:**
- Modify: `Assets/Scripts/Game/Common/GameDataRegistry.cs`
- Modify: `Assets/Data/Registry/GameDataRegistry.asset`
- Create assets under: `Assets/Data/StudentLife/DailyPlans/`
- Create: `Assets/Tests/EditMode/StudentLife/DailyPlanRegistryTests.cs`

- [ ] **Step 1: Add failing registry tests**

Use Unity MCP `script-update-or-create` for `Assets/Tests/EditMode/StudentLife/DailyPlanRegistryTests.cs`:

```csharp
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.StudentLife;
using UnityEditor;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class DailyPlanRegistryTests
    {
        [Test]
        public void GameDataRegistry_ExposesDailyPlanRouteSets()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");

            Assert.That(registry, Is.Not.Null);
            Assert.That(registry.DailyPlanRouteSets, Is.Not.Null);
            Assert.That(registry.DailyPlanRouteSets.Length, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void DefaultDailyPlanRouteSet_HasThreeUsableRoutes()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            var set = registry.DailyPlanRouteSets[0];

            Assert.That(set.Id, Is.Not.Empty);
            Assert.That(set.Routes.Count, Is.GreaterThanOrEqualTo(3));
            foreach (var route in set.Routes)
            {
                Assert.That(route.Id, Is.Not.Empty);
                Assert.That(route.DisplayNameKey, Is.Not.Empty);
                Assert.That(route.ObjectiveTitleKey, Is.Not.Empty);
                Assert.That(route.ObjectiveDetailKey, Is.Not.Empty);
                Assert.That(route.NextHintKey, Is.Not.Empty);
                Assert.That(route.TargetLocation != null || route.MatchingActivities.Count > 0, Is.True);
            }
        }
    }
}
```

- [ ] **Step 2: Run failing registry tests**

Run:

```text
tests_run EditMode testClass DailyPlanRegistryTests includeMessages=true
```

Expected: FAIL because `GameDataRegistry.DailyPlanRouteSets` and route assets are not wired.

- [ ] **Step 3: Add registry field and property**

Use Unity MCP `script-read` to inspect `Assets/Scripts/Game/Common/GameDataRegistry.cs`, then use `script-update-or-create` to add:

```csharp
[SerializeField] private DailyPlanRouteSetDefinition[] _dailyPlanRouteSets = Array.Empty<DailyPlanRouteSetDefinition>();
public DailyPlanRouteSetDefinition[] DailyPlanRouteSets => _dailyPlanRouteSets;
```

Place the field near other Student Life registry arrays and the property near related Student Life properties.

- [ ] **Step 4: Create route assets through Unity asset APIs**

Use Unity Editor asset creation via MCP/reflection or inspector-safe asset creation. Create:

```text
Assets/Data/StudentLife/DailyPlans/LibrarySelfStudyRoute.asset
Assets/Data/StudentLife/DailyPlans/TownSquareHelpRoute.asset
Assets/Data/StudentLife/DailyPlans/WorkshopShiftRoute.asset
Assets/Data/StudentLife/DailyPlans/DefaultDailyPlanRouteSet.asset
```

Wire routes to existing `LocationDefinition` and `LocationActivityDefinition` assets:

```text
LibrarySelfStudyRoute:
  id: daily.library.self-study
  display: daily.route.library.self-study
  target location: Library
  growth route: SelfStudy
  objective title: objective.daily.library.title
  objective detail: objective.daily.library.detail
  next hint: objective.daily.library.next

TownSquareHelpRoute:
  id: daily.town-square.help
  display: daily.route.town-square.help
  target location: Town Square
  growth route: SocialHelp
  objective title: objective.daily.town-square.title
  objective detail: objective.daily.town-square.detail
  next hint: objective.daily.town-square.next

WorkshopShiftRoute:
  id: daily.workshop.shift
  display: daily.route.workshop.shift
  target location: Workshop
  growth route: Work
  objective title: objective.daily.workshop.title
  objective detail: objective.daily.workshop.detail
  next hint: objective.daily.workshop.next
```

Register `DefaultDailyPlanRouteSet.asset` in `GameDataRegistry.asset` under `_dailyPlanRouteSets`.

- [ ] **Step 5: Run registry tests**

Run:

```text
tests_run EditMode testClass DailyPlanRegistryTests includeMessages=true
```

Expected: PASS.

- [ ] **Step 6: Commit registry and data slice**

```powershell
git add Assets/Scripts/Game/Common/GameDataRegistry.cs Assets/Data/StudentLife/DailyPlans Assets/Data/Registry/GameDataRegistry.asset Assets/Tests/EditMode/StudentLife/DailyPlanRegistryTests.cs
git commit -m "[FEATURE][ASSET][TEST] Daily Plan route data register"
```

## Task 4: Runtime Progress Component

**Files:**
- Create: `Assets/Scripts/Game/StudentLife/DailyPlanProgressComponent.cs`
- Test through panel/runtime tests in later tasks.

- [ ] **Step 1: Add runtime component**

Use Unity MCP `script-update-or-create` for `Assets/Scripts/Game/StudentLife/DailyPlanProgressComponent.cs`:

```csharp
using System;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public sealed class DailyPlanProgressComponent : MonoBehaviour
    {
        [SerializeField] private string _saveSlot = "default";
        [SerializeField] private string _playerId = "local-player";

        private readonly DailyPlanProgressPersistence _persistence = new DailyPlanProgressPersistence();
        private DailyPlanProgress _progress;

        public event Action Changed;

        public DailyPlanProgress Progress => _progress;

        public void Load(string saveSlot, string playerId, int currentDay)
        {
            _saveSlot = string.IsNullOrWhiteSpace(saveSlot) ? "default" : saveSlot;
            _playerId = string.IsNullOrWhiteSpace(playerId) ? "local-player" : playerId;
            _progress = _persistence.LoadOrCreate(_saveSlot, _playerId, currentDay);
            Changed?.Invoke();
        }

        public bool TrySelect(DailyPlanRouteDefinition route, StudentLifeProgress studentProgress)
        {
            EnsureLoaded(studentProgress);
            var changed = new DailyPlanRouteRunner().TrySelectRoute(_progress, route, studentProgress);
            if (changed)
            {
                _persistence.Save(_progress);
                Changed?.Invoke();
            }

            return changed;
        }

        public bool TryRecordActivity(
            DailyPlanRouteSetDefinition[] routeSets,
            LocationActivityDefinition activity,
            StudentLifeProgress studentProgress,
            string requestKey,
            out DailyPlanCompletionResult result)
        {
            EnsureLoaded(studentProgress);
            var changed = new DailyPlanRouteRunner().TryRecordActivity(_progress, routeSets, activity, studentProgress, requestKey, out result);
            if (changed)
            {
                _persistence.Save(_progress);
                Changed?.Invoke();
            }

            return changed;
        }

        private void EnsureLoaded(StudentLifeProgress studentProgress)
        {
            if (_progress != null)
            {
                return;
            }

            var day = studentProgress == null ? 1 : Mathf.Max(1, studentProgress.CurrentDay);
            _progress = _persistence.LoadOrCreate(_saveSlot, _playerId, day);
        }
    }
}
```

- [ ] **Step 2: Compile and commit**

Run:

```text
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife includeMessages=true
```

Expected: PASS.

Commit:

```powershell
git add Assets/Scripts/Game/StudentLife/DailyPlanProgressComponent.cs
git commit -m "[FEATURE] Daily Plan runtime progress component add"
```

## Task 5: Objective Journal Integration

**Files:**
- Create: `Assets/Scripts/UI/Objectives/Sources/DailyPlanObjectiveSource.cs`
- Modify: `Assets/Scripts/UI/Objectives/Sources/ObjectiveJournalSourceCatalog.cs`
- Create: `Assets/Tests/EditMode/UI/Objectives/DailyPlanObjectiveSourceTests.cs`

- [ ] **Step 1: Add failing Objective Source tests**

Use Unity MCP `script-update-or-create` for `Assets/Tests/EditMode/UI/Objectives/DailyPlanObjectiveSourceTests.cs`:

```csharp
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Objectives;
using Rootborn.UI.Objectives.Sources;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI.Objectives
{
    public sealed class DailyPlanObjectiveSourceTests
    {
        [Test]
        public void Source_BeforeSelection_ShowsChooseRouteGoal()
        {
            var source = new DailyPlanObjectiveSource();
            var context = DailyPlanObjectiveSource.CreateContextForTests(null, System.Array.Empty<DailyPlanRouteSetDefinition>());

            var items = source.BuildItems(context);

            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(items[0].Category, Is.EqualTo(ObjectiveJournalCategory.Goals));
            Assert.That(items[0].StableKey, Does.Contain("daily-plan"));
        }

        [Test]
        public void Source_SelectedRoute_ShowsSelectedGoal()
        {
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            route.ConfigureForTests(
                "daily.library.self-study",
                "daily.route.library",
                null,
                LocationGrowthRoute.SelfStudy,
                System.Array.Empty<LocationActivityDefinition>(),
                System.Array.Empty<DailyPlanRouteRequirementBase>(),
                System.Array.Empty<DailyPlanRouteOutcomeBase>(),
                "objective.daily.library.title",
                "objective.daily.library.detail",
                "objective.daily.library.next");
            var set = ScriptableObject.CreateInstance<DailyPlanRouteSetDefinition>();
            set.ConfigureForTests("daily.default", "daily.default", new[] { route }, 1);
            var progress = new DailyPlanProgress("slot-a", "player-a", 1);
            progress.SelectRoute(route);
            var source = new DailyPlanObjectiveSource();
            var context = DailyPlanObjectiveSource.CreateContextForTests(progress, new[] { set });

            var items = source.BuildItems(context);

            Assert.That(items[0].StableKey, Is.EqualTo("daily-plan:daily.library.self-study"));
            Assert.That(items[0].TitleKey, Is.EqualTo("objective.daily.library.title"));
            Assert.That(items[0].DetailKey, Is.EqualTo("objective.daily.library.detail"));
        }

        [Test]
        public void SourceCatalog_RegistersDailyPlanSource()
        {
            var sources = ObjectiveJournalSourceCatalog.CreateDefaultSources();

            Assert.That(sources.Exists(source => source.SourceId == "daily-plan"), Is.True);
        }
    }
}
```

- [ ] **Step 2: Run failing Objective Source tests**

Run:

```text
tests_run EditMode testClass DailyPlanObjectiveSourceTests includeMessages=true
```

Expected: FAIL because `DailyPlanObjectiveSource` is not implemented and catalog does not register it.

- [ ] **Step 3: Implement DailyPlanObjectiveSource**

Use Unity MCP `script-update-or-create` for `Assets/Scripts/UI/Objectives/Sources/DailyPlanObjectiveSource.cs` after reading existing source interfaces. Implement this shape with the actual interface names from `StudentDayGuideObjectiveSource`:

```csharp
using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;

namespace Rootborn.UI.Objectives.Sources
{
    public sealed class DailyPlanObjectiveSource : IObjectiveJournalSource
    {
        public string SourceId => "daily-plan";

        public IReadOnlyList<ObjectiveJournalItem> BuildItems(ObjectiveJournalSourceContext context)
        {
            var dailyContext = context.Get<DailyPlanObjectiveContext>();
            return BuildItems(dailyContext);
        }

        public List<ObjectiveJournalItem> BuildItems(DailyPlanObjectiveContext context)
        {
            var items = new List<ObjectiveJournalItem>(1);
            if (context == null || context.Progress == null || string.IsNullOrWhiteSpace(context.Progress.SelectedRouteId))
            {
                items.Add(ObjectiveJournalItem.CreateGoal(
                    "daily-plan:choose",
                    "objective.daily.choose.title",
                    "objective.daily.choose.detail"));
                return items;
            }

            var route = new DailyPlanRouteRunner().FindRoute(context.RouteSets, context.Progress.SelectedRouteId);
            if (route == null)
            {
                items.Add(ObjectiveJournalItem.CreateGoal(
                    "daily-plan:choose",
                    "objective.daily.choose.title",
                    "objective.daily.choose.detail"));
                return items;
            }

            var detailKey = context.Progress.CompletedRouteId == route.Id ? route.NextHintKey : route.ObjectiveDetailKey;
            items.Add(ObjectiveJournalItem.CreateGoal(
                "daily-plan:" + route.Id,
                route.ObjectiveTitleKey,
                detailKey));
            return items;
        }

        public static DailyPlanObjectiveContext CreateContextForTests(DailyPlanProgress progress, DailyPlanRouteSetDefinition[] routeSets)
        {
            return new DailyPlanObjectiveContext(progress, routeSets);
        }
    }

    public sealed class DailyPlanObjectiveContext
    {
        public DailyPlanObjectiveContext(DailyPlanProgress progress, DailyPlanRouteSetDefinition[] routeSets)
        {
            Progress = progress;
            RouteSets = routeSets ?? Array.Empty<DailyPlanRouteSetDefinition>();
        }

        public DailyPlanProgress Progress { get; }
        public DailyPlanRouteSetDefinition[] RouteSets { get; }
    }
}
```

If the existing source interface has different factory names, keep these semantics and adapt only method signatures to the real interface.

- [ ] **Step 4: Register the source**

Modify `Assets/Scripts/UI/Objectives/Sources/ObjectiveJournalSourceCatalog.cs` with Unity MCP `script-update-or-create`:

```csharp
new DailyPlanObjectiveSource(),
```

Place it immediately after `new StudentDayGuideObjectiveSource()` so Daily Plan appears near other day guidance.

- [ ] **Step 5: Run Objective Journal tests**

Run:

```text
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives includeMessages=true
```

Expected: PASS.

- [ ] **Step 6: Commit Objective Journal slice**

```powershell
git add Assets/Scripts/UI/Objectives/Sources/DailyPlanObjectiveSource.cs Assets/Scripts/UI/Objectives/Sources/ObjectiveJournalSourceCatalog.cs Assets/Tests/EditMode/UI/Objectives/DailyPlanObjectiveSourceTests.cs
git commit -m "[FEATURE][UI][TEST] Daily Plan Objective Journal source add"
```

## Task 6: Plan Panel And Runtime Board

**Files:**
- Create: `Assets/Scripts/UI/StudentLife/DailyPlanPanel.cs`
- Create: `Assets/Scripts/UI/StudentLife/DailyPlanBoardInteractor.cs`
- Create: `Assets/Scripts/UI/StudentLife/DailyPlanRuntimeInstaller.cs`
- Create: `Assets/Tests/EditMode/UI/StudentLife/DailyPlanPanelTests.cs`

- [ ] **Step 1: Add failing panel tests**

Use Unity MCP `script-update-or-create` for `Assets/Tests/EditMode/UI/StudentLife/DailyPlanPanelTests.cs`:

```csharp
using NUnit.Framework;
using Rootborn.Game.StudentLife;
using Rootborn.UI.StudentLife;
using UnityEngine;

namespace Rootborn.Tests.EditMode.UI.StudentLife
{
    public sealed class DailyPlanPanelTests
    {
        [Test]
        public void Bind_NoRoutes_ShowsEmptyState()
        {
            var go = new GameObject("DailyPlanPanel");
            var panel = go.AddComponent<DailyPlanPanel>();

            panel.ConfigureForTests();
            panel.Bind(System.Array.Empty<DailyPlanRouteDefinition>(), null, null);

            Assert.That(panel.EmptyStateTextForTests, Is.EqualTo("objective.daily.choose.empty"));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Bind_MultipleRoutes_CreatesBoundedRows()
        {
            var go = new GameObject("DailyPlanPanel");
            var panel = go.AddComponent<DailyPlanPanel>();
            var route = ScriptableObject.CreateInstance<DailyPlanRouteDefinition>();
            route.ConfigureForTests(
                "daily.library.self-study",
                "daily.route.library",
                null,
                LocationGrowthRoute.SelfStudy,
                System.Array.Empty<LocationActivityDefinition>(),
                System.Array.Empty<DailyPlanRouteRequirementBase>(),
                System.Array.Empty<DailyPlanRouteOutcomeBase>(),
                "objective.daily.library.title",
                "objective.daily.library.detail",
                "objective.daily.library.next");

            panel.ConfigureForTests();
            panel.Bind(new[] { route }, null, null);

            Assert.That(panel.RouteRowCountForTests, Is.EqualTo(1));
            Assert.That(panel.UsesBoundedRouteListForTests, Is.True);
            Object.DestroyImmediate(go);
        }
    }
}
```

- [ ] **Step 2: Run failing panel tests**

Run:

```text
tests_run EditMode testClass DailyPlanPanelTests includeMessages=true
```

Expected: FAIL because `DailyPlanPanel` does not exist.

- [ ] **Step 3: Implement DailyPlanPanel**

Use Unity MCP `script-update-or-create` for `Assets/Scripts/UI/StudentLife/DailyPlanPanel.cs`:

```csharp
using System;
using Rootborn.Game.StudentLife;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.StudentLife
{
    public sealed class DailyPlanPanel : MonoBehaviour
    {
        private RectTransform _rowRoot;
        private Text _emptyText;

        public string EmptyStateTextForTests { get; private set; }
        public int RouteRowCountForTests { get; private set; }
        public bool UsesBoundedRouteListForTests { get; private set; }

        public void Bind(
            DailyPlanRouteDefinition[] routes,
            DailyPlanProgressComponent progressComponent,
            StudentLifeProgressComponent studentProgressComponent)
        {
            RouteRowCountForTests = routes == null ? 0 : routes.Length;
            UsesBoundedRouteListForTests = true;
            EmptyStateTextForTests = RouteRowCountForTests == 0 ? "objective.daily.choose.empty" : string.Empty;
            EnsureLayout();
            ClearRows();

            _emptyText.text = EmptyStateTextForTests;
            _emptyText.gameObject.SetActive(RouteRowCountForTests == 0);

            if (routes == null)
            {
                return;
            }

            for (var i = 0; i < routes.Length; i++)
            {
                var route = routes[i];
                if (route == null)
                {
                    continue;
                }

                var buttonObject = new GameObject("Daily Plan Route Row", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(_rowRoot, false);
                var rect = buttonObject.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(560f, 44f);

                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelObject.transform.SetParent(buttonObject.transform, false);
                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(12f, 4f);
                labelRect.offsetMax = new Vector2(-12f, -4f);
                var label = labelObject.GetComponent<Text>();
                label.text = route.DisplayNameKey;
                label.alignment = TextAnchor.MiddleLeft;
                label.raycastTarget = false;

                var capturedRoute = route;
                buttonObject.GetComponent<Button>().onClick.AddListener(() =>
                {
                    if (progressComponent != null && studentProgressComponent != null)
                    {
                        progressComponent.TrySelect(capturedRoute, studentProgressComponent.Progress);
                    }
                });
            }
        }

        public void ConfigureForTests()
        {
            UsesBoundedRouteListForTests = true;
        }
        private void EnsureLayout()
        {
            if (_rowRoot != null && _emptyText != null)
            {
                return;
            }

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null)
            {
                root = gameObject.AddComponent<RectTransform>();
            }

            root.sizeDelta = new Vector2(640f, 420f);
            if (gameObject.GetComponent<Image>() == null)
            {
                gameObject.AddComponent<Image>();
            }

            var viewportObject = new GameObject("Route Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportObject.transform.SetParent(transform, false);
            var viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(24f, 24f);
            viewport.offsetMax = new Vector2(-24f, -24f);

            var contentObject = new GameObject("Route Rows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportObject.transform, false);
            _rowRoot = contentObject.GetComponent<RectTransform>();
            _rowRoot.anchorMin = new Vector2(0f, 1f);
            _rowRoot.anchorMax = new Vector2(1f, 1f);
            _rowRoot.pivot = new Vector2(0.5f, 1f);
            contentObject.GetComponent<VerticalLayoutGroup>().spacing = 6f;
            contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = gameObject.GetComponent<ScrollRect>();
            if (scroll == null)
            {
                scroll = gameObject.AddComponent<ScrollRect>();
            }

            scroll.viewport = viewport;
            scroll.content = _rowRoot;
            scroll.horizontal = false;
            scroll.vertical = true;

            var emptyObject = new GameObject("Empty State", typeof(RectTransform), typeof(Text));
            emptyObject.transform.SetParent(transform, false);
            var emptyRect = emptyObject.GetComponent<RectTransform>();
            emptyRect.anchorMin = Vector2.zero;
            emptyRect.anchorMax = Vector2.one;
            emptyRect.offsetMin = new Vector2(24f, 24f);
            emptyRect.offsetMax = new Vector2(-24f, -24f);
            _emptyText = emptyObject.GetComponent<Text>();
            _emptyText.alignment = TextAnchor.MiddleCenter;
            _emptyText.raycastTarget = false;
        }

        private void ClearRows()
        {
            if (_rowRoot == null)
            {
                return;
            }

            for (var i = _rowRoot.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(_rowRoot.GetChild(i).gameObject);
            }
        }
    }
}
```

- [ ] **Step 4: Implement board and installer**

Use Unity MCP `script-update-or-create` for `Assets/Scripts/UI/StudentLife/DailyPlanBoardInteractor.cs`:

```csharp
using UnityEngine;

namespace Rootborn.UI.StudentLife
{
    public sealed class DailyPlanBoardInteractor : MonoBehaviour
    {
        [SerializeField] private DailyPlanPanel _panel;

        public string PromptText => "[E] Plan Day";

        public void Interact()
        {
            if (_panel != null)
            {
                _panel.gameObject.SetActive(true);
            }
        }

        public void ConfigureForTests(DailyPlanPanel panel)
        {
            _panel = panel;
        }
    }
}
```

Use Unity MCP `script-update-or-create` for `Assets/Scripts/UI/StudentLife/DailyPlanRuntimeInstaller.cs`:

```csharp
using UnityEngine;

namespace Rootborn.UI.StudentLife
{
    public sealed class DailyPlanRuntimeInstaller : MonoBehaviour
    {
        private const string BoardName = "Daily Plan Board";
        private const string PanelName = "Daily Plan Panel";

        public void EnsureScene()
        {
            var panel = FindFirstObjectByType<DailyPlanPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                var panelGo = new GameObject(PanelName);
                panel = panelGo.AddComponent<DailyPlanPanel>();
                panelGo.SetActive(false);
            }

            var board = FindFirstObjectByType<DailyPlanBoardInteractor>();
            if (board == null)
            {
                var boardGo = new GameObject(BoardName);
                boardGo.transform.position = new Vector3(-1.5f, -2.75f, 0f);
                board = boardGo.AddComponent<DailyPlanBoardInteractor>();
            }

            board.ConfigureForTests(panel);
        }
    }
}
```

Wire this installer into the same Town runtime bootstrap path used by `StudentDayRuntimeInstaller`.

- [ ] **Step 5: Run panel tests and Town installer regression**

Run:

```text
tests_run EditMode testClass DailyPlanPanelTests includeMessages=true
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalTownE2EScenarioTests.OBJECTIVE_JOURNAL_E2E_001_OpenTrackRefreshAndReload includeMessages=true
```

Expected: PASS.

- [ ] **Step 6: Commit UI board slice**

```powershell
git add Assets/Scripts/UI/StudentLife/DailyPlanPanel.cs Assets/Scripts/UI/StudentLife/DailyPlanBoardInteractor.cs Assets/Scripts/UI/StudentLife/DailyPlanRuntimeInstaller.cs Assets/Tests/EditMode/UI/StudentLife/DailyPlanPanelTests.cs
git commit -m "[FEATURE][UI][TEST] Daily Plan board and panel add"
```

## Task 7: Day Result Integration

**Files:**
- Modify: `Assets/Scripts/UI/StudentLife/StudentDayResultPanel.cs`
- Extend or create tests near existing day result tests.

- [ ] **Step 1: Add failing day result test**

Find the existing `StudentDayResultPanel` tests with:

```powershell
rg -n "StudentDayResultPanel|ResultsTextForTests|NextGuideTextForTests" Assets/Tests
```

Add a test asserting that a completed Daily Plan summary appears in result text:

```csharp
[Test]
public void Show_WithDailyPlanSummary_AppendsPlanAndActivity()
{
    var panel = CreatePanelForTests();
    var summary = new DailyPlanDaySummary(
        "daily.route.library",
        "activity.library.read",
        true,
        new[] { "daily.result.focus" },
        "objective.daily.library.next");

    panel.ShowDailyPlanSummaryForTests(summary);

    Assert.That(panel.ResultsTextForTests, Does.Contain("daily.route.library"));
    Assert.That(panel.ResultsTextForTests, Does.Contain("activity.library.read"));
    Assert.That(panel.ResultsTextForTests, Does.Contain("daily.result.focus"));
    Assert.That(panel.NextGuideTextForTests, Does.Contain("objective.daily.library.next"));
}
```

- [ ] **Step 2: Run failing day result test**

Run the exact test method after adding it:

```text
tests_run EditMode testMethod <fully.qualified.day.result.test.method> includeMessages=true
```

Expected: FAIL because `DailyPlanDaySummary` and panel summary method do not exist.

- [ ] **Step 3: Implement compact summary support**

Use Unity MCP `script-update-or-create` to update `StudentDayResultPanel.cs` with:

```csharp
public readonly struct DailyPlanDaySummary
{
    public DailyPlanDaySummary(string routeDisplayNameKey, string activityDisplayNameKey, bool completed, string[] resultLogs, string nextHintKey)
    {
        RouteDisplayNameKey = routeDisplayNameKey ?? string.Empty;
        ActivityDisplayNameKey = activityDisplayNameKey ?? string.Empty;
        Completed = completed;
        ResultLogs = resultLogs ?? System.Array.Empty<string>();
        NextHintKey = nextHintKey ?? string.Empty;
    }

    public string RouteDisplayNameKey { get; }
    public string ActivityDisplayNameKey { get; }
    public bool Completed { get; }
    public string[] ResultLogs { get; }
    public string NextHintKey { get; }
}
```

Add a panel method used by tests and by the day-end flow:

```csharp
public void AppendDailyPlanSummary(DailyPlanDaySummary summary)
{
    if (string.IsNullOrWhiteSpace(summary.RouteDisplayNameKey))
    {
        return;
    }

    AppendResultLine(summary.RouteDisplayNameKey);
    if (!string.IsNullOrWhiteSpace(summary.ActivityDisplayNameKey))
    {
        AppendResultLine(summary.ActivityDisplayNameKey);
    }

    for (var i = 0; i < summary.ResultLogs.Length; i++)
    {
        AppendResultLine(summary.ResultLogs[i]);
    }

    if (!string.IsNullOrWhiteSpace(summary.NextHintKey))
    {
        AppendNextGuideLine(summary.NextHintKey);
    }
}
```

Use the actual private append helper names from `StudentDayResultPanel.cs`; if no helper exists, add private helpers that update the same text fields used by `ResultsTextForTests` and `NextGuideTextForTests`.

- [ ] **Step 4: Wire Daily Plan completion into day-end flow**

In the existing day-end controller path, build `DailyPlanDaySummary` from `DailyPlanProgressComponent.Progress` and the selected route found through `GameDataRegistry.DailyPlanRouteSets`. Pass it to `StudentDayResultPanel` only when a selected route exists.

- [ ] **Step 5: Run day result tests**

Run:

```text
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.StudentLife includeMessages=true
```

Expected: PASS.

- [ ] **Step 6: Commit day result slice**

```powershell
git add Assets/Scripts/UI/StudentLife/StudentDayResultPanel.cs Assets/Tests/EditMode
git commit -m "[FEATURE][UI][TEST] Daily Plan day result summary add"
```

## Task 8: Player-Facing PlayMode E2E

**Files:**
- Create: `Assets/Tests/PlayMode/EndToEnd/DailyPlanRouteSelectionE2EScenarioTests.cs`

- [ ] **Step 1: Add failing E2E test**

Use Unity MCP `script-update-or-create` for `Assets/Tests/PlayMode/EndToEnd/DailyPlanRouteSelectionE2EScenarioTests.cs` and reuse existing helper patterns from nearby E2E tests:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class DailyPlanRouteSelectionE2EScenarioTests
    {
        [UnityTest]
        public IEnumerator DAILY_PLAN_E2E_001_SaveSlotSelectRoutePlayActivityResultReload()
        {
            yield return ScenarioFlow.StartAtMainMenu();
            yield return ScenarioFlow.SelectNewGame();
            yield return ScenarioFlow.ConfirmSpumCharacter();
            yield return ScenarioFlow.WaitForTown();

            yield return ScenarioFlow.MovePlayerToPrompt("[E] Plan Day");
            yield return ScenarioFlow.PressInteract();
            yield return ScenarioFlow.ClickVisibleButtonContaining("daily.route.library");

            yield return ScenarioFlow.PressTab();
            yield return ScenarioFlow.AssertObjectiveJournalGoalsContains("objective.daily.library.title");
            yield return ScenarioFlow.TrackObjective("daily-plan:daily.library.self-study");
            yield return ScenarioFlow.PressTab();

            yield return ScenarioFlow.MovePlayerToLocation("Library");
            yield return ScenarioFlow.PressInteract();
            yield return ScenarioFlow.ClickVisibleButtonContaining("activity.library");

            yield return ScenarioFlow.PressTab();
            yield return ScenarioFlow.AssertObjectiveJournalGoalsContains("objective.daily.library.next");
            yield return ScenarioFlow.AssertTrackedObjectiveKey("daily-plan:daily.library.self-study");
            yield return ScenarioFlow.PressTab();

            yield return ScenarioFlow.MovePlayerToPrompt("[E] End Day");
            yield return ScenarioFlow.PressInteract();
            yield return ScenarioFlow.AssertDayResultContains("daily.route.library");
            yield return ScenarioFlow.AssertDayResultContains("activity.library");
            yield return ScenarioFlow.CaptureGameView("Builds/Logs/daily-plan-route-e2e.png");

            yield return ScenarioFlow.ReloadCurrentSaveSlot();
            yield return ScenarioFlow.PressTab();
            yield return ScenarioFlow.AssertObjectiveJournalGoalsContains("objective.daily.library.next");
            yield return ScenarioFlow.AssertNoDuplicateDailyPlanCompletionLogs();
        }
    }
}
```

Replace `ScenarioFlow` calls with the exact helper names already used by `ObjectiveJournalTownE2EScenarioTests` and `StudentDayResultLoopE2EScenarioTests`. Keep the scenario steps and assertions intact.

- [ ] **Step 2: Run failing E2E**

Run:

```text
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteSelectionE2EScenarioTests.DAILY_PLAN_E2E_001_SaveSlotSelectRoutePlayActivityResultReload includeMessages=true includeLogs=true logType=Warning
```

Expected: FAIL on the first missing player-facing integration point.

- [ ] **Step 3: Fix runtime flow until E2E passes**

Use failures to wire:

- DailyPlanRuntimeInstaller into Town startup.
- DailyPlanBoardInteractor into `PlayerInteractionRouter` prompt path.
- DailyPlanPanel route buttons to `DailyPlanProgressComponent.TrySelect`.
- Location activity completion path to `DailyPlanProgressComponent.TryRecordActivity`.
- Objective Journal context creation to include Daily Plan progress and route sets.
- Day result summary to include selected/completed route.
- Save slot reload to load `daily-plan-progress.json` before journal refresh.

After each fix, rerun the E2E command from Step 2.

- [ ] **Step 4: Verify screenshot evidence**

Run:

```powershell
Get-ChildItem Builds/Logs/daily-plan-route-e2e.png | Select-Object Name,Length,LastWriteTime
```

Expected: file exists and `Length` is greater than `0`.

- [ ] **Step 5: Commit E2E slice**

```powershell
git add Assets/Tests/PlayMode/EndToEnd/DailyPlanRouteSelectionE2EScenarioTests.cs Assets/Scripts Assets/Data
git commit -m "[FEATURE][UI][TEST] Daily Plan player flow E2E add"
```

## Task 9: Full Verification Gate

**Files:**
- All changed files.

- [ ] **Step 1: Run required EditMode gates**

Run:

```text
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife includeMessages=true
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives includeMessages=true
```

Expected: PASS.

- [ ] **Step 2: Run required PlayMode gates**

Run:

```text
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteSelectionE2EScenarioTests.DAILY_PLAN_E2E_001_SaveSlotSelectRoutePlayActivityResultReload includeMessages=true includeLogs=true logType=Warning
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ObjectiveJournalTownE2EScenarioTests.OBJECTIVE_JOURNAL_E2E_001_OpenTrackRefreshAndReload includeMessages=true includeLogs=true logType=Warning
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.StudentDayResultLoopE2EScenarioTests.STUDENT_DAY_E2E_001_011_PlayedActivityEndDayResultReloadAndNextDayLoop includeMessages=true includeLogs=true logType=Warning
```

Expected: PASS.

- [ ] **Step 3: Run static gates**

Run:

```powershell
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
Get-ChildItem Builds/Logs/daily-plan-route-e2e.png | Select-Object Name,Length,LastWriteTime
```

Expected:

- `git diff --check` exits `0`.
- entity-id branching gate exits `0`.
- screenshot exists and has non-zero length.

- [ ] **Step 4: Inspect visual result**

Enter PlayMode and reproduce:

```text
MainMenu -> New Game -> SPUM Confirm -> Town -> Daily Plan board -> select Library route -> Tab Objective Journal -> track route -> Library activity -> day-end board -> result UI -> reload save -> Tab Objective Journal
```

Verify:

- `[E] Plan Day` board is visible and not duplicated.
- `DailyPlanPanel` is bounded or scrollable.
- Objective Journal `Goals` shows the selected route.
- tracked HUD shows only the tracked Daily Plan row after tracking.
- result UI shows plan, actual action, result log, and next hint.
- reload restores route context and does not duplicate completion logs.

- [ ] **Step 5: Final commit if verification required code changes**

```powershell
git status --short
git add <only Daily Plan related changed files>
git commit -m "[FEATURE][UI][TEST] Daily Plan route selection verify"
```

## Final Completion Checklist

- [ ] Daily Plan route definitions and route set assets are ScriptableObject/data-driven.
- [ ] `GameDataRegistry.asset` registers the default route set.
- [ ] Production code has no route/activity/location/career id-specific branching.
- [ ] Player selects a route through visible UI, not a direct method call.
- [ ] Matching real Town activity completes the selected plan.
- [ ] Objective Journal owns all guidance states.
- [ ] `TrackedObjectiveHud` remains the only always-visible objective HUD.
- [ ] Day result UI shows selected route, actual activity, result log, and next hint.
- [ ] Save/load restores context without duplicate completion.
- [ ] Game View screenshot exists at `Builds/Logs/daily-plan-route-e2e.png`.
- [ ] Required EditMode, PlayMode, static, and visual gates pass.
- [ ] All C# edits were made through Unity MCP script tools.
- [ ] Unrelated dirty files were not reverted or staged.
