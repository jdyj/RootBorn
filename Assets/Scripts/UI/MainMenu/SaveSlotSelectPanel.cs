using Rootborn.Game.Family;
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
        [SerializeField] private string _townScene = "Town";

        private static string s_saveRootOverride;

        private readonly CharacterCustomization _selectedCharacter = new CharacterCustomization();
        private readonly CharacterAppearance _selectedAppearance = new CharacterAppearance();
        private GameObject _root;

        public static void SetSaveRootForTests(string rootDirectory)
        {
            s_saveRootOverride = rootDirectory;
        }

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

        public void SetSelectedAppearancePart(string categoryId, string partId)
        {
            _selectedAppearance.SetSelectedPart(categoryId, partId);
        }

        public SaveSlotMetadata CreateMetadataForSelectedCharacter(string slotId, int worldSeed, int tileSeed)
        {
            var metadata = CreateMetadataForNewSlot(slotId, CopyCharacter(_selectedCharacter), worldSeed, tileSeed);
            metadata.Appearance = CopyAppearance(_selectedAppearance);
            return metadata;
        }

        public SaveSlotMetadata CreateMetadataForNewSlot(string slotId, CharacterCustomization character, int worldSeed, int tileSeed)
        {
            var service = CreateService(slotId);
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

            var service = CreateService("slot-0");
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
            SceneManager.LoadScene(_townScene);
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
            rt.sizeDelta = new Vector2(960f, 64f);
            panel.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.18f, 1f);

            MakeText(panel.transform, "SelectionLabel", FormatCharacterPreview(new SaveSlotMetadata { Character = _selectedCharacter, Appearance = _selectedAppearance }), new Vector2(-360f, 0f), new Vector2(200f, 52f), 16, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            BuildCharacterPreviewImage(panel.transform, new SaveSlotMetadata { Character = _selectedCharacter, Appearance = _selectedAppearance });
            var selectionPreview = panel.transform.Find("CharacterPreviewImage") as RectTransform;
            if (selectionPreview != null)
            {
                selectionPreview.anchoredPosition = new Vector2(-490f, 0f);
                selectionPreview.sizeDelta = new Vector2(48f, 48f);
            }
            MakeButton(panel.transform, "BodyNextButton", "Body +", new Vector2(-180f, 0f), new Vector2(120f, 44f), () =>
            {
                _selectedCharacter.BodyVariant++;
                CycleSelectedPart("body", "character.body.01", "character.body.02");
                BuildOrRebuild();
            });
            MakeButton(panel.transform, "EyesNextButton", "Eyes +", new Vector2(-40f, 0f), new Vector2(120f, 44f), () =>
            {
                CycleSelectedPart("eyes", "character.eyes.blue", "character.eyes.brown");
                BuildOrRebuild();
            });
            MakeButton(panel.transform, "HairNextButton", "Hair +", new Vector2(100f, 0f), new Vector2(120f, 44f), () =>
            {
                _selectedCharacter.HairVariant++;
                CycleSelectedPart("hair", "character.hair.short.blonde", "character.hair.short.brown_dark");
                BuildOrRebuild();
            });
            MakeButton(panel.transform, "OutfitNextButton", "Outfit +", new Vector2(240f, 0f), new Vector2(120f, 44f), () =>
            {
                _selectedCharacter.OutfitVariant++;
                CycleSelectedPart("outfit", "character.outfit.braces.brown", "character.outfit.braces.green");
                BuildOrRebuild();
            });
            MakeButton(panel.transform, "AccessoryNextButton", "Hat +", new Vector2(380f, 0f), new Vector2(120f, 44f), () =>
            {
                CycleSelectedPart("accessory", "character.accessory.bamboo.brown", "character.accessory.straw.black");
                BuildOrRebuild();
            });
        }

        private void CycleSelectedPart(string categoryId, string firstPartId, string secondPartId)
        {
            string current = _selectedAppearance.GetSelectedPartId(categoryId);
            _selectedAppearance.SetSelectedPart(categoryId, current == firstPartId ? secondPartId : firstPartId);
        }

        private static SaveService CreateService(string slotId)
        {
            return string.IsNullOrEmpty(s_saveRootOverride)
                ? new SaveService(slotId)
                : new SaveService(slotId, s_saveRootOverride);
        }

        private static string FormatCharacterPreview(SaveSlotMetadata metadata)
        {
            var character = metadata != null ? metadata.Character : null;
            if (character == null)
            {
                return "Character Preview";
            }

            string appearance = string.Empty;
            var selectedAppearance = metadata.Appearance;
            if (selectedAppearance != null)
            {
                string body = selectedAppearance.GetSelectedPartId("body");
                if (!string.IsNullOrEmpty(body)) appearance = "\n" + body;
            }

            return $"Body {character.BodyVariant}\nHair {character.HairVariant}\nOutfit {character.OutfitVariant}{appearance}";
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

            var background = go.GetComponent<Image>();
            var character = metadata != null ? metadata.Character : null;
            int bodyVariant = character != null ? character.BodyVariant : 0;
            int hairVariant = character != null ? character.HairVariant : 0;
            int outfitVariant = character != null ? character.OutfitVariant : 0;
            background.color = CharacterPreviewColor(bodyVariant, hairVariant, outfitVariant);

            var registry = Rootborn.Game.Managers.Managers.Data != null ? Rootborn.Game.Managers.Managers.Data.Registry : null;
            if (registry == null || registry.CharacterParts == null || registry.CharacterParts.Length == 0)
            {
                return;
            }

            background.color = Color.clear;
            var appearance = CharacterAppearance.ResolveWithDefaults(metadata != null ? metadata.Appearance : null, registry.CharacterParts);
            for (int i = 0; i < registry.CharacterParts.Length; i++)
            {
                var candidate = registry.CharacterParts[i];
                if (candidate == null || string.IsNullOrEmpty(candidate.CategoryId))
                {
                    continue;
                }
                var part = FindPreviewPart(registry.CharacterParts, appearance.GetSelectedPartId(candidate.CategoryId), candidate.CategoryId);
                if (part == null || part.PreviewSprite == null || go.transform.Find("PreviewPart_" + part.CategoryId) != null)
                {
                    continue;
                }

                var layer = new GameObject("PreviewPart_" + part.CategoryId, typeof(RectTransform), typeof(Image));
                layer.transform.SetParent(go.transform, false);
                var layerRt = (RectTransform)layer.transform;
                layerRt.anchorMin = Vector2.zero;
                layerRt.anchorMax = Vector2.one;
                layerRt.offsetMin = Vector2.zero;
                layerRt.offsetMax = Vector2.zero;
                var image = layer.GetComponent<Image>();
                image.sprite = part.PreviewSprite;
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
        }

        private static CharacterPartDefinition FindPreviewPart(CharacterPartDefinition[] parts, string selectedId, string categoryId)
        {
            CharacterPartDefinition fallback = null;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part == null || part.CategoryId != categoryId)
                {
                    continue;
                }
                if (part.Id == selectedId)
                {
                    return part;
                }
                if (fallback == null && part.IsDefault)
                {
                    fallback = part;
                }
            }
            return fallback;
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

        private static CharacterAppearance CopyAppearance(CharacterAppearance source)
        {
            var copy = new CharacterAppearance();
            if (source == null || source.Parts == null)
            {
                return copy;
            }

            for (int i = 0; i < source.Parts.Length; i++)
            {
                var part = source.Parts[i];
                if (part != null)
                {
                    copy.SetSelectedPart(part.CategoryId, part.PartId);
                }
            }
            return copy;
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