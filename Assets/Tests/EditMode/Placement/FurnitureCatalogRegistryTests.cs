using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Placement;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Placement
{
    public sealed class FurnitureCatalogRegistryTests
    {
        [Test]
        public void GameDataRegistry_ExposesFurnitureDefinitions()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");

            Assert.IsNotNull(registry, "The project registry asset must exist at the canonical data path.");
            Assert.IsNotNull(registry.FurnitureDefinitions);
        }

        [Test]
        public void FurnitureAssets_AreRegisteredInGameDataRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            var assetGuids = AssetDatabase.FindAssets("t:FurnitureDefinition", new[] { "Assets/Data/Interiors/Furniture" });
            var assets = assetGuids
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(path))
                .Where(asset => asset != null)
                .ToArray();

            Assert.GreaterOrEqual(assets.Length, 2, "Furniture catalog needs at least one single-tile object and one multi-tile object asset.");
            CollectionAssert.IsSubsetOf(assets, registry.FurnitureDefinitions);
        }

        [Test]
        public void PlayerFacingDesk_IsSingleTileAndDoesNotIncludeComputerTile()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.IsNotNull(registry);

            var desks = registry.FurnitureDefinitions
                .Where(definition => definition != null && definition.Category == "desk")
                .ToArray();

            var regularDesk = desks.SingleOrDefault(definition => definition.StableId == "desk.basic");
            Assert.IsNotNull(regularDesk, "The player-facing regular desk must be a distinct 1x1 furniture definition named desk.basic.");
            Assert.AreEqual(1, regularDesk.FootprintCells.Count, "Regular desk must occupy exactly one cell.");
            Assert.AreEqual(Vector2Int.zero, regularDesk.FootprintCells[0]);
            Assert.AreEqual(1, regularDesk.TileParts.Count, "Regular desk must render exactly one tile part.");

            var tileName = regularDesk.TileParts[0].Tile != null ? regularDesk.TileParts[0].Tile.name : string.Empty;
            StringAssert.DoesNotContain("Computer", tileName, "Regular desk must not include a computer tile part.");
        }

        [Test]
        public void DeskComputerCombination_IsNotNamedAsRegularDesk()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.IsNotNull(registry);
            var deskComputer = registry.FurnitureDefinitions.SingleOrDefault(definition => definition != null && definition.StableId == "desk.large");

            Assert.IsNotNull(deskComputer, "Existing desk.large may remain as a separate multi-tile object.");
            Assert.Greater(deskComputer.FootprintCells.Count, 1);
            StringAssert.Contains("Computer", string.Join(",", deskComputer.TileParts.Select(part => part.Tile != null ? part.Tile.name : string.Empty)));
            StringAssert.DoesNotContain("Basic", deskComputer.DisplayName, "Multi-tile desk+computer must not be labeled as the regular 1x1 desk.");
        }

        [Test]
        public void RuntimeFurnitureCatalog_DoesNotUseResourcesLoadAll()
        {
            var source = File.ReadAllText("Assets/Scripts/UI/Interiors/InteriorFurnitureCatalog.cs");

            StringAssert.DoesNotContain("Resources.LoadAll", source);
        }
    }
}