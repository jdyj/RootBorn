using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    public sealed class InventoryEquipmentRegressionCatalogTests
    {
        [Test]
        public void REG_001_InventoryUiTests_RemainInEditModeAssembly()
        {
            AssertTypeExists("Rootborn.Tests.EditMode.InventoryUiTests");
        }

        [Test]
        public void REG_002_GatherInteractorInventoryRegressionTests_RemainInEditModeAssembly()
        {
            var type = AssertTypeExists("Rootborn.Tests.EditMode.GatherInteractorTests");
            AssertHasAnyTestMethod(type, "Inventory", "Control", "InteractAction");
        }

        [Test]
        public void REG_003_WorldMovementAxisRegressionCoverage_RemainsPresent()
        {
            AssertTypeExists("Rootborn.Tests.EditMode.PlayerControllerTests");
            AssertTypeExists("Rootborn.Tests.EditMode.WorldGeneration.SeededWorldGeneratorTests");
            AssertTypeExists("Rootborn.Tests.PlayMode.FarmFlowTests, Rootborn.Tests.PlayMode");
        }

        [Test]
        public void REG_004_FarmingAndToolRegressionCoverage_RemainsPresent()
        {
            AssertTypeExists("Rootborn.Tests.EditMode.Farming.ToolEffectTests");
            AssertTypeExists("Rootborn.Tests.EditMode.Farming.FarmGridTests");
            AssertTypeExists("Rootborn.Tests.PlayMode.Farming.FarmingScenarioTests, Rootborn.Tests.PlayMode");
        }

        private static Type AssertTypeExists(string typeName)
        {
            var type = Type.GetType(typeName) ?? AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName.Split(',')[0].Trim()))
                .FirstOrDefault(found => found != null);
            Assert.IsNotNull(type, $"Regression test type missing: {typeName}");
            return type;
        }

        private static void AssertHasAnyTestMethod(Type type, params string[] nameFragments)
        {
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool found = methods.Any(method => nameFragments.Any(fragment => method.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(found, $"{type.FullName} does not expose an expected regression test method.");
        }
    }
}
