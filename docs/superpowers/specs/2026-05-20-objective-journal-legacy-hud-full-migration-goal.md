# Goal: Objective Journal Legacy HUD Full Migration

## Objective

Objective Journal legacy HUD migration을 Campaign 이후 남은 전체 범위까지 순차적으로 완료한다.

`CampaignHudPanel` 이관은 이미 완료되었으므로, 다음 작업자는 남은 legacy objective UI를 아래 순서대로 `ObjectiveJournalPanel`과 `TrackedObjectiveHud`로 이관한다.

1. `MilestoneHudPanel`
2. `QuestChainHudWidget`
3. `CareerInterestHudWidget`

최종 상태에서는 플레이어가 지금 무엇을 하면 되는지 알려주는 목표성 UI는 `ObjectiveJournalPanel`이 목록/상세를 소유하고, 상시 HUD는 `TrackedObjectiveHud` 하나만 남아야 한다.

## Source Context

- Design: `docs/superpowers/specs/2026-05-19-objective-journal-legacy-hud-migration-design.md`
- Completed plan: `docs/superpowers/plans/2026-05-19-campaign-hud-objective-journal-migration.md`
- Completed commit: `eae280e0d [REFACTOR][UI][TEST] Campaign HUD를 Objective Journal로 이관`
- Existing Objective Journal source: `Assets/Scripts/UI/Objectives/ObjectiveJournalRuntimeProvider.cs`
- Ownership gate: `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalUiOwnershipGateTests.cs`

## Global Constraints

- `Assets/**/*.cs` files must be created, edited, or deleted only through Unity MCP tools such as `script-update-or-create` and `script-delete`.
- Do not add entity ID-specific C# branches for milestone, quest chain, career interest, objective, route, reward, or candidate IDs.
- Objective data must come from ScriptableObject definitions, registry data, and progress state.
- Do not add new always-visible goal, quest, campaign, milestone, hint, or career HUD canvases.
- `TrackedObjectiveHud` remains the only always-visible objective HUD.
- Visible/UI changes require actual PlayMode player-facing verification and Game View or Camera screenshot evidence.
- Runtime checks, direct method calls, forced state setup, or asset-name assertions alone are not enough for completion.
- Keep day result summaries and immediate reward/result feedback panels only if they are not always-visible objective HUDs.
- Before final completion, run `git diff --check` and `Scripts/ci/check-no-entity-id-branching.sh`.

## Sequential Work Plan

### Phase 1: Milestone HUD Migration

Migrate `MilestoneHudPanel` into Objective Journal `Hints` category and `TrackedObjectiveHud`.

Required behavior:

- Hints category displays milestone entries generated from milestone definitions and milestone progress.
- Each milestone row shows state: `Active`, `Completed`, or `RewardClaimed`.
- Progress text includes per-objective progress and route/recommendation guidance equivalent to the legacy HUD.
- Row click updates Objective Journal detail.
- Track click updates `TrackedObjectiveHud` with the selected milestone objective.
- Tracked milestone stable key restores after reload when the milestone still exists.
- `StudentDayRuntimeInstaller`, `StudentDayEndInteractor`, and `DialoguePanel` no longer create, refresh, hide, or show `MilestoneHudPanel`.
- `MilestoneHudPanel.cs` and its `.meta` are deleted after all compile-time references are removed.
- `ObjectiveJournalUiOwnershipGateTests` removes the `MilestoneHudPanel` type/factory allowlist entries.

Required tests:

- EditMode: milestone source displays active, completed, reward-claimed, and per-objective progress state.
- EditMode: ownership gate fails before deletion and passes after migration.
- PlayMode/E2E: `OpenEndedMilestoneGrowthE2EScenarioTests` uses actual player flow, opens Objective Journal with `Tab`, selects Hints/Milestone row, tracks it, performs outside-school progress, verifies detail/HUD update, reloads, and captures screenshot evidence.

Completion:

- No `MilestoneHudPanel` type references remain in production code.
- Name-based PlayMode assertions verify `MilestoneHudPanel`, `MilestoneHudRoot`, and any legacy milestone canvas/root are absent.

### Phase 2: Quest Chain HUD Widget Migration

Migrate `QuestChainHudWidget` into Objective Journal `Quests` category and `TrackedObjectiveHud`.

Required behavior:

- Quests category displays quest chain entries generated from quest chain definitions and quest chain progress.
- Active step, completed step, blocked/failed/expired reason, and reward state are visible in row/detail text.
- Row click updates detail for the selected chain/step.
- Track click updates `TrackedObjectiveHud` with the active quest-chain step.
- Tracked quest-chain stable key restores after reload when the chain/step still exists.
- `QuestChainRuntimeInstaller` must not create a separate always-visible quest-chain HUD.
- `QuestChainLogPanel` must be classified explicitly:
  - keep only if it is a user-opened log/detail panel, not an always-visible objective HUD;
  - otherwise move the relevant always-visible guidance into Objective Journal.
- `QuestChainHudWidget` is deleted after all compile-time references are removed.
- `ObjectiveJournalUiOwnershipGateTests` removes the `QuestChainHudWidget` type/factory allowlist entries.

Required tests:

- EditMode: quest-chain source builds active/completed/blocked/failed/expired/reward-state objective items.
- EditMode: ownership gate fails before deletion and passes after migration.
- PlayMode/E2E: relevant quest-chain E2E opens Objective Journal with real `Tab`, selects Quests row, tracks it, advances quest-chain state through player-facing UI/input, verifies detail/HUD update, reloads, and captures screenshot evidence.

Completion:

- No production code creates `QuestChainHudWidget` as an always-visible objective HUD.
- `TrackedObjectiveHud` is the only persistent objective HUD for quest-chain guidance.

### Phase 3: Career Interest HUD Widget Migration

Migrate `CareerInterestHudWidget` into Objective Journal Hints or career-related objective rows and `TrackedObjectiveHud`.

Required behavior:

- Objective Journal displays current career interest, candidate, recommendation, linked quest, and next action state.
- Category placement must be explicit:
  - use `Hints` if the entry is guidance/recommendation;
  - use `Quests` only if it represents an actionable quest-chain objective.
- Row click updates detail for the selected career/candidate objective.
- Track click updates `TrackedObjectiveHud`.
- Tracked career objective stable key restores after reload when the objective still exists.
- `CareerInterestRuntimeInstaller` may keep modal/panel logic, but must not create a separate always-visible career HUD.
- `CareerInterestHudWidget` is deleted after all compile-time references are removed.
- `ObjectiveJournalUiOwnershipGateTests` removes the `CareerInterestHudWidget` type/factory allowlist entries.

Required tests:

- EditMode: career objective source displays current interest, recommendation, candidate, linked quest, and next action from data/progress state.
- EditMode: ownership gate fails before deletion and passes after migration.
- PlayMode/E2E: `CareerInterestSelectionLoopE2EScenarioTests` uses actual player flow, opens Objective Journal with `Tab`, selects career-related row, tracks it, advances career-interest state, verifies detail/HUD update, reloads, and captures screenshot evidence.

Completion:

- No production code creates `CareerInterestHudWidget` as an always-visible objective HUD.
- Career-interest guidance is visible through Objective Journal and optional tracked HUD only.

### Phase 4: Final Ownership Audit

After Phases 1-3 are complete, perform a full objective UI ownership audit.

Required checks:

- `CampaignHudPanel`, `MilestoneHudPanel`, `QuestChainHudWidget`, and `CareerInterestHudWidget` are absent from production code as always-visible objective HUDs.
- `ObjectiveJournalUiOwnershipGateTests` allowlist contains only reviewed owners:
  - `ObjectiveJournalPanel`
  - `TrackedObjectiveHud`
  - non-objective or explicitly user-opened panels that are intentionally allowed
- No installer creates separate objective HUD canvases for campaign, milestone, quest-chain, or career guidance.
- Dialogue/background HUD suppression no longer references deleted legacy HUD types.
- All migrated E2E tests include actual player-facing input/click flow and screenshot evidence.

Required final verification:

- EditMode objective tests:
  - `Rootborn.Tests.EditMode.UI.Objectives`
- PlayMode objective tests:
  - `Rootborn.Tests.PlayMode.Objectives`
- Campaign E2E regression
- Milestone E2E regression
- Quest-chain E2E regression
- Career-interest E2E regression
- `git diff --check`
- `Scripts/ci/check-no-entity-id-branching.sh`

## Recommended Implementation Discipline

For each phase:

1. Update the ownership gate allowlist first and verify it fails for the legacy HUD/widget.
2. Convert or add Objective Journal source tests and verify the new/strengthened tests fail for missing behavior.
3. Implement provider/source behavior with data-driven logic only.
4. Convert the relevant PlayMode/E2E test from legacy HUD assertions to actual Objective Journal flow.
5. Remove runtime installer creation/refresh/hide/show coupling for the legacy HUD/widget.
6. Delete the legacy HUD/widget script through Unity MCP after all type references are gone.
7. Run the phase-specific EditMode, PlayMode, E2E, screenshot, static, and ownership checks.
8. Commit the phase separately with Korean commit message and appropriate prefix.

## Commit Boundaries

Use separate commits per phase:

- `[REFACTOR][UI][TEST] Milestone HUD를 Objective Journal로 이관`
- `[REFACTOR][UI][TEST] Quest Chain HUD를 Objective Journal로 이관`
- `[REFACTOR][UI][TEST] Career Interest HUD를 Objective Journal로 이관`
- `[TEST][CHORE] Objective Journal legacy HUD 소유권 최종 점검`

Do not include unrelated working-tree changes such as existing `Assets/AddressableAssetsData/link.xml*` deletions unless the user explicitly asks to handle them.

## Done Definition

This goal is complete only when all four phases are done and committed, all required tests pass, screenshot evidence exists for visible PlayMode flows, and no legacy always-visible objective HUD remains outside `ObjectiveJournalPanel` and `TrackedObjectiveHud`.
