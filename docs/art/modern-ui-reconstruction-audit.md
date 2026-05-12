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
- `docs/art/modern-ui-reconstruction-manifest.json`
  - Extended the reconstruction manifest from settings/inventory/status preview coverage to panel reconstruction coverage.
  - Added concrete 16x16 sub-sprite mappings for `title-tab`, `selection-cursor`, `scrollbar-track`, `scrollbar-thumb`, `popup-frame`, `item-action-button`, `portrait-frame`, `icon-frame`, `gauge-track`, `quest-row`, `quest-detail-frame`, `reward-frame`, and `claim-button`.
  - Added `quest` and `popup` recipes and expanded the `inventory` and `status` recipes with popup/icon-frame requirements.
- `Assets/Scripts/UI/Modern/ModernUiRecipes.cs`
  - Added runtime `QuestWindow` and `PopupWindow` recipes so panel builders can share the same manifest vocabulary.
- `Assets/Scripts/UI/Modern/ModernUiInventoryPanel.cs`
  - Added a standalone tiled Modern UI inventory panel with title tab, slot grid, scrollbar, bottom controls, and deterministic 16-slot layout.
  - Slot clicks open a tiled `ItemDetailPopup` with item icon, name, description, count, and tiled action button.
  - Empty slot clicks close the popup without throwing.
- `Assets/Scripts/UI/Modern/ModernUiStatusPanel.cs`
  - Added a standalone tiled Modern UI status panel with title tab, portrait frame, status value frame, gauge rows, icon frame, and bottom button area.
- `Assets/Scripts/UI/Modern/ModernUiPanelInputRouter.cs`
  - Added the shared `I` and `TAB` input contract for Modern UI panels.
  - `I` toggles `ModernUiInventoryPanel`; `TAB` toggles `ModernUiStatusPanel`.
  - Inventory/status overlays are mutually exclusive through the router.
- `Assets/Scripts/UI/Modern/ModernUiPanelAutoInstaller.cs`
  - Adds Farm-scene runtime attachment for `ModernUiInventoryPanel`, `ModernUiStatusPanel`, and `ModernUiPanelInputRouter` on the active canvas.
  - Binds `PlayerInventory` into the tiled inventory panel when present.
  - Uses scene-root traversal instead of `GameObject.Find`, `FindObjectOfType`, or `Resources.Load`.
- `Assets/Scripts/Game/Bootstrap/FarmModernUiPanelBridge.cs`
  - Adds a Game-assembly Farm scene bridge that polls for the runtime canvas and `PlayerInventory`.
  - Calls the UI installer by reflection to preserve the existing `Rootborn.Game` -> `Rootborn.UI` assembly boundary.
  - Uses scene-root traversal instead of global find/resource APIs.
- `Assets/Scripts/UI/Quests/QuestLogPanel.cs`
  - Replaced the bind-only shell with a deterministic Modern UI tiled quest panel structure.
  - Creates `QuestTitleTab`, `QuestList`, `QuestDetail`, `ObjectiveProgress`, `RewardRow`, `QuestScrollbar`, and a tiled `ClaimButton`.
  - Added quest-array binding that renders quest rows, selected quest detail text, objective state text, and reward row text.
  - Keeps the reward claim path on `QuestRewardButton`, preserving `QuestLog.CanClaimReward` preflight and `QuestLog.ClaimReward` atomic/idempotent behavior.
- `Assets/Scripts/UI/Quests/QuestHudAutoFiller.cs`
  - Passes registry quests and a `RewardRuntimeContext` into `QuestLogPanel`, so the tiled quest panel receives list/detail/reward data and reward preflight context at runtime.

## Deferred

- `Assets/Scripts/UI/MainMenu/SaveSlotSelectPanel.cs`: out of first-scope reconstruction unless needed for settings/inventory/status validation.
- `Assets/Scripts/UI/Quests/QuestHudAutoFiller.cs`: still uses its existing scene wiring; `QuestLogPanel` now builds the tiled structure once present.
- `Assets/Scripts/UI/Quests/DialoguePanel.cs`: out of first-scope reconstruction.
- Formal PlayMode TestRunner validation for `ModernUiPanelAutoInstaller` remains open.
- Formal end-to-end `I`/`TAB` input-system validation and screenshot capture remain open; current verification covers the compiled router contract, source-level Farm attachment guard, manual fresh Play Mode attachment evidence, and direct router toggle behavior.
- Current PlayMode runner state is unstable: the targeted `ModernUiPanelAutoInstallerPlayModeTests` initially reproduced missing runtime attachment, then later Unity Test Framework/MCP runs misrouted to unrelated tests, failed in Test Framework bootstrap, or entered generated `InitTestScene*` scenes. Direct TestRunner API can list the PlayMode test, but PlayMode domain reload discards the dynamic callback used for result capture. Treat formal PlayMode validation as not yet green.
- Quest panel scroll mechanics and richer objective/reward formatting remain open; current binding renders rows/detail/state/reward text and preserves claim preflight.
- Inventory/status reference screenshot pixel audits remain open; the existing strict pixel audit still covers settings only. Runtime Game View screenshot capture through MCP returned null while Play Mode was active, so no persisted inventory/status screenshot audit artifact is available yet.

## Verification

- `ModernUiRecipeManifestTests`
- `ModernUiPanelReconstructionManifestTests`
- `ModernUiRecipeRuntimeTests`
- `ModernUiPanelReconstructionRuntimeTests`
- `ModernUiInventoryPanelTests`
- `ModernUiStatusPanelTests`
- `ModernUiPanelInputRouterTests`
- `ModernUiPanelAutoInstallerSourceTests`
  - `AutoInstaller_AttachesPanelsRouterAndBindsPlayerInventoryWithoutGlobalFindCalls`: PASS via direct reflection execution.
  - `FarmBridge_PollsFarmSceneCanvasAndCallsUiInstallerByReflectionWithoutGlobalFindCalls`: PASS via direct reflection execution.
- `ModernUiPanelAutoInstallerPlayModeTests`
  - Added as the required runtime scenario for Farm attachment and panel toggles.
  - RED evidence captured: before the bridge, it failed because `ModernUiPanelInputRouter` was not attached at runtime.
  - Fresh manual Play Mode attachment evidence captured: `MODERN_UI_FRESH_COUNTS scene=Farm roots=22 canvases=1 routers=1 invPanels=1 statusPanels=1 playerInventories=1 bridges=1`.
  - Manual fresh Play Mode router evidence captured: `MANUAL_MODERN_UI_RUNTIME_PASS scene=Farm canvases=1 bridgeCount=1 playerInventoryCount=1 inventoryAfterInventory=True statusAfterInventory=False inventoryAfterStatus=False statusAfterStatus=True inventoryTiles=6770 statusTiles=6770`.
  - Manual inventory-open evidence captured for screenshot setup: `MODERN_UI_OPEN_INVENTORY_PASS visibleInventory=True visibleStatus=False scene=Farm`.
  - Formal GREEN evidence is still blocked by Unity Test Framework/MCP runner instability.
- `Scripts/ci/check-no-entity-id-branching.sh`
  - PASS via Git Bash: `OK: no entity-id branching in system code.`
- `ModernUiTileImageTests`
- `ModernUiSourceAuditTests`
- `QuestUiTests`
- `QuestHudAutoFillerSourceTests`
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
