using System.IO;
using NUnit.Framework;
using Rootborn.Game.Housing;
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
    }
}