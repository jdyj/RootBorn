using NUnit.Framework;
using Rootborn.Game.Housing;
using Rootborn.Game.Interiors;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Housing
{
    public sealed class HouseUpgradeDefinitionTests
    {
        [Test]
        public void HOUSE_UPGRADE_010_StageDefinitionClampsCostsAndRequiresPositiveStage()
        {
            var profile = InteriorGenerationProfile.CreateDefaultOfficeForTests();
            var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.1", 1, 300, 120, profile, null, null);

            Assert.AreEqual("house.stage.1", stage.Id);
            Assert.AreEqual(1, stage.StageIndex);
            Assert.AreEqual(300, stage.HireCost);
            Assert.AreEqual(120, stage.DirectCost);
            Assert.AreSame(profile, stage.Profile);
        }

        [Test]
        public void HOUSE_UPGRADE_011_BlueprintRejectsMissingRequiredCells()
        {
            var blueprint = HouseConstructionBlueprintDefinition.CreateForTests(
                "blueprint.stage.1",
                new RectInt(0, 0, 4, 4),
                new[] { HouseConstructionCellRequirement.Floor(1, 1), HouseConstructionCellRequirement.Wall(1, 2), HouseConstructionCellRequirement.Door(2, 1) });

            Assert.AreEqual(3, blueprint.RequiredCells.Count);
            Assert.IsFalse(blueprint.IsCellAllowed(new Vector2Int(5, 5), HouseConstructionCellKind.Floor));
            Assert.IsTrue(blueprint.IsCellAllowed(new Vector2Int(1, 1), HouseConstructionCellKind.Floor));
        }

        [Test]
        public void HOUSE_STATE_001_SelectedRoomPresetIdPersistsThroughNormalize()
        {
            var state = new HouseStateSaveData
            {
                CurrentStageIndex = 1,
                SelectedRoomPresetId = "preset.expanded.study"
            };

            HouseStatePersistence.Save("house-state-preset-test", state);
            var loaded = HouseStatePersistence.Load("house-state-preset-test");

            Assert.AreEqual(1, loaded.CurrentStageIndex);
            Assert.AreEqual("preset.expanded.study", loaded.SelectedRoomPresetId);
        }
#if UNITY_EDITOR
        [Test]
        public void HOUSE_UPGRADE_040_FirstSliceAssetsExistAndAreValid()
        {
            var stage = UnityEditor.AssetDatabase.LoadAssetAtPath<HouseUpgradeStageDefinition>("Assets/Data/Housing/UpgradeStages/HouseStage_ExpandedRoom_01.asset");
            var blueprint = UnityEditor.AssetDatabase.LoadAssetAtPath<HouseConstructionBlueprintDefinition>("Assets/Data/Housing/Blueprints/HouseBlueprint_ExpandedRoom_01.asset");
            var condition = UnityEditor.AssetDatabase.LoadAssetAtPath<HouseUpgradeConditionBase>("Assets/Data/Housing/Conditions/HouseCondition_InteriorEligible_Test.asset");
            var effect = UnityEditor.AssetDatabase.LoadAssetAtPath<HouseUpgradeEffectBase>("Assets/Data/Housing/Effects/HouseEffect_DirectConstructionReward.asset");
            var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<InteriorGenerationProfile>("Assets/Data/Housing/Profiles/InteriorProfile_ExpandedRoom_01.asset");

            Assert.IsNotNull(stage);
            Assert.IsNotNull(blueprint);
            Assert.IsNotNull(condition);
            Assert.IsNotNull(effect);
            Assert.IsNotNull(profile);
            Assert.AreEqual(1, stage.StageIndex);
            Assert.AreEqual(300, stage.HireCost);
            Assert.AreEqual(120, stage.DirectCost);
            Assert.AreSame(profile, stage.Profile);
            Assert.AreSame(blueprint, stage.Blueprint);
            Assert.GreaterOrEqual(blueprint.RequiredCells.Count, 3);
        }
#endif
    }
}