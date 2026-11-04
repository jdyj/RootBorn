# Day guide presentation correction

Actual native House and School screenshots show English Student Day Guide and a literal Day2 hint on days3/12, including an adult level3 save. The tutorial objective can remain unfinished across days, so do not clear it merely because its original label contains an earlier day number. This is a presentation/data-text correction, not a progression migration.

Preserve all objective stable keys, tutorial progress, rewards and save schemas. Localize the guide title/progress/hints from GameTextCatalog. Prefer the currently registered tutorial stage's authored guide text over old literal NextGuideText cached in older saves, falling back to unknown/custom hints without data loss. Author the two tutorial stage guide strings as localization keys with timeless action text. Resolve those keys in UI; existing day result panel already calls ResolveText. When TP is exhausted, show a day-end prompt. For adult progress, show a career-operations title and generic facility-work/management guidance instead of a student tutorial; no facility-ID branches.

- [x] Complete classroom art native verification first.
- [x] Add red source-level presentation tests for Korean current-day text, registered-stage versus legacy saved hint, exhausted TP, adult phase, custom fallback and unchanged progress/stable key.
- [x] Update guide source and data catalog/stage guide keys through Unity MCP.
- [x] Run objective/guide/day-result regressions and actual packed student/adult save entry; no direct gameplay changes in native proof.
- [x] Rebuild affected Addressables data and verify localized bundled catalog/stages; restore QA defaults and refresh records.

Integration order adjustment: classroom initial native appearance/pixels verified; solid-back chair correction is imported and tested. Its final packed verification will share one data/content build with the guide correction, avoiding a redundant full Addressables build. The pending-activity hint has priority over TP-exhausted day-end guidance.

Verified continuation: classroom final build273.6024433s; packed8 textures and3 furniture dependencies; actual School screenshot chalkboard clear. Current student/adult Korean guidance and automatic closed-journal day13→14 verified; evidence under output/design-comfy-20261031/evidence. Latest related broad regression142/142.
