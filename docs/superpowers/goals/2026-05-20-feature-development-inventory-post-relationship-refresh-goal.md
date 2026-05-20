# Goal: Feature Development Inventory Post Relationship Refresh

This goal was selected by the Autonomous Recurring Feature Improvement Loop.

## Objective

Refresh the feature-development inventory after the Discovery route unlock hardening, Objective Journal route contract hardening, First Guide relationship milestone slice, and completed milestone route recommendation fix.

## Non-Negotiable Constraints

- This is a documentation and audit cycle only.
- Do not change production code, assets, scenes, prefabs, tests, or gameplay behavior in this cycle.
- Treat the workspace as shared; do not stage or commit unrelated dirty or untracked files.
- Do not make unsupported claims. Every recommendation must be grounded in inspected files, recent commits, tests, or existing evidence.
- Keep Objective Journal ownership and data-driven entity rules visible in the recommendations.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:verification-before-completion`

## Required Work

- Create:
  `docs/superpowers/audits/2026-05-20-feature-development-inventory-post-relationship-refresh.md`
- Inspect current code, data, tests, screenshots, recent commits, and recent goal docs.
- Update domain summaries for at least:
  - Daily Plan route loop
  - Discovery clue route unlock loop
  - Objective Journal route/source loop
  - NPC relationship and schedule loop
  - Career interest/candidate/job loop
  - Quest and quest-chain loop
  - Housing/interior reward loop
  - Network/dedicated server loop
  - Legacy Farm/Crop/Generation loop
- Rank at least five next feature candidates.
- Name exactly one recommended next goal and two runner-up goals.
- Include minimum PlayMode scenario templates with actual player-facing flow and screenshot/evidence paths for the ranked candidates.

## Required TDD

TDD is not applicable because this cycle does not change code or runtime behavior.

Instead, verification is by command evidence, placeholder scan, static diff checks, and a focused commit containing only the goal and audit documents.

## Required Verification

Run:

```powershell
rg -n "class .*Interactor|RuntimeInstaller|ObjectiveJournal|TrackedObjectiveHud|DailyPlan|QuestChain|CareerInterest|Relationship|Knowledge|Discovery|House|Furniture|Network|Dedicated|Farm|Crop|Generation" Assets/Scripts Assets/Tests -g "*.cs"
Get-ChildItem Assets/Data -Recurse -File | Group-Object DirectoryName | Sort-Object Count -Descending | Select-Object -First 40 Count,Name
Get-ChildItem Assets/Tests/EditMode,Assets/Tests/PlayMode -Recurse -File -Filter "*.cs" | Select-Object FullName
Get-ChildItem Builds/Logs -File | Sort-Object LastWriteTime -Descending | Select-Object -First 30 Name,Length,LastWriteTime
git log --oneline -12
git diff --check
git status --short
```

Then scan the new audit for:

```powershell
rg -n "TBD|TODO|placeholder|verify as needed" docs/superpowers/audits/2026-05-20-feature-development-inventory-post-relationship-refresh.md
```

The scan must return no matches.

## Acceptance Criteria

- The audit reflects the post-relationship milestone and completed-route recommendation fix state.
- It identifies one next recommended goal and two runner-ups.
- Each ranked candidate names player value, systems touched, UI ownership, tests, PlayMode flow, screenshot path, save/load expectation, and performance risk.
- No runtime or asset behavior changes are mixed into this documentation cycle.
- The cycle is committed separately.

## Commit

Use:

```text
[DOCS] 기능 개발 인벤토리 관계 milestone 이후 갱신
```

## Loop Continuation

After this goal is documented, verified, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
