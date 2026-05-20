# Goal: Quest Interior Reward Saved-State Regression Hardening

This goal was selected by the Autonomous Recurring Feature Improvement Loop after dedicated route/relationship smoke and long-run network regression passed.

## Objective

Harden the existing quest-to-interior reward slice so duplicate reward claims, reloads, and House placement saves cannot silently duplicate furniture unlock tokens, placed furniture tiles, occupancy cells, or saved layout records.

## Non-Negotiable Constraints

- Treat the workspace as shared. Do not stage or commit unrelated dirty/untracked files.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for C# files.
- Write failing tests first for behavior or regression changes.
- Do not add production C# branching on quest IDs, reward IDs, furniture IDs, room IDs, route IDs, location IDs, NPC IDs, crop IDs, tool IDs, or similar identifiers.
- Keep quest reward, furniture unlock, catalog filtering, and placement behavior data-driven.
- Visible and interactive verification must use the player-facing PlayMode flow and capture Game View screenshot evidence.
- Runtime visual checks are not enough; serialized unlock and layout save files must also be inspected.
- `ObjectiveJournalPanel` owns quest guidance; do not create new quest HUDs.
- Preserve `Assets/AddressableAssetsData/link.xml` and `.meta`; restore generated Addressables file churn before commit.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Tighten existing EditMode regression coverage in `Rootborn.Tests.EditMode.Interiors.QuestInteriorUnlockRewardTests`.
- Tighten existing PlayMode E2E coverage in `Rootborn.Tests.PlayMode.EndToEnd.QuestInteriorRewardE2EScenarioTests.QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload`.
- Verify the existing quest reward furniture remains locked before claim and visible after claim.
- Verify duplicate claim attempts do not mutate unlock save data after the first claim.
- Verify serialized furniture unlock save data contains the unlock token exactly once.
- Verify serialized House furniture layout contains the reward furniture id and tile name exactly once after placement and reload.
- Verify the visible House tilemap and occupancy tilemap still contain exactly one restored placed furniture instance.

## Required TDD

RED:

- Add or tighten a test that fails if `FurnitureUnlockProgress.FromSaveData` permits duplicate unlock tokens to inflate saved-state count.
- Add or tighten PlayMode assertions that fail if duplicate claim changes the unlock file after the reward is already claimed.
- Add or tighten PlayMode assertions that fail if `house-furniture-layout*.json` contains duplicate reward furniture records after save/reload.

GREEN:

- Duplicate unlock tokens collapse to one progress token.
- Duplicate reward claim remains disabled and does not mutate the saved unlock file.
- Reloaded House layout has one reward furniture record, one visible object tile, and one occupancy cell.

## Required Verification

Run narrow tests:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Interiors
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Quests
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.QuestInteriorRewardE2EScenarioTests.QUEST_INTERIOR_REWARD_E2E_001_ClaimUnlockPlaceReload
Get-ChildItem Builds/Logs/quest-interior-reward-e2e.png | Select-Object Name,Length,LastWriteTime
```

Run baseline checks:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

## Acceptance Criteria

- Existing quest interior reward feature remains player-facing and visible.
- Locked furniture is absent before reward claim and available after reward claim.
- Reward claimed state survives reload and cannot be claimed again.
- Serialized furniture unlock data contains the unlock token exactly once.
- Serialized House furniture layout contains the reward furniture id and tile name exactly once.
- Runtime House tilemaps contain exactly one visible reward furniture tile and matching occupancy after reload.
- Screenshot evidence exists and has non-zero length.
- No production entity-ID branching is introduced.

## Commit

Use:

```text
[TEST] Quest interior reward 저장 회귀 강화
```

## Loop Continuation

After this goal is implemented, tested, visually verified where required, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
