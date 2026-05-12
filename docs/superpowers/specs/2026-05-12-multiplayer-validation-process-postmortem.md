# Multiplayer Validation Process Postmortem

Date: 2026-05-12

## Why The Last Validation Took Too Long

1. Dedicated Server feasibility was discovered late.
   - The local Unity install lacks a visible Dedicated Server playback module.
   - Server builds failed with `Dedicated Server support for Win is not installed`.
   - This should have been a preflight check before attempting server validation.

2. Broad PlayMode verification used slow and unreliable paths.
   - Full unfiltered PlayMode through MCP exceeded tool timeout.
   - CLI PlayMode with batchmode/nographics hit render-loop or result XML issues.
   - Targeted Unity MCP namespaces were the reliable path: EndToEnd, Quests, World, and Network EditMode.

3. Direct multiplayer evidence was gathered through repeated manual runs.
   - Host + two Client validation needed many logs to prove identity, personal state, world time, reconnect, and restart.
   - Earlier runs mixed useful evidence with failed attempts, increasing audit time.

4. Process cleanup was reactive.
   - Unity, `unity-mcp-server`, `Unity.ILPP.Runner`, and `Library/ilpp.pid` residue caused Unity-open friction.
   - Cleanup should be a standard preflight and shutdown step.

5. Some regressions were found late in broad verification.
   - Quests PlayMode exposed dialogue-flow and Farm UI inventory binding issues after most direct evidence was collected.
   - These are now covered by targeted tests, but they should be part of the standard validation set.

## Improvements Applied

- Added `Scripts/validation/Invoke-MultiplayerValidationPreflight.ps1`.
  - Reports stale Unity/rootborn/MCP/ILPP processes.
  - Optionally cleans stale processes and `Library/ilpp.pid`.
  - Checks Unity playback engines for a visible server module before Dedicated Server attempts.
  - Points to the direct validation evidence folders.
  - Prints the recommended fast-path validation order.

- Updated the final audit to distinguish:
  - Required Host + two Client direct validation: complete.
  - Dedicated Server validation: environment-blocked optional configuration on this machine.
  - Full unfiltered PlayMode: future CI hardening, not a blocker for this goal.

## Recommended Future Sequence

```powershell
Scripts\validation\Invoke-MultiplayerValidationPreflight.ps1 -CleanStaleProcesses
```

Then use this order:

1. Build or reuse the latest client only after preflight.
2. Run Host + two Client direct validation with one saveSlot and one port per scenario.
3. Capture only the required evidence folders:
   - initial Town/world-time sync
   - all-ready day transition
   - reconnect/current server time snapshot
   - same-saveSlot restart restore
   - quest/inventory personal isolation
   - relationship/status personal isolation
4. Run targeted automated verification:
   - full EditMode
   - `Rootborn.Tests.PlayMode.EndToEnd`
   - `Rootborn.Tests.PlayMode.Quests`
   - `Rootborn.Tests.PlayMode.World`
   - `Rootborn.Tests.EditMode.Network`
   - `Scripts/ci/check-no-entity-id-branching.sh`
5. Run Dedicated Server validation only if preflight shows the server module is installed.
6. Clean Unity/rootborn/MCP/ILPP processes and `Library/ilpp.pid` before final report.

## Remaining Process Risk

The direct Host + two Client validation still depends on ad hoc executable launch orchestration. The next useful improvement is a single smoke-run harness that launches Host, Client 1, and Client 2, tails logs for required markers, enforces a timeout, and always cleans processes.
