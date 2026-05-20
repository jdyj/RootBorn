# Goal Prompt Sequence: Next Foundation Stabilization

Use these prompts in order. Each prompt is intentionally scoped as a separate goal session so commits, verification, and review stay clean.

Recommended order:

1. Workspace Dirty State Triage
2. PlayMode E2E Common Test Driver Refactor
3. Objective Journal / Daily Plan / Discovery Clue Integration Hardening
4. Discovery Route Unlock Second Slice

Do not execute prompt 2 until prompt 1 is resolved. Do not start prompt 4 until prompt 2 and 3 have passed their verification gates.

---

## Prompt 1: Workspace Dirty State Triage

Paste this prompt into a goal session and execute it as written.

### Objective

Audit and resolve the current dirty/untracked workspace state before any new feature or refactor work. The goal is to prevent accidental staging, deletion, or overwriting of user-owned changes.

Current known workspace items:

- `D Assets/AddressableAssetsData/link.xml`
- `D Assets/AddressableAssetsData/link.xml.meta`
- `M production/qa/evidence/registry-provider-town-flow.png`
- `?? Assets/CharacterKit.meta`
- `?? Assets/CharacterKit/`
- `?? Temp_ModernInteriors_Generator_ContactSheet.png`

### Non-Negotiable Constraints

- Do not revert, delete, move, or stage any dirty/untracked file until its ownership and intended fate are established.
- Treat unknown changes as user-owned.
- Do not use `git reset --hard`, `git checkout --`, or destructive cleanup commands.
- If a file is clearly generated evidence, document why before proposing deletion or `.gitignore`.
- Unity C# edit rules still apply: do not write `Assets/**/*.cs` directly through shell.
- This is a stabilization goal. Do not implement gameplay features here.

### Required Skills

Use:

- `superpowers:executing-plans`
- `superpowers:systematic-debugging` if any file origin is unclear
- `superpowers:verification-before-completion`

### Required Work

Create an audit document:

- `docs/superpowers/audits/2026-05-20-workspace-dirty-state-triage.md`

The audit must include:

- exact `git status --short` output,
- per-file classification:
  - user-owned change,
  - generated artifact,
  - stale build output,
  - deleted asset requiring restore,
  - intentional asset import,
  - unknown / needs user decision,
- recommended action for each item,
- whether the action was executed or deferred,
- confirmation that unrelated files were not staged or reverted.

### Required Commands

Run and record:

```powershell
git status --short
git diff --name-status
git diff --stat
git ls-files --others --exclude-standard
git diff --check
```

If `Assets/CharacterKit/` appears to be an imported package or generated asset tree, inspect only enough to classify it:

```powershell
Get-ChildItem Assets/CharacterKit -Recurse | Select-Object -First 80 FullName,Length,LastWriteTime
```

### Acceptance Criteria

- Audit file exists and has no `TBD`, `TODO`, or unsupported claim.
- No unknown workspace item is deleted or staged.
- If cleanup is safe and executed, it is committed separately with a clear Korean commit message.
- If cleanup is not safe, the audit clearly lists the user decision needed.
- Final `git status --short` is reported.

### Commit

If only the audit is written:

```text
[DOCS] 워크스페이스 dirty 상태 감사 작성
```

If safe cleanup is also performed, use a separate commit:

```text
[CHORE] 생성 산출물 정리
```

---

## Prompt 2: PlayMode E2E Common Test Driver Refactor

Paste this prompt into a goal session and execute it as written.

### Objective

Refactor repeated PlayMode E2E helper logic into a shared test driver without changing player-facing behavior. The goal is to make future strict player-flow tests cheaper, more reliable, and easier to review.

Target repeated flows:

- MainMenu -> SaveSlot New Game / Load -> optional SPUM confirm -> Town
- waiting for Town runtime objects
- keyboard movement to an interactable
- pressing interact key
- clicking UI buttons through EventSystem
- opening Objective Journal with Tab
- selecting Journal categories and rows
- capturing and verifying Game View screenshots

### Non-Negotiable Constraints

- This is a test infrastructure refactor, not gameplay behavior work.
- No production behavior changes unless a test exposes a real player-flow bug that must be fixed.
- All C# under `Assets/**/*.cs` must be edited only through Unity MCP script tools.
- Keep tests player-facing: do not replace keyboard movement, interact key, UI click, scene transition, or screenshot evidence with direct method calls.
- Do not reduce assertions in existing E2E tests.
- Do not introduce entity ID branching in production code.
- Keep unrelated dirty files untouched and unstaged.

### Required Skills

Use:

- `superpowers:executing-plans`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging` for failing PlayMode tests
- `superpowers:verification-before-completion`

### Required Work

Create a shared PlayMode helper under:

- `Assets/Tests/PlayMode/EndToEnd/Support/`

Recommended class names:

- `PlayerFlowE2EDriver`
- `ObjectiveJournalE2EDriver`
- `ScreenshotEvidence`

Keep helpers test-only and narrowly scoped. Do not create a broad framework.

Refactor these tests first:

- `DailyPlanRouteSelectionE2EScenarioTests`
- `DiscoveryRouteUnlockE2EScenarioTests`
- `ClueInterpretationInteractorE2EScenarioTests`

### Required TDD

Before refactoring production-like helper behavior, add focused PlayMode or EditMode tests for helper contracts where practical:

- SaveSlot driver enters Town from MainMenu.
- Button click helper fails clearly when button is missing.
- Screenshot helper verifies non-zero output length.
- Journal helper selects category and row through visible UI.

Expected RED:

- helper type does not exist or old tests still own duplicated logic.

Expected GREEN:

- target E2E tests pass after refactor with no weaker assertions.

### Required Verification

Run:

```powershell
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DiscoveryRouteUnlockE2EScenarioTests.DISCOVERY_ROUTE_E2E_001_SaveSlotClueInterpretationUnlocksDailyPlanRouteAndReload
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DailyPlanRouteSelectionE2EScenarioTests.DAILY_PLAN_E2E_001_SaveSlotSelectRoutePlayActivityResultReload
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.ClueInterpretationInteractorE2EScenarioTests.CLUE_INTERPRET_E2E_012_PlayerMovesToInterpretationInteractorAndCompletesViaPromptAndChoiceButton
Get-ChildItem Builds/Logs/discovery-route-unlock-e2e.png,Builds/Logs/daily-plan-route-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

### Acceptance Criteria

- Shared helpers reduce duplicated E2E code without hiding player-facing steps.
- Existing E2E scenarios still use keyboard movement, interact key, UI click, scene transition, and screenshot evidence.
- Failure messages remain scenario-specific and actionable.
- No production feature behavior changes are mixed into the refactor.

### Commit

```text
[REFACTOR][TEST] PlayMode E2E 공통 드라이버 정리
```

---

## Prompt 3: Objective Journal / Daily Plan / Discovery Clue Integration Hardening

Paste this prompt into a goal session and execute it as written.

### Objective

Harden the integration boundaries between Objective Journal, Daily Plan route selection, and Discovery Clue route unlocks after the first unlock slice.

The target behavior:

- unlocked but unselected Daily Plan routes may appear as `Hints`,
- selected Daily Plan route guidance appears as `Goals`,
- `TrackedObjectiveHud` shows only one tracked objective,
- no source creates a separate permanent HUD,
- save/load restores route availability, route selection, Journal guidance, and tracked HUD state without duplication.

### Non-Negotiable Constraints

- `ObjectiveJournalPanel` owns all goal-like UI.
- `TrackedObjectiveHud` is the only always-visible objective HUD.
- Do not create a duplicate HUD, floating hint panel, source-specific tracked widget, or new permanent Canvas.
- Do not branch production C# on clue, route, world-state, location, NPC, quest, campaign, or activity IDs.
- All C# under `Assets/**/*.cs` must be edited only through Unity MCP script tools.
- Every behavior change follows TDD.
- Visible or interactive changes require direct PlayMode player-flow verification plus Game View or Camera screenshot evidence.
- Keep unrelated dirty files untouched and unstaged.

### Required Work

Audit and harden:

- `DailyPlanObjectiveSource`
- `DailyPlanRouteHintObjectiveSource`
- `ObjectiveJournalSourceCatalog`
- `ObjectiveJournalAggregator`
- `ObjectiveJournalPanel`
- `TrackedObjectiveHud`
- relevant PlayMode E2E tests

Clarify and enforce source ownership:

- `daily-plan-route-hints` emits available unlocked routes only when they are unselected.
- `daily-plan` emits selected/completed Daily Plan route state in `Goals`.
- if a route is selected, the same route must not also appear as a route unlock hint.
- tracking a route item must not create multiple tracked HUD instances.

### Required TDD

Add or tighten tests:

- selected unlocked route is removed from `Hints`.
- selected unlocked route appears in `Goals`.
- tracking selected route restores exactly one `TrackedObjectiveHud` after reload.
- source catalog order is deterministic.
- null registry/player/save context degrades without throwing.
- repeated provider refresh does not duplicate rows or tracked HUD state.

Expected RED:

- at least one tightened test fails before implementation if current behavior is incomplete.

Expected GREEN:

- `Rootborn.Tests.EditMode.UI.Objectives` passes.
- strict player-facing PlayMode route unlock E2E passes.

### Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DiscoveryRouteUnlockE2EScenarioTests.DISCOVERY_ROUTE_E2E_001_SaveSlotClueInterpretationUnlocksDailyPlanRouteAndReload
Get-ChildItem Builds/Logs/discovery-route-unlock-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

### Acceptance Criteria

- Goals/Hints ownership is deterministic and covered by tests.
- No duplicate tracked HUD appears after refresh or reload.
- Route unlock hint and selected route goal do not duplicate the same route in different categories.
- Existing Objective Journal migration rules remain intact.

### Commit

```text
[UI][TEST] Objective Journal Daily Plan 통합 안정화
```

---

## Prompt 4: Discovery Route Unlock Second Slice

Paste this prompt into a goal session and execute it as written.

### Objective

Add the second data-driven Discovery Clue -> Daily Plan route unlock slice after the first route unlock is stable.

This must prove the system supports more than one unlock path without hardcoded entity IDs or a parallel unlocked-route save list.

### Non-Negotiable Constraints

- All clue, interpretation, world-state, route, location, and activity behavior must be ScriptableObject/data-driven.
- Do not branch production C# on specific IDs.
- Do not create a separate route unlock save list. Source of truth remains `WorldStateProgress` plus route requirements.
- Do not add a new permanent HUD.
- `ObjectiveJournalPanel` owns route guidance.
- All C# under `Assets/**/*.cs` must be edited only through Unity MCP script tools.
- Every behavior change follows TDD.
- Visible or interactive changes require direct PlayMode player-flow verification plus Game View or Camera screenshot evidence.
- Keep unrelated dirty files untouched and unstaged.

### Required Work

Create or wire exactly one additional unlock path:

- one registered `WorldStateFlagDefinition`,
- one registered clue or existing clue source suitable for Town,
- one registered `ClueInterpretationDefinition` with visible choice text,
- one `ClueInterpretationWorldStateOutcome` activating the route unlock flag,
- one `DailyPlanRouteDefinition` gated by `DailyPlanWorldStateRequirement`,
- route references an existing reachable `LocationDefinition`,
- route references an existing reachable `LocationActivityDefinition` or matches by `TargetLocation + GrowthRoute`,
- route has non-empty Objective Journal title/detail/next hint keys,
- all new assets are registered in `Assets/Data/Registry/GameDataRegistry.asset`.

### Required TDD

Add or tighten asset/data tests before wiring:

- second route unlock flag exists in registry.
- second clue interpretation outcome references the same flag used by the second route requirement.
- first unlock route and second unlock route use separate route IDs and separate world-state flags.
- locked second route is unavailable before world-state activation.
- second route is available after activating its registered flag.
- Objective Journal exposes available unselected second route as `Hints`.
- selecting the second route moves it to `Goals` and removes it from `Hints`.

Expected RED:

- tests fail because second registered data path does not exist.

Expected GREEN:

- StudentLife, DiscoveryClues, and UI Objectives EditMode tests pass.

### Required PlayMode E2E

Create or extend a strict player-facing PlayMode test:

- start from MainMenu SaveSlot flow,
- enter Town,
- use keyboard movement to reach the second clue or interpretation source,
- press interact key,
- click visible interpretation choice,
- verify world-state flag active,
- move to Daily Plan board by keyboard,
- press interact key,
- verify second route row visible,
- click second route row,
- open Objective Journal with Tab,
- verify route guidance in `Goals`,
- track route and verify only one `TrackedObjectiveHud`,
- capture screenshot:
  - `Builds/Logs/discovery-route-unlock-second-slice-e2e.png`,
- reload same save slot,
- verify world-state, route availability, route selection, clue/interpretation records, Journal state, and tracked HUD state restore without duplication.

Forbidden shortcuts:

- direct `TryComplete` as the only proof,
- direct world-state activation,
- direct route selection,
- JSON-only verification,
- route availability checks without opening the player-facing Daily Plan board.

### Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.DiscoveryClues
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.DiscoveryRouteUnlockSecondSliceE2EScenarioTests.DISCOVERY_ROUTE_E2E_002_SaveSlotSecondClueInterpretationUnlocksSecondDailyPlanRouteAndReload
Get-ChildItem Builds/Logs/discovery-route-unlock-second-slice-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
```

### Acceptance Criteria

- Two independent data-driven route unlocks exist and are covered by tests.
- No production code branches on specific IDs.
- No route unlock save list is introduced.
- Objective Journal ownership remains intact.
- PlayMode proof uses actual player-facing input/UI flow and screenshot evidence.

### Commit

```text
[FEATURE][ASSET][TEST] Discovery route unlock 두 번째 슬라이스 추가
```
