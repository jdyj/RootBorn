using System.Collections;
using System.Collections.Generic;
using Rootborn.Game.Interiors;
using Rootborn.Game.Save;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.UI.Interiors
{
    public sealed class InteriorRoomPresetToolbarInstaller : MonoBehaviour
    {
        private const int MaxPresetButtons = 2;
        private readonly List<Button> _buttons = new List<Button>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCurrentSceneInstaller()
        {
            EnsureInstaller(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInstaller(scene);
        }

        private static void EnsureInstaller(Scene scene)
        {
            if (scene.name != "House")
            {
                return;
            }

            if (FindFirstObjectByType<InteriorRoomPresetToolbarInstaller>() != null)
            {
                return;
            }

            var go = new GameObject("[InteriorRoomPresetToolbarInstaller]");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<InteriorRoomPresetToolbarInstaller>();
        }

        private IEnumerator Start()
        {
            for (int i = 0; i < 120; i++)
            {
                if (TryInstallButtons())
                {
                    yield break;
                }

                yield return null;
            }
        }

        private bool TryInstallButtons()
        {
            if (_buttons.Count > 0)
            {
                return true;
            }

            var toolbar = GameObject.Find("InteriorPlacementToolbar");
            if (toolbar == null)
            {
                return false;
            }

            var presets = InteriorRoomPresetCatalog.LoadPresets();
            int count = Mathf.Min(MaxPresetButtons, presets.Count);
            for (int i = 0; i < count; i++)
            {
                var preset = presets[i];
                if (preset == null)
                {
                    continue;
                }

                var buttonObject = ModernUiPanelBuilder.CreateCommonPanel48Button(
                    toolbar.transform,
                    "RoomPresetButton_" + preset.StableId,
                    new Vector2(435f + i * 170f, 0f),
                    new Vector2(160f, 60f));
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(buttonObject.transform, false);
                var rect = label.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(8f, 4f);
                rect.offsetMax = new Vector2(-8f, -4f);
                var text = label.GetComponent<Text>();
                text.text = ShortLabel(preset.DisplayName);
                text.alignment = TextAnchor.MiddleCenter;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 14;
                text.color = new Color(0.95f, 0.93f, 0.84f, 1f);
                text.raycastTarget = false;

                var button = buttonObject.GetComponent<Button>();
                button.onClick.AddListener(() => ApplyPreset(preset));
                _buttons.Add(button);
            }

            return _buttons.Count > 0;
        }

        private static string ShortLabel(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "Room";
            }

            return displayName.Length <= 18 ? displayName : displayName.Substring(0, 18);
        }

        public static bool ApplyPresetForTests(InteriorRoomPresetDefinition preset)
        {
            return ApplyPresetAndPersist(preset);
        }

        private static void ApplyPreset(InteriorRoomPresetDefinition preset)
        {
            ApplyPresetAndPersist(preset);
        }

        private static bool ApplyPresetAndPersist(InteriorRoomPresetDefinition preset)
        {
            if (preset == null)
            {
                return false;
            }

            var applier = FindFirstObjectByType<InteriorTilemapApplier>();
            if (applier == null || applier.LastGeneratedMap == null)
            {
                return false;
            }

            if (!InteriorRoomPresetRuntimeUtility.CanFitPreset(preset, applier.LastGeneratedMap))
            {
                return false;
            }

            var floor = FindTilemap("HouseGroundTilemap");
            var walls = FindTilemap("HouseWallTilemap");
            var doors = FindTilemap("HouseDoorTilemap");
            var decorations = FindTilemap("HouseDecorationTilemap");
            var collision = FindTilemap("HouseCollisionTilemap");
            if (floor == null)
            {
                return false;
            }

            walls?.ClearAllTiles();
            doors?.ClearAllTiles();
            decorations?.ClearAllTiles();
            collision?.ClearAllTiles();
            InteriorRoomPresetApplier.ApplyBaseLayer(preset, floor);
            InteriorRoomPresetRuntimeProbe.Record(preset, floor);
            InteriorRoomPresetRuntimeUtility.SaveSelectedPreset(ActiveSaveContext.SlotId, preset.StableId);

            var overlay = InteriorPlacementPreviewOverlay.Ensure();
            overlay?.BindGeneratedMapForPersistence(applier.LastGeneratedMap, applier);

            var status = GameObject.Find("InteriorPlacementStatusText")?.GetComponent<Text>();
            if (status != null)
            {
                status.text = "Applied " + preset.DisplayName;
            }

            return true;
        }
        private static Tilemap FindTilemap(string name)
        {
            return GameObject.Find(name)?.GetComponent<Tilemap>();
        }
    }
}
