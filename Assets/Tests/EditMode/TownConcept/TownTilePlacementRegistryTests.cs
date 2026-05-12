using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests.Effects;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Tiles;
using UnityEditor;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class TownTilePlacementRegistryTests
    {
        [Test]
        public void TILE_PLACE_QUEST_002_005_GameDataRegistryRegistersTilePlacementQuestDataAssets()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.IsNotNull(registry);

            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<PlaceableTileDefinition>("Assets/Data/Tiles/Tile_Placeable_Ground.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TilePlacedQuestObjective>("Assets/Data/Quests/CareerRoutes/Objectives/Objective_TilePlaced_Interior.asset"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TraitDeltaCompletionEffect>("Assets/Data/Quests/CareerRoutes/Effects/Effect_TilePlacementInterior.asset"));

            Assert.That(registry.PlaceableTiles, Has.Some.Matches<PlaceableTileDefinition>(tile => tile != null && tile.Id == "tile.placeable.ground" && tile.Tile != null));
            Assert.That(registry.QuestObjectives, Has.Some.InstanceOf<TilePlacedQuestObjective>());
            Assert.That(registry.QuestCompletionEffects, Has.Some.InstanceOf<TraitDeltaCompletionEffect>());
            Assert.That(registry.Quests, Has.Some.Matches<Rootborn.Game.Quests.QuestDefinition>(quest => quest != null && quest.Id == "quest.tile-placement.interior"));
        }
    }
}
