using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rootborn.UI.MainMenu
{
    public sealed class SaveSlotSelectPanel : MonoBehaviour
    {
        [SerializeField] private string _farmScene = "Farm";

        private GameObject _root;

        public static SaveSlotSelectPanel EnsureInScene()
        {
            var existing = Object.FindFirstObjectByType<SaveSlotSelectPanel>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("SaveSlotSelectPanel");
            Object.DontDestroyOnLoad(go);
            return go.AddComponent<SaveSlotSelectPanel>();
        }

        public void Show()
        {
            BuildOrRebuild();
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void BuildOrRebuild()
        {
            EnsureEventSystem();
            var canvas = EnsureCanvas();
            if (_root != null)
            {
                Destroy(_root);
            }

            _root = new GameObject("SaveSlotSelectRoot", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(canvas.transform, false);
            var rootRt = (RectTransform)_root.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.12f, 0.96f);

            MakeText(_root.transform, "Title", "Save Slots", new Vector2(0f, -90f), new Vector2(600f, 80f), 44, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            var service = new SaveService("slot-0");
            var slots = service.ListUiSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                BuildSlotCard(_root.transform, service, slots[i], i);
            }
        }

        private void BuildSlotCard(Transform parent, SaveService service, SaveSlotSummary summary, int index)
        {
            var card = new GameObject($"SaveSlotCard_{summary.SlotId}", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(parent, false);
            var rt = (RectTransform)card.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 420f);
            rt.anchoredPosition = new Vector2((index - 1) * 460f, 0f);
            card.GetComponent<Image>().color = new Color(0.20f, 0.24f, 0.25f, 1f);

            string title = summary.Exists ? summary.Metadata.DisplayName : "Empty Slot";
            MakeText(card.transform, "SlotTitle", title, new Vector2(0f, -32f), new Vector2(360f, 44f), 30, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            string seedText = summary.Exists
                ? $"World {summary.Metadata.WorldSeed}\nTile {summary.Metadata.TileSeed}"
                : "No save data";
            MakeText(card.transform, "SeedText", seedText, new Vector2(0f, -120f), new Vector2(340f, 90f), 24, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            string characterText = summary.Exists && summary.Metadata.Character != null
                ? $"Body {summary.Metadata.Character.BodyVariant}\nHair {summary.Metadata.Character.HairVariant}\nOutfit {summary.Metadata.Character.OutfitVariant}"
                : "Character Preview";
            MakeText(card.transform, "CharacterPreview", characterText, new Vector2(0f, -220f), new Vector2(340f, 92f), 22, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            if (summary.Exists)
            {
                MakeButton(card.transform, "LoadButton", "Load", new Vector2(-90f, 60f), new Vector2(150f, 58f), () => LoadSlot(summary.Metadata));
                MakeButton(card.transform, "DeleteButton", "Delete", new Vector2(90f, 60f), new Vector2(150f, 58f), () =>
                {
                    service.DeleteSlot(summary.SlotId);
                    BuildOrRebuild();
                });
            }
            else
            {
                MakeButton(card.transform, "NewGameButton", "New Game", new Vector2(0f, 60f), new Vector2(220f, 62f), () => CreateSlot(service, summary.SlotId));
            }
        }

        private void CreateSlot(SaveService service, string slotId)
        {
            int worldSeed = unchecked(System.Environment.TickCount * 397) ^ slotId.GetHashCode();
            int tileSeed = unchecked(System.Environment.TickCount * 491) ^ (slotId.GetHashCode() << 1);
            var metadata = service.CreateUiMetadata(slotId, new Rootborn.Game.Player.CharacterCustomization(), worldSeed, tileSeed);
            service.SaveMetadata(metadata);
            LoadSlot(metadata);
        }

        private void LoadSlot(SaveSlotMetadata metadata)
        {
            ActiveSaveContext.Set(metadata);
            Hide();
            SceneManager.LoadScene(_farmScene);
        }

        private static Canvas EnsureCanvas()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                return canvas;
            }

            var canvasGo = new GameObject("SaveSlotCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static Text MakeText(Transform parent, string name, string text, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = new Color(0.96f, 0.93f, 0.84f, 1f);
            return label;
        }

        private static Button MakeButton(Transform parent, string name, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.42f, 0.34f, 0.22f, 1f);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            MakeText(go.transform, "Label", label, Vector2.zero, size, 24, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            return button;
        }
    }
}
