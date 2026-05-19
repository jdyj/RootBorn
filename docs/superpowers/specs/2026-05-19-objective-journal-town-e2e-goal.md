# Goal: Objective Journal Town E2E Tracking Flow

## Objective

Objective Journal integrated objective tracking must work through the real player-facing Town flow.

The player should be able to enter Town from the MainMenu/New Game path, open Objective Journal with actual `Tab` input, inspect live objectives, track one objective, perform a related Town activity, and see Objective Journal plus `TrackedObjectiveHud` update from the resulting progress state.

## Scope

- Verify the real Town PlayMode path.
- Strengthen Objective Journal provider refresh timing where needed.
- Keep `TrackedObjectiveHud` as the only always-visible objective HUD.
- Verify tracked objective stable key save and restore behavior.
- Capture Game View screenshot evidence.

## Completion Criteria

- PlayMode test uses actual keyboard input to press `Tab` and open Objective Journal.
- Journal displays at least Student Day Guide plus available Quest, Campaign, and Milestone objective entries when the relevant runtime data exists.
- Player-facing flow tracks one objective and updates `TrackedObjectiveHud`.
- A related Town activity or interaction updates objective progress text after refresh.
- Tracked stable key is saved under the active save slot and restored after reload when the objective still exists.
- No separate always-visible goal, quest, campaign, hint, or milestone HUD/Canvas is introduced.
- Objective entries are generated from data and progress state without entity ID-specific C# branching.
- Game View screenshot evidence is written under `Builds/Logs/`.

## Required Tests

- EditMode provider/source aggregation and tracking persistence regression tests.
- PlayMode E2E: MainMenu/New Game -> Town -> `Tab` -> Objective Journal visible -> track objective -> perform related activity -> objective state updates.
- `git diff --check`
- `Scripts/ci/check-no-entity-id-branching.sh`

## Constraints

- `Assets/**/*.cs` must be created or edited only through Unity MCP `script-update-or-create`.
- Visible UI/gameplay changes require actual PlayMode player-flow verification and screenshot evidence.
- `ObjectiveJournalPanel` owns objective list/detail UI.
- `TrackedObjectiveHud` remains the single persistent objective HUD.
- Do not add entity ID-specific C# branches.
