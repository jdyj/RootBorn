using Rootborn.Game.Player;
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

        private readonly CharacterCustomization _selectedCharacter = new CharacterCustomization();
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

        public void SetSelectedCharacterSelection(int bodyVariant, int hairVariant, int outfitVariant, CharacterCustomization.Facing facing)
        {
            _selectedCharacter.BodyVariant = bodyVariant;
            _selectedCharacter.HairVariant = hairVariant;
            _selectedCharacter.OutfitVariant = outfitVariant;
            _selectedCharacter.DefaultFacing = facing;
        }

        public SaveSlotMetadata CreateMetadataForSelectedCharacter(string slotId, int worldSeed, int tileSeed)
        {
            return CreateMetadataForNewSlot(slotId, CopyCharacter(_selectedCharacter), worldSeed, tileSeed);
        }

        public SaveSlotMetadata CreateMetadataForNewSlot(string slotId, CharacterCustomization character, int worldSeed, int tileSeed)
        {
            var service = new SaveService(slotId);
            return service.CreateUiMetadata(slotId, CopyCharacter(character), worldSeed, tileSeed);
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

            BuildCharacterSelectionControls(_root.transform);

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
            rt.anchoredPosition = new Vector2((index - 1) * 460f, -30f);
            card.GetComponent<Image>().color = new Color(0.20f, 0.24f, 0.25f, 1f);

            string title = summary.Exists ? summary.Metadata.DisplayName : "Empty Slot";
            MakeText(card.transform, "SlotTitle", title, new Vector2(0f, -32f), new Vector2(360f, 44f), 30, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            string seedText = summary.Exists
                ? $"World {summary.Metadata.WorldSeed}\nTile {summary.Metadata.TileSeed}"
                : "No save data";
            MakeText(card.transform, "SeedText", seedText, new Vector2(0f, -112f), new Vector2(340f, 78f), 24, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            BuildCharacterPreviewImage(card.transform, summary.Metadata);
            MakeText(card.transform, "CharacterPreview", FormatCharacterPreview(summary.Metadata), new Vector2(0f, -292f), new Vector2(340f, 78f), 22, TextAnchor.MiddleCenter,
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
            var metadata = CreateMetadataForSelectedCharacter(slotId, worldSeed, tileSeed);
            service.SaveMetadata(metadata);
            LoadSlot(metadata);
        }

        private void LoadSlot(SaveSlotMetadata metadata)
        {
            ActiveSaveContext.Set(metadata);
            Hide();
            SceneManager.LoadScene(_farmScene);
        }

        private void BuildCharacterSelectionControls(Transform parent)
        {
            var panel = new GameObject("CharacterSelectionPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -168f);
            rt.sizeDelta = new Vector2(720f, 64f);
            panel.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.18f, 1f);

            MakeText(panel.transform, "SelectionLabel", FormatCharacterPreview(new SaveSlotMetadata { Character = _selectedCharacter }), new Vector2(-240f, 0f), new Vector2(180f, 52f), 18, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            MakeButton(panel.transform, "BodyNextButton", "Body +", new Vector2(-60f, 0f), new Vector2(120f, 44f), () =>
            {
                _selectedCharacter.BodyVariant++;
                BuildOrRebuild();
            });
            MakeButton(panel.transform, "HairNextButton", "Hair +", new Vector2(80f, 0f), new Vector2(120f, 44f), () =>
            {
                _selectedCharacter.HairVariant++;
                BuildOrRebuild();
            });
            MakeButton(panel.transform, "OutfitNextButton", "Outfit +", new Vector2(220f, 0f), new Vector2(120f, 44f), () =>
            {
                _selectedCharacter.OutfitVariant++;
                BuildOrRebuild();
            });
        }

        private static string FormatCharacterPreview(SaveSlotMetadata metadata)
        {
            var character = metadata != null ? metadata.Character : null;
            if (character == null)
            {
                return "Character Preview";
            }

            return $"Body {character.BodyVariant}\nHair {character.HairVariant}\nOutfit {character.OutfitVariant}";
        }

        private static void BuildCharacterPreviewImage(Transform parent, SaveSlotMetadata metadata)
        {
            var go = new GameObject("CharacterPreviewImage", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -210f);
            rt.sizeDelta = new Vector2(72f, 72f);

            var character = metadata != null ? metadata.Character : null;
            int bodyVariant = character != null ? character.BodyVariant : 0;
            int hairVariant = character != null ? character.HairVariant : 0;
            int outfitVariant = character != null ? character.OutfitVariant : 0;
            go.GetComponent<Image>().color = CharacterPreviewColor(bodyVariant, hairVariant, outfitVariant);
        }

        private static Color CharacterPreviewColor(int bodyVariant, int hairVariant, int outfitVariant)
        {
            float red = 0.35f + Mathf.Repeat(bodyVariant * 0.11f, 0.45f);
            float green = 0.35f + Mathf.Repeat(hairVariant * 0.13f, 0.45f);
            float blue = 0.35f + Mathf.Repeat(outfitVariant * 0.17f, 0.45f);
            return new Color(red, green, blue, 1f);
        }

        private static CharacterCustomization CopyCharacter(CharacterCustomization source)
        {
            var character = source ?? new CharacterCustomization();
            return new CharacterCustomization
            {
                BodyVariant = character.BodyVariant,
                HairVariant = character.HairVariant,
                OutfitVariant = character.OutfitVariant,
                DefaultFacing = character.DefaultFacing,
            };
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
