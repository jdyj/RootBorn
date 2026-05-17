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
            var state = new HouseStateSaveData();
            var wallet = new HouseCurrencyWallet(500);
            var service = new HouseUpgradeService(new[] { stage });

            var result = service.TryHire(stage, state, wallet);

            Assert.AreEqual(HouseUpgradeResultKind.Applied, result.Kind);
            Assert.AreEqual(1, state.CurrentStageIndex);
            Assert.AreEqual(200, wallet.Balance);
            Assert.AreEqual(HouseUpgradeRouteKind.HireConstruction, state.LatestRoute);
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

        private static HouseConstructionBlueprintDefinition CreateBlueprintForTests()
        {
            return HouseConstructionBlueprintDefinition.CreateForTests(
                "blueprint.stage.1",
                new RectInt(0, 0, 2, 2),
                new[] { HouseConstructionCellRequirement.Floor(0, 0) });
        }

        private static void SetDirectConditionsForTests(HouseUpgradeStageDefinition stage, params HouseUpgradeConditionBase[] conditions)
        {
            var directConditions = typeof(HouseUpgradeStageDefinition).GetField("_directConditions", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(directConditions);
            directConditions.SetValue(stage, conditions);
        }
    }
}
