# Career completion trophy art implementation plan

Follow the user's approved career-growth design and approved urban molding UI style. Continue autonomously under the existing instruction to generate, apply and test assets. The design document lists career trophies/badges and level-three career endings; no new narrative ending or reward economy is introduced.

Goal: Show each career's ComfyUI trophy in its facility panel after the saved career ending is earned.

Design: Add an optional achievement Sprite to FacilityLevelDefinition. FacilityManagementPanel displays it only for the selected adult facility's completed ending and current level. Place an 80px icon beside the existing summary, leave the close control clear, use Point sampling, preserve aspect and disable raycasts. Missing art retains the existing text presentation. No entity-ID branch, save-format change or progression grant.

Files: Assets/Scripts/Game/Careers/FacilityLevelDefinition.cs owns optional art; Assets/Scripts/UI/Careers/FacilityManagementPanel.cs owns display; four authored Level3 SOs own sprite references. Assets/Art/CareerFacilities/Achievements owns sprites. Stage C# through output and apply only with Unity MCP per AGENTS.md.

- [x] Add failing UI tests for earned completion, not-earned completion, lower stage, student state and missing-art fallback.
- [x] Add optional Sprite field and passive UI image; run focused UI/data regression.
- [x] Import four reviewed 64x64 trophies, Point/48PPU/no mipmaps/uncompressed, center pivot; register Addressables and save four level-three references.
- [x] Verify saved refs and inspect all four native facility panels using already-earned isolated saves, with actual walking and E input.
- [x] Rebuild content after final art changes, compare bundle pixels and repeat native packed panel verification.
- [x] Refresh manifests, review archive, evidence and project paths. Continue the approved design work.

Safety: User saves untouched; no progress injection; editor scene references resolve through canonical registry definitions; preserve shared dirty checkout and existing GUIDs.

Evidence: output/design-comfy-20261031/evidence/career-achievements-native.txt and packed-trophy-texture-hashes.txt; all four actual packed panels passed.
