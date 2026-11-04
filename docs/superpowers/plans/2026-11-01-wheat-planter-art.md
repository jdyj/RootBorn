# Wheat Planters Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Add four static decorative wheat planters to the existing House furniture catalogue, with approved dimensional oak/navy/ivory Comfy art and real placement/save/reload verification.

**Architecture:** Reuse FurnitureSpriteAutoImporter and FurnitureDefinition/Tile assets, the current catalogue, placement input and generic per-cell depth presenter. These are decorative variants rather than a new crop-growth simulation. Existing Crop_Wheat, Farm scene, harvest mechanics, character art, House/Town geometry and current saves remain unchanged. The user authorized continuing design/integration without confirmation; execute inline in the running farmer project and preserve its shared dirty work.

**Tech Stack:** FEVM ComfyUI Qwen2511, Python/Pillow for technical matte/crop/palette work, Unity MCP asset authoring, Addressables packed play, actual InputSystem keyboard/pointer drivers.

---

### Task 1: Review and export four generated props

**Files:** `output/design-comfy-20261031/room-wheat-planters-four-v1-workflow.json`, `wheat-planters-manifest.json`, `wheat-planter-exports/Activity31_wheat_planter_{sprout,leaf,green,ripe}_1x1.png`.

- [x] Generate Comfy162 using the reviewed161/159 wheat-study reference; save submission/provenance without interrupting other jobs.
- [x] Fetch with `python -X utf8 output/design-comfy-20261031/fetch_wheat_planters.py` and inspect all four complete silhouettes. Reject duplicated mature top-right plants, generated text, diagonal containers or thin disconnected stems.
- [x] Export48x96 at48PPU, custom pivot24,24, one-cell footprint and visible floor24. Use nearest2px clusters, binary alpha, at most32 opaque colors, Point sampling and no mipmaps. Review the native-size preview before setting `nativeExportReviewed:true` in the manifest.

### Task 2: Test and author source/catalogue assets

**Files:** `Assets/Art/CareerFurniture/Activity31/Activity31_wheat_planter_{sprout,leaf,green,ripe}_1x1.png`, `Assets/Data/Interiors/Furniture/Activity31/Furniture_Activity31_wheat_planter_{sprout,leaf,green,ripe}.asset`, corresponding `Tiles/Activity31_wheat_planter_{sprout,leaf,green,ripe}_Tile.asset`, `Assets/Tests/EditMode/Interiors/WheatPlanterArtTests.cs`.

- [x] Stage the following asset-contract assertions in `output/design-comfy-20261031/WheatPlanterArtTests.cs.txt`, apply through MCP and run them before import. The missing sprites/definitions must fail.

```csharp
foreach (var slug in new[]{"sprout","leaf","green","ripe"}) {
    string stem="Activity31_wheat_planter_"+slug;
    var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/CareerFurniture/Activity31/"+stem+"_1x1.png");
    Assert.That(sprite,Is.Not.Null);
    Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(48,96)));
    Assert.That(sprite.pivot,Is.EqualTo(new Vector2(24,24)));
    Assert.That(sprite.pixelsPerUnit,Is.EqualTo(48));
    var furniture=AssetDatabase.LoadAssetAtPath<FurnitureDefinition>("Assets/Data/Interiors/Furniture/Activity31/Furniture_"+stem+".asset");
    Assert.That(furniture,Is.Not.Null);
    Assert.That(furniture.FootprintCells,Is.EquivalentTo(new[]{Vector2Int.zero}));
    Assert.That(furniture.AllowedSurfaceIds,Does.Contain("house"));
    Assert.That(furniture.TileParts.Count,Is.EqualTo(1));
    Assert.That(furniture.BlocksMovement,Is.True);
}
```

- [x] Use the existing import pattern below for each reviewed file, adding Point/no-mips/uncompressed/NPOTNone checks and retaining the generated first-cell pivot. Apply authoring helpers through `qa_step.py script`, never write Assets C# directly.

```csharp
var result=FurnitureSpriteAutoImporter.ImportSprite(assetPath,"Assets/Data/Interiors/Furniture");
if(!result.Success)throw new Exception(result.Message);
var serialized=new SerializedObject(result.FurnitureDefinition);
serialized.FindProperty("_displayName").stringValue=displayName;
serialized.ApplyModifiedPropertiesWithoutUndo();
string key=result.FurnitureDefinition.StableId.Replace('.','-');
var paths=new[]{assetPath,result.FurnitureAssetPath,result.TileAssetPath};
var groups=new[]{"Sprites","Data","Tiles"};
var prefixes=new[]{"sprites/furniture/","data/furniture/","tiles/furniture/"};
for(int i=0;i<3;i++){
 var entry=settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(paths[i]),settings.FindGroup(groups[i]));
 entry.SetAddress(prefixes[i]+key);entry.SetLabel("WheatPlanters31",true);
}
```

- [x] Use Korean names `밀 화분 · 새싹`, `밀 화분 · 잎`, `밀 화분 · 푸른 이삭`, `밀 화분 · 황금 이삭`. Keep categoryActivity31 and house/town-test surfaces, movement blocking, one-cell footprints and existing placement behavior.
- [x] Rerun the contract fixture and scoped art/UI regressions. Verify every requested fixture appears in the results, rather than accepting an empty filtered run.

### Task 3: Verify real packed placement, save and reload

**Files:** `output/design-comfy-20261031/saves-wheat-planters-verification`, `evidence/wheat-planters-*`, `VerifyPackedWheatPlanterHashes.cs.txt`, `VerifyPackedWheatPlanterDependencies.cs.txt`.

- [x] Copy the completed exploration/story QA root and record source hashes. Preserve user save storage and the source QA root.
- [x] Record imported GPU hashes, build Addressables, start Boot with packed builder1 and use actual Single Play/Load buttons to enter the copied save.
- [x] Open existing placement UI via real input. Search `밀 화분`, select each new catalogue entry and place it with actual pointer input on four vacant House floor cells. Inspect runtime collision and existing tiles before choosing the cells; initial candidate cells are(3,-5),(5,-3),(7,-3),(11,-5).
- [x] Use the actual Save button. Check four stable IDs, cells, sprite pivots, Point/no-mips and generic depth-renderer state; inspect a Game View screenshot and one front/back player traversal.
- [x] Cold restart through the real slot UI and verify the same four placements persist. Load each independent sprite and its furniture/tile dependencies from real bundles and compare exact GPU hashes.
- [x] Stop Play Mode, clear QA overrides and restore original builder0. Verify source QA hashes and unchanged House/Town/Crop_Wheat files, then update the review archive and manifests with actual evidence. Do not claim growth/harvest simulation, Farm accessibility or a Windows executable build.

Verified outcome: Comfy162 diagonal trial rejected; final Comfy164 source-mask composition accepted. Actual audited cells green(10,-6),sprout(2,-6),leaf(6,-6),ripe(1,-6). Four bundled hashes/dependency chains exact;228/228 tests,60 requested fixtures present. No new legacy Farm loop.
