# Read-only student status UI, 2026-11-01

Continue the user-approved continuous ComfyUI dimensional urban UI work. Actual C-key evidence `output/design-comfy-20261031/evidence/student-status-native-before.png/txt` shows four English labels outside their root, floating empty frames, a blank portrait and two static fake gauges. No student progress is bound. Keep the existing ModernUiStatusPanel/C-key/modal-exclusivity public API; replace this prototype with a coherent560x540 Comfy-molding window and3 inset cards for Energy,Focus,Stress. No new character art or invented health/max-stat gauge.

Layout: outer Comfy CommonPanel; title at32,-32; existing Comfy close glyph in48px upper-right button; day/remaining-activity subtitle32,-88; three496x96 slot cards at32,-140/-248/-356 with48px icon,localized label and actual numeric value; footer C-to-close at32,-484. Card icons reuse existing Comfy energy/focus; Comfy184 new pressure-meter48px icon represents stress only (static symbol, not a claimed live gauge).

Data/ownership: installer passes the same local-player GameObject already selected for inventory. BindOwner stores that owner; only resolve its StudentLifeProgressComponent,never global Find/remote fallback. UI never calls EnsureProgress or mutates progression. Subscribe component TimeChanged/VitalsChanged and static AvailabilityChanged for late ownership data; remove on hide/destroy/rebind. Resolve added component only on explicit owner/availability change. Refresh language labels if GameText.Language changes while visible. Missing owner shows loading/dashes,not guessed values.

Localization: append7 bilingual ui.status.* keys542->549 (title,day-points,energy,focus,stress,waiting,close-hint); retain every prior value and all data definitions. Shared theme append stress21->22; reuse energy/focus/close icons and shared loader.48x48/16PPU/center24/2px/binary/<=32colors new raster only.

- [x] Capture native before and preserve original UI/installer/catalog/theme source and scene/data/art hashes.
- [x] Tests first: local-owner isolation,missing owner/late component availability,event updates without data changes,hide/rebind/destroy subscriptions,all labels/values inside molding and KO/EN,installer binds selected inventory owner,stress sprite/theme geometry.
- [x] Implement existing panel and installer binding; migrate old portrait/static-health placeholder test to meaningful3-card/close contract (do not simply remove tests).
- [x] Generate/review/import Comfy184 stress icon; native-size review before binding.
- [x] Broaden regression,build/verify exact icon/theme/catalog and unchanged item dependencies.
- [x] Actual C open,pointer close,C reopen,owned live values vs actual player progress,cold reload and source/QA-save preservation. No direct native progression changes; if value-change test needed use a real earned gameplay action on copied save.
- [x] Restore builder0/overrides; package only verified accepted art and actual evidence. No full-GDD/project-pass/executable claim.

Verified:359/359 across93 named fixtures. Actual C and pointer Close,real House sleep19->20/weekend3TP,Help3->2/vitals11/10/3,House-Town roundtrip and cold full DTO/new GUID+legacy requests exact. Late null inventory layout install initially cleared owner;regression failed first,guarded binding now survives actual long route and cold reload.10/10 exact bundled textures,all549 bilingual values/22 theme entries/2 item dependencies verified.76 captured original files and original earned QA save hashes exact. Builder0/defaults restored.97 game review exports+3 unintegrated architecture studies;no executable/full-project/GDD completion claim.
