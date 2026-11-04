# Live day guide refresh

Actual input reproduced a stale HUD after day12→13 while model TP reset to3. Provider only refreshed when the journal opened. Keep journal selection and persisted tracking unchanged; observe owner values and update only the compact tracked item on changes.

- [x] Reproduce using actual bed/E/NextDay input; retain stale vs fresh-source evidence.
- [x] Add regression for hidden journal day advance and in-place TP spend; original provider fails.
- [x] Implement owner-state observation, tracked-key readout and HUD-only refresh; no automatic persistence or row selection.
- [x]27/27 related source/ownership/presentation tests pass.
- [x] Actual packed slot UI day13→bed/E/NextDay→day14 verifies same-session refresh with journal closed, no manual refresh or direct progression mutation.
- [x] Restore QA routing and prior play builder0.
