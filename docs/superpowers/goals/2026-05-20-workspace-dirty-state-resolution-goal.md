# Goal: Workspace Dirty State Resolution

Paste this prompt into a goal session and execute it as written.

## Objective

Resolve the currently known dirty and untracked workspace items from `docs/superpowers/audits/2026-05-20-workspace-dirty-state-triage.md` so the next feature session can start from a clean, deliberate working tree. This goal is stabilization only: classify ownership, get or infer safe disposition from local evidence where possible, execute only safe cleanup or preservation actions, and commit only the resolved workspace-state changes that are intentionally owned by this goal.

## Non-Negotiable Constraints

- Do not implement gameplay features, UI behavior, Unity scene changes, prefab changes, or production code in this goal.
- Treat unknown dirty or untracked workspace files as user-owned until their intended fate is established by evidence or explicit user instruction.
- Do not delete, revert, move, stage, or commit unrelated dirty files.
- Do not use `git reset --hard`, `git checkout --`, or destructive cleanup commands.
- Do not write or edit `Assets/**/*.cs` through shell commands.
- If a tracked Unity asset deletion is kept, keep its `.meta` decision paired with it.
- If a generated evidence artifact is preserved, move or keep it in a deliberate evidence/docs location and include any paired `.meta` only when it remains under `Assets/`.
- If a generated artifact is removed, document why it is safe before removing it.
- If any item remains ambiguous after investigation, leave it untouched and write a follow-up audit entry instead of guessing.
- Keep the following previously known items isolated from feature work:
  - `D Assets/AddressableAssetsData/link.xml`
  - `D Assets/AddressableAssetsData/link.xml.meta`
  - `M production/qa/evidence/registry-provider-town-flow.png`
  - `?? Assets/CharacterKit.meta`
  - `?? Assets/CharacterKit/`
  - `?? Temp_ModernInteriors_Generator_ContactSheet.png`

## Required Skills

Use these skills in order:

- `superpowers:executing-plans`
- `superpowers:systematic-debugging` if file ownership or origin is unclear
- `superpowers:verification-before-completion`
- `superpowers:finishing-a-development-branch` only after all cleanup decisions and verification are complete

## Required Work

1. Re-read `docs/superpowers/audits/2026-05-20-workspace-dirty-state-triage.md`.
2. Capture fresh workspace evidence:

```powershell
git status --short
git diff --name-status
git diff --stat
git ls-files --others --exclude-standard
git log --oneline -- Assets/AddressableAssetsData/link.xml Assets/AddressableAssetsData/link.xml.meta production/qa/evidence/registry-provider-town-flow.png
Get-ChildItem Assets/CharacterKit -Recurse | Select-Object FullName,Length,LastWriteTime
Get-Item Temp_ModernInteriors_Generator_ContactSheet.png | Select-Object FullName,Length,LastWriteTime
```

3. Create a resolution audit:

```text
docs/superpowers/audits/2026-05-20-workspace-dirty-state-resolution.md
```

The audit must include:

- the fresh command output,
- one decision for each dirty or untracked item,
- evidence supporting each decision,
- the exact action executed or explicitly deferred,
- confirmation that no feature files were modified,
- final `git status --short`.

4. Resolve `Assets/AddressableAssetsData/link.xml` and `Assets/AddressableAssetsData/link.xml.meta` as a paired decision:

- If evidence shows the deletion is accidental or unexplained, restore both files from `HEAD` using a non-destructive restore command scoped only to those two paths.
- If evidence shows the deletion is intentional, document the reason and stage both deletions together.
- If still ambiguous, leave both untouched and record the unresolved decision in the resolution audit.

5. Resolve `production/qa/evidence/registry-provider-town-flow.png`:

- If it corresponds to current registry-provider verification evidence, keep and stage it with the audit.
- If it is stale or accidental, restore only this file from `HEAD`.
- If still ambiguous, leave it untouched and record the unresolved decision in the resolution audit.

6. Resolve `Assets/CharacterKit.meta` and `Assets/CharacterKit/`:

- Inspect the generated report content.
- If it is useful evidence, move the report to a deliberate docs or `production/qa/evidence` path and remove the Unity `Assets/CharacterKit*` generated tree only after documenting why the `Assets/` location is inappropriate.
- If it is temporary generated output, remove the generated tree only after documenting why it is safe.
- If still ambiguous, leave it untouched and record the unresolved decision in the resolution audit.

7. Resolve `Temp_ModernInteriors_Generator_ContactSheet.png`:

- If it is useful visual evidence, move it to a deliberate evidence path and stage it.
- If it is temporary output, remove it only after documenting why it is safe.
- If still ambiguous, leave it untouched and record the unresolved decision in the resolution audit.

8. Stage only the resolution audit and the intentionally resolved workspace changes. Do not stage unrelated feature, generated, Unity import, or evidence files outside this goal's decision table.

## Required TDD

TDD is not applicable because this is a documentation and workspace-state stabilization goal, not production behavior. Verification replaces TDD:

- fresh workspace evidence must be recorded before cleanup,
- each cleanup action must map to a documented decision,
- `git diff --check` must pass,
- `git status --short` must show either a clean tree or only explicitly deferred unresolved items.

## Required Verification

Run:

```powershell
git status --short
git diff --name-status
git diff --stat
git diff --check
git diff --cached --check
git diff --cached --name-status
```

If any Unity asset or `.meta` file remains staged, also run an EditMode smoke check to ensure Unity can import the project state:

```text
tests_run EditMode testNamespace Rootborn.Tests.EditMode.UI.Objectives
```

If `Assets/AddressableAssetsData/link.xml` remains deleted intentionally, run or document why this static check is sufficient:

```powershell
git ls-files Assets/AddressableAssetsData/link.xml Assets/AddressableAssetsData/link.xml.meta
```

No PlayMode screenshot is required for this goal unless the resolution intentionally updates or preserves a visual evidence artifact. If a visual evidence artifact is kept, record its file size and last write time:

```powershell
Get-Item <evidence-path> | Select-Object FullName,Length,LastWriteTime
```

## Acceptance Criteria

- `docs/superpowers/audits/2026-05-20-workspace-dirty-state-resolution.md` exists and has no placeholder markers or unsupported claim.
- Every item from the original dirty-state audit has a concrete disposition: restored, committed, moved, removed, or explicitly deferred with a reason.
- No unrelated files are staged or committed.
- No production code, gameplay asset, scene, or prefab is modified as part of this stabilization goal.
- `git diff --check` and `git diff --cached --check` pass.
- Final `git status --short` is reported.

## Commit

Use one or two focused Korean commits depending on what is resolved:

```text
[DOCS] 워크스페이스 dirty 상태 해소 감사 작성
```

If safe cleanup or preservation actions are also executed, use a separate commit:

```text
[CHORE] 워크스페이스 생성 산출물 정리
```

If a tracked project asset deletion or restoration is intentionally included, use:

```text
[CHORE] Addressables 링크 파일 상태 정리
```

## Loop Continuation

After this goal is implemented, verified, documented, and committed, run the Recurring Next Feature Goal Generator again to select the next improvement.
