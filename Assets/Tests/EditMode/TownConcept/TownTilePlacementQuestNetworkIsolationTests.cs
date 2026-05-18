using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Effects;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Story;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Tiles;
using Rootborn.UI.Modern;
using Rootborn.UI.Quests;
using Rootborn.UI.Tiles;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.TownConcept
{
    public sealed class TownTilePlacementQuestNetworkIsolationTests
    {
        [Test]
        public void TILE_PLACE_QUEST_NET_001_PlayerOneTilePlacementRewardDoesNotMutatePlayerTwoProgress()
        {
            var trait = CreateTrait("trait.creativity");
            var career = CreateCareer("career.interior");
            var quest = CreateTileQuest(trait, career);
            var playerOneLog = new QuestLog(new[] { quest });
            var playerTwoLog = new QuestLog(new[] { quest });
            var playerOne = new StudentLifeProgress("slot-a", "player-1", 10, 10);
            var playerTwo = new StudentLifeProgress("slot-a", "player-2", 10, 10);
            var playerOneContext = new RewardRuntimeContext(playerOneLog, null, null, new StoryFlagSet(), playerOne);
            var playerTwoContext = new RewardRuntimeContext(playerTwoLog, null, null, new StoryFlagSet(), playerTwo);

            Assert.IsTrue(playerOneLog.Accept(quest));
            playerOneLog.RecordEvent(new QuestEvent(QuestEventKind.TilePlaced, "player-1-tile-1", tile: CreateTile("tile.p1")));
            Assert.IsTrue(playerOneLog.ClaimReward(quest, in playerOneContext));

            Assert.AreEqual(2, playerOne.GetTraitValue(trait));
            Assert.AreEqual(0, playerTwo.GetTraitValue(trait));
            Assert.AreEqual(QuestState.RewardClaimed, playerOneLog.GetState(quest));
            Assert.AreEqual(QuestState.NotStarted, playerTwoLog.GetState(quest));

            Assert.IsTrue(playerTwoLog.Accept(quest));
            playerTwoLog.RecordEvent(new QuestEvent(QuestEventKind.TilePlaced, "player-2-tile-1", tile: CreateTile("tile.p2")));
            Assert.IsTrue(playerTwoLog.ClaimReward(quest, in playerTwoContext));

            Assert.AreEqual(2, playerOne.GetTraitValue(trait));
            Assert.AreEqual(2, playerTwo.GetTraitValue(trait));
            Assert.AreEqual(QuestState.RewardClaimed, playerTwoLog.GetState(quest));
        }

        [Test]
        public void TILE_PLACE_QUEST_NET_001_TownRuntimePersistsQuestAndStudentLifeProgressPerPlayerFile()
        {
            string source = File.ReadAllText("Assets/Scripts/UI/Tiles/TownTilePlacementRuntimeInstaller.cs");

            StringAssert.Contains("ResolvePlayerId(player)", source);
            StringAssert.Contains("QuestLogFileNameFor(playerId)", source);
            StringAssert.Contains("StudentLifeFileNameFor(playerId)", source);
            StringAssert.Contains("quest-log-", source);
            StringAssert.Contains("student-life-progress-", source);
        }

        [Test]
        public void TILE_PLACE_UI_001_TilePlacementPanelUsesOneCommonPanelAndPlainButtons()
        {
            var panelGo = new GameObject("TilePlacementPanel", typeof(RectTransform), typeof(Image), typeof(TownTilePlacementRuntimeInstaller.TilePlacementPanel));
            var questGo = new GameObject("QuestLogPanel", typeof(RectTransform));
            var tilemapGo = new GameObject("TownDecorationTilemap", typeof(Tilemap));
            var trait = CreateTrait("trait.creativity");
            var career = CreateCareer("career.interior");
            var quest = CreateTileQuest(trait, career);
            try
            {
                var questLogPanel = questGo.AddComponent<QuestLogPanel>();
                var questLog = new QuestLog(new[] { quest });
                questLogPanel.Bind(questLog, new[] { quest }, default);

                var tile = CreateTile("tile.floor");
                var panel = panelGo.GetComponent<TownTilePlacementRuntimeInstaller.TilePlacementPanel>();
                panel.Bind(questLogPanel, quest, default, tilemapGo.GetComponent<Tilemap>(), new[] { tile }, "tile.json", "quest.json", "student.json");

                var rootTiles = panelGo.GetComponent<ModernUiTileImage>();
                Assert.IsNotNull(rootTiles, "Tile placement panel root must use ModernUiTileImage.");
                Assert.AreEqual(new Vector2(48f, 48f), rootTiles.TileSize, "Tile placement panel root should use the 48px CommonPanel tile scale.");
                Assert.Greater(rootTiles.TileCount, 0);

                var buttons = panelGo.GetComponentsInChildren<Button>(true);
                Assert.GreaterOrEqual(buttons.Length, 3, "Tile placement should expose accept, tile select, and claim buttons.");
                Assert.AreEqual(1, panelGo.GetComponentsInChildren<ModernUiTileImage>(true).Count(tileImage => tileImage.Recipe == ModernUiRecipes.CommonPanel48), "Tile placement should use exactly one CommonPanel48: the panel root.");
                Assert.IsTrue(buttons.All(button => button.GetComponent<ModernUiTileImage>() == null), "TilePlacementPanel buttons must be plain controls inside the outer CommonPanel, not nested CommonPanel backgrounds.");
                Assert.IsTrue(buttons.All(button => button.GetComponent<Image>().color.a == 0f), "Button root Image should be a transparent hit target, not a flat panel color.");
            }
            finally
            {
                Object.DestroyImmediate(panelGo);
                Object.DestroyImmediate(questGo);
                Object.DestroyImmediate(tilemapGo);
                Object.DestroyImmediate(quest);
                Object.DestroyImmediate(trait);
                Object.DestroyImmediate(career);
            }
        }

        private static QuestDefinition CreateTileQuest(TraitDefinition trait, CareerDefinition career)
        {
            var objective = ScriptableObject.CreateInstance<TilePlacedQuestObjective>();
            objective.ConfigureForRuntime("objective.tile", 1, null);

            var effect = ScriptableObject.CreateInstance<TraitDeltaCompletionEffect>();
            effect.ConfigureForTests(trait, 2, career);

            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            quest.ConfigureForRuntime(
                "quest.tile-placement.interior",
                "quest.tile-placement.interior.name",
                "quest.tile-placement.interior.desc",
                new QuestObjectiveBase[] { objective },
                new QuestRewardBase[0],
                new QuestCompletionEffectBase[] { effect });
            return quest;
        }

        private static PlaceableTileDefinition CreateTile(string id)
        {
            var tile = ScriptableObject.CreateInstance<PlaceableTileDefinition>();
            tile.ConfigureForRuntime(id, id, ScriptableObject.CreateInstance<Tile>(), "TownDecorationTilemap");
            return tile;
        }

        private static TraitDefinition CreateTrait(string id)
        {
            var trait = ScriptableObject.CreateInstance<TraitDefinition>();
            trait.ConfigureForTests(id, id);
            return trait;
        }

        private static CareerDefinition CreateCareer(string id)
        {
            var career = ScriptableObject.CreateInstance<CareerDefinition>();
            career.ConfigureForTests(id, id, null);
            return career;
        }
    }
}