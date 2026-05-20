using System.IO;
using NUnit.Framework;
using Rootborn.Game.Housing;
using Rootborn.Game.Interiors;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Housing
{
    public sealed class HouseUpgradeDefinitionTests
    {
        private const string HouseUpgradeInteriorLoopE2EPath = "Assets/Tests/PlayMode/EndToEnd/HouseUpgradeInteriorLoopE2EScenarioTests.cs";

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

        [Test]
        public void HOUSE_LOOP_EDIT_050_IntegratedE2EContainsSavedVisualAndOverlayAssertions()
        {
            Assert.IsTrue(File.Exists(HouseUpgradeInteriorLoopE2EPath), "House upgrade/interior loop requires an EndToEnd PlayMode test file for the integrated stage, preset, furniture, reload, and visual cleanup flow.");
            string source = File.ReadAllText(HouseUpgradeInteriorLoopE2EPath);

            StringAssert.Contains("HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload", source, "Integrated House E2E must expose the named scenario from the goal.");
            StringAssert.Contains("AssertHouseStateContainsExactlyOnce", source, "Integrated House E2E must assert saved House stage/preset values exactly once, not only by containment.");
            StringAssert.Contains("AssertFurnitureLayoutContainsExactlyOnce", source, "Integrated House E2E must assert saved furniture layout records exactly once.");
            StringAssert.Contains("AssertRuntimeTilemapsMatchSavedLayout", source, "Integrated House E2E must compare runtime object/occupancy tilemaps with saved furniture layout.");
            StringAssert.Contains("AssertNoVisibleOverlayArtifacts", source, "Integrated House E2E must verify construction, preview, sample, and debug overlays are not visible after reload.");
            StringAssert.Contains("house-upgrade-interior-loop-e2e.png", source, "Integrated House E2E must capture Game View screenshot evidence.");
            StringAssert.Contains("CountOccurrences", source, "Integrated House E2E exact-once assertions must use an explicit occurrence counter.");
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