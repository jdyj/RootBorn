using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.MainMenu;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.TownConcept
{
    public sealed class TownTilePlacementQuestScenarioTests
    {
        private const string TestSlotId = "slot-0";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-tile-placement-quest", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            SaveService.SetRootDirectoryForTests(null);
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TILE_PLACE_QUEST_001_002_003_004_005_PlayerAcceptsTileQuestSelectsPalettePlacesWorldTileAndClaimsInteriorGrowthThroughUi()
        {
            yield return LoadTownWithSlot(2026051101, 2026051102);
            var runtime = FindRuntime();
            var quest = FindTilePlacementQuest(runtime.QuestLogPanel);
            int beforeCreativity = runtime.StudentLife.Progress.GetTraitValueById("trait.creativity");
            int beforePlanning = runtime.StudentLife.Progress.GetTraitValueById("trait.planning");
            int beforeCells = CountOccupiedCells(runtime.DecorationTilemap);

            yield return OpenTilePlacementBoard(runtime);
            Assert.GreaterOrEqual(CountTilePaletteButtons(runtime.TilePanel), 1, "The temporary palette must expose at least the currently discoverable placeable tiles.");
            yield return ClickButton(FindButtonContaining(runtime.TilePanel, "Accept"), runtime.TilePanel);
            Assert.AreEqual(QuestState.Active, runtime.QuestLogPanel.QuestLog.GetState(quest));
            AssertPanelContains(runtime.QuestLogPanel.transform, "0 / 1");

            yield return ClickButton(FindButtonContaining(runtime.TilePanel, "Tile 1"), runtime.TilePanel);
            AssertPanelContains(runtime.TilePanel.transform, "Selected: Tile 1");
            Assert.AreEqual(beforeCells, CountOccupiedCells(runtime.DecorationTilemap), "Selecting a palette tile must not place a tile until the user clicks the world grid.");

            yield return ClickWorldCell(runtime, new Vector3(20.5f, 20.5f, 0f));

            Assert.Greater(CountOccupiedCells(runtime.DecorationTilemap), beforeCells, "A user world click must place a tile into a previously empty Tilemap cell.");
            Assert.AreEqual(QuestState.Completed, runtime.QuestLogPanel.QuestLog.GetState(quest));
            AssertPanelContains(runtime.QuestLogPanel.transform, "1 / 1");

            yield return ClickButton(FindButtonContaining(runtime.TilePanel, "Claim"), runtime.TilePanel);
            Assert.AreEqual(QuestState.RewardClaimed, runtime.QuestLogPanel.QuestLog.GetState(quest));
            Assert.Greater(runtime.StudentLife.Progress.GetTraitValueById("trait.creativity") + runtime.StudentLife.Progress.GetTraitValueById("trait.planning"), beforeCreativity + beforePlanning);
            AssertPanelContains(runtime.TilePanel.transform, "RewardClaimed");
        }

        [UnityTest]
        public IEnumerator TILE_PLACE_QUEST_006_007_PlacedTileAndRewardClaimPersistAfterTownReloadWithoutDuplicateReward()
        {
            yield return LoadTownWithSlot(2026051103, 2026051104);
            var runtime = FindRuntime();
            var quest = FindTilePlacementQuest(runtime.QuestLogPanel);

            yield return OpenTilePlacementBoard(runtime);
            yield return ClickButton(FindButtonContaining(runtime.TilePanel, "Accept"), runtime.TilePanel);
            Assert.AreEqual(QuestState.Active, runtime.QuestLogPanel.QuestLog.GetState(quest));
            yield return ClickButton(FindButtonContaining(runtime.TilePanel, "Tile 1"), runtime.TilePanel);
            yield return ClickWorldCell(runtime, new Vector3(21.5f, 21.5f, 0f));
            yield return ClickButton(FindButtonContaining(runtime.TilePanel, "Claim"), runtime.TilePanel);

            int claimedCreativity = runtime.StudentLife.Progress.GetTraitValueById("trait.creativity");
            int claimedPlanning = runtime.StudentLife.Progress.GetTraitValueById("trait.planning");
            int claimedCellCount = CountOccupiedCells(runtime.DecorationTilemap);
            Assert.AreEqual(QuestState.RewardClaimed, runtime.QuestLogPanel.QuestLog.GetState(quest));
            Assert.IsTrue(File.Exists(Path.Combine(_saveRoot, TestSlotId, "tile-placements.json")));

            yield return LoadScene("Town");
            yield return WaitForTownTileRuntime(10f);
            runtime = FindRuntime();
            quest = FindTilePlacementQuest(runtime.QuestLogPanel);

            Assert.GreaterOrEqual(CountOccupiedCells(runtime.DecorationTilemap), claimedCellCount);
            Assert.AreEqual(QuestState.RewardClaimed, runtime.QuestLogPanel.QuestLog.GetState(quest));
            Assert.AreEqual(claimedCreativity, runtime.StudentLife.Progress.GetTraitValueById("trait.creativity"));
            Assert.AreEqual(claimedPlanning, runtime.StudentLife.Progress.GetTraitValueById("trait.planning"));

            yield return OpenTilePlacementBoard(runtime);
            var claimAgain = FindButtonContaining(runtime.TilePanel, "Claim");
            if (claimAgain != null && claimAgain.interactable)
            {
                yield return ClickButton(claimAgain, runtime.TilePanel);
            }

            Assert.AreEqual(claimedCreativity, runtime.StudentLife.Progress.GetTraitValueById("trait.creativity"));
            Assert.AreEqual(claimedPlanning, runtime.StudentLife.Progress.GetTraitValueById("trait.planning"));
        }

        private IEnumerator LoadTownWithSlot(int worldSeed, int tileSeed)
        {
            var service = new SaveService(TestSlotId, _saveRoot);
            var metadata = service.CreateUiMetadata(TestSlotId, new CharacterCustomization(), worldSeed, tileSeed);
            service.SaveMetadata(metadata);
            ActiveSaveContext.Set(metadata);
            yield return LoadScene("Town");
            yield return WaitForTownTileRuntime(10f);
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " should be present in BuildSettings.");
            while (!op.isDone)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitForTownTileRuntime(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var player = GameObject.Find("Player");
                var board = GameObject.Find("TilePlacementBoard");
                var panel = GameObject.Find("TilePlacementPanel");
                var decoration = GameObject.Find("TownDecorationTilemap");
                var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
                if (player != null && player.GetComponent<GatherInteractor>() != null && player.GetComponent<PlayerInteractionRouter>() != null && player.GetComponent<StudentLifeProgressComponent>() != null && board != null && panel != null && decoration != null && decoration.GetComponent<Tilemap>() != null && questLogPanel != null && questLogPanel.QuestLog != null && EventSystem.current != null)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.Fail("Town tile placement quest runtime did not install within timeout.");
        }

        private static RuntimeRefs FindRuntime()
        {
            var player = GameObject.Find("Player");
            var board = GameObject.Find("TilePlacementBoard");
            var panel = GameObject.Find("TilePlacementPanel");
            var decoration = GameObject.Find("TownDecorationTilemap");
            return new RuntimeRefs(player, player.GetComponent<GatherInteractor>(), player.GetComponent<PlayerInteractionRouter>(), player.GetComponent<StudentLifeProgressComponent>(), board, panel, decoration.GetComponent<Tilemap>(), Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include));
        }

        private static IEnumerator OpenTilePlacementBoard(RuntimeRefs runtime)
        {
            runtime.Player.transform.position = runtime.Board.transform.position + new Vector3(0.45f, 0f, 0f);
            yield return null;
            runtime.Router.RefreshPromptNow();
            Assert.IsTrue(runtime.Router.PromptVisible);
            runtime.Gather.TriggerInteract();
            yield return null;
            Assert.IsTrue(runtime.TilePanel.activeInHierarchy);
        }

        private static IEnumerator ClickWorldCell(RuntimeRefs runtime, Vector3 worldPosition)
        {
            var camera = Camera.main;
            Assert.IsNotNull(camera);
            var eventData = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = camera.WorldToScreenPoint(worldPosition),
            };
            ExecuteEvents.Execute(runtime.TilePanel, eventData, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static int CountOccupiedCells(Tilemap tilemap)
        {
            int count = 0;
            var bounds = tilemap.cellBounds;
            foreach (var position in bounds.allPositionsWithin)
            {
                if (tilemap.HasTile(position))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountTilePaletteButtons(GameObject panel)
        {
            int count = 0;
            var buttons = panel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name.StartsWith("TileButton_", System.StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static QuestDefinition FindTilePlacementQuest(QuestLogPanel panel)
        {
            for (int i = 0; i < panel.Quests.Length; i++)
            {
                if (panel.Quests[i] != null && panel.Quests[i].DisplayNameKey.Contains("tile"))
                {
                    return panel.Quests[i];
                }
            }

            Assert.Fail("QuestLogPanel must be bound with a tile placement quest.");
            return null;
        }

        private static Button FindButtonContaining(GameObject root, string expectedText)
        {
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var texts = buttons[i].GetComponentsInChildren<Text>(true);
                for (int j = 0; j < texts.Length; j++)
                {
                    if (!string.IsNullOrEmpty(texts[j].text) && texts[j].text.Contains(expectedText))
                    {
                        return buttons[i];
                    }
                }
            }

            return null;
        }

        private static IEnumerator ClickButton(Button button, GameObject debugRoot)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " must be active for user click. Texts: " + CollectTexts(debugRoot));
            Assert.IsTrue(button.interactable, button.name + " must be interactable for user click. Texts: " + CollectTexts(debugRoot));
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static void AssertPanelContains(Transform panel, string expected)
        {
            var texts = panel.GetComponentsInChildren<Text>(true);
            var all = new List<string>();
            for (int i = 0; i < texts.Length; i++)
            {
                all.Add(texts[i].text);
                if (!string.IsNullOrEmpty(texts[i].text) && texts[i].text.Contains(expected))
                {
                    return;
                }
            }

            Assert.Fail("Expected visible text to contain '" + expected + "' but saw: " + string.Join(" | ", all));
        }

        private static string CollectTexts(GameObject root)
        {
            if (root == null)
            {
                return "<null>";
            }

            var texts = root.GetComponentsInChildren<Text>(true);
            var all = new List<string>();
            for (int i = 0; i < texts.Length; i++)
            {
                all.Add(texts[i].name + "=" + texts[i].text);
            }

            return string.Join(" | ", all);
        }

        private readonly struct RuntimeRefs
        {
            public RuntimeRefs(GameObject player, GatherInteractor gather, PlayerInteractionRouter router, StudentLifeProgressComponent studentLife, GameObject board, GameObject tilePanel, Tilemap decorationTilemap, QuestLogPanel questLogPanel)
            {
                Player = player;
                Gather = gather;
                Router = router;
                StudentLife = studentLife;
                Board = board;
                TilePanel = tilePanel;
                DecorationTilemap = decorationTilemap;
                QuestLogPanel = questLogPanel;
            }

            public GameObject Player { get; }
            public GatherInteractor Gather { get; }
            public PlayerInteractionRouter Router { get; }
            public StudentLifeProgressComponent StudentLife { get; }
            public GameObject Board { get; }
            public GameObject TilePanel { get; }
            public Tilemap DecorationTilemap { get; }
            public QuestLogPanel QuestLogPanel { get; }
        }
    }
}
