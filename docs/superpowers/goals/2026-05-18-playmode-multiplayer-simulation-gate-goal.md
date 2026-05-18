# Goal: PlayMode, Simulation, Multiplayer Gate Reinforcement

Current EditMode regression gate is recovered, but player-facing PlayMode coverage is not strong enough yet. This goal strengthens the gate so the project cannot claim readiness from data checks or two narrow simulations while user-facing flows still fail in Game View, UI, scene transitions, placement, persistence, or multiplayer sync.

## Non-Negotiable Rules

- Do not mark the work complete with internal method calls, forced state setup, direct scene loads, or asset-name checks alone.
- Reproduce player-facing paths in PlayMode: input, pointer click, movement, trigger, UI selection, scene transition, placement, save, reload, host/client join, or the exact flow under test.
- For visible gameplay, UI, placement, camera, tilemap, or scene behavior, capture or inspect Game View or Camera output and record runtime UI/Renderer state.
- For placement and saved content, verify both runtime state and saved scene/prefab/ScriptableObject state when persistence is expected.
- Do not disable, ignore, or weaken failing PlayMode tests. Classify failures and fix the smallest valid cause.
- New C# files must be written through Unity MCP `script-update-or-create` or Unity Editor paths, not direct disk writes.
- Do not add entity-specific C# branching, ID enums, `switch(toolId)`, `if (activityId == "...")`, or hardcoded gameplay entity logic.
- All gameplay entity behavior must remain ScriptableObject and registry driven.
- Run `Scripts/ci/check-no-entity-id-branching.sh` before completion.

## Gate Areas

### 1. Town Entry Gate

- Verify the real entry path from Boot/MainMenu/SaveSlot/SPUM/start flow into Town.
- Do not satisfy this with `SceneManager.LoadScene("Town")` alone.
- Confirm the player-facing scene state after entry: active scene, player object, camera target, core UI, input module, and Game View visibility.

### 2. Objective Journal Gate

- Open `ObjectiveJournalPanel` in PlayMode using actual input or UI flow.
- Confirm `TrackedObjectiveHud` displays only one tracked objective.
- Confirm goal/quest/campaign/hint UI does not create separate always-on Canvas panels outside the Objective Journal ownership model.
- Inspect visible panel state and runtime hierarchy.

### 3. Student Life Growth Gate

- Verify at least two non-school growth routes through PlayMode user flow.
- Each route must start from a player-facing Town interaction or UI choice and produce a real growth/progression result.
- Confirm results are reflected in day result/progression state and are sourced from registry-backed ScriptableObject data.
- School cannot be the only valid growth route.

### 4. Career Candidate And Hint Gate

- Verify `CareerInterest`, `CareerCandidate`, `CareerHint`, and `CareerCandidateRoute` data load through `GameDataRegistry`.
- In PlayMode, progress a user-facing flow that reveals or advances a career candidate or hint.
- Confirm no candidate, hint, route, location, activity, or reward path is implemented through entity-specific C# branching.
- Record the selected candidate or hint name, route source, and resulting UI/progression state.

### 5. House And Interior Placement Gate

- Enter placement or home design flow through real input/UI.
- Select furniture or room preset through UI, place it in a cell, and verify selected asset name, cell position, overlay state, renderer/tilemap state, and stale sample/debug object absence.
- Verify save/load when persistence is expected.
- For saved changes, inspect saved asset/scene/prefab state separately from runtime generated state.

### 6. Multiplayer Gate

- Verify host and at least one client can enter the relevant flow without desync or duplicate reward/state mutation.
- Include at least one host-mode PlayMode test and one client/server or simulated multiplayer test path using Netcode for GameObjects where the existing project supports it.
- Cover these multiplayer risks:
  - player spawn and ownership are correct;
  - Town entry or interaction state is visible on both host and client;
  - quest/objective/progression updates are applied once, not once per peer;
  - inventory/reward grants remain atomic and idempotent;
  - saveSlot or player state does not bleed between clients;
  - disconnect/reconnect or scene transition does not leave stale network objects.
- If full dedicated server plus client automation is blocked, state the blocker explicitly and provide the strongest available host/client PlayMode simulation, plus a follow-up task for real executable server/client validation.

## Required Workflow

1. Inventory existing PlayMode/E2E tests and map them to the six gate areas above.
2. Separate committed tests from dirty/untracked tests before staging anything.
3. Run the existing committed gate candidates first and record the baseline.
4. For every uncovered gate, write a failing PlayMode test first.
5. Watch each new test fail for the expected reason.
6. Implement the smallest valid fix or test harness addition.
7. Run the targeted PlayMode tests again until green.
8. Run the impacted EditMode tests or full EditMode suite when shared systems changed.
9. Run the entity ID branching CI gate.
10. Write an audit document mapping each gate requirement to evidence.
11. Stage only files directly related to this goal.
12. Commit with an allowed Korean prefix and push the current working branch.

## Completion Criteria

- Evidence exists for Town entry, Objective Journal, two non-school growth routes, career hint/candidate flow, House/Interior placement, and multiplayer state sync.
- PlayMode verification uses real player-facing input/UI/scene/interaction paths wherever possible.
- Game View/Camera or runtime UI/Renderer inspection evidence is recorded for visual flows.
- Multiplayer verification covers host/client visibility and at least one state mutation or reward/progression idempotency case.
- `Scripts/ci/check-no-entity-id-branching.sh` passes.
- A written audit states what was verified, what was not verified, and any remaining blockers.

## Suggested Commit Message

`[TEST] PlayMode 멀티플레이 시뮬레이션 게이트 보강`
