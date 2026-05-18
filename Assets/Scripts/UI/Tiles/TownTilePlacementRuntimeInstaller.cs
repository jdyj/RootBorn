using System;
using System.Collections;
using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Effects;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Save;
using Rootborn.Game.Story;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Tiles;
using Rootborn.UI.Modern;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.UI.Tiles
{
    public static class TownTilePlacementRuntimeInstaller
    {
        private const string TownSceneName = "Town";
        private const string RunnerName = "[TownTilePlacementRuntimeInstaller]";
        private const string BoardName = "TilePlacementBoard";
        private const string PanelName = "TilePlacementPanel";
        private const string DecorationTilemapName = "TownDecorationTilemap";
        private const string QuestLogFileName = "quest-log.json";
        private const string TilePlacementFileName = "tile-placements.json";
        private const string StudentLifeFileName = "student-life-progress.json";
        private const string TileQuestId = "quest.tile-placement.interior";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureForActiveScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TownSceneName)
            {
                Ensure(scene);
            }
        }

        private static void EnsureForActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == TownSceneName)
            {
                Ensure(scene);
            }
        }

        private static void Ensure(Scene scene)
        {
            if (FindByName(scene, RunnerName) != null)
            {
                return;
            }

            var go = new GameObject(RunnerName);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private IEnumerator Start()
            {
                var scene = SceneManager.GetActiveScene();
                GameObject player = null;
                QuestLogPanel questLogPanel = null;
                Tilemap decorationTilemap = null;
                Canvas canvas = null;
                float elapsed = 0f;

                while (elapsed < 8f)
                {
                    player = FindByName(scene, "Player");
                    questLogPanel = FindComponentInScene<QuestLogPanel>(scene);
                    var tilemapGo = FindByName(scene, DecorationTilemapName);
                    decorationTilemap = tilemapGo != null ? tilemapGo.GetComponent<Tilemap>() : null;
                    canvas = FindComponentInScene<Canvas>(scene);

                    if (player != null &&
                        player.GetComponent<GatherInteractor>() != null &&
                        player.GetComponent<PlayerInteractionRouter>() != null &&
                        player.GetComponent<StudentLifeProgressComponent>() != null &&
                        questLogPanel != null && questLogPanel.QuestLog != null &&
                        decorationTilemap != null &&
                        canvas != null &&
                        EventSystem.current != null)
                    {
                        break;
                    }

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (player == null || questLogPanel == null || questLogPanel.QuestLog == null || decorationTilemap == null || canvas == null)
                {
                    yield break;
                }

                string playerId = ResolvePlayerId(player);
                string questLogFileName = QuestLogFileNameFor(playerId);
                string studentLifeFileName = StudentLifeFileNameFor(playerId);

                var registry = Managers.Data != null ? Managers.Data.Registry : null;
                var tiles = CreatePlaceableTiles(scene, registry);
                if (tiles.Length == 0)
                {
                    yield break;
                }

                var quest = CreateTilePlacementQuest(registry);
                questLogPanel.QuestLog.AddQuest(quest);
                LoadQuestLog(questLogPanel.QuestLog, questLogFileName);

                var studentLife = player.GetComponent<StudentLifeProgressComponent>();
                LoadStudentLife(studentLife, registry, quest, studentLifeFileName);

                var context = new RewardRuntimeContext(
                    questLogPanel.QuestLog,
                    player.GetComponent<PlayerInventory>() != null ? player.GetComponent<PlayerInventory>().Inventory : null,
                    null,
                    new StoryFlagSet(),
                    studentLife.EnsureProgress());
                questLogPanel.Bind(questLogPanel.QuestLog, AppendQuest(questLogPanel.Quests, quest), context);

                var panel = EnsurePanel(canvas.transform);
                panel.Bind(questLogPanel, quest, context, decorationTilemap, tiles, TilePlacementFileName, questLogFileName, studentLifeFileName);
                panel.LoadSavedPlacements();

                var board = EnsureBoard(scene);
                board.Bind(panel);
            }

            private static TilePlacementPanel EnsurePanel(Transform canvas)
            {
                var existing = FindByName(canvas, PanelName);
                GameObject go = existing != null ? existing.gameObject : new GameObject(PanelName, typeof(RectTransform), typeof(Image), typeof(ModernUiTileImage), typeof(TilePlacementPanel));
                go.transform.SetParent(canvas, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(24f, -220f);
                rect.sizeDelta = new Vector2(360f, 260f);
                var image = go.GetComponent<Image>();
                image.color = Color.clear;
                image.raycastTarget = true;
                return go.GetComponent<TilePlacementPanel>();
            }

            private static TilePlacementBoard EnsureBoard(Scene scene)
            {
                var existing = FindByName(scene, BoardName);
                GameObject go = existing != null ? existing : new GameObject(BoardName);
                if (existing == null)
                {
                    SceneManager.MoveGameObjectToScene(go, scene);
                }
                go.transform.position = new Vector3(2.8f, 0f, 0f);

                var renderer = go.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    renderer = go.AddComponent<SpriteRenderer>();
                }
                if (renderer.sprite == null)
                {
                    renderer.sprite = CreateMarkerSprite(new Color(0.8f, 0.55f, 0.95f, 1f));
                }
                renderer.sortingOrder = 48;

                var collider = go.GetComponent<BoxCollider2D>();
                if (collider == null)
                {
                    collider = go.AddComponent<BoxCollider2D>();
                }
                collider.isTrigger = true;
                collider.size = new Vector2(0.9f, 0.9f);

                var board = go.GetComponent<TilePlacementBoard>();
                if (board == null)
                {
                    board = go.AddComponent<TilePlacementBoard>();
                }
                return board;
            }

            private static QuestDefinition CreateTilePlacementQuest(GameDataRegistry registry)
            {
                var objective = ScriptableObject.CreateInstance<TilePlacedQuestObjective>();
                objective.ConfigureForRuntime("quest.tile-placement.objective", 1, null);

                var effect = ScriptableObject.CreateInstance<TraitDeltaCompletionEffect>();
                effect.ConfigureForTests(FindTrait(registry, "trait.creativity"), 2, FindCareer(registry, "career.interior"));

                var quest = ScriptableObject.CreateInstance<QuestDefinition>();
                quest.ConfigureForRuntime(
                    TileQuestId,
                    "quest.tile-placement.interior.name",
                    "quest.tile-placement.interior.desc",
                    new QuestObjectiveBase[] { objective },
                    Array.Empty<QuestRewardBase>(),
                    new QuestCompletionEffectBase[] { effect });
                return quest;
            }

            private static TraitDefinition FindTrait(GameDataRegistry registry, string id)
            {
                if (registry != null && registry.StudentLifeTraits != null)
                {
                    for (int i = 0; i < registry.StudentLifeTraits.Length; i++)
                    {
                        var trait = registry.StudentLifeTraits[i];
                        if (trait != null && trait.Id == id)
                        {
                            return trait;
                        }
                    }
                }

                var created = ScriptableObject.CreateInstance<TraitDefinition>();
                created.ConfigureForTests(id, id);
                return created;
            }

            private static CareerDefinition FindCareer(GameDataRegistry registry, string id)
            {
                if (registry != null && registry.Careers != null)
                {
                    for (int i = 0; i < registry.Careers.Length; i++)
                    {
                        var career = registry.Careers[i];
                        if (career != null && career.Id == id)
                        {
                            return career;
                        }
                    }
                }

                var created = ScriptableObject.CreateInstance<CareerDefinition>();
                created.ConfigureForTests(id, id, null);
                return created;
            }

            private static PlaceableTileDefinition[] CreatePlaceableTiles(Scene scene, GameDataRegistry registry)
            {
                var tileBases = new List<TileBase>();
                var tilemaps = new List<Tilemap>();
                CollectComponents(scene, tilemaps);
                for (int i = 0; i < tilemaps.Count; i++)
                {
                    var tiles = tilemaps[i].GetTilesBlock(tilemaps[i].cellBounds);
                    for (int j = 0; j < tiles.Length; j++)
                    {
                        if (tiles[j] != null && !tileBases.Contains(tiles[j]))
                        {
                            tileBases.Add(tiles[j]);
                        }
                    }
                }

                if (tileBases.Count == 0 && registry != null && registry.GroundSprite != null)
                {
                    var tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = registry.GroundSprite;
                    tileBases.Add(tile);
                }

                var definitions = new PlaceableTileDefinition[tileBases.Count];
                for (int i = 0; i < tileBases.Count; i++)
                {
                    var definition = ScriptableObject.CreateInstance<PlaceableTileDefinition>();
                    definition.ConfigureForRuntime("tile.runtime." + i, "Tile " + (i + 1), tileBases[i], DecorationTilemapName);
                    definitions[i] = definition;
                }

                return definitions;
            }

            private static QuestDefinition[] AppendQuest(QuestDefinition[] quests, QuestDefinition quest)
            {
                if (quest == null)
                {
                    return quests ?? Array.Empty<QuestDefinition>();
                }

                quests ??= Array.Empty<QuestDefinition>();
                for (int i = 0; i < quests.Length; i++)
                {
                    if (quests[i] == quest || (quests[i] != null && quests[i].Id == quest.Id))
                    {
                        return quests;
                    }
                }

                var result = new QuestDefinition[quests.Length + 1];
                Array.Copy(quests, result, quests.Length);
                result[result.Length - 1] = quest;
                return result;
            }

            private static void LoadQuestLog(QuestLog questLog, string questLogFileName)
            {
                var metadata = ActiveSaveContext.Metadata;
                if (metadata == null || string.IsNullOrEmpty(metadata.SlotId))
                {
                    return;
                }

                string json = new SaveService(metadata.SlotId).ReadJson(questLogFileName);
                if (!string.IsNullOrEmpty(json))
                {
                    questLog.LoadFromSaveData(JsonUtility.FromJson<QuestLogSaveData>(json));
                }
            }

            private static void LoadStudentLife(StudentLifeProgressComponent component, GameDataRegistry registry, QuestDefinition quest, string studentLifeFileName)
            {
                var metadata = ActiveSaveContext.Metadata;
                if (component == null || metadata == null || string.IsNullOrEmpty(metadata.SlotId))
                {
                    return;
                }

                string json = new SaveService(metadata.SlotId).ReadJson(studentLifeFileName);
                if (string.IsNullOrEmpty(json))
                {
                    return;
                }

                component.RestoreFromSaveData(
                    JsonUtility.FromJson<StudentLifeProgressSaveData>(json),
                    CollectTraits(registry, quest),
                    registry != null ? registry.StudentLifeSkills : Array.Empty<SkillDefinition>(),
                    CollectCareers(registry, quest));
            }

            private static TraitDefinition[] CollectTraits(GameDataRegistry registry, QuestDefinition quest)
            {
                var list = new List<TraitDefinition>();
                if (registry != null && registry.StudentLifeTraits != null)
                {
                    list.AddRange(registry.StudentLifeTraits);
                }

                if (quest != null && quest.CompletionEffects != null)
                {
                    for (int i = 0; i < quest.CompletionEffects.Length; i++)
                    {
                        if (quest.CompletionEffects[i] is TraitDeltaCompletionEffect effect && effect.Trait != null && !list.Contains(effect.Trait))
                        {
                            list.Add(effect.Trait);
                        }
                    }
                }

                return list.ToArray();
            }

            private static CareerDefinition[] CollectCareers(GameDataRegistry registry, QuestDefinition quest)
            {
                var list = new List<CareerDefinition>();
                if (registry != null && registry.Careers != null)
                {
                    list.AddRange(registry.Careers);
                }

                if (quest != null && quest.CompletionEffects != null)
                {
                    for (int i = 0; i < quest.CompletionEffects.Length; i++)
                    {
                        if (quest.CompletionEffects[i] is TraitDeltaCompletionEffect effect && effect.CareerHint != null && !list.Contains(effect.CareerHint))
                        {
                            list.Add(effect.CareerHint);
                        }
                    }
                }

                return list.ToArray();
            }
        }

        private static Sprite CreateMarkerSprite(Color color)
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            texture.name = "TilePlacementMarker";
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 16f);
            sprite.name = texture.name;
            return sprite;
        }

        private static string ResolvePlayerId(GameObject player)
        {
            var identity = player != null ? player.GetComponent<PlayerIdentity>() : null;
            return identity != null ? identity.PlayerId : PlayerIdentity.DefaultPlayerId;
        }

        private static string QuestLogFileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId)
            {
                return QuestLogFileName;
            }

            return "quest-log-" + SanitizeFileName(playerId) + ".json";
        }

        private static string StudentLifeFileNameFor(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId)
            {
                return StudentLifeFileName;
            }

            return "student-life-progress-" + SanitizeFileName(playerId) + ".json";
        }

        private static string SanitizeFileName(string value)
        {
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static GameObject FindByName(Scene scene, string objectName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindByName(roots[i].transform, objectName);
                if (match != null)
                {
                    return match.gameObject;
                }
            }

            return null;
        }

        private static Transform FindByName(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindByName(root.GetChild(i), objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = roots[i].GetComponentInChildren<T>(true);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void CollectComponents<T>(Scene scene, List<T> results) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                results.AddRange(roots[i].GetComponentsInChildren<T>(true));
            }
        }

        [Serializable]
        private sealed class TilePlacementSaveData
        {
            public TilePlacementEntry[] Entries = Array.Empty<TilePlacementEntry>();
        }

        [Serializable]
        private struct TilePlacementEntry
        {
            public string TileId;
            public int X;
            public int Y;
            public int Z;
        }

        public sealed class TilePlacementBoard : MonoBehaviour, IPlayerInteractable
        {
            private TilePlacementPanel _panel;

            public string InteractionPrompt => "[E] Tile Placement";
            public Vector3 InteractionPromptOffset => new Vector3(0f, 1.15f, 0f);
            public Transform InteractionTransform => transform;

            public void Bind(TilePlacementPanel panel)
            {
                _panel = panel;
            }

            public bool CanInteract(GameObject player)
            {
                return _panel != null && player != null;
            }

            public bool TryInteract(GameObject player)
            {
                if (!CanInteract(player))
                {
                    return false;
                }

                _panel.Open();
                return true;
            }
        }

        public sealed class TilePlacementPanel : MonoBehaviour, IPointerClickHandler
        {
            private static readonly Vector2 CommonPanelTileSize = new Vector2(48f, 48f);
            private readonly List<TilePlacementEntry> _placedEntries = new List<TilePlacementEntry>();
            private QuestLogPanel _questLogPanel;
            private QuestDefinition _quest;
            private RewardRuntimeContext _rewardContext;
            private Tilemap _tilemap;
            private PlaceableTileDefinition[] _tiles = Array.Empty<PlaceableTileDefinition>();
            private PlaceableTileDefinition _selectedTile;
            private string _tilePlacementFileName;
            private string _questLogFileName;
            private string _studentLifeFileName;
            private Text _statusText;
            private Button _acceptButton;
            private Button _claimButton;

            public void Bind(
                QuestLogPanel questLogPanel,
                QuestDefinition quest,
                RewardRuntimeContext rewardContext,
                Tilemap tilemap,
                PlaceableTileDefinition[] tiles,
                string tilePlacementFileName,
                string questLogFileName,
                string studentLifeFileName)
            {
                _questLogPanel = questLogPanel;
                _quest = quest;
                _rewardContext = rewardContext;
                _tilemap = tilemap;
                _tiles = tiles ?? Array.Empty<PlaceableTileDefinition>();
                _tilePlacementFileName = tilePlacementFileName;
                _questLogFileName = questLogFileName;
                _studentLifeFileName = studentLifeFileName;
                BuildUi();
                RefreshStateText();
            }

            public void Open()
            {
                gameObject.SetActive(true);
                RefreshStateText();
            }

            public void LoadSavedPlacements()
            {
                _placedEntries.Clear();
                var metadata = ActiveSaveContext.Metadata;
                if (metadata == null || string.IsNullOrEmpty(metadata.SlotId) || _tilemap == null)
                {
                    return;
                }

                string json = new SaveService(metadata.SlotId).ReadJson(_tilePlacementFileName);
                if (string.IsNullOrEmpty(json))
                {
                    return;
                }

                var saveData = JsonUtility.FromJson<TilePlacementSaveData>(json);
                if (saveData == null || saveData.Entries == null)
                {
                    return;
                }

                for (int i = 0; i < saveData.Entries.Length; i++)
                {
                    var entry = saveData.Entries[i];
                    var tile = FindTile(entry.TileId);
                    if (tile == null || tile.Tile == null)
                    {
                        continue;
                    }

                    _tilemap.SetTile(new Vector3Int(entry.X, entry.Y, entry.Z), tile.Tile);
                    _placedEntries.Add(entry);
                }
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (_selectedTile == null || _selectedTile.Tile == null || _tilemap == null || _questLogPanel == null || _questLogPanel.QuestLog == null || _quest == null)
                {
                    return;
                }

                if (_questLogPanel.QuestLog.GetState(_quest) != QuestState.Active)
                {
                    return;
                }

                if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
                {
                    return;
                }

                var camera = Camera.main;
                if (camera == null)
                {
                    return;
                }

                var screen = new Vector3(eventData.position.x, eventData.position.y, Mathf.Abs(camera.transform.position.z));
                var world = camera.ScreenToWorldPoint(screen);
                var cell = _tilemap.WorldToCell(world);
                if (_tilemap.HasTile(cell))
                {
                    cell = FindNearestEmptyCell(cell);
                }

                PlaceSelectedTile(cell);
            }

            private void PlaceSelectedTile(Vector3Int cell)
            {
                _tilemap.SetTile(cell, _selectedTile.Tile);

                var entry = new TilePlacementEntry { TileId = _selectedTile.Id, X = cell.x, Y = cell.y, Z = cell.z };
                UpsertEntry(entry);
                SavePlacements();

                _questLogPanel.QuestLog.RecordEvent(new QuestEvent(QuestEventKind.TilePlaced, "tile-placement:" + cell.x + ":" + cell.y + ":" + cell.z, tile: _selectedTile));
                SaveQuestLog();
                RefreshStateText();
            }

            private Vector3Int FindNearestEmptyCell(Vector3Int origin)
            {
                if (!_tilemap.HasTile(origin))
                {
                    return origin;
                }

                for (int radius = 1; radius <= 32; radius++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        for (int y = -radius; y <= radius; y++)
                        {
                            if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                            {
                                continue;
                            }

                            var candidate = new Vector3Int(origin.x + x, origin.y + y, origin.z);
                            if (!_tilemap.HasTile(candidate))
                            {
                                return candidate;
                            }
                        }
                    }
                }

                return origin + new Vector3Int(33, 0, 0);
            }

            private void BuildUi()
            {
                EnsureCommonPanelChrome();
                ClearChildren();
                MakeText(transform, "Title", "Tile Placement", new Vector2(0f, -18f), new Vector2(320f, 28f), 18);
                _statusText = MakeText(transform, "Status", string.Empty, new Vector2(0f, -52f), new Vector2(320f, 42f), 14);
                _acceptButton = MakeButton(transform, "AcceptButton", "Accept Tile Quest", new Vector2(0f, -98f), new Vector2(260f, 34f), AcceptQuest);

                for (int i = 0; i < _tiles.Length; i++)
                {
                    int index = i;
                    MakeButton(transform, "TileButton_" + i, _tiles[i].DisplayNameKey, new Vector2(0f, -142f - i * 34f), new Vector2(260f, 30f), () => SelectTile(index));
                }

                _claimButton = MakeButton(transform, "ClaimButton", "Claim Reward", new Vector2(0f, -226f), new Vector2(260f, 34f), ClaimReward);
            }

            private void EnsureCommonPanelChrome()
            {
                var image = GetComponent<Image>();
                if (image == null)
                {
                    image = gameObject.AddComponent<Image>();
                }
                image.color = Color.clear;
                image.raycastTarget = true;

                var tiles = GetComponent<ModernUiTileImage>();
                if (tiles == null)
                {
                    tiles = gameObject.AddComponent<ModernUiTileImage>();
                }
                tiles.SetRecipe(ModernUiRecipes.CommonPanel48);
                tiles.SetTileSize(CommonPanelTileSize);
                tiles.Rebuild();
            }

            private void AcceptQuest()
            {
                if (_questLogPanel == null || _questLogPanel.QuestLog == null || _quest == null)
                {
                    return;
                }

                if (_questLogPanel.QuestLog.GetState(_quest) == QuestState.NotStarted)
                {
                    _questLogPanel.QuestLog.Accept(_quest);
                    SaveQuestLog();
                }
                RefreshStateText();
            }

            private void SelectTile(int index)
            {
                if (index < 0 || index >= _tiles.Length)
                {
                    return;
                }

                _selectedTile = _tiles[index];
                RefreshStateText();
            }

            private void ClaimReward()
            {
                if (_questLogPanel == null || _questLogPanel.QuestLog == null || _quest == null)
                {
                    return;
                }

                if (_questLogPanel.QuestLog.ClaimReward(_quest, in _rewardContext))
                {
                    SaveQuestLog();
                    SaveStudentLife();
                }
                RefreshStateText();
            }

            private void RefreshStateText()
            {
                if (_statusText == null)
                {
                    return;
                }

                var state = _questLogPanel != null && _questLogPanel.QuestLog != null && _quest != null
                    ? _questLogPanel.QuestLog.GetState(_quest)
                    : QuestState.NotStarted;
                int count = _questLogPanel != null && _questLogPanel.QuestLog != null && _quest != null
                    ? _questLogPanel.QuestLog.GetObjectiveCount(_quest, 0)
                    : 0;
                string selected = _selectedTile != null ? "Selected: " + _selectedTile.DisplayNameKey : "Selected: none";
                _statusText.text = state + " | " + count + " / 1\n" + selected;

                if (_acceptButton != null)
                {
                    _acceptButton.interactable = state == QuestState.NotStarted || state == QuestState.Active;
                }
                if (_claimButton != null)
                {
                    _claimButton.interactable = state == QuestState.Completed;
                }
            }

            private PlaceableTileDefinition FindTile(string tileId)
            {
                for (int i = 0; i < _tiles.Length; i++)
                {
                    if (_tiles[i] != null && _tiles[i].Id == tileId)
                    {
                        return _tiles[i];
                    }
                }

                return null;
            }

            private void UpsertEntry(TilePlacementEntry entry)
            {
                for (int i = 0; i < _placedEntries.Count; i++)
                {
                    var existing = _placedEntries[i];
                    if (existing.X == entry.X && existing.Y == entry.Y && existing.Z == entry.Z)
                    {
                        _placedEntries[i] = entry;
                        return;
                    }
                }

                _placedEntries.Add(entry);
            }

            private void SavePlacements()
            {
                var metadata = ActiveSaveContext.Metadata;
                if (metadata == null || string.IsNullOrEmpty(metadata.SlotId))
                {
                    return;
                }

                var saveData = new TilePlacementSaveData { Entries = _placedEntries.ToArray() };
                new SaveService(metadata.SlotId).WriteJson(_tilePlacementFileName, JsonUtility.ToJson(saveData, true));
            }

            private void SaveQuestLog()
            {
                var metadata = ActiveSaveContext.Metadata;
                if (metadata == null || string.IsNullOrEmpty(metadata.SlotId) || _questLogPanel == null || _questLogPanel.QuestLog == null)
                {
                    return;
                }

                new SaveService(metadata.SlotId).WriteJson(_questLogFileName, JsonUtility.ToJson(_questLogPanel.QuestLog.ToSaveData(), true));
            }

            private void SaveStudentLife()
            {
                var metadata = ActiveSaveContext.Metadata;
                if (metadata == null || string.IsNullOrEmpty(metadata.SlotId) || _rewardContext.StudentLifeProgress == null)
                {
                    return;
                }

                new SaveService(metadata.SlotId).WriteJson(_studentLifeFileName, JsonUtility.ToJson(_rewardContext.StudentLifeProgress.ToSaveData(), true));
            }

            private void ClearChildren()
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    var child = transform.GetChild(i);
                    if (child.name.StartsWith("Tile_", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    Destroy(transform.GetChild(i).gameObject);
                }
            }

            private static Text MakeText(Transform parent, string name, string text, Vector2 position, Vector2 size, int fontSize)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Text));
                go.transform.SetParent(parent, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
                var label = go.GetComponent<Text>();
                label.text = text;
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = fontSize;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;
                return label;
            }

            private static Button MakeButton(Transform parent, string name, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(parent, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = position;
                rect.sizeDelta = size;

                var image = go.GetComponent<Image>();
                image.color = Color.clear;
                image.raycastTarget = true;

                var button = go.GetComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(action);
                MakeText(go.transform, "Text", text, Vector2.zero, size, 14);
                return button;
            }
        }
    }
}
