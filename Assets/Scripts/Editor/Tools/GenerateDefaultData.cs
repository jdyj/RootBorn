using System.Collections.Generic;
using System.IO;
using Rootborn.Game.Common;
using Rootborn.Game.Crops;
using Rootborn.Game.Generation;
using Rootborn.Game.Heir;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Knowledge.Triggers;
using Rootborn.Game.Resources;
using Rootborn.Game.Status;
using Rootborn.Game.Tools;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class GenerateDefaultData
    {
        private const string DataRoot = "Assets/Data";
        private const string CropSheetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Farm/Crops/crops 16x16.png";
        private const string ItemSheetPath = "Assets/Pixelwood Valley Icon Pack 1.0/1.0/Items 16x16.png";
        private const string TileSheetPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Tiles/Tile.png";
        private const string PlayerIdleDownPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Idle/Down.png";
        private const string TreeSpritePath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Trees/2.png";
        private const string RockSpritePath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Rocks/1.png";

        [MenuItem("Rootborn/Data/Generate Default Data")]
        public static void Generate()
        {
            PixelwoodSliceSetup.SliceAll();
            PixelwoodSliceSetup.ConfigureFantasyBookUI();
            EnsureSingleSpriteImporter(TreeSpritePath);
            EnsureSingleSpriteImporter(RockSpritePath);

            EnsureFolder(DataRoot);
            EnsureFolder($"{DataRoot}/Tools");
            EnsureFolder($"{DataRoot}/Crops");
            EnsureFolder($"{DataRoot}/Resources");
            EnsureFolder($"{DataRoot}/Knowledge");
            EnsureFolder($"{DataRoot}/Knowledge/Triggers");
            EnsureFolder($"{DataRoot}/Status");
            EnsureFolder($"{DataRoot}/Traits");
            EnsureFolder($"{DataRoot}/Generations");
            EnsureFolder($"{DataRoot}/Registry");
            EnsureFolder($"{DataRoot}/Items");
            EnsureFolder("Assets/Resources");

            var itemSprites = LoadSubSprites(ItemSheetPath);
            var cropSprites = LoadSubSprites(CropSheetPath);
            var treeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TreeSpritePath);
            var rockSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RockSpritePath);

            var bareHand = CreateOrLoad<ToolDefinition>($"{DataRoot}/Tools/Tool_BareHand.asset", t =>
            {
                SetField(t, "_id", "BareHand");
                SetField(t, "_displayKey", "tool.bareHand");
                SetField(t, "_powerMultiplier", 0.3f);
                SetField(t, "_isStartingTool", true);
                SetField(t, "_icon", PickItemSprite(itemSprites, 8));
            });

            var stoneAxe = CreateOrLoad<ToolDefinition>($"{DataRoot}/Tools/Tool_StoneAxe.asset", t =>
            {
                SetField(t, "_id", "StoneAxe");
                SetField(t, "_displayKey", "tool.stoneAxe");
                SetField(t, "_powerMultiplier", 1.2f);
                SetField(t, "_icon", PickItemSprite(itemSprites, 0));
            });

            var stoneHoe = CreateOrLoad<ToolDefinition>($"{DataRoot}/Tools/Tool_StoneHoe.asset", t =>
            {
                SetField(t, "_id", "StoneHoe");
                SetField(t, "_displayKey", "tool.stoneHoe");
                SetField(t, "_powerMultiplier", 1.0f);
                SetField(t, "_icon", PickItemSprite(itemSprites, 16));
            });

            var crop = CreateOrLoad<CropDefinition>($"{DataRoot}/Crops/Crop_Wheat.asset", c =>
            {
                SetField(c, "_id", "Wheat");
                SetField(c, "_displayKey", "crop.wheat");
                SetField(c, "_stageDurationsSec", new float[] { 30f, 30f, 30f, 30f });
                SetField(c, "_growthStageSprites", PickCropStages(cropSprites, rowFromBottom: 0, count: 4));
            });

            var resTree = CreateOrLoad<ResourceNodeDefinition>($"{DataRoot}/Resources/Resource_Tree.asset", r =>
            {
                SetField(r, "_id", "Tree");
                SetField(r, "_displayKey", "resource.tree");
                SetField(r, "_baseHitsToBreak", 5f);
                SetField(r, "_preferredTool", stoneAxe);
                SetField(r, "_surfaceTag", "ground");
                SetField(r, "_sprite", treeSprite);
                SetField(r, "_drops", new[] { MakeDrop("Wood", 1, 3) });
            });

            var resRock = CreateOrLoad<ResourceNodeDefinition>($"{DataRoot}/Resources/Resource_Rock.asset", r =>
            {
                SetField(r, "_id", "Rock");
                SetField(r, "_displayKey", "resource.rock");
                SetField(r, "_baseHitsToBreak", 6f);
                SetField(r, "_preferredTool", null);
                SetField(r, "_surfaceTag", "ground");
                SetField(r, "_drops", new[] { MakeDrop("Stone", 1, 2) });
                SetField(r, "_sprite", rockSprite);
            });

            var triggerHitGround = CreateOrLoad<HitGroundWithRockTrigger>($"{DataRoot}/Knowledge/Triggers/Trigger_HitGroundWithRock.asset", t =>
            {
                SetField(t, "_expectedTargetResourceId", "Rock");
                SetField(t, "_expectedSurface", "ground");
                SetField(t, "_expectedTool", bareHand);
                SetField(t, "_requiredRepeats", 10);
            });

            var knowStone = CreateOrLoad<KnowledgeNode>($"{DataRoot}/Knowledge/Knowledge_StoneTool.asset", k =>
            {
                SetField(k, "_id", "StoneTool");
                SetField(k, "_displayKey", "knowledge.stoneTool");
                SetField(k, "_triggers", new KnowledgeTriggerBase[] { triggerHitGround });
                SetField(k, "_unlocksTools", new[] { stoneAxe, stoneHoe });
            });

            var knowFire = CreateOrLoad<KnowledgeNode>($"{DataRoot}/Knowledge/Knowledge_Fire.asset", k =>
            {
                SetField(k, "_id", "Fire");
                SetField(k, "_displayKey", "knowledge.fire");
            });

            var hunger = CreateOrLoad<StatusEffectDefinition>($"{DataRoot}/Status/Status_Hunger.asset", s =>
            {
                SetField(s, "_id", "Hunger");
                SetField(s, "_displayKey", "status.hunger");
                SetField(s, "_decayPerSecond", 0.05f);
            });

            var fatigue = CreateOrLoad<StatusEffectDefinition>($"{DataRoot}/Status/Status_Fatigue.asset", s =>
            {
                SetField(s, "_id", "Fatigue");
                SetField(s, "_displayKey", "status.fatigue");
                SetField(s, "_decayPerSecond", 0.04f);
            });

            var loneliness = CreateOrLoad<StatusEffectDefinition>($"{DataRoot}/Status/Status_Loneliness.asset", s =>
            {
                SetField(s, "_id", "Loneliness");
                SetField(s, "_displayKey", "status.loneliness");
                SetField(s, "_decayPerSecond", 0.02f);
            });

            var hardy = CreateOrLoad<HeirTrait>($"{DataRoot}/Traits/Trait_Hardy.asset", t =>
            {
                SetField(t, "_id", "Hardy");
                SetField(t, "_displayKey", "trait.hardy");
                SetField(t, "_fatigueDecayMul", 0.8f);
            });

            var greenThumb = CreateOrLoad<HeirTrait>($"{DataRoot}/Traits/Trait_GreenThumb.asset", t =>
            {
                SetField(t, "_id", "GreenThumb");
                SetField(t, "_displayKey", "trait.greenThumb");
                SetField(t, "_gatherSpeedMul", 1.2f);
            });

            var quickLearner = CreateOrLoad<HeirTrait>($"{DataRoot}/Traits/Trait_QuickLearner.asset", t =>
            {
                SetField(t, "_id", "QuickLearner");
                SetField(t, "_displayKey", "trait.quickLearner");
                SetField(t, "_learnSpeedMul", 1.3f);
            });

            var genStone = CreateOrLoad<GenerationProfile>($"{DataRoot}/Generations/Gen_StoneAge.asset", g =>
            {
                SetField(g, "_generationIndex", 2);
                SetField(g, "_displayKey", "generation.stoneAge");
                SetField(g, "_lifetimeSec", 1800f);
                SetField(g, "_startingKnowledge", new[] { knowStone });
                SetField(g, "_startingInventory", new[] { stoneAxe, stoneHoe });
            });

            var genDefault = CreateOrLoad<GenerationProfile>($"{DataRoot}/Generations/Gen_Default.asset", g =>
            {
                SetField(g, "_generationIndex", 1);
                SetField(g, "_displayKey", "generation.primal");
                SetField(g, "_lifetimeSec", 1800f);
                SetField(g, "_startingInventory", new[] { bareHand });
                SetField(g, "_nextGeneration", genStone);
            });

            var tileSprites = LoadSubSprites(TileSheetPath);
            Sprite groundSprite = null;
            for (int i = 0; i < tileSprites.Length; i++)
            {
                if (tileSprites[i].name == "Tile_r2_c4" || tileSprites[i].name == "Tile_r3_c4")
                {
                    groundSprite = tileSprites[i];
                    break;
                }
            }
            if (groundSprite == null && tileSprites.Length > 0) groundSprite = tileSprites[0];

            var playerSprites = LoadSubSprites(PlayerIdleDownPath);
            if (playerSprites.Length == 0)
            {
                Debug.LogWarning($"[ROOTBORN/GenerateData] No sub-sprites found at {PlayerIdleDownPath}. Reimporting and retrying...");
                AssetDatabase.ImportAsset(PlayerIdleDownPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                playerSprites = LoadSubSprites(PlayerIdleDownPath);
            }
            Sprite playerSprite = null;
            if (playerSprites.Length > 0)
            {
                playerSprite = playerSprites.Length > 1 ? playerSprites[1] : playerSprites[0];
                Debug.Log($"[ROOTBORN/GenerateData] PlayerSprite wired: '{playerSprite.name}' from {PlayerIdleDownPath} ({playerSprites.Length} sub-sprites available).");
            }
            else
            {
                Debug.LogError($"[ROOTBORN/GenerateData] Could not load any sub-sprite from {PlayerIdleDownPath}. Player will use red fallback. Re-run 'Rootborn -> Pixelwood -> Slice Sprite Sheets' manually.");
            }

            var itemWood = CreateOrLoad<ItemDefinition>($"{DataRoot}/Items/Item_Wood.asset", it =>
            {
                SetField(it, "_id", "Wood");
                SetField(it, "_displayKey", "item.wood");
                SetField(it, "_icon", PickItemSprite(itemSprites, 32));
                SetField(it, "_maxStack", 99);
                SetField(it, "_category", ItemCategory.Resource);
            });
            var itemStone = CreateOrLoad<ItemDefinition>($"{DataRoot}/Items/Item_Stone.asset", it =>
            {
                SetField(it, "_id", "Stone");
                SetField(it, "_displayKey", "item.stone");
                SetField(it, "_icon", PickItemSprite(itemSprites, 33));
                SetField(it, "_maxStack", 99);
                SetField(it, "_category", ItemCategory.Resource);
            });
            var itemBareHand = CreateOrLoad<ItemDefinition>($"{DataRoot}/Items/Item_Tool_BareHand.asset", it =>
            {
                SetField(it, "_id", "BareHand");
                SetField(it, "_displayKey", "tool.bareHand");
                SetField(it, "_icon", bareHand != null ? bareHand.Icon : null);
                SetField(it, "_maxStack", 1);
                SetField(it, "_category", ItemCategory.Tool);
                SetField(it, "_toolSpritePrefix", string.Empty);
            });
            var itemStoneAxe = CreateOrLoad<ItemDefinition>($"{DataRoot}/Items/Item_Tool_StoneAxe.asset", it =>
            {
                SetField(it, "_id", "StoneAxe");
                SetField(it, "_displayKey", "tool.stoneAxe");
                SetField(it, "_icon", stoneAxe != null ? stoneAxe.Icon : null);
                SetField(it, "_maxStack", 1);
                SetField(it, "_category", ItemCategory.Tool);
                SetField(it, "_toolSpritePrefix", "Axe");
            });

            DeleteIfExists($"{DataRoot}/UISpriteCatalog.asset");
            DeleteIfExists($"{DataRoot}/Registry/GameDataRegistry.asset");
            var registry = CreateOrLoad<GameDataRegistry>("Assets/Resources/GameDataRegistry.asset", r =>
            {
                SetField(r, "_crops", new[] { crop });
                SetField(r, "_tools", new[] { bareHand, stoneAxe, stoneHoe });
                SetField(r, "_resources", new[] { resTree, resRock });
                SetField(r, "_knowledge", new[] { knowStone, knowFire });
                SetField(r, "_traits", new[] { hardy, greenThumb, quickLearner });
                SetField(r, "_statuses", new[] { hunger, fatigue, loneliness });
                SetField(r, "_generations", new[] { genDefault, genStone });
                SetField(r, "_items", new[] { itemWood, itemStone, itemBareHand, itemStoneAxe });
                SetField(r, "_groundSprite", groundSprite);
                SetField(r, "_playerSprite", playerSprite);
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ROOTBORN] Default data generated under {DataRoot}/. Registry: {AssetDatabase.GetAssetPath(registry)}");
        }

        private static void DeleteIfExists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        private static void EnsureMonoScriptWired<T>(T asset) where T : ScriptableObject
        {
            var so = new SerializedObject(asset);
            var scriptProp = so.FindProperty("m_Script");
            if (scriptProp == null) return;
            if (scriptProp.objectReferenceValue != null) return;

            var typeName = typeof(T).Name;
            var guids = AssetDatabase.FindAssets($"t:MonoScript {typeName}");
            foreach (var guid in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(p);
                if (ms != null && ms.GetClass() == typeof(T))
                {
                    scriptProp.objectReferenceValue = ms;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"[ROOTBORN/Data] Repaired m_Script for {AssetDatabase.GetAssetPath(asset)} -> {p}");
                    return;
                }
            }
            Debug.LogWarning($"[ROOTBORN/Data] Could not find MonoScript for {typeName} — m_Script remains broken.");
        }

        private static T CreateOrLoad<T>(string path, System.Action<T> configure) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            else
            {
                EnsureMonoScriptWired(asset);
            }
            configure?.Invoke(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ResourceDrop MakeDrop(string id, int min, int max)
        {
            var drop = new ResourceDrop();
            SetStructField(ref drop, "_resourceId", id);
            SetStructField(ref drop, "_minCount", min);
            SetStructField(ref drop, "_maxCount", max);
            return drop;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (field == null)
            {
                Debug.LogWarning($"[ROOTBORN] field '{fieldName}' not found on {target.GetType().Name}");
                return;
            }
            field.SetValue(target, value);
        }

        private static void SetStructField<TStruct>(ref TStruct target, string fieldName, object value) where TStruct : struct
        {
            object boxed = target;
            var field = typeof(TStruct).GetField(fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);
            if (field == null) return;
            field.SetValue(boxed, value);
            target = (TStruct)boxed;
        }

        private static Sprite[] LoadSubSprites(string assetPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var list = new List<Sprite>();
            foreach (var a in assets)
            {
                if (a is Sprite s) list.Add(s);
            }
            list.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
            return list.ToArray();
        }

        private static Sprite[] PickCropStages(Sprite[] cropSprites, int rowFromBottom, int count)
        {
            if (cropSprites == null || cropSprites.Length == 0) return System.Array.Empty<Sprite>();
            int cols = 0;
            foreach (var s in cropSprites)
            {
                if (s.name.StartsWith("Crop_r0_c"))
                {
                    cols++;
                }
            }
            if (cols == 0) cols = 6;

            int row = rowFromBottom;
            int totalRows = cropSprites.Length / cols;
            int rowFromTop = Mathf.Max(0, totalRows - 1 - row);

            var stages = new List<Sprite>();
            int taken = 0;
            foreach (var s in cropSprites)
            {
                string prefix = $"Crop_r{rowFromTop}_c";
                if (s.name.StartsWith(prefix))
                {
                    stages.Add(s);
                    taken++;
                    if (taken >= count) break;
                }
            }
            if (stages.Count < count)
            {
                int needed = count - stages.Count;
                for (int i = 0; i < needed && i < cropSprites.Length; i++)
                {
                    stages.Add(cropSprites[i]);
                }
            }
            return stages.ToArray();
        }

        private static Sprite PickItemSprite(Sprite[] itemSprites, int index)
        {
            if (itemSprites == null || itemSprites.Length == 0) return null;
            return itemSprites[Mathf.Clamp(index, 0, itemSprites.Length - 1)];
        }

        private static void EnsureSingleSpriteImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
