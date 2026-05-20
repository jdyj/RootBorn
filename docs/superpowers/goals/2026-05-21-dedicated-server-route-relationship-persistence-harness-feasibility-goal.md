# Goal: Dedicated Server Route Relationship Persistence Harness Feasibility

This goal was selected by the Autonomous Recurring Feature Improvement Loop after the Daily Plan route completion cycle.

## Objective

Determine whether the recommended dedicated-server route/relationship persistence smoke can be executed safely with the current automated test harness, without falsely reporting host-mode or server-only coverage as dedicated-server client coverage.

## Non-Negotiable Constraints

- This is a documentation and feasibility gate cycle only.
- Do not change production code, gameplay assets, scenes, prefabs, or test behavior in this cycle.
- Do not claim host-mode coverage as dedicated-server coverage.
- Do not claim a server-only `NetworkManager.StartServer()` smoke as route/relationship player-flow persistence coverage.
- Treat the workspace as shared; do not stage or commit unrelated dirty/untracked files.
- If `Assets/AddressableAssetsData/link.xml` or its `.meta` is removed by Unity tooling, restore it immediately.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Inspect current network session code and PlayMode network tests.
- Identify whether current tests can run a true dedicated server plus client flow.
- Identify the smallest missing harness capability, if blocked.
- Create:
  `docs/superpowers/audits/2026-05-21-dedicated-server-route-relationship-persistence-harness-feasibility.md`
- Recommend one of:
  - proceed with dedicated server smoke implementation,
  - add a dedicated process harness first,
  - defer dedicated server and move to Objective Journal route source regression hardening.

## Required TDD

TDD is not applicable because this cycle does not change runtime behavior or tests.

Verification is by inspected files, command output, and a focused audit document with no incomplete markers or unsupported claims.

## Required Verification

Run:

```powershell
rg -n "DedicatedServerSession|ClientSession|HostSession|NetworkBootstrap|NetworkManager|StartServer|StartClient|StartHost|NETWORK_" Assets/Scripts Assets/Tests -g "*.cs"
Get-Content Assets/Scripts/Network/Session/DedicatedServerSession.cs
Get-Content Assets/Scripts/Network/Session/ClientSession.cs
Get-Content Assets/Tests/PlayMode/Network/NetworkSessionHostPlayModeTests.cs
Get-Content Assets/Tests/PlayMode/EndToEnd/NetworkRouteRelationshipHostE2EScenarioTests.cs
rg -n "TBD|TODO|placeholder|verify as needed" docs/superpowers/audits/2026-05-21-dedicated-server-route-relationship-persistence-harness-feasibility.md
git diff --check
git status --short
```

The incomplete-marker scan must return no matches.

## Acceptance Criteria

- The audit names the exact evidence for current host, server, and client coverage.
- The audit clearly states whether true dedicated server route/relationship persistence can be verified now.
- If blocked, the audit names the minimum missing harness capability and the next safe goal.
- No runtime behavior changes are mixed into the feasibility cycle.
- The cycle is committed separately.

## Commit

Use:

```text
[DOCS] Dedicated server persistence harness 가능성 정리
```

## Loop Continuation

After this goal is documented, verified, and committed, return to the Autonomous Recurring Feature Improvement Loop. If dedicated server route/relationship persistence is blocked by missing harness capability, do not implement a fake smoke; select the next safe improvement.
