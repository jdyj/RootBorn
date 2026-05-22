# Town Modern Interactable Sprites Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Town's placeholder/empty interactable marker sprites with registered Modern-series sprites and prove the result through EditMode, PlayMode, runtime audit, and Game View screenshot verification.

**Architecture:** Add a small data-driven visual profile registered in `GameDataRegistry`. Runtime installers keep creating/binding interactables, but they delegate sprite selection to a generic `TownInteractableVisualApplicator` that reads profile entries by scene object name and role, without gameplay entity ID branches. Tests first prove current null/placeholder sprites, then implementation wires Modern sprites and validates the player-facing flow.

**Tech Stack:** Unity 6000.3.13f1, C#, Unity Test Framework EditMode/PlayMode, Unity MCP `script-update-or-create` for all `Assets/**/*.cs` changes, Unity MCP asset tools/script execution for `.asset` creation and registry wiring.

---

## File Structure

**Create**

- `Assets/Scripts/Game/World/TownInteractableVisualProfile.cs`
  - ScriptableObject profile with entries that map either exact scene object names or generic visual roles to a `Sprite`, sorting order, scale, and collider size.
- `Assets/Scripts/Game/World/TownInteractableVisualApplicator.cs`
  - Static helper used by installers to apply the profile to a `SpriteRenderer`/`BoxCollider2D`.
- `Assets/Tests/EditMode/TownModernInteractableSpriteTests.cs`
  - Saved/runtime-compatible audit for placeholder sprites and approved Modern asset paths.
- `Assets/Tests/PlayMode/EndToEnd/TownModernInteractableSpriteE2ETests.cs`
  - Representative player-facing flow plus screenshot capture.
- `Assets/Data/StudentLife/TownInteractableVisuals/TownInteractableVisualProfile.asset`
  - Data profile containing the selected Modern sprite references.

**Modify**

- `Assets/Scripts/Game/Common/GameDataRegistry.cs`
  - Add one serialized `TownInteractableVisualProfile _townInteractableVisualProfile` and public property.
- `Assets/Scripts/UI/StudentLife/LocationNpcRuntimeInstaller.cs`
  - Replace fallback marker assignment with `TownInteractableVisualApplicator.Apply`.
- `Assets/Scripts/UI/StudentLife/DailyPlanRuntimeInstaller.cs`
  - Apply board visual role.
- `Assets/Scripts/UI/StudentLife/TownHelpActionRuntimeInstaller.cs`
  - Apply help visual role.
- `Assets/Scripts/UI/StudentLife/TownExplorationDiscoveryRuntimeInstaller.cs`
  - Apply discovery visual role.
- `Assets/Scripts/UI/StudentLife/TownExplorationInteractionRuntimeInstaller.cs`
  - Apply exploration object visual role.
- `Assets/Scripts/UI/StudentLife/OutsideSchoolRuntimeInstaller.cs`
  - Apply outside-school activity visual role.
- `Assets/Scripts/UI/Tiles/TownTilePlacementRuntimeInstaller.cs`
  - Apply tile board visual role.
- `Assets/Scripts/Game/StudentLife/PartTimeWorkRuntimeInstaller.cs`
  - Apply work/shop visual role.
- `Assets/Scripts/Game/StudentLife/StudentLifeSceneInteraction.cs`
  - Apply student-life activity and career board visual roles where runtime scene interactors are configured.
- `Assets/Scripts/UI/StudentLife/StudentDayRuntimeInstaller.cs`
  - Apply day-end board visual role.
- `Assets/Data/Registry/GameDataRegistry.asset`
  - Register the `TownInteractableVisualProfile.asset` reference.

**Do Not Modify**

- Do not add NPC/location/activity-specific C# classes.
- Do not branch in C# on `npcId`, `locationId`, `activityId`, or object-specific IDs.
- Do not use Pixelwood fallback sprites.
- Do not directly write `Assets/**/*.cs` from shell or `apply_patch`; use Unity MCP `script-update-or-create`.

---

## Task 1: Add Failing EditMode Sprite Audit

**Files:**
- Create: `Assets/Tests/EditMode/TownModernInteractableSpriteTests.cs`

- [ ] **Step 1: Create the failing EditMode test with MCP `script-update-or-create`**

Use Unity MCP `script-update-or-create` for `Assets/Tests/EditMode/TownModernInteractableSpriteTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    public sealed class TownModernInteractableSpriteTests
    {
        private const string TownScenePath = "Assets/Scenes/Town.unity";

        private static readonly HashSet<string> PlaceholderSpriteNames = new HashSet<string>
        {
            "TownHelpActionMarker",
            "TownDiscoveryMarker",
            "DailyPlanBoardMarker",
            "StudentLifeActivityMarker",
            "StudentDayEndMarker",
            "OutsideSchoolActivityMarker",
            "TownLocationNpcMarker",
            "TilePlacementMarker",
            "TownExplorationInteractionMarker",
            string.Empty
        };

        private static readonly string[] ApprovedModernRoots =
        {
            "Assets/Modern_Town/",
            "Assets/Modern_Farm_v1.2/",
            "Assets/moderninteriors-win/",
            "Assets/modernuserinterface-win/",
            "Assets/Tiles/ModernTownOffice/"
        };

        private static readonly string[] ExpectedVisibleTargets =
        {
            "TownHelpActionPoint",
            "TownDiscoveryPoint",
            "TownHelpActionPoint_town_help_teacher_study_prep",
            "TownReturnPortal",
            "DailyPlanBoard",
            "SchoolEntryActivity",
            "StudentDayEndBoard",
            "StudyBasicsActivity",
            "CareerPracticeBoard",
            "GuideNpc",
            "OutsideSchoolActivityPoint",
            "TownNpc_npc_teacher",
            "TilePlacementBoard",
            "OutsideSchoolActivityPoint_1",
            "PartTimeWorkPoint",
            "LocationVisit_location_library",
            "LocationVisit_location_town_square",
            "TownExplorationInteractionPoint",
            "TownNpc_npc_librarian",
            "TownNpc_npc_first_guide",
            "LocationVisit_location_workshop",
            "LocationVisit_location_school",
            "TownNpc_npc_work_manager",
            "LocationVisit_location_corner_store",
            "TownNpc_npc_shopkeeper"
        };

        [Test]
        public void TownInteractablesHaveNonPlaceholderSprites()
        {
            var scene = EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);
            var interactables = CollectInteractableGameObjects(scene.GetRootGameObjects());

            foreach (string expected in ExpectedVisibleTargets)
            {
                Assert.IsTrue(interactables.Any(go => go.name == expected), "Expected Town interactable target missing: " + expected);
            }

            var failures = new List<string>();
            foreach (var go in interactables.OrderBy(value => value.name))
            {
                var renderer = go.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(value => value != null && value.enabled);
                if (renderer == null)
                {
                    failures.Add(go.name + " has no enabled SpriteRenderer.");
                    continue;
                }

                string spriteName = renderer.sprite != null ? renderer.sprite.name : string.Empty;
                if (renderer.sprite == null || PlaceholderSpriteNames.Contains(spriteName))
                {
                    failures.Add(go.name + " uses placeholder/null sprite '" + spriteName + "'.");
                }
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void TownInteractableSpritesUseModernAssetPaths()
        {
            var scene = EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Single);
            var interactables = CollectInteractableGameObjects(scene.GetRootGameObjects());
            var failures = new List<string>();

            foreach (var go in interactables.OrderBy(value => value.name))
            {
                var renderer = go.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(value => value != null && value.enabled);
                if (renderer == null || renderer.sprite == null)
                {
                    failures.Add(go.name + " has no sprite to resolve.");
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(renderer.sprite);
                if (!ApprovedModernRoots.Any(root => path.StartsWith(root, StringComparison.Ordinal)))
                {
                    failures.Add(go.name + " sprite path is not Modern-approved: " + renderer.sprite.name + " path=" + path);
                }
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        private static HashSet<GameObject> CollectInteractableGameObjects(GameObject[] roots)
        {
            var result = new HashSet<GameObject>();
            foreach (var root in roots)
            {
                var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var behaviour in behaviours)
                {
                    if (behaviour is IPlayerInteractable)
                    {
                        result.Add(behaviour.gameObject);
                    }
                }
            }

            return result;
        }
    }
}
```

- [ ] **Step 2: Run the EditMode test to verify RED**

Run:

```text
tests_run EditMode testClass Rootborn.Tests.EditMode.TownModernInteractableSpriteTests
```

Expected: FAIL because current Town interactables still use placeholder marker sprite names or runtime-generated sprites with no asset path.

- [ ] **Step 3: Commit the failing test**

```bash
git add Assets/Tests/EditMode/TownModernInteractableSpriteTests.cs
git commit -m "[TEST] Town Modern 상호작용 스프라이트 감사 테스트 추가" -m "Town 상호작용 오브젝트가 null/placeholder sprite를 쓰지 않고 Modern 경로 sprite를 참조해야 함을 먼저 실패 테스트로 고정했다."
```

---

## Task 2: Add Data Model And Registry Hook

**Files:**
- Create: `Assets/Scripts/Game/World/TownInteractableVisualProfile.cs`
- Modify: `Assets/Scripts/Game/Common/GameDataRegistry.cs`
- Create in Task 4: `Assets/Data/StudentLife/TownInteractableVisuals/TownInteractableVisualProfile.asset`

- [ ] **Step 1: Create `TownInteractableVisualProfile` with MCP `script-update-or-create`**

Use Unity MCP `script-update-or-create` for `Assets/Scripts/Game/World/TownInteractableVisualProfile.cs`:

```csharp
using System;
using UnityEngine;

namespace Rootborn.Game.World
{
    public enum TownInteractableVisualRole
    {
        None = 0,
        Npc = 10,
        Location = 20,
        Activity = 30,
        Board = 40,
        Portal = 50,
        Help = 60,
        Discovery = 70,
        Exploration = 80,
        Work = 90,
        DayEnd = 100,
        TileBoard = 110,
        OutsideSchool = 120
    }

    [Serializable]
    public sealed class TownInteractableVisualEntry
    {
        [SerializeField] private string _targetName;
        [SerializeField] private TownInteractableVisualRole _role;
        [SerializeField] private Sprite _sprite;
        [SerializeField] private int _sortingOrder = 50;
        [SerializeField] private Vector2 _colliderSize = new Vector2(0.85f, 0.85f);
        [SerializeField] private Vector3 _scale = Vector3.one;

        public string TargetName => _targetName;
        public TownInteractableVisualRole Role => _role;
        public Sprite Sprite => _sprite;
        public int SortingOrder => _sortingOrder;
        public Vector2 ColliderSize => _colliderSize;
        public Vector3 Scale => _scale;

        public void ConfigureForTests(string targetName, TownInteractableVisualRole role, Sprite sprite, int sortingOrder, Vector2 colliderSize, Vector3 scale)
        {
            _targetName = targetName ?? string.Empty;
            _role = role;
            _sprite = sprite;
            _sortingOrder = sortingOrder;
            _colliderSize = colliderSize;
            _scale = scale;
        }
    }

    [CreateAssetMenu(fileName = "TownInteractableVisualProfile", menuName = "Rootborn/World/Town Interactable Visual Profile")]
    public sealed class TownInteractableVisualProfile : ScriptableObject
    {
        [SerializeField] private TownInteractableVisualEntry[] _entries = Array.Empty<TownInteractableVisualEntry>();

        public TownInteractableVisualEntry[] Entries => _entries;

        public bool TryResolve(string targetName, TownInteractableVisualRole role, out TownInteractableVisualEntry entry)
        {
            entry = null;
            if (_entries == null || _entries.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < _entries.Length; i++)
            {
                var candidate = _entries[i];
                if (candidate == null || candidate.Sprite == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(candidate.TargetName) && candidate.TargetName == targetName)
                {
                    entry = candidate;
                    return true;
                }
            }

            for (int i = 0; i < _entries.Length; i++)
            {
                var candidate = _entries[i];
                if (candidate == null || candidate.Sprite == null)
                {
                    continue;
                }

                if (candidate.Role == role && string.IsNullOrEmpty(candidate.TargetName))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        public void ConfigureForTests(TownInteractableVisualEntry[] entries)
        {
            _entries = entries ?? Array.Empty<TownInteractableVisualEntry>();
        }
    }
}
```

- [ ] **Step 2: Modify `GameDataRegistry` with MCP `script-update-or-create`**

Add the namespace:

```csharp
using Rootborn.Game.World;
```

Add the serialized field near the other Town/World references:

```csharp
[SerializeField] private TownInteractableVisualProfile _townInteractableVisualProfile;
```

Add the property near the other public registry properties:

```csharp
public TownInteractableVisualProfile TownInteractableVisualProfile => _townInteractableVisualProfile;
```

- [ ] **Step 3: Run compile-focused tests**

Run:

```text
tests_run EditMode testClass Rootborn.Tests.EditMode.TownModernInteractableSpriteTests
```

Expected: still FAIL for placeholder sprites, but no compilation errors.

- [ ] **Step 4: Commit data model**

```bash
git add Assets/Scripts/Game/World/TownInteractableVisualProfile.cs Assets/Scripts/Game/Common/GameDataRegistry.cs
git commit -m "[FEATURE] Town 상호작용 비주얼 프로필 모델 추가" -m "Town 상호작용 sprite를 C# ID 분기 없이 데이터 프로필과 Registry 참조로 해석할 수 있는 기반을 추가했다."
```

---

## Task 3: Add Generic Runtime Applicator

**Files:**
- Create: `Assets/Scripts/Game/World/TownInteractableVisualApplicator.cs`

- [ ] **Step 1: Write the applicator test first if existing test utilities allow isolated EditMode object tests**

Use Unity MCP `script-update-or-create` to extend `Assets/Tests/EditMode/TownModernInteractableSpriteTests.cs` with:

```csharp
[Test]
public void TownInteractableVisualProfileResolvesExactNameBeforeRoleFallback()
{
    var roleSprite = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 16f);
    roleSprite.name = "role-sprite";
    var exactSprite = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 16f);
    exactSprite.name = "exact-sprite";

    var role = new Rootborn.Game.World.TownInteractableVisualEntry();
    role.ConfigureForTests(string.Empty, Rootborn.Game.World.TownInteractableVisualRole.Board, roleSprite, 10, Vector2.one, Vector3.one);
    var exact = new Rootborn.Game.World.TownInteractableVisualEntry();
    exact.ConfigureForTests("DailyPlanBoard", Rootborn.Game.World.TownInteractableVisualRole.Board, exactSprite, 11, Vector2.one * 2f, Vector3.one * 3f);

    var profile = ScriptableObject.CreateInstance<Rootborn.Game.World.TownInteractableVisualProfile>();
    profile.ConfigureForTests(new[] { role, exact });

    Assert.IsTrue(profile.TryResolve("DailyPlanBoard", Rootborn.Game.World.TownInteractableVisualRole.Board, out var resolved));
    Assert.AreSame(exactSprite, resolved.Sprite);
    Assert.AreEqual(11, resolved.SortingOrder);
    Assert.AreEqual(Vector2.one * 2f, resolved.ColliderSize);
    Assert.AreEqual(Vector3.one * 3f, resolved.Scale);
}
```

- [ ] **Step 2: Run the new isolated test to verify RED/GREEN boundary**

Run:

```text
tests_run EditMode testMethod Rootborn.Tests.EditMode.TownModernInteractableSpriteTests.TownInteractableVisualProfileResolvesExactNameBeforeRoleFallback
```

Expected before Task 2 implementation: compile failure or FAIL. Expected after Task 2 implementation: PASS.

- [ ] **Step 3: Create `TownInteractableVisualApplicator` with MCP `script-update-or-create`**

Use Unity MCP `script-update-or-create` for `Assets/Scripts/Game/World/TownInteractableVisualApplicator.cs`:

```csharp
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using UnityEngine;

namespace Rootborn.Game.World
{
    public static class TownInteractableVisualApplicator
    {
        public static bool Apply(GameObject target, TownInteractableVisualRole role, int fallbackSortingOrder, Vector2 fallbackColliderSize)
        {
            return Apply(target, role, ResolveProfile(), fallbackSortingOrder, fallbackColliderSize);
        }

        public static bool Apply(GameObject target, TownInteractableVisualRole role, TownInteractableVisualProfile profile, int fallbackSortingOrder, Vector2 fallbackColliderSize)
        {
            if (target == null)
            {
                return false;
            }

            var renderer = target.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = target.AddComponent<SpriteRenderer>();
            }

            if (profile != null && profile.TryResolve(target.name, role, out var entry) && entry != null && entry.Sprite != null)
            {
                renderer.sprite = entry.Sprite;
                renderer.sortingOrder = entry.SortingOrder;
                target.transform.localScale = entry.Scale;
                ApplyCollider(target, entry.ColliderSize);
                return true;
            }

            renderer.sortingOrder = fallbackSortingOrder;
            ApplyCollider(target, fallbackColliderSize);
            return false;
        }

        private static void ApplyCollider(GameObject target, Vector2 size)
        {
            var collider = target.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = target.AddComponent<BoxCollider2D>();
            }

            collider.isTrigger = true;
            collider.size = size.sqrMagnitude > 0.0001f ? size : Vector2.one * 0.85f;
        }

        private static TownInteractableVisualProfile ResolveProfile()
        {
            GameDataRegistry registry = Managers.Data != null ? Managers.Data.Registry : null;
            if (registry != null && registry.TownInteractableVisualProfile != null)
            {
                return registry.TownInteractableVisualProfile;
            }

#if UNITY_EDITOR
            var editorRegistry = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            return editorRegistry != null ? editorRegistry.TownInteractableVisualProfile : null;
#else
            return null;
#endif
        }
    }
}
```

- [ ] **Step 4: Run isolated tests**

Run:

```text
tests_run EditMode testMethod Rootborn.Tests.EditMode.TownModernInteractableSpriteTests.TownInteractableVisualProfileResolvesExactNameBeforeRoleFallback
```

Expected: PASS.

- [ ] **Step 5: Commit applicator**

```bash
git add Assets/Scripts/Game/World/TownInteractableVisualApplicator.cs Assets/Tests/EditMode/TownModernInteractableSpriteTests.cs
git commit -m "[FEATURE][TEST] Town 상호작용 비주얼 적용기 추가" -m "TownInteractableVisualProfile exact-name 우선 해석과 generic applicator를 추가해 런타임 설치기들이 공통 sprite 적용 경로를 쓰도록 준비했다."
```

---

## Task 4: Create And Wire The Modern Visual Profile Asset

**Files:**
- Create: `Assets/Data/StudentLife/TownInteractableVisuals/TownInteractableVisualProfile.asset`
- Modify: `Assets/Data/Registry/GameDataRegistry.asset`

- [ ] **Step 1: Select concrete Modern sprite assets**

Use Unity MCP `assets-find` and `assets-get-data` to select sprites from approved roots. Initial acceptable selection:

```text
NPC: Assets/Modern_Farm_v1.2/Generated/ModernFarmer_IdleDown_16x16.png
Location/board/work/help/discovery/exploration: distinct Sprite assets under Assets/Modern_Town/1_Room_Builder_Office_48x48/ or Assets/Tiles/ModernTownOffice/
UI board/icon if needed: Assets/modernuserinterface-win/
```

Every chosen sprite path must start with an approved Modern root.

- [ ] **Step 2: Create the profile asset with Unity MCP `script-execute`**

Run a Unity editor script through MCP that:

1. creates `Assets/Data/StudentLife/TownInteractableVisuals/`,
2. loads selected `Sprite` assets,
3. creates a `TownInteractableVisualProfile`,
4. populates exact-name entries for every current visible target,
5. saves it at `Assets/Data/StudentLife/TownInteractableVisuals/TownInteractableVisualProfile.asset`,
6. assigns it to `GameDataRegistry.asset` field `_townInteractableVisualProfile`,
7. saves assets and refreshes.

The entry list must include:

```text
TownHelpActionPoint
TownDiscoveryPoint
TownHelpActionPoint_town_help_teacher_study_prep
TownReturnPortal
DailyPlanBoard
SchoolEntryActivity
StudentDayEndBoard
StudyBasicsActivity
CareerPracticeBoard
GuideNpc
OutsideSchoolActivityPoint
TownNpc_npc_teacher
TilePlacementBoard
OutsideSchoolActivityPoint_1
PartTimeWorkPoint
LocationVisit_location_library
LocationVisit_location_town_square
TownExplorationInteractionPoint
TownNpc_npc_librarian
TownNpc_npc_first_guide
LocationVisit_location_workshop
LocationVisit_location_school
TownNpc_npc_work_manager
LocationVisit_location_corner_store
TownNpc_npc_shopkeeper
```

- [ ] **Step 3: Add a registry wiring assertion to the EditMode test**

Use Unity MCP `script-update-or-create` to add:

```csharp
[Test]
public void TownInteractableVisualProfileIsRegisteredAndUsesModernSprites()
{
    var registry = AssetDatabase.LoadAssetAtPath<Rootborn.Game.Common.GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
    Assert.IsNotNull(registry);
    Assert.IsNotNull(registry.TownInteractableVisualProfile);

    foreach (var entry in registry.TownInteractableVisualProfile.Entries)
    {
        Assert.IsNotNull(entry);
        Assert.IsNotNull(entry.Sprite, "Town visual entry has no sprite: " + entry.TargetName);
        string path = AssetDatabase.GetAssetPath(entry.Sprite);
        Assert.IsTrue(ApprovedModernRoots.Any(root => path.StartsWith(root, StringComparison.Ordinal)), entry.TargetName + " uses non-Modern sprite path=" + path);
    }
}
```

- [ ] **Step 4: Run registry test**

Run:

```text
tests_run EditMode testMethod Rootborn.Tests.EditMode.TownModernInteractableSpriteTests.TownInteractableVisualProfileIsRegisteredAndUsesModernSprites
```

Expected: PASS after asset/profile wiring.

- [ ] **Step 5: Commit profile asset**

```bash
git add Assets/Data/StudentLife/TownInteractableVisuals Assets/Data/Registry/GameDataRegistry.asset Assets/Tests/EditMode/TownModernInteractableSpriteTests.cs
git commit -m "[ASSET][TEST] Town Modern 상호작용 비주얼 프로필 등록" -m "Modern 계열 sprite 참조를 담은 TownInteractableVisualProfile을 생성하고 GameDataRegistry에 등록했다."
```

---

## Task 5: Wire Runtime Installers To The Applicator

**Files:**
- Modify: `Assets/Scripts/UI/StudentLife/LocationNpcRuntimeInstaller.cs`
- Modify: `Assets/Scripts/UI/StudentLife/DailyPlanRuntimeInstaller.cs`
- Modify: `Assets/Scripts/UI/StudentLife/TownHelpActionRuntimeInstaller.cs`
- Modify: `Assets/Scripts/UI/StudentLife/TownExplorationDiscoveryRuntimeInstaller.cs`
- Modify: `Assets/Scripts/UI/StudentLife/TownExplorationInteractionRuntimeInstaller.cs`
- Modify: `Assets/Scripts/UI/StudentLife/OutsideSchoolRuntimeInstaller.cs`
- Modify: `Assets/Scripts/UI/Tiles/TownTilePlacementRuntimeInstaller.cs`
- Modify: `Assets/Scripts/Game/StudentLife/PartTimeWorkRuntimeInstaller.cs`
- Modify: `Assets/Scripts/Game/StudentLife/StudentLifeSceneInteraction.cs`
- Modify: `Assets/Scripts/UI/StudentLife/StudentDayRuntimeInstaller.cs`

- [ ] **Step 1: Modify installers with MCP `script-update-or-create`**

For each file, add:

```csharp
using Rootborn.Game.World;
```

Replace marker-only logic with calls like:

```csharp
TownInteractableVisualApplicator.Apply(go, TownInteractableVisualRole.Board, 48, new Vector2(0.85f, 0.85f));
```

Role mapping:

```text
LocationNpcRuntimeInstaller.ApplyNpcObject -> Npc, fallback sorting 54
LocationNpcRuntimeInstaller.EnsureLocations -> Location, fallback sorting 52
DailyPlanRuntimeInstaller.EnsureBoard -> Board, fallback sorting 48
TownHelpActionRuntimeInstaller.EnsureHelpPoint -> Help, fallback sorting 50
TownExplorationDiscoveryRuntimeInstaller.EnsureDiscoveryPoint -> Discovery, fallback sorting 55
TownExplorationInteractionRuntimeInstaller.EnsureInteractionPoint -> Exploration, fallback sorting 56
OutsideSchoolRuntimeInstaller.EnsureActivityPoint -> OutsideSchool, fallback sorting 49
TownTilePlacementRuntimeInstaller.Runner.EnsureBoard -> TileBoard, fallback sorting 48
PartTimeWorkRuntimeInstaller.EnsureWorkPoint -> Work, fallback sorting 48
StudentLifeSceneInteraction.StudentLifeActivityInteractor runtime creation path -> Activity, fallback sorting 48
StudentLifeSceneInteraction.CareerPracticeBoard runtime creation path -> Board, fallback sorting 48
StudentDayRuntimeInstaller day end board -> DayEnd, fallback sorting 48
WorldPortal/TownReturnPortal saved object, if not produced by an installer -> apply via scene profile script in Task 6
```

Do not remove colliders or existing interactor binding. Remove or leave `CreateMarkerSprite` methods only after every call site in that file is gone.

- [ ] **Step 2: Run the EditMode audit to verify GREEN for installer-created objects where tests can see them**

Run:

```text
tests_run EditMode testClass Rootborn.Tests.EditMode.TownModernInteractableSpriteTests
```

Expected: registry tests pass. Scene-level tests may still fail for saved `TownReturnPortal` or saved objects not touched by runtime installers; those are handled in Task 6.

- [ ] **Step 3: Commit installer wiring**

```bash
git add Assets/Scripts/UI/StudentLife/LocationNpcRuntimeInstaller.cs Assets/Scripts/UI/StudentLife/DailyPlanRuntimeInstaller.cs Assets/Scripts/UI/StudentLife/TownHelpActionRuntimeInstaller.cs Assets/Scripts/UI/StudentLife/TownExplorationDiscoveryRuntimeInstaller.cs Assets/Scripts/UI/StudentLife/TownExplorationInteractionRuntimeInstaller.cs Assets/Scripts/UI/StudentLife/OutsideSchoolRuntimeInstaller.cs Assets/Scripts/UI/Tiles/TownTilePlacementRuntimeInstaller.cs Assets/Scripts/Game/StudentLife/PartTimeWorkRuntimeInstaller.cs Assets/Scripts/Game/StudentLife/StudentLifeSceneInteraction.cs Assets/Scripts/UI/StudentLife/StudentDayRuntimeInstaller.cs
git commit -m "[FEATURE] Town 런타임 상호작용 Modern sprite 적용" -m "Town 런타임 설치기들이 placeholder marker 대신 GameDataRegistry의 TownInteractableVisualProfile을 통해 Modern sprite를 적용하도록 연결했다."
```

---

## Task 6: Apply Saved Scene Visuals And Verify EditMode

**Files:**
- Modify: `Assets/Scenes/Town.unity`

- [ ] **Step 1: Apply profile to saved Town scene objects with Unity MCP `script-execute`**

Run an editor script through MCP that:

1. opens `Assets/Scenes/Town.unity`,
2. loads `GameDataRegistry.asset`,
3. finds every GameObject listed in the profile,
4. applies the matching sprite, sorting order, scale, and collider size,
5. saves `Town.unity`,
6. reopens or re-inspects the scene.

The script must not create new gameplay objects except missing visual renderer/collider components on already expected interactable objects.

- [ ] **Step 2: Run EditMode full audit**

Run:

```text
tests_run EditMode testClass Rootborn.Tests.EditMode.TownModernInteractableSpriteTests
```

Expected: PASS.

- [ ] **Step 3: Inspect saved scene state**

Use Unity MCP `scene_get_data` or `script_execute` to report:

```text
target name
SpriteRenderer.sprite.name
AssetDatabase.GetAssetPath(sprite)
renderer.enabled
sortingOrder
BoxCollider2D.size
```

Expected: all expected visible targets have enabled, non-placeholder Modern sprite references.

- [ ] **Step 4: Commit saved scene**

```bash
git add Assets/Scenes/Town.unity Assets/Scenes/Town.unity.meta
git commit -m "[ASSET] Town 저장 씬 상호작용 Modern sprite 반영" -m "Town.unity의 저장된 상호작용 오브젝트들이 Modern sprite를 참조하도록 저장 씬 상태를 갱신했다."
```

---

## Task 7: Add And Run PlayMode Player-Facing Visual Test

**Files:**
- Create: `Assets/Tests/PlayMode/EndToEnd/TownModernInteractableSpriteE2ETests.cs`

- [ ] **Step 1: Create PlayMode test with MCP `script-update-or-create`**

Use Unity MCP `script-update-or-create` for `Assets/Tests/PlayMode/EndToEnd/TownModernInteractableSpriteE2ETests.cs`:

```csharp
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class TownModernInteractableSpriteE2ETests
    {
        private static readonly string[] RepresentativeTargets =
        {
            "GuideNpc",
            "LocationVisit_location_library",
            "DailyPlanBoard",
            "TownDiscoveryPoint",
            "PartTimeWorkPoint"
        };

        [UnityTest]
        public IEnumerator TOWN_VISUAL_E2E_001_PlayerSeesModernSpritesOnRepresentativeInteractables()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForTownRuntime(12f);

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "Town player missing.");
            var router = player.GetComponent<PlayerInteractionRouter>();
            Assert.IsNotNull(router, "PlayerInteractionRouter missing.");

            foreach (string targetName in RepresentativeTargets)
            {
                var target = GameObject.Find(targetName);
                Assert.IsNotNull(target, "Representative target missing: " + targetName);
                var renderer = target.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(value => value != null && value.enabled);
                Assert.IsNotNull(renderer, targetName + " has no enabled SpriteRenderer.");
                Assert.IsNotNull(renderer.sprite, targetName + " has null sprite.");
                StringAssert.DoesNotContain("Marker", renderer.sprite.name, targetName + " still uses marker sprite.");

                player.transform.position = target.transform.position + Vector3.left * 0.5f;
                yield return null;
                router.RefreshPromptNow();
                Assert.IsTrue(router.PromptVisible, "Prompt did not show near " + targetName + ".");
                Assert.IsNotEmpty(router.PromptText, "Prompt text empty near " + targetName + ".");
            }

            Directory.CreateDirectory("Builds/Logs");
            ScreenCapture.CaptureScreenshot("Builds/Logs/town-modern-interactable-sprites-e2e.png");
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(File.Exists("Builds/Logs/town-modern-interactable-sprites-e2e.png"), "Screenshot file missing.");
        }

        private static IEnumerator WaitForTownRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                bool ready = GameObject.Find("Player") != null;
                for (int i = 0; i < RepresentativeTargets.Length; i++)
                {
                    ready &= GameObject.Find(RepresentativeTargets[i]) != null;
                }

                if (ready)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Town runtime did not expose representative interactables within timeout.");
        }
    }
}
```

Note: This test uses transform movement to systematically approach targets after loading Town. It still exercises the real `PlayerInteractionRouter`, prompt visibility, and runtime renderers. Final manual/MCP Game View screenshot inspection remains required by the visual gate.

- [ ] **Step 2: Run PlayMode test to verify RED before visual wiring**

Run:

```text
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.TownModernInteractableSpriteE2ETests.TOWN_VISUAL_E2E_001_PlayerSeesModernSpritesOnRepresentativeInteractables
```

Expected before Tasks 4-6: FAIL on marker/null sprite. Expected after Tasks 4-6: PASS.

- [ ] **Step 3: Commit PlayMode test**

```bash
git add Assets/Tests/PlayMode/EndToEnd/TownModernInteractableSpriteE2ETests.cs
git commit -m "[TEST] Town Modern 상호작용 sprite PlayMode 검증 추가" -m "대표 Town 상호작용 대상에 접근해 prompt와 Modern sprite 상태를 확인하고 Game View screenshot을 저장하는 PlayMode 검증을 추가했다."
```

---

## Task 8: Final Verification

**Files:**
- No code changes expected.

- [ ] **Step 1: Run focused EditMode tests**

Run:

```text
tests_run EditMode testClass Rootborn.Tests.EditMode.TownModernInteractableSpriteTests
```

Expected: PASS.

- [ ] **Step 2: Run focused PlayMode test**

Run:

```text
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.TownModernInteractableSpriteE2ETests.TOWN_VISUAL_E2E_001_PlayerSeesModernSpritesOnRepresentativeInteractables
```

Expected: PASS and `Builds/Logs/town-modern-interactable-sprites-e2e.png` exists.

- [ ] **Step 3: Capture or inspect Game View/Camera screenshot**

Use Unity MCP `screenshot-game-view` or `screenshot-camera`.

Expected:

```text
Representative NPC, board, location, discovery/help/work target sprites are visible and distinct.
No obvious UI overlay hides the representative world sprites.
```

- [ ] **Step 4: Run static entity branching gate**

Run:

```powershell
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

Expected: PASS.

- [ ] **Step 5: Run diff checks**

Run:

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors. Status contains only intended files plus unrelated pre-existing dirty files.

- [ ] **Step 6: Final commit if any verification-only changes were made**

If screenshot artifacts are intentionally committed, use:

```bash
git add Builds/Logs/town-modern-interactable-sprites-e2e.png
git commit -m "[TEST] Town Modern 상호작용 sprite 시각 증거 추가" -m "PlayMode 대표 상호작용 flow에서 캡처한 Game View 증거를 추가했다."
```

If screenshot artifacts are not tracked, do not commit them. Report the path and file size in the final response.

---

## Self-Review

- Spec coverage: The plan covers Modern-only assets, all current visible Town interactables, non-placeholder tests, profile/registry data, runtime installer wiring, saved scene inspection, PlayMode representative flow, screenshot verification, and no entity ID-specific branching.
- Placeholder scan: No `TBD`, `TODO`, or "fill in later" requirements remain. Sprite choices are intentionally selected during Task 4 with concrete approved roots and then persisted in the asset profile.
- Type consistency: `TownInteractableVisualProfile`, `TownInteractableVisualEntry`, `TownInteractableVisualRole`, and `TownInteractableVisualApplicator` names are consistent across tasks.
- Risk note: The PlayMode test moves the player transform near targets to automate representative inspection. The final visual gate still requires actual Game View/Camera screenshot inspection and must report if direct player-input movement verification is blocked.
