# Keep classroom art visible under the gameplay HUD

Actual School screenshots and saved renderer audits show the classroom chalkboard is drawn at(7.5,-1.33) with a valid high-depth SortingGroup, but the broad top-left clock/resource row covers it while the bounded camera remains fixed near the top of the room. Keep authored furniture/scene transforms and collision geometry unchanged. Keep the approved Comfy urban frames/fonts/colors. Move the clock/energy/focus row into the right side reserved HUD column, and put the tracked guide directly below that row. Keep204 logical pixels clear at the far right for existing edge controls and keep left action buttons reachable. Use one shared layout helper to keep spacing consistent; no scene/entity-ID branches.

- [x] Add red layout invariants: clock/resources/guide leave the left gameplay center clear at reference size, have no mutual overlap, preserve far-right inset and remain anchored on resize/reinstall.
- [x] Apply shared layout to existing presenters and update obsolete left-position assertions to layout/user-visible invariants.
- [x] Run HUD owner/font/modal/guide regressions.
- [x] Share the classroom final content build with current guide/label fixes; actual native School screenshot must show chalkboard below the UI, and adult House menus remain usable.
- [x] Restore QA defaults and refresh review records.

Verified continuation: classroom final build273.6024433s; packed8 textures and3 furniture dependencies; actual School screenshot chalkboard clear. Current student/adult Korean guidance and automatic closed-journal day13→14 verified; evidence under output/design-comfy-20261031/evidence. Latest related broad regression142/142.
