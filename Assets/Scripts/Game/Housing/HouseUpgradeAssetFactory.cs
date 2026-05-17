#if UNITY_EDITOR
using System.IO;
using Rootborn.Game.Interiors;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Game.Housing
{
    public static class HouseUpgradeAssetFactory
    {
        public static void CreateFirstSliceAssetsForEditor()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Housing");
            EnsureFolder("Assets/Data/Housing/UpgradeStages");
            EnsureFolder("Assets/Data/Housing/Blueprints");
            EnsureFolder("Assets/Data/Housing/Conditions");
            EnsureFolder("Assets/Data/Housing/Effects");
            EnsureFolder("Assets/Data/Housing/Profiles");

            var profile = LoadOrCreateExpandedProfile("Assets/Data/Housing/Profiles/InteriorProfile_ExpandedRoom_01.asset");
            var blueprint = LoadOrCreate<HouseConstructionBlueprintDefinition>("Assets/Data/Housing/Blueprints/HouseBlueprint_ExpandedRoom_01.asset");
            var condition = LoadOrCreate<HouseAlwaysCondition>("Assets/Data/Housing/Conditions/HouseCondition_InteriorEligible_Test.asset");
            var effect = LoadOrCreate<HouseNoOpUpgradeEffect>("Assets/Data/Housing/Effects/HouseEffect_DirectConstructionReward.asset");
            var stage = LoadOrCreate<HouseUpgradeStageDefinition>("Assets/Data/Housing/UpgradeStages/HouseStage_ExpandedRoom_01.asset");

            blueprint.ConfigureForTests(
                "house.blueprint.expanded_room.01",
                new RectInt(0, 0, 4, 4),
                new[] { HouseConstructionCellRequirement.Floor(1, 1), HouseConstructionCellRequirement.Wall(1, 2), HouseConstructionCellRequirement.Door(2, 1) });
            stage.ConfigureForTests("house.stage.expanded_room.01", 1, 300, 120, profile, null, blueprint);
            stage.ConfigureConditionsForTests(null, new HouseUpgradeConditionBase[] { condition });
            stage.ConfigureEffectsForTests(null, new HouseUpgradeEffectBase[] { effect });

            EditorUtility.SetDirty(profile);
            EditorUtility.SetDirty(blueprint);
            EditorUtility.SetDirty(condition);
            EditorUtility.SetDirty(effect);
            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static InteriorGenerationProfile LoadOrCreateExpandedProfile(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<InteriorGenerationProfile>(path);
            if (existing != null) return existing;

            DeleteIncompatibleAsset(path);
            var profile = InteriorGenerationProfile.CreateExpandedOfficeForTests();
            AssetDatabase.CreateAsset(profile, path);
            return AssetDatabase.LoadAssetAtPath<InteriorGenerationProfile>(path) ?? profile;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            DeleteIncompatibleAsset(path);
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return AssetDatabase.LoadAssetAtPath<T>(path) ?? asset;
        }

        private static void DeleteIncompatibleAsset(string path)
        {
            var hadAsset = AssetDatabase.LoadMainAssetAtPath(path) != null;
            var hadFile = File.Exists(path);
            if (!hadAsset && !hadFile) return;

            if (!AssetDatabase.DeleteAsset(path))
            {
                if (File.Exists(path)) File.Delete(path);
                var metaPath = path + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
            }

            AssetDatabase.Refresh();
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var name = Path.GetFileName(path);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
#endif