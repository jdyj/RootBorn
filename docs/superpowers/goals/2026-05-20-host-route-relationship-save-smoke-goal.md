# Goal: Host-Mode Route And Relationship Save Smoke

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Add one strict host-mode PlayMode smoke scenario that verifies recent Daily Plan route state and First Guide relationship milestone state survive save/reload without duplicate Journal or HUD state.

## Candidate Selection

Considered candidates:

- Host-Mode Route And Relationship Save Smoke
  - Why now: recent route and relationship slices are verified in single-player flows, but host-mode persistence needs a small regression guard before more gameplay content is added.
  - Value: protects route, relationship, Objective Journal, tracked HUD, and save-slot state under the network host path.
  - Risk: host flow may not expose the same SaveSlot UI reload path as single-player.
  - Verification burden: one PlayMode host flow with screenshot plus static gates.
  - Category: network, test, foundation.
- Quest Chain Rewards An Interior Unlock
  - Why now: quest reward routing is now proven and can naturally unlock visible home/interior content.
  - Value: high player-facing reward value.
  - Risk: larger visual placement, saved-state, catalog, and reward idempotency surface.
  - Verification burden: EditMode, PlayMode, screenshot, saved placement/state inspection.
  - Category: feature, asset, test.
- Daily Plan Route Completion And Day Result Feedback
  - Why now: selected routes currently emphasize discovery/selection more than completion feedback.
  - Value: improves the daily loop.
  - Risk: may require product judgment on result UI and route outcome semantics.
  - Verification burden: PlayMode route completion, Journal filtering, result UI, reload persistence.
  - Category: feature, UI, test.

Selected candidate: Host-Mode Route And Relationship Save Smoke.

## Non-Negotiable Constraints

- This is a network/host-mode validation slice, not a broad dedicated-server campaign.
- Do not introduce new gameplay data unless a missing test fixture is unavoidable.
- Do not replace real player-facing movement, interaction, UI selection, or reload with internal method calls as the only proof.
- `ObjectiveJournalPanel` owns goal-like guidance.
- `TrackedObjectiveHud` remains the only always-visible objective HUD and may show only one tracked objective.
- Do not edit `Assets/**/*.cs` through shell writes; use Unity MCP script tools for Unity C# files.
- All gameplay/entity behavior must remain ScriptableObject-driven.
- Do not add production C# branching on entity IDs, route IDs, quest IDs, activity IDs, location IDs, NPC IDs, crop IDs, tool IDs, or similar identifiers.
- Keep unrelated dirty or untracked files untouched and unstaged.
- If `Assets/AddressableAssetsData/link.xml` or `.meta` is deleted by Unity tooling, restore it immediately and keep it out of this cycle unless the cycle explicitly changes it.

## Required Skills

- `superpowers:brainstorming` for candidate selection and scope shaping.
- `superpowers:test-driven-development` because this cycle adds or tightens PlayMode coverage.
- `superpowers:systematic-debugging` for host-flow, network, or PlayMode failures.
- `superpowers:verification-before-completion` before claiming completion or committing.

## Required Work

Create one host-mode smoke test using existing route and relationship data:

- host session starts through a project-supported player-facing host flow or the closest existing host test harness that still exercises NGO host startup
- enter Town with a valid local host player identity
- perform or verify one existing Daily Plan route selection or route availability state
- perform or verify one existing First Guide relationship milestone state
- open Objective Journal and inspect route/relationship state through existing source contracts
- track one objective if applicable and verify exactly one `TrackedObjectiveHud`
- save/reload or host restart through the project-supported path and verify state restores without duplication

Prefer reusing existing E2E helpers and recent route/relationship flows. If the current host scene cannot support the full single-player SaveSlot UI reload path, the test must state that limitation in its assertion messages and still prove the network host state through the strongest existing project-supported host flow.

## Required TDD

Add a failing PlayMode test first:

```text
Rootborn.Tests.PlayMode.EndToEnd.NetworkRouteRelationshipHostE2EScenarioTests.NETWORK_ROUTE_RELATIONSHIP_PM_001_HostProgressSaveReload
```

The initial RED should fail because the host route/relationship smoke scenario does not exist yet, or because host startup/reload does not preserve the required state.

Expected GREEN:

- the host-mode PlayMode scenario enters Town as host,
- route state and First Guide relationship/milestone state are observable,
- Objective Journal and tracked HUD state are non-duplicated,
- reload or host restart restores state without duplicating route, relationship, milestone, Journal, or HUD state.

## Required PlayMode Scenario

Create or extend:

```text
NETWORK_ROUTE_RELATIONSHIP_PM_001_HostProgressSaveReload
```

The scenario must:

1. Start from the host-capable player-facing flow or the existing host test harness if that is the only project-supported host entry point.
2. Enter Town.
3. Use actual keyboard movement to reach the route or relationship interaction source when the scene exposes that interaction path.
4. Use actual interact key or visible UI buttons to advance or select state.
5. Verify visible UI text/state in Game View.
6. Verify runtime network player identity, route state, relationship progress, milestone state, Journal state, and tracked HUD state.
7. Capture screenshot:

```text
Builds/Logs/network-route-relationship-host-e2e.png
```

8. Reload through the project-supported host/save slot path, or restart the host flow with the same save slot if that is the available project-supported path.
9. Verify state restores without duplicate route, relationship, milestone, Journal, or HUD state.

Forbidden shortcuts:

- direct route, relationship, milestone, Journal, or HUD mutation as the only proof
- direct scene loading as the only host proof when a host startup harness exists
- direct Objective Journal source calls as the only PlayMode proof
- JSON-only save verification without host reload or restart

## Required Verification

Run:

```powershell
tests_run PlayMode testMethod Rootborn.Tests.PlayMode.EndToEnd.NetworkRouteRelationshipHostE2EScenarioTests.NETWORK_ROUTE_RELATIONSHIP_PM_001_HostProgressSaveReload
Get-ChildItem Builds/Logs/network-route-relationship-host-e2e.png | Select-Object Name,Length,LastWriteTime
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

If the host scenario touches shared route, relationship, or Objective Journal helpers, also run the narrow affected test namespaces:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.StudentLife
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
```

## Acceptance Criteria

- One host-mode route and relationship smoke scenario exists.
- Runtime network player identity is verified.
- Route state and First Guide relationship/milestone state are verified under host mode.
- Objective Journal guidance remains source-owned and no duplicate tracked HUD is created.
- State restores after the project-supported host reload/restart path without duplication.
- Screenshot evidence exists and has non-zero length.
- No production entity ID branching is introduced.

## Commit

Use:

```text
[NETWORK][TEST] Host route relationship save smoke 추가
```

## Loop Continuation

After this goal is implemented, tested, visually verified, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
