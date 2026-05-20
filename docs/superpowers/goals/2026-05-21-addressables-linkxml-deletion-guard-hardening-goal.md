# Goal: Addressables `link.xml` Deletion Guard Hardening

This goal was selected by the Autonomous Recurring Feature Improvement Loop after Unity MCP test runs repeatedly removed `Assets/AddressableAssetsData/link.xml` and its `.meta`.

## Objective

Harden the existing Addressables preservation guard so accidental deletion of `Assets/AddressableAssetsData/link.xml` or `Assets/AddressableAssetsData/link.xml.meta` is caught by both Unity EditMode tests and a cheap repository-level CI command.

## Non-Negotiable Constraints

- This is a foundation/test cycle, not gameplay behavior work.
- Do not change player-facing UI, scenes, prefabs, gameplay behavior, Addressables groups, or linker type coverage unless a failing test proves it is required.
- Do not edit `Assets/**/*.cs` through shell writes. Use Unity MCP script tools for C# tests.
- Keep all gameplay/entity behavior ScriptableObject-driven.
- Do not add production C# branching on entity, route, quest, activity, location, NPC, crop, tool, furniture, career, job, or similar IDs.
- Keep unrelated dirty or untracked files untouched and unstaged.
- If Unity tooling removes `Assets/AddressableAssetsData/link.xml` or `.meta`, restore both immediately before continuing.

## Required Skills

- `superpowers:brainstorming`
- `superpowers:test-driven-development`
- `superpowers:systematic-debugging`
- `superpowers:verification-before-completion`

## Required Work

- Extend `Assets/Tests/EditMode/Addressables/AddressablesLinkXmlPreservationTests.cs` so it fails when `Assets/AddressableAssetsData/link.xml.meta` is missing.
- Add a repository-level script:

```text
Scripts/ci/check-addressables-linkxml-preserved.ps1
```

- The script must fail if either file is missing, deleted in git status, or no longer tracked by git.
- The script must be cheap enough to run after every Unity MCP test batch.

## Required TDD

RED:

- Add the EditMode assertion for the repository-level guard script before creating the script.
- Run the Addressables EditMode test and confirm it fails because `Scripts/ci/check-addressables-linkxml-preserved.ps1` does not exist.

GREEN:

- Add the PowerShell guard script.
- Re-run the Addressables EditMode test and confirm it passes.
- Run the PowerShell guard script and confirm it passes with both files present and tracked.

## Required Verification

Run:

```powershell
tests_run EditMode testNamespace Rootborn.Tests.EditMode.Addressables
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/ci/check-addressables-linkxml-preserved.ps1
git diff --check
& 'C:\Program Files\Git\bin\bash.exe' Scripts/ci/check-no-entity-id-branching.sh
git status --short
```

No PlayMode or screenshot verification is required because this cycle only changes a test, a CI script, and a goal document. It does not alter visible gameplay, UI, scene, prefab, camera, tilemap, placement, or interaction behavior.

## Acceptance Criteria

- The existing XML type preservation test still passes.
- Missing `link.xml.meta` fails the EditMode guard.
- Missing, untracked, or git-deleted `link.xml`/`.meta` fails the CI script.
- The workspace is clean after the cycle commit.

## Commit

Use:

```text
[TEST][CHORE] Addressables link.xml 삭제 가드 강화
```

## Loop Continuation

After this goal is implemented, tested, documented, and committed, return to the Autonomous Recurring Feature Improvement Loop and select the next improvement.
