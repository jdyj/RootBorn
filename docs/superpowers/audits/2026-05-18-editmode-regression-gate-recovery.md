# EditMode Regression Gate Recovery Audit

Date: 2026-05-18
Branch: town-playable-baseline-recovery
Goal: docs/superpowers/goals/2026-05-18-editmode-regression-gate-recovery-goal.md

## Requirement Mapping

- Full EditMode regression gate is green.
  - Evidence: Unity EditMode test run passed 789/789, failed 0, skipped 0, duration 00:00:47.8011626.

- Entity-id branching remains blocked.
  - Evidence: `Scripts/ci/check-no-entity-id-branching.sh` returned `OK: no entity-id branching in system code.`

- Modern UI and Modern Interiors sprite/addressable failures are resolved.
  - Evidence: targeted `ModernUiSliceSetupTests`, `ModernUiAddressablesSetupTests`, `ModernInteriorsSliceSetupTests`, `ModernInteriorsAddressablesSetupTests`, `ModernHudSpriteKeysTests`, and `ModernUiRecipeManifestTests` passed.
  - Fix evidence: Modern Interiors 16x16 core sheets were restored under the expected project paths, Modern UI/Interiors slicing and addressable wiring were rerun.

- Character part sub-sprite loading resolves registered sub-sprites.
  - Evidence: `CharacterPartResourceManagerTests` passed.
  - Fix evidence: `ResourceManager.LoadSubSpriteAsync` no longer returns null before trying to resolve a requested sub-sprite and has an editor fallback for sliced sprite assets.

- Interior placement UI model reports generated furniture summary.
  - Evidence: `InteriorFurniturePlacementUiModelTests` passed.
  - Fix evidence: successful regeneration now returns a summary containing Desk, Chair, Computer, Sofa, and Plant counts.

- Career candidate registry asset wiring is complete.
  - Evidence: `CareerCandidateRegistryAssetTests` passed.
  - Fix evidence: `GameDataRegistry.asset` now references career candidates, hints, and alternative route definitions; one candidate has two routes.

- MainMenu/Town source audit matches current player flow.
  - Evidence: `TownDefaultFlowSourceAuditTests` passed.
  - Fix evidence: audit now treats Bootstrap -> MainMenu -> save slot/SPUM -> Town as the expected flow, while still blocking Farm fallback.

- Quest and inventory Modern UI audits are internally consistent.
  - Evidence: `QuestUiTests`, `ModernUiTileImageTests`, and `ModernUiInventoryPanelTests` passed.
  - Fix evidence: QuestLogPanel keeps exactly one outer CommonPanel48 and plain inner claim button; inventory tests inspect the current nested tab/grid hierarchy and popup action chrome.

- Static runtime search/resource usage gate has a reviewed baseline.
  - Evidence: `StaticRuntimeUsageGateTests` passed.
  - Fix evidence: current reviewed usages are captured in `Assets/Tests/EditMode/StaticRuntimeUsageGateReviewedAllowlist.txt`; new usages outside this baseline still fail the gate.

## Verification Commands

- Unity MCP EditMode full suite: PASS 789/789.
- `Scripts/ci/check-no-entity-id-branching.sh`: PASS.

## Residual Notes

- This goal was an EditMode regression-gate recovery. No new visible gameplay or UI flow was claimed complete, so PlayMode visual verification was not part of the completion gate for this goal.
- The worktree contains many unrelated dirty and untracked files from broader ongoing work; commit staging must remain scoped to files directly supporting this recovery.
