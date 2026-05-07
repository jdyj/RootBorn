# Town Concept Domain Mapping

Date: 2026-05-08

## Direction

ROOTBORN now targets a town-life fantasy. The player is a city resident, not a farmer.

## Mapping

| Farm concept | Town-life replacement | First implementation action |
| --- | --- | --- |
| Farm scene | Town scene / district scene | Use `Assets/Scenes/Town.unity` as the default playable direction after audit. |
| Farmer | City resident | Update user-facing labels and status text before technical renames. |
| Crop | Activity progress / personal task / work shift | Remove crop growth from the first user-facing loop. |
| Farm tool | Daily-life tool / phone / transit card / work badge / notebook | Reuse `ToolDefinition` only as data, not as farm semantics. |
| Resource node | Shop, service counter, errand point, neighborhood object | Remove tree/rock gathering from the default town path. |
| Recipe | Preparation, purchase, service use, work output | Keep data-driven result logic. |
| Quest | Neighbor request, job task, city event, relationship event | Preserve reward transaction preflight and idempotency. |
| Knowledge | District info, job skill, relationship tip, life tip | Keep `KnowledgeNode` data-driven. |
| Status | Energy, money, anxiety, knowledge, relationships | Use `ModernSocietyStats` as the first town stat vocabulary. |
| Inventory | Bag / possessions | Preserve capacity and duplicate handling. |

## First-Scope User-Facing Terms

Replace or hide these terms in first-scope UI: `Farm`, `Farmer`, `Crop`, `Harvest`, `Water Crop`, `Fertilize Crop`, `Farm Help`.

Allowed legacy terms only in source symbols or audit notes: `Farm`, `Crop`, `Farming`.
