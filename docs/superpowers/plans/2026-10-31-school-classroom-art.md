# School classroom dimensional pixel art

Final five motifs/eight variants are integrated in the approved oak/navy/ivory dimensional style. Desk reuses Comfy125; teacher desk/chalkboard use132/133; solid-back chair134 replaces the rejected thin-spindle school export. Open book uses masked Comfy136/137 edit of the original silhouette;135 was rejected at24x14. No characters.

Standalone canvases/pivots: desk64x48/(32,4), chair32x48/(16,4), teacher96x64/(48,4), chalkboard96x64/(48,32), book24x14/(12,7). Tile variants96x96,48x96,96x96 use first-cell pivot/floor24 and preserve original footprints. Old desk/chair Tile canvases grew transparently to prevent clipping. Original GUIDs and addresses remain. Only BookVisual child moved to(3.7,-6.48), with1px contact over desk; interactive root/trigger/collisions remain.

- [x] Audit source bindings and back up PNG/meta and scene before modifications.
- [x] Generate/reuse motifs through FEVM ComfyUI and inspect native exports; reject spindles and thin book outline.
- [x] Import exact reviewed pixels preserving GUIDs, standalone geometry and Tile footprints.
- [x] Actual slot UI/School entry and115-waypoint WASD classroom approach; exact reviewed GPU pixels across11 scene renderer refs.
- [x] Exact8 Addressables bundle texture hashes and3 furniture definition/Tile/Sprite dependency roots.
- [x] Right-column gameplay HUD leaves actual chalkboard clear; current Korean guide uses authored keys.
- [x]108/108 related regression tests, then12/12 School checks after final book contact adjustment.
- [x] QA routing restored and70-PNG review archive verified.

Evidence: output/design-comfy-20261031/school-classroom-manifest.json, school-book-manifest.json, evidence/school-classroom-native-after.png and corresponding exact-hash/dependency text. Native scene Sprite refs and independent bundle verification are separate evidence; Windows executable not rebuilt.
