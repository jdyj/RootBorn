using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class OpeningStoryAddressableTests
    {
        [Test]
        public void AuthoredOpeningAssets_AreRegisteredInLocalDataOrTilesGroups_WithUniqueAddresses()
        {
            var chain = AssetDatabase.LoadAssetAtPath<QuestChainDefinition>("Assets/Data/StudentLife/MainStory/QuestChain_SchoolOpening.asset");
            Assert.That(chain, Is.Not.Null, "Create the authored opening before validating its build reachability.");
            var settings = AddressableAssetSettingsDefaultObject.Settings; Assert.That(settings, Is.Not.Null);
            var paths = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Data/StudentLife/MainStory", "Assets/Data/Interiors/Furniture/Story" })
                .Select(AssetDatabase.GUIDToAssetPath).Concat(new[] {
                    "Assets/Data/Items/Item_Allowance.asset",
                    "Assets/Data/Interiors/Furniture/Story/Tiles/Story_PhotoBoard_Tile.asset",
                    "Assets/Data/StudentLife/PartTimeWork/PartTimeWork_WorkshopHelper.asset",
                    "Assets/Data/StudentLife/PartTimeWork/Workplace_Workshop.asset",
                    "Assets/Data/StudentLife/PartTimeWork/PartTimeWork_RestaurantHelper.asset",
                    "Assets/Data/StudentLife/PartTimeWork/PartTimeWork_ClinicHelper.asset",
                    "Assets/Data/StudentLife/PartTimeWork/PartTimeWork_StudioHelper.asset",
                    "Assets/Data/StudentLife/PartTimeWork/PartTimeWork_TrainingHelper.asset",
                    "Assets/Data/StudentLife/PartTimeWork/Workplace_Restaurant.asset",
                    "Assets/Data/StudentLife/PartTimeWork/Workplace_Clinic.asset",
                    "Assets/Data/StudentLife/PartTimeWork/Workplace_Studio.asset",
                    "Assets/Data/StudentLife/PartTimeWork/Workplace_TrainingGround.asset"
                }).Concat(new[] { "Restaurant", "MedicalClinic", "DesignStudio", "TrainingGround" }.SelectMany(name => new[] {
                    "Assets/Data/StudentLife/Locations/Location_" + name + ".asset",
                    "Assets/Data/StudentLife/LocationIdentities/LocationIdentity_" + name + ".asset"
                })).Distinct().ToArray();
            var addresses = new System.Collections.Generic.HashSet<string>();
            foreach (string path in paths)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path); Assert.That(asset, Is.Not.Null, path);
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
                Assert.That(entry, Is.Not.Null, "Missing Addressables entry: " + path);
                Assert.That(entry.parentGroup.Name, Is.EqualTo(asset is TileBase ? "Tiles" : "Data"));
                StringAssert.StartsWith(asset is TileBase ? "tiles/" : "data/", entry.address);
                Assert.That(addresses.Add(entry.address), Is.True, "Duplicate opening address: " + entry.address);
            }
        }

        [Test]
        public void AuthoredOpeningReferences_AreRegisteredOnce_WithStableUniqueEntityIds()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.That(registry, Is.Not.Null);
            var chain = AssetDatabase.LoadAssetAtPath<QuestChainDefinition>("Assets/Data/StudentLife/MainStory/QuestChain_SchoolOpening.asset");
            Assert.That(chain, Is.Not.Null);
            Assert.That(registry.QuestChains.Count(x => x == chain), Is.EqualTo(1));
            Assert.That(chain.Steps.Select(x => x.Id).Distinct().Count(), Is.EqualTo(5));
            var openingLocations = registry.Locations.Where(x => x != null && new[] { "Location_Restaurant", "Location_MedicalClinic", "Location_DesignStudio", "Location_TrainingGround" }.Contains(x.name)).ToArray();
            Assert.That(openingLocations.Length, Is.EqualTo(4));
            Assert.That(openingLocations.Select(x => x.Id).Distinct().Count(), Is.EqualTo(4));
            Assert.That(registry.Items.Count(x => x != null && x.Id == "item.allowance"), Is.EqualTo(1));
            Assert.That(registry.PartTimeWorks.Count(x => x != null && x.Id == "work.workshop-helper"), Is.EqualTo(1));
            Assert.That(registry.Campaigns.Count(x => x != null && x.Id == "campaign.school-opening-three-days"), Is.EqualTo(1));
            Assert.That(registry.FurnitureDefinitions.Count(x => x != null && x.StableId == "story.photo-board"), Is.EqualTo(1));
            Assert.That(registry.FurnitureDefinitions.Count(x => x != null && x.StableId == "story.study-desk"), Is.EqualTo(1));
        }

        // A purchasable or quest-accepted piece without art would complete the quest while nothing appears in the room.
        [Test]
        public void ForSaleAndMyRoomFurniture_AllHaveVisibleArt()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            var objective = AssetDatabase.LoadAssetAtPath<Rootborn.Game.Quests.Objectives.FurniturePlacedQuestObjective>("Assets/Data/StudentLife/MainStory/Objectives/Objective_MyRoom.asset");
            var pieces = registry.FurnitureDefinitions.Where(x => x != null && x.IsForSale).Concat(objective.AcceptedFurniture).Distinct().ToArray();
            Assert.That(pieces.Length, Is.GreaterThanOrEqualTo(2));
            var missing = pieces.Where(x => x.TileParts.Count == 0 || x.TileParts.Any(part => !(part.Tile is Tile tile) || tile.sprite == null)).Select(x => x.StableId).ToArray();
            Assert.That(missing, Is.Empty, string.Join(", ", missing));
        }
    }
}
