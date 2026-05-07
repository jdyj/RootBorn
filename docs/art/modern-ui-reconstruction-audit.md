# Modern UI Reconstruction Audit

## Replaced

- `Assets/Scripts/UI/HUD/StatusHud.cs`
  - Removed first-scope `Image.Type.Sliced` single-sprite panel usage.
  - Replaced procedural fallback slot backgrounds with `ModernUiTileImage`.
  - Replaced generic `MakePanel` backgrounds with `ModernUiTileImage`.
  - Replaced the inventory `BookPanel` root background with `ModernUiTileImage` instead of a large single sprite.
  - Replaced book buttons and ribbon headers with tiled `ModernUiTileImage` backgrounds.
  - Wired `SettingsPanel` to the Farm HUD via Escape key toggle.
- `Assets/Scripts/UI/Modern/SettingsPanel.cs`
  - Added an open/closeable settings panel built from tiled Modern UI panel recipes.
- `Assets/Scripts/UI/Modern/ModernUiWindowBuilder.cs`
  - Added reusable inventory/status preview construction using the same tiled Modern UI panel recipe.
- `Assets/Scripts/UI/Modern/ModernUiTileImage.cs`
  - Adds deterministic 16x16 child-tile panel construction.
- `Assets/Scripts/UI/Modern/ModernUiPixelSimilarityAudit.cs`
  - Adds crop-normalized reference-vs-render PNG pixel similarity scoring for Modern UI audit runs.
  - Enforces strict minimum thresholds in code: color >= 0.800000, edge >= 0.500000, combined >= 0.710000.

## Deferred

- `Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs`: out of first-scope reconstruction unless needed for settings/inventory/status validation.
- `Assets/Scripts/UI/Quests/QuestHudAutoFiller.cs`: out of first-scope reconstruction.
- `Assets/Scripts/UI/Quests/DialoguePanel.cs`: out of first-scope reconstruction.

## Verification

- `ModernUiRecipeManifestTests`
- `ModernUiRecipeRuntimeTests`
- `ModernUiTileImageTests`
- `ModernUiSourceAuditTests`
- `ModernUiReconstructionScenarioTests`
  - Boots `Managers` before capture so every generated tile has a real cached sub-sprite.
  - Verifies no generated `Tile_*` Image is missing a Sprite.
- `ModernUiPixelSimilarityAuditTests`
  - Captures an isolated settings-panel screenshot with real Modern UI sub-sprites.
  - Fails if strict pixel similarity thresholds are not met.
- Screenshot artifact: `Builds/Logs/modern-ui/settings-panel.png`
- Screenshot artifact: `Builds/Logs/modern-ui/inventory-status-preview.png`
- Pixel similarity artifact: `Builds/Logs/modern-ui/pixel-similarity-report.json`
  - Current baseline compares `docs/art/reference-modern-ui/settings-frame37.png` to `Builds/Logs/modern-ui/settings-panel-pixel-audit.png`.
  - Current baseline score: `colorSimilarity=0.808810`, `edgeSimilarity=0.509563`, `combinedSimilarity=0.719036`.
  - Strict threshold result: `strictPass=true`.
