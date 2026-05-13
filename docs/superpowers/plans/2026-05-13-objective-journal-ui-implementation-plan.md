# Objective Journal UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first playable Objective Journal UI slice: Tab opens a shared goals/quests/campaign/hints panel, only one tracked objective appears on HUD, and future sessions have a rule to avoid ad hoc goal panels.

**Architecture:** Add a focused `Rootborn.UI.Objectives` shell with small view models, a `TrackedObjectiveHud`, and router/autoinstaller integration. Existing quest/campaign/goal systems are not rewritten in this slice; the journal establishes layout, input ownership, and provider-ready model boundaries.

**Tech Stack:** Unity 6000.3 UGUI, `Rootborn.UI`, Modern UI Style2 `ModernUiTileImage`, NUnit EditMode tests, Unity MCP `script-update-or-create` for all `Assets/**/*.cs`.

---

## File Map

- Create `Assets/Scripts/UI/Objectives/ObjectiveJournalModels.cs`: immutable-ish display structs for category, item, detail, and action rows.
- Create `Assets/Scripts/UI/Objectives/TrackedObjectiveHud.cs`: single tracked objective HUD using Style2 common panel.
- Create `Assets/Scripts/UI/Objectives/ObjectiveJournalPanel.cs`: central journal shell with category rail, list, detail, tracking action, and seed/demo items.
- Modify `Assets/Scripts/UI/Modern/ModernUiPanelInputRouter.cs`: route Tab to Objective Journal and keep full panels mutually exclusive.
- Modify `Assets/Scripts/UI/Modern/ModernUiPanelAutoInstaller.cs`: create journal and tracked HUD hosts on the common canvas.
- Create `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalPanelTests.cs`: RED tests for shell, tracking HUD, and router behavior.
- Modify `.claude/rules/ui-standards.md`: document Objective Journal ownership rule for future sessions.

## Tasks

### Task 1: RED Tests For Objective Journal Shell

**Files:**
- Create: `Assets/Tests/EditMode/UI/Objectives/ObjectiveJournalPanelTests.cs`

- [ ] Write tests proving `ObjectiveJournalPanel` exists, builds `ObjectiveCategoryRail`, `ObjectiveList`, `ObjectiveDetail`, and uses `ModernUiTileImage`.
- [ ] Write tests proving selecting/tracking an item updates `TrackedObjectiveHud` and the HUD shows only one tracked objective.
- [ ] Write tests proving `ModernUiPanelInputRouter` exposes `Bind(..., ObjectiveJournalPanel)` and `ToggleObjectiveJournalPanel`, and opening journal hides inventory/status/settings.
- [ ] Run the EditMode test class and verify RED failure caused by missing objective UI types/methods.

### Task 2: GREEN Objective UI Components

**Files:**
- Create: `Assets/Scripts/UI/Objectives/ObjectiveJournalModels.cs`
- Create: `Assets/Scripts/UI/Objectives/TrackedObjectiveHud.cs`
- Create: `Assets/Scripts/UI/Objectives/ObjectiveJournalPanel.cs`

- [ ] Implement display models.
- [ ] Implement `TrackedObjectiveHud` with `Show(ObjectiveJournalItem)`, `Hide()`, `VisibleText`, and one Style2 root panel.
- [ ] Implement `ObjectiveJournalPanel` with default categories `Goals`, `Quests`, `Campaign`, `Hints`, list/detail slots, `BindTrackedHud`, `Show`, `Hide`, `SelectCategory`, `SelectItem`, and `TrackSelectedItem`.
- [ ] Run the Objective tests and verify the component tests pass.

### Task 3: Router And Installer Integration

**Files:**
- Modify: `Assets/Scripts/UI/Modern/ModernUiPanelInputRouter.cs`
- Modify: `Assets/Scripts/UI/Modern/ModernUiPanelAutoInstaller.cs`

- [ ] Add journal binding overload without breaking existing overloads.
- [ ] Make Tab toggle `ObjectiveJournalPanel`.
- [ ] Preserve public inventory/status/settings toggle methods for existing tests and callers.
- [ ] Install `ObjectiveJournalPanel` and `TrackedObjectiveHud` on the same canvas and bind them to the router.
- [ ] Run existing router/autoinstaller tests plus Objective tests.

### Task 4: Constitutional UI Rule

**Files:**
- Modify: `.claude/rules/ui-standards.md`

- [ ] Add a Korean rule section stating goal/quest/campaign/hint UI must go through `ObjectiveJournalPanel` and `TrackedObjectiveHud`, not independent always-on coordinate panels.
- [ ] Include the Tab ownership rule and Style2 requirement.
- [ ] Verify the diff only touches the new rule section.

### Task 5: Verification

- [ ] Run targeted EditMode tests for Objective, router, and autoinstaller.
- [ ] Run the entity branching CI gate: `bash Scripts/ci/check-no-entity-id-branching.sh` or Git Bash equivalent.
- [ ] Check `git diff --stat` and ensure no unrelated worktree changes are staged or reverted.
