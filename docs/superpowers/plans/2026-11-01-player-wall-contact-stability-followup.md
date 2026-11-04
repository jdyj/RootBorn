# Player wall-contact stability follow-up

Actual native clinic104 cold check exposed movement instability with unchanged body,
colliders and PlayerController code. First test passed, cold failed; do not describe
the PNG integration as a physics fix or a complete native acceptance pass.

Evidence: clinic-level1-cold-collision-observation-failure.json records0.04004 settled
edge excursion (budget0.02). clinic-level1-postphysics-contact-diagnostic.json records
0.03152 edge excursion and0.03967 body range (budget0.03). Explicit post-physics and
editor observations are retained. No wall traversal was observed; W/actual contact
and S retreat were observed. Temporary observer/input devices restored successfully.

After the current facility design pass, reproduce in an isolated actual-input Play
fixture and inspect the controller's constant velocity into a wall versus native
position correction. Do not assume that cause without comparative evidence. Keep
0.02 settled-edge and0.03 body-range budgets, full fixed-step coverage and raw traces.
Evaluate a controller solid-collider cast/velocity clipping approach only after a red
test, preserving speed in free space, diagonal sliding, trigger portals, interaction,
UI movement locks, original colliders, save routing and all career progression.
Do not write actor positions/velocities from QA helpers to manufacture success.

Root cause found in the actual actor: controller's private body reference did not
match the existing root Rigidbody2D. TownSinglePlayerFallbackInstaller adds the
controller first;TownPlayableBaselineRuntimeInstaller reinforces the body later.
Original Awake-only lookup leaves the controller in direct Transform fallback after
physics exists. This explains the root/body discrepancy;constant velocity alone was
an insufficient initial hypothesis. The collider-cast prototype and its9 unit tests
were retired with backups;their old results remain explicitly superseded evidence.

Accepted minimal fix resolves an existing missing/destroyed body at the start of
Update and FixedUpdate. No new body,casts,physics settings,colliders or save schema.
Six construction-order tests:original4failed/2passed;fix6passed. Scoped regression
452/111 fixtures and presenter lifecycle1 passed. Actual ownedInterior2 first/cold
and Chef1/unownedClinic1 first/cold passed the original physical budgets plus an
added root-range<=.03 check. All four stable windows measured root/body/edge range0.
Original initial discrete impact allowance remains.1;no universal zero-penetration
claim. Each run retains actual target contact,W/player-up input,19 fixed observations
over.36sec,S retreat,body reference assertion,and observer/device cleanup.

The W witness now reads W and player input in the same post-physics phase;the earlier
editor-phase false flag while19 game-phase observations were true is retained as a
QA sampling correction,not a relaxed gameplay/contact threshold. Actual Town-House-
Town input traversal also passed with matching body references in both scenes.

Observer API reference: Unity6000 Physics2DFixedUpdate is a native player-loop system:
https://docs.unity3d.com/cn/6000.0/ScriptReference/PlayerLoop.FixedUpdate.Physics2DFixedUpdate.html
SetPlayerLoop permits inserted delegates; only the temporary witness was inserted
and removed without changing original systems:
https://docs.unity.com/ja-jp/engine/6000.3/script-reference/unityengine/lowlevel/playerloop/setplayerloop
