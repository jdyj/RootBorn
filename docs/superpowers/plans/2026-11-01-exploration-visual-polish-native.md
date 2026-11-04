# Exploration Visual Polish Native Verification Plan

> **For agentic workers:** Parent executes the serialized Unity work using `superpowers:executing-plans`. These runners/helpers are output-only staged artifacts; staging does not run Unity.

**Goal:** Verify actual world-prompt clearance at two player approach positions and actual exploration-result containment, while preserving the original localization proof, earned save, and art113 checkpoint.

**Architecture:** Phase A reads the existing earned 11-file QA through real WASD movement. Phase B uses a separate one-time copy of the immutable pre-Inspect 8-file source and owns its own E/pointer/Inspect/Leave/cold flow. Both phases accept only the reviewed panel/router/HUD/service source changes and preserve earlier evidence.

**Tech Stack:** Existing Python native harness, parent Unity script execution, input-system keyboard/pointer helpers, runtime renderer/UI projections, JSON DTOs, SHA256, fresh decoded PNGs.

## Immutable baseline and exact source scope

- The original functional native proof remains `output/design-comfy-20261031/evidence/exploration-localization-first-and-cold.json`. Its before-polish backup and visual findings are historical evidence and must not be rewritten.
- `evidence/exploration-visual-layout-measure-actual-and-hypothetical-layout.json` contains the actual pre-polish overlap: prompt [589.230774,396.799988,874.769226,420.800018], card [823.8125,410.8125,1313.8125,563.9375], about 50.9567 by 9.9875 pixels. The hypothetical Top16 projection was rejected as a general fix; actual Top remains36.
- `output/design-comfy-20261031/exploration-visual-polish-approved-sources.json` records the exact applied source bytes, exact reviewed stage bytes, normalized text equality, added service/meta membership, unchanged HUD layout source, and old measurement hash. Existing files retain Unity's newline serialization; their raw hashes must not be confused with the stage-file hashes.
- The parent's `evidence/exploration-visual-polish-applied-source-hashes.json` must agree with those four reviewed source/stage pairs. Existing Addressables content is reused; no blanket fresh-build or source drift exemption is allowed.
- Protected original10184/current tracked membership10148, original113 checkpoint15, immutable source8 and student seed8, original earned11, and old evidence remain guarded. These counts describe distinct sets and must not be conflated.

## Phase A: existing-earned geometry

**Runner:** `output/design-comfy-20261031/run_exploration_visual_polish_earned.py`

**QA:** `output/design-comfy-20261031/saves-exploration-localization-verification` (the existing earned11; no copy/reset).

**Evidence prefix:** `exploration-visual-polish-earned`.

- [ ] Parent finishes any pending Unity operation and inspects the runner/helpers. The entry guard requires idle EditMode, builder0, clean editor scenes, and default storage routing. The runner is one-time and refuses an existing start/result/builder backup.
- [ ] Run the staged runner from the project root. It selects packed play once, starts the normal Boot/Single/Continue flow, and isolates only to the existing earned QA root.
- [ ] Capture the cold-loaded full5DTO/11-file state before movement. Compare it to the latest original native final state, not to a newly accepted baseline.
- [ ] Use the near WASD route to approximately(22,1.5); capture actual state, actual prompt/HUD geometry, and a fresh screenshot.
- [ ] Use the farther WASD route to approximately(22,.8), with tighter waypoint arrival tolerance. Require the actual measured player to be at least .5 world units below the near pose and at least1.1 below the interaction target, while the router still selects that target. Capture actual geometry and a fresh screenshot.
- [ ] For each pose require visible registered tracked card, Top36, same four HUD screen rectangles/layout positions/text/fonts as the pre-polish measurement, no prompt overlap, and at least8 pixels of separation and padded viewport containment. The recorded numerical allowance is .01 screen pixel for floating-point projection; retain raw measured values.
- [ ] Require exact authored prompt worldY/Z and camera depth, with screen-anchor Y unchanged within projection tolerance. The helper reads actual renderer bounds and anchor positions; it does not move transforms or call the fitter to manufacture geometry.
- [ ] Recompare all5 DTO strings and all11 file hashes after movement/capture. Stop play, restore builder0 and default roots, verify idle, and recheck all pinned evidence/source/save bytes.
- [ ] Parent inspects the PNGs. Runner PASS means measured geometry and state integrity; no actual Inspect/result-panel/reward claim is made by this phase. Failure retains observations and screenshots and attempts safe cleanup without resetting saves.

## Phase B: fresh one-time result and persistence

**Runner:** `output/design-comfy-20261031/run_exploration_localization_phase_b.py` (staged by inventory agent).

**QA:** `output/design-comfy-20261031/saves-exploration-localization-phase-b-verification`.

**Evidence prefix:** `exploration-localization-phase-b`.

- [ ] Review that runner's own guards and plan before execution. It may create the new QA once from immutable source8; it must never reset or write the old earned11.
- [ ] Use its prepare phase for normal Boot/Single/Continue, real WASD/E, and initial open-panel capture. No forced result state is allowed.
- [ ] Use its exercise phase for one real Inspect click, the three-line localized result inside the 24-logical-unit paper inset, actual Leave, and cold reload/Leave. Require exact Wood1, focus trait1, energy-1, stress+1 and no TP/time changes, plus collector/history/persistence checks owned by that runner.
- [ ] Require fresh complete PNGs and compare complete DTOs/dynamic file membership after earning and cold reload. Preserve all original proofs and stop/restore0/defaults even on failure.

## Staging status

Phase A Python AST, source-policy parity, current protected membership, checkpoint15, and earned11 were checked read-only. Ten unique Phase A C# helpers were staged. Neither Phase A nor Phase B was executed by this delegated agent; C# compilation, actual input, measured clearance, result containment, and screenshots remain parent verification work.
