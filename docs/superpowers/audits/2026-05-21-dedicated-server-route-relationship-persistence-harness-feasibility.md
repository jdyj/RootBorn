# Dedicated Server Route Relationship Persistence Harness Feasibility

Date: 2026-05-21

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

No current test starts a dedicated server process and a separate client process for player-facing route/relationship persistence.

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
