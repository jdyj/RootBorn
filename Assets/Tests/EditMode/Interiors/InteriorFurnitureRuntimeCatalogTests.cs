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

            Assert.AreEqual(1, furniture.Count);
            Assert.AreEqual("chair.black", furniture[0].Id);
            Assert.AreEqual("Black Chair", furniture[0].DisplayName);
            Assert.AreEqual(InteriorObjectKind.Chair, furniture[0].ObjectKind);
            Assert.AreEqual(1, furniture[0].Footprint.Count);
            Assert.AreEqual(1, furniture[0].Tiles.Count);
        }
    }
}
