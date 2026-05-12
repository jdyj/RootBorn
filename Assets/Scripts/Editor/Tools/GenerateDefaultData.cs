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
        private const string ModernFarmRoot = "Assets/Modern_Farm_v1.2";
        private const string GroundSpritePath = ModernFarmRoot + "/16x16/Single_Files_16x16/0_Complete_Tileset_Singles_16x16/Topsoil_16x16.png";
        private const string TreeSpritePath = ModernFarmRoot + "/16x16/Single_Files_16x16/0_Complete_Tileset_Singles_16x16/Tree_Oak_Green_Small_16x16.png";
        private const string RockSpritePath = ModernFarmRoot + "/16x16/Single_Files_16x16/0_Complete_Tileset_Singles_16x16/Rock_Big_16x16.png";
        private const string WheatSproutPath = ModernFarmRoot + "/16x16/Single_Files_16x16/Crops_16x16/Crop_Grain_Sprout_16x16.png";
        private const string WheatStage1Path = ModernFarmRoot + "/16x16/Single_Files_16x16/Crops_16x16/Crop_Grain_Stage_1_16x16.png";
        private const string WheatStage2Path = ModernFarmRoot + "/16x16/Single_Files_16x16/Crops_16x16/Crop_Grain_Stage_2_16x16.png";
        private const string WheatRipePath = ModernFarmRoot + "/16x16/Single_Files_16x16/Crops_16x16/Crop_Grain_Ripe_16x16.png";
        private const string WoodIconPath = ModernFarmRoot + "/16x16/Single_Files_16x16/Pickup_Items_16x16/Pickup_Fishing_Branch_16x16.png";
        private const string StoneIconPath = ModernFarmRoot + "/16x16/Single_Files_16x16/Pickup_Items_16x16/Pickup_Resource_1_16x16.png";
        private const string AxeIconPath = ModernFarmRoot + "/Icons/Icons_16x16/Icons_16x16_Singles/Icons_16x16_Tools_Axe.png";
        private const string HoeIconPath = ModernFarmRoot + "/Icons/Icons_16x16/Icons_16x16_Singles/Icons_16x16_Tools_Shovel.png";
        private const string PickaxeIconPath = ModernFarmRoot + "/Icons/Icons_16x16/Icons_16x16_Singles/Icons_16x16_Tools_Shovel.png";
        private const string BareHandIconPath = ModernFarmRoot + "/Icons/Icons_16x16/Icons_16x16_Singles/Icons_16x16_Tools_Bag.png";
        private const string PlayerSpritePath = ModernFarmRoot + "/Generated/ModernFarmer_IdleDown_16x16.png";

        [MenuItem("Rootborn/Data/Generate Default Data")]
        public static void Generate()
        {
            ModernFarmSliceSetup.SliceCore16();
            EnsureModernSpriteImporters();

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

            var bareHandIcon = LoadSprite(BareHandIconPath);
            var axeIcon = LoadSprite(AxeIconPath);
            var hoeIcon = LoadSprite(HoeIconPath);
            var pickaxeIcon = LoadSprite(PickaxeIconPath);
            var woodIcon = LoadSprite(WoodIconPath);
            var stoneIcon = LoadSprite(StoneIconPath);
            var groundSprite = LoadSprite(GroundSpritePath);
            var treeSprite = LoadSprite(TreeSpritePath);
            var rockSprite = LoadSprite(RockSpritePath);
            var playerSprite = LoadSprite(PlayerSpritePath);

            var bareHand = CreateOrLoad<ToolDefinition>($"{DataRoot}/Tools/Tool_BareHand.asset", t =>
            {
                SetField(t, "_id", "BareHand");
                SetField(t, "_displayKey", "tool.bareHand");
                SetField(t, "_powerMultiplier", 0.3f);
                SetField(t, "_isStartingTool", true);
                SetField(t, "_icon", bareHandIcon);
            });

            var stoneAxe = CreateOrLoad<ToolDefinition>($"{DataRoot}/Tools/Tool_StoneAxe.asset", t =>
            {
                SetField(t, "_id", "StoneAxe");
                SetField(t, "_displayKey", "tool.stoneAxe");
                SetField(t, "_powerMultiplier", 1.2f);
                SetField(t, "_icon", axeIcon);
            });

            var stoneHoe = CreateOrLoad<ToolDefinition>($"{DataRoot}/Tools/Tool_StoneHoe.asset", t =>
            {
                SetField(t, "_id", "StoneHoe");
                SetField(t, "_displayKey", "tool.stoneHoe");
                SetField(t, "_powerMultiplier", 1.0f);
                SetField(t, "_icon", hoeIcon);
            });

            var stonePickaxe = CreateOrLoad<ToolDefinition>($"{DataRoot}/Tools/Tool_StonePickaxe.asset", t =>
            {
                SetField(t, "_id", "StonePickaxe");
                SetField(t, "_displayKey", "tool.stonePickaxe");
                SetField(t, "_powerMultiplier", 1.2f);
                SetField(t, "_icon", pickaxeIcon);
            });

            var crop = CreateOrLoad<CropDefinition>($"{DataRoot}/Crops/Crop_Wheat.asset", c =>
            {
                SetField(c, "_id", "Wheat");
                SetField(c, "_displayKey", "crop.wheat");
                SetField(c, "_stageDurationsSec", new float[] { 30f, 30f, 30f, 30f });
                SetField(c, "_growthStageSprites", new[]
                {
                    LoadSprite(WheatSproutPath),
                    LoadSprite(WheatStage1Path),
                    LoadSprite(WheatStage2Path),
                    LoadSprite(WheatRipePath)
                });
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
                SetField(r, "_preferredTool", stonePickaxe);
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
                SetField(k, "_unlocksTools", new[] { stoneAxe, stoneHoe, stonePickaxe });
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
                SetField(g, "_startingInventory", new[] { stoneAxe, stoneHoe, stonePickaxe });
            });

            var genDefault = CreateOrLoad<GenerationProfile>($"{DataRoot}/Generations/Gen_Default.asset", g =>
            {
                SetField(g, "_generationIndex", 1);
                SetField(g, "_displayKey", "generation.primal");
                SetField(g, "_lifetimeSec", 1800f);
                SetField(g, "_startingInventory", new[] { bareHand });
                SetField(g, "_nextGeneration", genStone);
            });

            var itemWood = CreateOrLoad<ItemDefinition>($"{DataRoot}/Items/Item_Wood.asset", it =>
            {
                SetField(it, "_id", "Wood");
                SetField(it, "_displayKey", "item.wood");
                SetField(it, "_icon", woodIcon);
                SetField(it, "_maxStack", 99);
                SetField(it, "_category", ItemCategory.Resource);
            });
            var itemStone = CreateOrLoad<ItemDefinition>($"{DataRoot}/Items/Item_Stone.asset", it =>
            {
                SetField(it, "_id", "Stone");
                SetField(it, "_displayKey", "item.stone");
                SetField(it, "_icon", stoneIcon);
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
            var itemStonePickaxe = CreateOrLoad<ItemDefinition>($"{DataRoot}/Items/Item_Tool_StonePickaxe.asset", it =>
            {
                SetField(it, "_id", "StonePickaxe");
                SetField(it, "_displayKey", "tool.stonePickaxe");
                SetField(it, "_icon", stonePickaxe != null ? stonePickaxe.Icon : null);
                SetField(it, "_maxStack", 1);
                SetField(it, "_category", ItemCategory.Tool);
                SetField(it, "_toolSpritePrefix", "Pickaxe");
            });

            DeleteIfExists($"{DataRoot}/UISpriteCatalog.asset");
            DeleteIfExists($"{DataRoot}/Registry/GameDataRegistry.asset");
            var registry = CreateOrLoad<GameDataRegistry>("Assets/Resources/GameDataRegistry.asset", r =>
            {
                SetField(r, "_crops", new[] { crop });
                SetField(r, "_tools", new[] { bareHand, stoneAxe, stoneHoe, stonePickaxe });
                SetField(r, "_resources", new[] { resTree, resRock });
                SetField(r, "_knowledge", new[] { knowStone, knowFire });
                SetField(r, "_traits", new[] { hardy, greenThumb, quickLearner });
                SetField(r, "_statuses", new[] { hunger, fatigue, loneliness });
                SetField(r, "_generations", new[] { genDefault, genStone });
                SetField(r, "_items", new[] { itemWood, itemStone, itemBareHand, itemStoneAxe, itemStonePickaxe });
                SetField(r, "_groundSprite", groundSprite);
                SetField(r, "_playerSprite", playerSprite);
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ROOTBORN] Modern default data generated under {DataRoot}/. Registry: {AssetDatabase.GetAssetPath(registry)}");
        }

        private static void EnsureModernSpriteImporters()
        {
            foreach (string path in new[]
            {
                GroundSpritePath,
                TreeSpritePath,
                RockSpritePath,
                WheatSproutPath,
                WheatStage1Path,
                WheatStage2Path,
                WheatRipePath,
                WoodIconPath,
                StoneIconPath,
                AxeIconPath,
                HoeIconPath,
                PickaxeIconPath,
                BareHandIconPath,
                PlayerSpritePath
            })
            {
                EnsureSingleSpriteImporter(path);
            }
        }

        private static Sprite LoadSprite(string assetPath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[ROOTBORN/Data] Missing Modern Farm sprite: {assetPath}");
            }

            return sprite;
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
            Debug.LogWarning($"[ROOTBORN/Data] Could not find MonoScript for {typeName}; m_Script remains broken.");
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
