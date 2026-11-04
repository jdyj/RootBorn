using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Interiors;
using Rootborn.UI.Interiors;
using UnityEditor;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class InteriorFurnitureRuntimeCatalogTests
    {
        [Test]
        public void LoadChairFurniture_ReturnsRegistryBackedChairObjectDefinitions()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");

            var furniture = InteriorFurnitureCatalog.LoadChairFurniture(registry);

            Assert.GreaterOrEqual(furniture.Count, 1);
            Assert.AreEqual(furniture.Count, furniture.Select(f => f.Id).Distinct().Count());
            var chair = furniture.Single(f => f.Id == "chair.black");
            Assert.AreEqual("chair.black", chair.Id);
            Assert.AreEqual("Black Chair", chair.DisplayName);
            Assert.AreEqual(InteriorObjectKind.Chair, chair.ObjectKind);
            Assert.AreEqual(1, chair.Footprint.Count);
            Assert.AreEqual(1, chair.Tiles.Count);
        }
    }
}
