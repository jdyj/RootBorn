# Missing Image References - 2026-05-10

## Confirmed Missing Serialized References

- None found by Unity `SerializedObject` scan across `Assets/Data`, `Assets/Scenes`, and `Assets/AddressableAssetsData` after repair.

## Removed Optional Pack Paths Found During Test Repair

- `Assets/moderninteriors-win/1_Interiors/16x16/Room_Builder_subfiles/Room_Builder_Floors_16x16.png`
- `Assets/moderninteriors-win/1_Interiors/16x16/Room_Builder_subfiles/Room_Builder_Walls_16x16.png`
- `Assets/moderninteriors-win/4_User_Interface_Elements/UI_16x16.png`

## Repaired Reference

- `Assets/Data/NPCs/Npc_FirstGuide.asset`
  - Broken previous `_worldTexture` GUID: `edd742aa781b8da4580badf30a0c5ee8`
  - Rewired to: `Assets/Modern_Farm_v1.2/Generated/ModernFarmer_IdleDown_16x16.png`
