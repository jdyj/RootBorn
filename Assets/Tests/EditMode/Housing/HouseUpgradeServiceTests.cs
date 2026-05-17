using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Housing;
using Rootborn.Game.Interiors;
using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Housing
{
    public sealed class HouseUpgradeServiceTests
    {
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, "house-upgrade-tests-" + TestContext.CurrentContext.Test.ID);
            SaveService.SetRootDirectoryForTests(_saveRoot);
        }

        [TearDown]
        public void TearDown()
        {
            SaveService.SetRootDirectoryForTests(null);
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }

        [Test]
        public void HOUSE_UPGRADE_001_CurrencySpendIsAtomicWhenFundsAreInsufficient()
        {
            var wallet = new HouseCurrencyWallet(50);
            Assert.IsFalse(wallet.TrySpend(80));
            Assert.AreEqual(50, wallet.Balance);
        }

        [Test]
        public void HOUSE_UPGRADE_002_HouseStatePersistsStageAndActiveConstruction()
        {
            var state = new HouseStateSaveData
            {
                CurrentStageIndex = 1,
                ActiveConstructionStageId = "house.stage.1",
                LatestRoute = HouseUpgradeRouteKind.DirectConstruction,
                CompletionHistoryIds = new[] { "house.stage.1:direct" }
            };

            HouseStatePersistence.Save("slot-0", state);
            var loaded = HouseStatePersistence.Load("slot-0");

            Assert.AreEqual(1, loaded.CurrentStageIndex);
            Assert.AreEqual("house.stage.1", loaded.ActiveConstructionStageId);
            Assert.AreEqual(HouseUpgradeRouteKind.DirectConstruction, loaded.LatestRoute);
            Assert.AreEqual("house.stage.1:direct", loaded.CompletionHistoryIds[0]);
        }

        [Test]
        public void HOUSE_UPGRADE_030_HireRouteRejectsInsufficientFundsWithoutStageChange()
        {
            var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.1", 1, 300, 120, InteriorGenerationProfile.CreateDefaultOfficeForTests(), null, null);
            var state = new HouseStateSaveData();
            var wallet = new HouseCurrencyWallet(100);
            var service = new HouseUpgradeService(new[] { stage });

            var result = service.TryHire(stage, state, wallet);

            Assert.AreEqual(HouseUpgradeResultKind.InsufficientCurrency, result.Kind);
            Assert.AreEqual(0, state.CurrentStageIndex);
            Assert.AreEqual(100, wallet.Balance);
        }

        [Test]
        public void HOUSE_UPGRADE_031_HireRoutePaysAndRaisesStageAtomically()
        {
            var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.1", 1, 300, 120, InteriorGenerationProfile.CreateDefaultOfficeForTests(), null, null);
            var effect = ScriptableObject.CreateInstance<CountingHouseEffect>();
            SetHireEffectsForTests(stage, effect);
            var state = new HouseStateSaveData();
            var wallet = new HouseCurrencyWallet(500);
            var service = new HouseUpgradeService(new[] { stage });

            var result = service.TryHire(stage, state, wallet);

            Assert.AreEqual(HouseUpgradeResultKind.Applied, result.Kind);
            Assert.AreEqual(1, state.CurrentStageIndex);
            Assert.AreEqual(200, wallet.Balance);
            Assert.AreEqual(HouseUpgradeRouteKind.HireConstruction, state.LatestRoute);
            Assert.AreEqual(1, effect.ApplyCount);
        }

        [Test]
        public void HOUSE_UPGRADE_032_DirectRouteIsUnavailableWithoutDirectCondition()
        {
            var blueprint = CreateBlueprintForTests();
            var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.1", 1, 300, 120, InteriorGenerationProfile.CreateDefaultOfficeForTests(), null, blueprint);
            var state = new HouseStateSaveData();
            var wallet = new HouseCurrencyWallet(500);
            var service = new HouseUpgradeService(new[] { stage });

            Assert.IsFalse(service.CanStartDirect(stage, new HouseUpgradeContext(state, wallet, null)));
        }

        [Test]
        public void HOUSE_UPGRADE_033_DirectRouteFailsClosedWhenDirectConditionReferenceIsNull()
        {
            var blueprint = CreateBlueprintForTests();
            var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.1", 1, 300, 120, InteriorGenerationProfile.CreateDefaultOfficeForTests(), null, blueprint);
            SetDirectConditionsForTests(stage, new HouseUpgradeConditionBase[] { null });
            var state = new HouseStateSaveData();
            var wallet = new HouseCurrencyWallet(500);
            var service = new HouseUpgradeService(new[] { stage });

            Assert.IsFalse(service.CanStartDirect(stage, new HouseUpgradeContext(state, wallet, null)));
        }

        [Test]
        public void HOUSE_UPGRADE_034_DirectCompletionRejectsUnavailableRouteWithoutSpending()
        {
            var blueprint = CreateBlueprintForTests();
            var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.1", 1, 300, 120, InteriorGenerationProfile.CreateDefaultOfficeForTests(), null, blueprint);
            var state = new HouseStateSaveData();
            var wallet = new HouseCurrencyWallet(500);
            var session = CreateCompletedSession(blueprint);
            var service = new HouseUpgradeService(new[] { stage });

            var result = service.TryCompleteDirect(stage, state, wallet, session);

            Assert.AreEqual(HouseUpgradeResultKind.RequirementFailed, result.Kind);
            Assert.AreEqual(0, state.CurrentStageIndex);
            Assert.AreEqual(500, wallet.Balance);
            Assert.AreEqual(HouseUpgradeRouteKind.None, state.LatestRoute);
        }

        [Test]
        public void HOUSE_UPGRADE_035_DirectCompletionRejectsIncompleteSessionWithoutSpending()
        {
            var blueprint = CreateBlueprintForTests();
            var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.1", 1, 300, 120, InteriorGenerationProfile.CreateDefaultOfficeForTests(), null, blueprint);
            SetDirectConditionsForTests(stage, ScriptableObject.CreateInstance<AlwaysHouseConditionForTests>());
            var state = new HouseStateSaveData();
            var wallet = new HouseCurrencyWallet(500);
            var session = new HouseConstructionSession(blueprint);
            var service = new HouseUpgradeService(new[] { stage });

            var result = service.TryCompleteDirect(stage, state, wallet, session);

            Assert.AreEqual(HouseUpgradeResultKind.ConstructionIncomplete, result.Kind);
            Assert.AreEqual(0, state.CurrentStageIndex);
            Assert.AreEqual(500, wallet.Balance);
        }

        [Test]
        public void HOUSE_UPGRADE_036_DirectCompletionRejectsUnregisteredStageWithoutSpending()
        {
            var blueprint = CreateBlueprintForTests();
            var registeredStage = HouseUpgradeStageDefinition.CreateForTests("house.stage.registered", 1, 300, 120, InteriorGenerationProfile.CreateDefaultOfficeForTests(), null, blueprint);
            var requestedStage = HouseUpgradeStageDefinition.CreateForTests("house.stage.external", 1, 300, 120, InteriorGenerationProfile.CreateDefaultOfficeForTests(), null, blueprint);
            SetDirectConditionsForTests(requestedStage, ScriptableObject.CreateInstance<AlwaysHouseConditionForTests>());
            var state = new HouseStateSaveData();
            var wallet = new HouseCurrencyWallet(500);
            var session = CreateCompletedSession(blueprint);
            var service = new HouseUpgradeService(new[] { registeredStage });

            var result = service.TryCompleteDirect(requestedStage, state, wallet, session);

            Assert.AreEqual(HouseUpgradeResultKind.InvalidStage, result.Kind);
            Assert.AreEqual(0, state.CurrentStageIndex);
            Assert.AreEqual(500, wallet.Balance);
        }

        [Test]
        public void HOUSE_UPGRADE_037_DirectCompletionPaysAndRaisesStageAtomically()
        {
            var blueprint = CreateBlueprintForTests();
            var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.1", 1, 300, 120, InteriorGenerationProfile.CreateDefaultOfficeForTests(), null, blueprint);
            var effect = ScriptableObject.CreateInstance<CountingHouseEffect>();
            SetDirectConditionsForTests(stage, ScriptableObject.CreateInstance<AlwaysHouseConditionForTests>());
            SetDirectEffectsForTests(stage, effect);
            var state = new HouseStateSaveData
            {
                ActiveConstructionStageId = "house.stage.1",
                PlacedConstructionCells = new[] { new HouseConstructionCellSaveData { X = 0, Y = 0, Kind = HouseConstructionCellKind.Floor } }
            };
            var wallet = new HouseCurrencyWallet(500);
            var session = CreateCompletedSession(blueprint);
            var service = new HouseUpgradeService(new[] { stage });

            var result = service.TryCompleteDirect(stage, state, wallet, session);

            Assert.AreEqual(HouseUpgradeResultKind.Applied, result.Kind);
            Assert.AreEqual(1, state.CurrentStageIndex);
            Assert.AreEqual(380, wallet.Balance);
            Assert.AreEqual(string.Empty, state.ActiveConstructionStageId);
            Assert.AreEqual(0, state.PlacedConstructionCells.Length);
            Assert.AreEqual(HouseUpgradeRouteKind.DirectConstruction, state.LatestRoute);
            Assert.AreEqual(1, effect.ApplyCount);
        }

        [Test]
        public void HOUSE_UPGRADE_038_HireRouteRejectsStageSkippingWithoutSpending()
        {
            var stageOne = HouseUpgradeStageDefinition.CreateForTests("house.stage.1", 1, 300, 120, InteriorGenerationProfile.CreateDefaultOfficeForTests(), null, null);
            var stageTwo = HouseUpgradeStageDefinition.CreateForTests("house.stage.2", 2, 600, 240, InteriorGenerationProfile.CreateDefaultOfficeForTests(), null, null);
            var state = new HouseStateSaveData();
            var wallet = new HouseCurrencyWallet(1000);
            var service = new HouseUpgradeService(new[] { stageOne, stageTwo });

            var result = service.TryHire(stageTwo, state, wallet);

            Assert.AreEqual(HouseUpgradeResultKind.InvalidStage, result.Kind);
            Assert.AreEqual(0, state.CurrentStageIndex);
            Assert.AreEqual(1000, wallet.Balance);
        }

        [Test]
        public void HOUSE_UPGRADE_039_HireRouteRejectsFailedGeneralConditionWithoutSpending()
        {
            var stage = HouseUpgradeStageDefinition.CreateForTests("house.stage.1", 1, 300, 120, InteriorGenerationProfile.CreateDefaultOfficeForTests(), null, null);
            SetGeneralConditionsForTests(stage, ScriptableObject.CreateInstance<NeverHouseConditionForTests>());
            var state = new HouseStateSaveData();
            var wallet = new HouseCurrencyWallet(500);
            var service = new HouseUpgradeService(new[] { stage });

            var result = service.TryHire(stage, state, wallet);

            Assert.AreEqual(HouseUpgradeResultKind.RequirementFailed, result.Kind);
            Assert.AreEqual(0, state.CurrentStageIndex);
            Assert.AreEqual(500, wallet.Balance);
        }

        private static HouseConstructionBlueprintDefinition CreateBlueprintForTests()
        {
            return HouseConstructionBlueprintDefinition.CreateForTests(
                "blueprint.stage.1",
                new RectInt(0, 0, 2, 2),
                new[] { HouseConstructionCellRequirement.Floor(0, 0) });
        }

        private static HouseConstructionSession CreateCompletedSession(HouseConstructionBlueprintDefinition blueprint)
        {
            var session = new HouseConstructionSession(blueprint);
            Assert.IsTrue(session.TryPlace(new Vector2Int(0, 0), HouseConstructionCellKind.Floor));
            return session;
        }

        private static void SetGeneralConditionsForTests(HouseUpgradeStageDefinition stage, params HouseUpgradeConditionBase[] conditions)
        {
            SetPrivateArray(stage, "_generalConditions", conditions);
        }

        private static void SetDirectConditionsForTests(HouseUpgradeStageDefinition stage, params HouseUpgradeConditionBase[] conditions)
        {
            SetPrivateArray(stage, "_directConditions", conditions);
        }

        private static void SetHireEffectsForTests(HouseUpgradeStageDefinition stage, params HouseUpgradeEffectBase[] effects)
        {
            SetPrivateArray(stage, "_hireEffects", effects);
        }

        private static void SetDirectEffectsForTests(HouseUpgradeStageDefinition stage, params HouseUpgradeEffectBase[] effects)
        {
            SetPrivateArray(stage, "_directEffects", effects);
        }

        private static void SetPrivateArray<T>(HouseUpgradeStageDefinition stage, string fieldName, T[] value)
        {
            var field = typeof(HouseUpgradeStageDefinition).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            field.SetValue(stage, value);
        }

        private sealed class AlwaysHouseConditionForTests : HouseUpgradeConditionBase
        {
            public override bool IsMet(in HouseUpgradeContext context) => true;
        }

        private sealed class NeverHouseConditionForTests : HouseUpgradeConditionBase
        {
            public override bool IsMet(in HouseUpgradeContext context) => false;
        }

        private sealed class CountingHouseEffect : HouseUpgradeEffectBase
        {
            public int ApplyCount { get; private set; }

            public override bool CanApply(in HouseUpgradeContext context) => true;

            public override void Apply(in HouseUpgradeContext context)
            {
                ApplyCount++;
            }
        }
    }
}