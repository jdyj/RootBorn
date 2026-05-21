# Dedicated Server Route Relationship Persistence Harness Feasibility

Date: 2026-05-21

## Current Status Refresh

Status as of the later 2026-05-21 stabilization cycles: the original blocker recorded in this audit has been superseded for the route/relationship and core gameplay smoke paths.

The project now has executable server/client process evidence through:

- `Scripts/qa/run-dedicated-route-relationship-smoke.ps1`
  - launches `rootborn-server.exe` with `-mode server`, `-port`, `-maxPlayers`, `-saveSlot`, and `-directValidationTrace`;
  - launches a separate `rootborn.exe` client with `-mode client`, `-joinIp 127.0.0.1`, `-port`, `-saveSlot`, route/relationship autoplay flags, and screenshot capture;
  - runs an action pass and an observe-only reload pass;
  - verifies server start, client connection, Town synchronization, screenshots, state markers, fatal log absence, and cleanup.
- `Scripts/qa/run-dedicated-core-gameplay-matrix.ps1`
  - composes route/relationship, career Daily Plan route, Daily Plan completion, discovery route unlock, and resource/recipe dedicated smoke slices;
  - checks action/reload markers, screenshots, state patterns, and duplicate Journal/HUD evidence per slice.
- `Assets/Tests/EditMode/Network/DedicatedRouteRelationshipAutoplayContractTests.cs`
  - protects the route/relationship smoke runner, autoplay flags, observe-only reload contract, screenshot contract, freshness guard, and long-run cleanup contract.
- `Assets/Tests/EditMode/Network/DedicatedCoreGameplayMatrixContractTests.cs`
  - protects matrix composition across the dedicated gameplay slices.
- `docs/superpowers/audits/2026-05-21-student-life-scenario-registry-coverage.md`
  - tracks the dedicated route/relationship smoke and core gameplay matrix as active executable `NET` coverage.
- `Builds/Logs/network-dedicated-core-gameplay-matrix`
  - contains the latest matrix action/reload logs and nonzero screenshot artifacts for the dedicated slices.

The historical analysis below remains useful as the reason the first attempt should not have been reported as complete. It should no longer be read as the current project state for route/relationship dedicated process coverage.

Remaining network work is now future expansion rather than a missing harness prerequisite: extending long-run/reconnect/restart coverage, adding more gameplay slices to the matrix, and keeping build freshness/process cleanup gates strict.

## Decision

Do not implement `NETWORK_DEDICATED_PERSISTENCE_PM_001_RouteRelationshipReload` yet.

The current automated PlayMode harness can prove host-mode route/relationship persistence, and it can start Netcode sessions inside one Unity process. It does not currently provide a true dedicated-server plus separate client player-facing flow that can enter Town, move a client player, interact with route/relationship sources, persist state, restart, reconnect, and verify reload state.

The next safe implementation goal should be Objective Journal route source regression hardening, while a separate network foundation goal should add a real dedicated server/client process harness before claiming dedicated persistence coverage.

## Evidence Inspected

- `Assets/Scripts/Network/Session/DedicatedServerSession.cs`
  - Starts `NetworkManager.Singleton` with `StartServer()`.
  - Sets transport to `0.0.0.0:<port>`.
  - Requests network scene load to `Town`.
  - `IsClient` is always false.
  - It has no local player-facing input path by itself.
- `Assets/Scripts/Network/Session/ClientSession.cs`
  - Starts `NetworkManager.Singleton` with `StartClient()`.
  - Connects to `joinIp:<port>`.
  - Requires its own `NetworkManager.Singleton`.
- `Assets/Scripts/Network/Session/NetworkBootstrap.cs`
  - Maintains one static `ActiveSession`.
  - Starts one session mode from one `AppConfig`.
- `Assets/Tests/PlayMode/Network/NetworkSessionHostPlayModeTests.cs`
  - Creates one in-process `NetworkManager`.
  - Verifies `HostSession.StartHost()`, local ownership, and cleanup.
  - Does not run a separate dedicated server and client.
- `Assets/Tests/PlayMode/EndToEnd/NetworkRouteRelationshipHostE2EScenarioTests.cs`
  - Verifies host-mode player flow, route selection, First Guide relationship progress, save files, restart, and reload.
  - This is valuable host coverage, but it is explicitly not dedicated-server coverage.

## Current Coverage

Host mode has strict E2E coverage:

- Starts from the project host-capable UI flow.
- Enters Town.
- Uses keyboard movement and interact key.
- Selects a Daily Plan route.
- Advances First Guide relationship state.
- Opens Objective Journal and tracks a route.
- Captures `Builds/Logs/network-route-relationship-host-e2e.png`.
- Restarts host flow and verifies state reload without duplicate HUD.

Session unit coverage exists for host startup and owned local player identity.

At the time of this original feasibility decision, no current test started a dedicated server process and a separate client process for player-facing route/relationship persistence. This historical statement is superseded by the current-status refresh above.

## Why Server-Only Is Not Enough

A `DedicatedServerSession.StartAsync()` PlayMode smoke would verify that `StartServer()` listens, but it would not satisfy the requested route/relationship persistence smoke because:

- no client player exists to perform keyboard movement, UI clicks, or interact key flow,
- no player-facing Game View state exists on the server-only process,
- relationship and route advancement would need to be forced internally or skipped,
- save/load could only be checked as server state, not client-observed gameplay state.

Reporting that as dedicated route/relationship persistence would violate the direct player-flow verification gate.

## Minimum Missing Harness

The dedicated persistence smoke needs a network foundation slice that can:

- build or launch a dedicated server process with explicit `-mode server -port <port> -saveSlot <slot>`,
- launch or simulate a separate client process with explicit `-mode client -joinIp 127.0.0.1 -port <port>`,
- expose readiness probes for server listening, client connected, and Town scene synchronized,
- route client-side input through the same player-facing movement/interact/UI helpers used by current E2E tests,
- capture client Game View screenshot evidence,
- isolate temp save roots per test run,
- shut down both processes cleanly and collect logs.

## Recommended Next Goal

Proceed next with Objective Journal route source regression hardening.

Reason:

- The Daily Plan route completion cycle just changed Goal filtering and tracked HUD stale-key behavior.
- Career, quest reward, relationship, Daily Plan completion, and host smoke sources now converge on Objective Journal.
- A compact regression matrix can be implemented with the current test harness and will reduce risk before adding a dedicated multi-process harness.

## Deferred Network Goal

After the Objective Journal regression hardening, create a separate network foundation goal:

```text
Dedicated Server Client Process Harness
```

That goal should produce a reusable harness first. Only after that harness exists should `NETWORK_DEDICATED_PERSISTENCE_PM_001_RouteRelationshipReload` be implemented.

## Stop/Continue Status

This audit does not block the autonomous loop. It blocks only the specific dedicated persistence smoke implementation as originally phrased. The loop can safely continue to Objective Journal route source regression hardening.
