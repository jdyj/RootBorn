# Registry Provider Follow-up Verification - 2026-05-19

## Result

PASS. The remaining `MilestoneGrowth` direct `Resources.Load<Rootborn.Game.Common.GameDataRegistry>` fallback was removed, and the static direct Registry load gate now catches both namespaced and non-namespaced `GameDataRegistry` generic loads.

## Prompt-to-Artifact Checklist

| Requirement | Evidence |
| --- | --- |
| `MilestoneGrowth.cs` direct Registry fallback removed or routed through provider | `Assets/Scripts/Game/StudentLife/MilestoneGrowth.cs` now uses `GameDataRegistryProvider.TryGet(...)` instead of `UnityEngine.Resources.Load<Rootborn.Game.Common.GameDataRegistry>(...)`. |
| Static gate catches namespaced/non-namespaced direct Registry loads | `Assets/Tests/EditMode/StaticRuntimeUsageGateTests.cs` uses `Resources\.Load\s*<\s*(Rootborn\.Game\.Common\.)?GameDataRegistry\s*>`. |
| Reviewed allowlist no longer permits the old direct Registry load | `Assets/Tests/EditMode/StaticRuntimeUsageGateReviewedAllowlist.txt` no longer contains the old `MilestoneGrowth.cs` direct load line. |
| No direct runtime Registry load remains in `Assets/Scripts` for the required patterns | `rg -n "Resources\.Load\s*<\s*(Rootborn\.Game\.Common\.)?GameDataRegistry\s*>" Assets/Scripts -g "*.cs"` returned no matches. |
| Registry Provider EditMode tests pass | `production/qa/registry-provider/registry-provider-followup-final-20260519/editmode-registry-provider.txt`: PASS, 4 passed, 0 failed. |
| Static runtime allowlist gate passes | `production/qa/registry-provider/registry-provider-followup-final-20260519/static-runtime-allowlist.txt`: PASS, 1 passed, 0 failed. |
| Migrated Registry caller gate passes | `production/qa/registry-provider/registry-provider-followup-final-20260519/static-migrated-registry-callers.txt`: PASS, 1 passed, 0 failed. |
| MainMenu -> Single Play -> New Game -> Confirm -> Town PlayMode E2E passes | `production/qa/registry-provider/registry-provider-followup-final-20260519/playmode-mainmenu-town-provider-flow.txt`: PASS, 1 passed, 0 failed. |
| Game View evidence exists | `production/qa/evidence/registry-provider-town-flow.png` exists and was refreshed by the PlayMode E2E. |
| QA simulation script passes | `Scripts/qa/run-registry-provider-playmode-simulation.ps1 -RunName registry-provider-followup-final-20260519 -SaveSlot registry-provider-followup-final-20260519`: PASS. |
| Entity ID branching gate passes | `production/qa/registry-provider/registry-provider-followup-final-20260519/entity-branching.txt`: `OK: no entity-id branching in system code.` |
| `git diff --check` passes | PASS. Output contained only CRLF normalization warnings for existing workspace files. |
| Existing dirty `link.xml` deletions untouched | `git status --short` still reports only `D Assets/AddressableAssetsData/link.xml` and `D Assets/AddressableAssetsData/link.xml.meta` after committed work. |

## Commands

```powershell
rg -n "Resources\.Load\s*<\s*(Rootborn\.Game\.Common\.)?GameDataRegistry\s*>" Assets/Scripts -g "*.cs"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts\qa\run-registry-provider-playmode-simulation.ps1 `
  -RunName registry-provider-followup-final-20260519 `
  -SaveSlot registry-provider-followup-final-20260519

& "C:\Program Files\Git\bin\bash.exe" Scripts/ci/check-no-entity-id-branching.sh

git diff --check
git status --short
```

## Notes

- C# changes were applied through Unity MCP `script-update-or-create`.
- The existing `FindFirstObjectByType<OutsideSchoolActivityInteractor>` fallback in `MilestoneGrowth.cs` remains reviewed runtime fallback scope and is still listed in the reviewed allowlist.
- The pre-existing dirty deletions of `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta` were not modified or staged.
