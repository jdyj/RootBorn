# World Prompt HUD Reservations Implementation Plan

> **For agentic workers:** Use `superpowers:executing-plans` for the parent-owned serialized Unity work. The delegated work is output-only design and RED-fixture staging. No production implementation is authorized before the parent observes the intended RED assertions.

**Goal:** Keep the existing tracked objective card visible and keep the complete world prompt readable as the player approaches from different positions, without changing the prompt's authored vertical anchor, HUD layout, font, copy, art, costs, or saves.

**Architecture:** One public static service in `Rootborn.Game.Player` stores owner-scoped screen-rectangle providers and exposes a pure horizontal fitter. `TrackedObjectiveHud` registers its actual visible root rectangle; `PlayerInteractionRouter` uses that rectangle after its existing camera-size fitting. There is no new interface, MonoBehaviour, serialized field, scene dependency, entity-ID branch, or Addressables data change.

**Tech Stack:** Unity 6000.3, C#, UnityEngine geometry, existing UGUI ownership in Rootborn.UI, reflective NUnit EditMode tests, parent-owned packed native measurement.

## Evidence and source baseline

- `output/design-comfy-20261031/evidence/exploration-visual-layout-findings.json` records functional native PASS and rejected visual overlap. The original localization proof, 11-file earned QA, and 113-art checkpoint remain immutable historical inputs.
- Current `Assets/Scripts/Game/Player/PlayerInteractionRouter.cs:102` defines **FitPromptToCamera**, not FitPromptToViewport. It uniformly limits measured text to 24 screen pixels high and min(360 pixels, 85% viewport width). It does not currently clamp horizontal position. RefreshPrompt restores the authored world anchor each refresh; TextMesh uses MiddleCenter.
- `Assets/Scripts/UI/Objectives/TrackedObjectiveHud.cs` currently has Show/Hide/LateUpdate and no registration lifecycle. Hide deactivates `_root`, while the component can stay enabled. LateUpdate changes CanvasGroup alpha when modal input is blocked.
- `Assets/Scripts/UI/HUD/UrbanGameplayHudLayout.cs` keeps Top=36. A fixed Top=16 moves the whole HUD stack but only clears the observed standing pose. An approach about 0.66 world units farther below the target can move the prompt upward by about 21 screen pixels at the observed 32 pixels/world. That is a regression scenario, not an assertion that this second pose was already rendered.
- Existing Rootborn.Game bootstrap/legacy files already reference UGUI. Do not add UGUI or Rootborn.UI dependencies to the **new service/router implementation**, and do not claim or enforce an unrelated assembly-wide cleanup.

## Alternatives and recommendation

1. **Recommended: reserve the visible card and fit the prompt horizontally.** Uses current screen geometry at any approach pose or supported viewport size, preserves the approved HUD and authored prompt Y, and reuses a small allocation-free calculation.
2. Moving the card to Top=16 is a small edit but fails when the world prompt moves higher; moving only the card also threatens its gap below the top HUD row. Retain this as a rejected proposal, not an applied fix.
3. Hiding the card, shortening copy, or moving the prompt vertically changes the approved information or prop clearance. These are outside scope and unnecessary while there is free horizontal screen space.

## Public API contract

Create `Assets/Scripts/Game/Player/InteractionPromptScreenReservations.cs` only after real RED:

```csharp
namespace Rootborn.Game.Player
{
    public static class InteractionPromptScreenReservations
    {
        public static void Register(UnityEngine.Object owner, System.Func<UnityEngine.Rect?> provider);
        public static void Unregister(UnityEngine.Object owner);
        public static void CollectRects(System.Collections.Generic.List<UnityEngine.Rect> destination);
        public static bool TryFitHorizontal(UnityEngine.Rect prompt, UnityEngine.Rect viewport,
            System.Collections.Generic.IReadOnlyList<UnityEngine.Rect> blockers, float padding,
            System.Collections.Generic.List<UnityEngine.Vector2> scratch,
            out UnityEngine.Rect fitted, out float scale);
    }
}
```

These are signature declarations for agreement, not a staged production implementation.

Registration is keyed by the exact Unity object owner. Re-registering the same owner replaces its provider, never adds another reservation. Unregister removes only that owner's entry and is idempotent, including after that owner was destroyed. CollectRects clears and reuses its destination, invokes live providers, ignores null/invalid rectangles, and prunes destroyed Unity owners without invoking their delegates. It must not inspect scene names or entity IDs. A private method marked `RuntimeInitializeOnLoadMethod(SubsystemRegistration)` clears static registrations at a new runtime subsystem session. Tests must never invoke this reset or globally clear unrelated registrations.

## Pure geometry rules

- Coordinates are screen pixels with bottom-left origin. The viewport is the camera pixelRect, not an assumed zero-origin full screen. Padding is 8 screen pixels at the router call site and is an explicit parameter to the pure function.
- Require finite positive prompt/viewport sizes and non-negative finite padding. The prompt's original vertical band must already fit the padded viewport; if it does not, return false. This narrowly scoped change does not invent vertical clamping or off-screen-label policy.
- Only blockers whose padding-expanded vertical band intersects the original prompt band constrain X. This is conservative during shrinking and does not need a second blocker allocation. Clip their expanded X intervals to the padded viewport, merge overlapping/touching intervals using the caller's cleared/reused `List<Vector2>`, and inspect the free intervals.
- Prefer scale=1 in any free interval that fits the complete original width. Choose the placement with the smallest absolute horizontal center displacement; use the leftmost candidate for exact ties. An already safe prompt is unchanged. Clamp an off-screen X position into a fitting interval.
- Only when no full-size interval fits, use the widest positive interval and uniformly scale both width and height by intervalWidth/originalWidth, capped at 1. Break equal-width ties by nearest center, then leftmost. Preserve the input rectangle's center Y. Never nonuniformly compress the glyphs.
- False means no positive horizontal space (or invalid geometry); return the original rectangle and scale=1. Do not hide the tracked card or move the prompt downward as a hidden fallback. The caller retains existing presentation and this case cannot be claimed visually resolved. The normal Town case has substantial free space on the left.
- Reuse caller-owned lists and a cached noncapturing comparer/in-place ordering. No new arrays, LINQ, delegates, lists, or scene-wide searches in the per-frame path. Registration and capacity growth on first use are not per-frame steady-state allocations.

## HUD integration after RED

Modify only `TrackedObjectiveHud.cs`:

- Add OnEnable registration, OnDisable/OnDestroy owner-only unregister, and a cached provider delegate. Register before `_root` exists safely; its provider then returns null.
- Cache the built root RectTransform, its four-corner array, and its parent Canvas. Refresh the Canvas cache when built/enabled/reparented, not by repeated per-frame GetComponent. Resolve effective rendering through the root Canvas, including nested canvases: null camera for screen-space overlay; the effective canvas render camera for camera/world-space modes. Return null for absent/disabled canvas or unavailable required render camera.
- Provider returns null if component/root is inactive, `_isVisible` is false, or the root CanvasGroup is fully hidden. Show/Hide must continue retaining the existing objective and text. Do not force glyph requests, fonts, language, theme, dimensions, anchors, or HUD positions.
- Project all four cached root corners and return their screen AABB. This reserves the actual decorative card, rather than a guessed 640x200 screen rectangle. Canvas scaling and layout remain owned by the existing UI code.

## Router integration after RED

Modify only `PlayerInteractionRouter.cs`:

- Keep RefreshPrompt's authored world anchor and existing FitPromptToCamera size fit. Add the reservation fit afterward, with cached `List<Rect>`, `List<Vector2>`, and projection storage. No UI type or interface is introduced in Game.Player.
- Measure the actual MeshRenderer screen bounds, call CollectRects, then TryFitHorizontal using the camera pixelRect. Apply the uniform scale and horizontal screen displacement at the existing depth. Keep the authored vertical anchor and Z; do not edit InteractionPromptOffset, player position, or the interactable.
- **Mesh-bearing caveat:** TextAnchor.MiddleCenter does not prove that rendered mesh bounds and the transform anchor have exactly the same center. Keep the authored root Y/depth, reproject actual bounds after scale, and compute the final X correction from those actual bounds. Do not blindly use predicted `fitted.y` as a transform destination. Use the original full vertical band conservatively for blocker selection. If actual scaled bounds unexpectedly escape that band, re-evaluate actual geometry rather than claiming clearance.
- Current gameplay cameras use an unrolled 2D presentation. Verify the actual camera mapping in native proof. A screen-X displacement must not silently introduce a world-Y change. No arbitrary rotated-camera behavior is claimed without a matching test.
- Refresh starts from the authored anchor and existing camera-size fit every time, so reservations do not accumulate X drift or irreversible shrink. Removing/hiding the owner restores the original placement and readable size on the next refresh.

## Task 1: Observe real RED without production edits

**Stage:** `output/design-comfy-20261031/InteractionPromptScreenReservationsTests.cs.txt`

**Intended test path:** `Assets/Tests/EditMode/Player/InteractionPromptScreenReservationsTests.cs` (parent imports through approved Unity script tooling).

- [ ] Parent checks engine idle and imports this fixture only. It references the missing service through its name/reflection, so absence produces an NUnit assertion rather than a compile error.
- [ ] Run the fixture and retain actual RED evidence for missing `Rootborn.Game.Player.InteractionPromptScreenReservations`. If anything fails from compilation, namespace/API mismatch, or fixture cleanup, repair the fixture and rerun RED before production work.
- [ ] Geometry coverage includes the observed-layout approximation and an additional +21.12 px Y pose, nearest full-size horizontal placement, padded viewport containment, overlapping/unsorted blockers, uniform narrow-interval shrinking, impossible full coverage, and reuse of scratch without stale intervals.
- [ ] Owner coverage includes replace/idempotent unregister, live/null providers, one-owner removal preserving another owner and ambient registrations, destroyed Unity-owner pruning, subsystem-reset attribute, and actual HUD Show/Hide/disable/destroy registration through overlay, camera, and nested camera canvases.

## Task 2: Minimal implementation and GREEN

**Create:** the service above. **Modify:** PlayerInteractionRouter.cs and TrackedObjectiveHud.cs only. Keep all production changes serialized through the parent Unity tools.

- [ ] After confirmed RED, implement the agreed registry/fitter contract and run this fixture to GREEN.
- [ ] Add the HUD lifecycle and router use described above; run existing objective HUD modal/ownership, player interaction, HUD layout, and localization regression fixtures. Do not edit UrbanGameplayHudLayout.Top.
- [ ] Check no new references to entity IDs, Ui assembly types in Game.Player, per-frame list/array/closure allocation, copy/language setters, saved scene changes, or Addressables data changes.
- [ ] Extend protected-source evidence for exactly the accepted source/test additions; preserve old proof as historical, not an impossible immutable-current-source claim.

## Task 3: Parent native verification

- [ ] Finish the already staged **before** measurement on the unchanged earned QA first. Retain actual current HUD rects, prompt bounds, and state/file equality. It is evidence collection, not acceptance of Top16.
- [ ] After implementation and tests, boot packed Town against that same earned 11-file QA. Use only WASD to reach the current interaction pose and then a farther-below still-interactable pose. Do not E, Inspect, reset, or mutate progression.
- [ ] Capture fresh decoded screenshots and actual post-fit prompt/HUD rectangles at both poses. Require padded non-overlap, viewport containment, unchanged authored prompt Y and prop clearance, unchanged card position/copy/fonts, full five DTO equality and exact 11-file hashes. Verify the card remains visible.
- [ ] Retain a no-reservation/hidden-owner regression fixture proving authored position/size returns. Native proof must show a normal interaction prompt still tracks the current target; the pure geometry tests alone do not prove router integration.
- [ ] Stop and restore builder0/defaults even on failure. Preserve findings and evidence; only then claim the overlap fixed. The separately rejected exploration-result frame layout remains a separate scoped repair.

## Staging status

Design and reflective test fixture only. No production C# file, scene, prefab, SO, localization, Comfy job, or save was changed. The RED fixture has not been imported or executed by this delegated agent. Native clearance and visual acceptance remain pending parent work.
