# Goal: Dedicated Server Client Process Harness

This goal was selected by the Autonomous Recurring Feature Improvement Loop after Objective Journal source regression hardening.

## Objective

Add a reusable test harness for launching and validating a true dedicated server process plus a separate client process, without claiming route/relationship dedicated persistence coverage yet.

## Non-Negotiable Constraints

- Treat the current clean workspace as shared; do not stage or commit unrelated dirty/untracked files if they appear.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for Unity C# files.
- This is network foundation work, not the route/relationship persistence smoke itself.
- Do not label host-mode or single-process Netcode coverage as dedicated-server coverage.
- Do not require unavailable build artifacts to pass silently; missing server/client executables must produce explicit prerequisite diagnostics.
- Keep launched processes isolated by explicit `saveSlot`, `port`, log directory, and shutdown handling.
- Do not add production entity-ID branching.
- If `Assets/AddressableAssetsData/link.xml` or its `.meta` is removed by Unity tooling, restore it immediately before continuing.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Add a PlayMode test utility that describes and launches a dedicated server executable and a separate client executable.
- Generate deterministic `ProcessStartInfo` for:
  - server: `-mode server -batchmode -nographics -port <port> -maxPlayers <n> -saveSlot <slot>`
  - client: `-mode client -joinIp <ip> -port <port> -saveSlot <slot>`
- Validate prerequisites before launch:
  - server executable exists,
  - client executable exists,
  - port is valid,
  - save slot is non-empty,
  - log directory is non-empty.
- Add readiness probes for server start, client start/connection, Town scene synchronization, and process cleanup.
- Add tests that prove missing build artifacts are reported as prerequisites rather than being mistaken for a passed dedicated smoke.
- Keep the next route/relationship dedicated smoke deferred until this harness exists.

## Required TDD

Write failing tests first:

- Harness config reports missing server executable.
- Harness config reports missing client executable.
- Server start info includes mode, port, maxPlayers, saveSlot, batchmode, and nographics.
- Client start info includes mode, joinIp, port, and saveSlot.
- Readiness probe recognizes server started, client connected, Town synchronized, and cleanup-complete log signals.
- Cleanup closes both launched process handles.

Expected RED:

- The dedicated server/client process harness type and readiness probe do not exist yet.

Expected GREEN:

- The harness utility and tests pass without requiring current build artifacts, and missing artifacts are explicit prerequisite diagnostics.

## Required Verification

Run:

```powershell
tests_run PlayMode testNamespace Rootborn.Tests.PlayMode.Network
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

Inspect:

```powershell
Get-ChildItem Builds/Logs -File | Sort-Object LastWriteTime -Descending | Select-Object -First 20 Name,Length,LastWriteTime
```

## Acceptance Criteria

- A reusable dedicated server/client process harness exists under PlayMode test support.
- The harness distinguishes true server/client process coverage from host-mode coverage.
- Missing executable prerequisites are reported explicitly.
- Server/client command lines, log paths, readiness probes, and cleanup behavior are covered by tests.
- No route/relationship dedicated smoke is claimed in this cycle.

## Commit

Use:

```text
[NETWORK][TEST] Dedicated server client 하네스 추가
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
