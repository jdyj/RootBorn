# SPUM Visual Scale and Performance Audit

Date: 2026-05-15

## Scope

Task 13 verifies the SPUM character visual integration against the Pixelwood reference scale and a small representative runtime budget.

No SPUM package files were copied or imported during this task. The task added only the PlayMode verification test and this audit note.

## Verification

- Test: `SPUM_VISUAL_PERF_001`
- Test file: `Assets/Tests/PlayMode/EndToEnd/SpumVisualScaleComparisonTests.cs`
- Evidence file: `Builds/Logs/spum-visual-scale-performance.txt`
- Reference setup: one Pixelwood reference sprite, one SPUM player visual, and eight representative SPUM NPC visuals

## Scale Budget

The approved SPUM-to-Pixelwood visual height ratio is `32/49`, matching `SpumCharacterVisualView.DefaultVisualScale`.

The PlayMode test allows a 5% tolerance around the approved `32/49` ratio. The captured bounds evidence from the RED run before this document was added:

- Pixelwood height: `1.0000`
- SPUM height: `0.6531`
- Actual ratio: `0.6531`
- Relative delta: `0.00%`

## Performance Budget

Representative budget for one player plus eight NPC visuals:

- Renderer count: maximum `10`
- Animator count: maximum `9`
- CharacterVisuals preload probe: maximum `250 ms`
- Memory delta: maximum `16 MB`

Captured probe evidence:

- Renderer count: `10`
- Animator count: `9`
- CharacterVisuals preload milliseconds: `1.25`
- Memory delta bytes: `124136`

## Unity Editor Actions

- Created `SpumVisualScaleComparisonTests.cs` through `script-update-or-create`.
- Ran the PlayMode test once in RED state and confirmed it failed on this missing audit document.
- No Unity scene, prefab, ScriptableObject registry, or SPUM copied asset path was changed by this task.
