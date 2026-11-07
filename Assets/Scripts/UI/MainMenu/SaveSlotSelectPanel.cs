using System;
using System.Collections.Generic;
using Rootborn.Game.Characters;
using Rootborn.Game.Common;
using Rootborn.Game.Family;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.World;
using Rootborn.Game.Localization;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.UI.MainMenu
{
    public sealed class SaveSlotSelectPanel : MonoBehaviour
    {
        private Func<SaveSlotMetadata, string, string, bool> _entryHandler;
        private Text _entryErrorLabel;
        public string LastEntryError { get; private set; } = string.Empty;

        private static string s_saveRootOverride;

        private readonly CharacterCustomization _selectedCharacter = new CharacterCustomization();
        private readonly CharacterAppearance _selectedAppearance = new CharacterAppearance();
        private GameObject _root;
        private GameObject _deleteConfirmation;
        private GameObject _deleteReturnSelection;
        private readonly List<KeyValuePair<Button, bool>> _deleteSuspendedButtons = new List<KeyValuePair<Button, bool>>();
        private static readonly Color Ink = new Color32(48, 71, 83, 255);
        private SpumCharacterCreatorPanel _spumCreatorPanel;
        private readonly List<KeyValuePair<GameObject, bool>> _suspendedSlotContents = new List<KeyValuePair<GameObject, bool>>();

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

        public void ShowEntryFailure()
        {
            Show();
            EntryFailure();
        }

        public void Hide()
        {
            CloseDeleteConfirmation();
            CloseSpumCreator();
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
            CloseDeleteConfirmation();
            CloseSpumCreator();
            if (_root != null)
            {
                _root.SetActive(false);
                DestroyUiObject(_root);
            }
            _spumCreatorPanel = null;
            _entryErrorLabel = null; LastEntryError = string.Empty;
            _root = new GameObject("SaveSlotSelectRoot", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(canvas.transform, false);
            var rootRt = (RectTransform)_root.transform;
            rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero; rootRt.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = new Color32(211, 216, 212, 255);
            _root.GetComponent<Image>().raycastTarget = true;

            var frame = new GameObject("SaveSlotFrame", typeof(RectTransform), typeof(ModernUiTileImage));
            frame.transform.SetParent(_root.transform, false);
            var frameRt = (RectTransform)frame.transform;
            frameRt.anchorMin = frameRt.anchorMax = frameRt.pivot = new Vector2(0.5f, 0.5f);
            frameRt.sizeDelta = new Vector2(1440f, 840f);
            StyleSurface(frame, ModernUiRecipes.CommonPanel);
            MakeText(frame.transform, "Title", Local("ui.save.title", "이어갈 이야기 선택", "Choose your story"), new Vector2(0f, -68f), new Vector2(1250f, 64f), 34,
                TextAnchor.MiddleLeft, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            BuildCharacterSelectionControls(frame.transform);
            // The character creator replaced this row for players: its buttons no longer change how the character looks.
            if (!Debug.isDebugBuild && frame.transform.Find("CharacterSelectionPanel") is Transform oldRow) oldRow.gameObject.SetActive(false);
            var service = CreateService("slot-0");
            var slots = service.ListUiSlots();
            for (int i = 0; i < slots.Count; i++) BuildSlotCard(frame.transform, service, slots[i], i);
            MakeButton(frame.transform, "BackButton", Local("ui.save.back", "돌아가기", "Back"), new Vector2(-505f, 70f), new Vector2(240f, 58f), Hide);
            MakeText(frame.transform, "DeleteSafetyHint", Local("ui.save.delete-hint", "기록 삭제는 확인 후 진행됩니다.", "Deleting a story requires confirmation."),
                new Vector2(325f, 70f), new Vector2(600f, 44f), 18, TextAnchor.MiddleRight, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        }

        private void BuildSlotCard(Transform parent, SaveService service, SaveSlotSummary summary, int index)
        {
            var card = new GameObject($"SaveSlotCard_{summary.SlotId}", typeof(RectTransform), typeof(Image), typeof(ModernUiTileImage));
            card.transform.SetParent(parent, false);
            var rt = (RectTransform)card.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(414f, 470f);
            rt.anchoredPosition = new Vector2((index - 1) * 445f, -447f);
            card.GetComponent<Image>().raycastTarget = false;
            StyleSurface(card, ModernUiRecipes.SlotPanel);
            var symbol = new GameObject("SaveSlotSymbol", typeof(RectTransform), typeof(Image), typeof(UrbanUiIcon));
            symbol.transform.SetParent(card.transform, false);
            var symbolRt = (RectTransform)symbol.transform;
            symbolRt.anchorMin = symbolRt.anchorMax = new Vector2(0.5f, 1f);
            symbolRt.pivot = new Vector2(0.5f, 0.5f);
            symbolRt.anchoredPosition = new Vector2(0f, -104f); symbolRt.sizeDelta = new Vector2(144f, 144f);
            symbol.GetComponent<Image>().raycastTarget = false;
            symbol.GetComponent<UrbanUiIcon>().Configure(summary.Exists ? "journal" : "plan");

            string title = summary.Exists && !string.IsNullOrWhiteSpace(summary.Metadata.DisplayName)
                ? summary.Metadata.DisplayName : Local("ui.save.empty", "새로운 이야기", "New story");
            MakeText(card.transform, "SlotTitle", title, new Vector2(0f, -215f), new Vector2(342f, 52f), 25, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            MakeText(card.transform, "SeedText", summary.Exists
                ? Local("ui.save.continue-description", "남겨 둔 기록에서 이야기를 이어갑니다.", "Continue from your saved story.")
                : Local("ui.save.empty-description", "아직 기록이 없어요.", "There is no story here yet."),
                new Vector2(0f, -272f), new Vector2(342f, 54f), 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            if (summary.Exists)
            {
                BuildCharacterPreviewImage(card.transform, summary.Metadata);
                var preview = card.transform.Find("CharacterPreviewImage") as RectTransform;
                if (preview != null) { preview.anchoredPosition = new Vector2(0f, -331f); preview.sizeDelta = new Vector2(48f, 48f); }
                MakeText(card.transform, "CharacterPreview", Local("ui.save.saved-character", "저장된 인물", "Saved character"), new Vector2(0f, -374f), new Vector2(342f, 28f), 16, TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                MakeButton(card.transform, "LoadButton", Local("ui.save.load", "이어서 시작", "Continue"), new Vector2(-87f, 45f), new Vector2(152f, 58f), () => LoadSlot(summary.Metadata));
                MakeButton(card.transform, "DeleteButton", Local("ui.save.delete", "삭제", "Delete"), new Vector2(87f, 45f), new Vector2(152f, 58f), () => ShowDeleteConfirmation(service, summary));
            }
            else
            {
                MakeText(card.transform, "CharacterPreview", Local("ui.save.choose-character", "시작할 때 인물의 모습을 고를 수 있어요.", "Choose your character when you begin."),
                    new Vector2(0f, -349f), new Vector2(342f, 58f), 18, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                MakeButton(card.transform, "NewGameButton", Local("ui.save.new", "새 이야기", "New story"), new Vector2(0f, 45f), new Vector2(278f, 58f), () => OpenSpumCreator(service, summary.SlotId));
            }
        }

        private void ShowDeleteConfirmation(SaveService service, SaveSlotSummary summary)
        {
            CloseDeleteConfirmation();
            if (_root == null || !summary.Exists) return;
            _deleteReturnSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            foreach (var button in _root.GetComponentsInChildren<Button>(true))
            {
                _deleteSuspendedButtons.Add(new KeyValuePair<Button, bool>(button, button.interactable));
                button.interactable = false;
            }
            _deleteConfirmation = new GameObject("DeleteSlotConfirmation", typeof(RectTransform), typeof(Image));
            _deleteConfirmation.transform.SetParent(_root.transform, false);
            var overlay = (RectTransform)_deleteConfirmation.transform;
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            var blocker = _deleteConfirmation.GetComponent<Image>(); blocker.color = new Color(0f, 0f, 0f, 0.45f); blocker.raycastTarget = true;
            var box = new GameObject("DeleteSlotDialog", typeof(RectTransform), typeof(ModernUiTileImage));
            box.transform.SetParent(overlay, false);
            var rect = (RectTransform)box.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f); rect.sizeDelta = new Vector2(720f, 360f);
            StyleSurface(box, ModernUiRecipes.CommonPanel);
            MakeText(rect, "DeleteSlotPrompt", Local("ui.save.delete-prompt", "이 이야기를 지울까요?", "Delete this story?") + "\n" + summary.Metadata.DisplayName + "\n" +
                Local("ui.save.delete-warning", "삭제한 기록은 되돌릴 수 없어요.", "This cannot be undone."),
                new Vector2(0f, -118f), new Vector2(600f, 132f), 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            MakeButton(rect, "DeleteSlotConfirmButton", Local("ui.save.confirm-delete", "기록 삭제", "Delete story"), new Vector2(-145f, 66f), new Vector2(250f, 58f), () =>
            {
                service.DeleteSlot(summary.SlotId);
                CloseDeleteConfirmation();
                BuildOrRebuild();
            });
            var cancel = MakeButton(rect, "DeleteSlotCancelButton", Local("ui.save.cancel", "취소", "Cancel"), new Vector2(145f, 66f), new Vector2(250f, 58f), CloseDeleteConfirmation);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cancel.gameObject);
        }

        private void CloseDeleteConfirmation()
        {
            if (_deleteConfirmation != null)
            {
                _deleteConfirmation.SetActive(false);
                DestroyUiObject(_deleteConfirmation);
                _deleteConfirmation = null;
            }
            foreach (var state in _deleteSuspendedButtons) if (state.Key != null) state.Key.interactable = state.Value;
            _deleteSuspendedButtons.Clear();
            if (_deleteReturnSelection != null && _deleteReturnSelection.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_deleteReturnSelection);
            _deleteReturnSelection = null;
        }

        private void OpenSpumCreator(SaveService service, string slotId)
        {
            CloseSpumCreator();
            if (_root == null) return;
            // Preserve exact active states: cancel must not reveal a previously hidden control.
            for (int i = 0; i < _root.transform.childCount; i++)
            {
                var child = _root.transform.GetChild(i).gameObject;
                _suspendedSlotContents.Add(new KeyValuePair<GameObject, bool>(child, child.activeSelf));
                child.SetActive(false);
            }

            var go = new GameObject("SpumCharacterCreatorPanel", typeof(RectTransform));
            go.transform.SetParent(_root.transform, false);
            go.transform.SetAsLastSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _spumCreatorPanel = go.AddComponent<SpumCharacterCreatorPanel>();
            try
            {
                GameDataRegistryProvider.TryGet(out var registry);
                _spumCreatorPanel.Build(registry != null ? registry.CharacterAppearances : null, string.Empty,
                    (snapshot, playerName) => ConfirmSpumCreator(service, slotId, snapshot, playerName), CloseSpumCreator);
            }
            catch
            {
                CloseSpumCreator();
                throw;
            }
        }

        private void CloseSpumCreator()
        {
            var creator = _spumCreatorPanel;
            _spumCreatorPanel = null;
            if (creator != null)
            {
                creator.gameObject.SetActive(false);
                // Deferred PlayMode destruction must not become part of the next modal's saved sibling state.
                creator.transform.SetParent(null, false);
                DestroyUiObject(creator.gameObject);
            }
            foreach (var state in _suspendedSlotContents)
                if (state.Key != null) state.Key.SetActive(state.Value);
            _suspendedSlotContents.Clear();
        }

        private static void DestroyUiObject(GameObject value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void ConfirmSpumCreator(SaveService service, string slotId, CharacterAppearanceSnapshot snapshot, string playerName)
        {
            int worldSeed = unchecked(System.Environment.TickCount * 397) ^ slotId.GetHashCode();
            int tileSeed = unchecked(System.Environment.TickCount * 491) ^ (slotId.GetHashCode() << 1);
            var metadata = CreateMetadataForSelectedCharacter(slotId, worldSeed, tileSeed);
            metadata.CharacterAppearanceSnapshot = snapshot;
            if (!string.IsNullOrWhiteSpace(playerName)) metadata.DisplayName = playerName.Trim();
            service.SaveMetadata(metadata);
            TryEnterSlot(metadata, true);
        }

        private void LoadSlot(SaveSlotMetadata metadata) => TryEnterSlot(metadata, false);

        public void ConfigureEntryHandler(Func<SaveSlotMetadata, string, string, bool> handler) => _entryHandler = handler;

        public bool TryEnterSlot(SaveSlotMetadata metadata, bool newGame)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.SlotId)
                || !GameDataRegistryProvider.TryGet(out var registry) || registry.WorldTravelCatalog == null)
                return EntryFailure();
            WorldEntryPoint entry;
            try
            {
                entry = newGame ? WorldEntryService.ResolveNewGame(registry.WorldTravelCatalog)
                    : WorldEntryService.ResolveContinue(registry.WorldTravelCatalog, CreateService(metadata.SlotId));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                return EntryFailure();
            }
            if (entry == null || !(_entryHandler ?? Rootborn.UI.World.PlayerWorldTravelAdapter.RequestEntry)(metadata, entry.SceneName, entry.SpawnId))
                return EntryFailure();
            LastEntryError = string.Empty;
            if (_entryErrorLabel != null) _entryErrorLabel.text = string.Empty;
            Hide();
            return true;
        }
        private bool EntryFailure()
        {
            LastEntryError = GameText.Get("world.entry.failed");
            if (_root != null)
            {
                if (_entryErrorLabel == null)
                    _entryErrorLabel = MakeText(_root.transform.Find("SaveSlotFrame") ?? _root.transform, "WorldEntryError", LastEntryError, new Vector2(0f, 116f), new Vector2(1100f, 36f), 18, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
                _entryErrorLabel.text = LastEntryError;
                _entryErrorLabel.color = new Color(0.65f, 0.24f, 0.17f, 1f);
            }
            return false;
        }

        private void BuildCharacterSelectionControls(Transform parent)
        {
            var panel = new GameObject("CharacterSelectionPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -148f);
            rt.sizeDelta = new Vector2(1232f, 72f);
            panel.GetComponent<Image>().raycastTarget = false;
            StyleSurface(panel, ModernUiRecipes.SlotPanel);

            MakeText(panel.transform, "SelectionLabel", Local("ui.save.character-options", "인물의 기본 모습", "Character options"), new Vector2(-425f, 0f), new Vector2(180f, 44f), 16, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            BuildCharacterPreviewImage(panel.transform, new SaveSlotMetadata { Character = _selectedCharacter, Appearance = _selectedAppearance });
            var selectionPreview = panel.transform.Find("CharacterPreviewImage") as RectTransform;
            if (selectionPreview != null)
            {
                selectionPreview.anchoredPosition = new Vector2(-552f, 0f);
                selectionPreview.sizeDelta = new Vector2(48f, 48f);
            }
            MakeButton(panel.transform, "BodyNextButton", Local("ui.save.customize.BodyNextButton", "몸", "Body +"), new Vector2(-230f, 0f), new Vector2(120f, 44f), () =>
            {
                _selectedCharacter.BodyVariant++;
                CycleSelectedPart("body", "character.body.01", "character.body.02");
                BuildOrRebuild();
            });
            MakeButton(panel.transform, "EyesNextButton", Local("ui.save.customize.EyesNextButton", "눈", "Eyes +"), new Vector2(-90f, 0f), new Vector2(120f, 44f), () =>
            {
                CycleSelectedPart("eyes", "character.eyes.blue", "character.eyes.brown");
                BuildOrRebuild();
            });
            MakeButton(panel.transform, "HairNextButton", Local("ui.save.customize.HairNextButton", "머리", "Hair +"), new Vector2(50f, 0f), new Vector2(120f, 44f), () =>
            {
                _selectedCharacter.HairVariant++;
                CycleSelectedPart("hair", "character.hair.short.blonde", "character.hair.short.brown_dark");
                BuildOrRebuild();
            });
            MakeButton(panel.transform, "OutfitNextButton", Local("ui.save.customize.OutfitNextButton", "의상", "Outfit +"), new Vector2(190f, 0f), new Vector2(120f, 44f), () =>
            {
                _selectedCharacter.OutfitVariant++;
                CycleSelectedPart("outfit", "character.outfit.braces.brown", "character.outfit.braces.green");
                BuildOrRebuild();
            });
            MakeButton(panel.transform, "AccessoryNextButton", Local("ui.save.customize.AccessoryNextButton", "장식", "Hat +"), new Vector2(330f, 0f), new Vector2(120f, 44f), () =>
            {
                CycleSelectedPart("accessory", "character.accessory.bamboo.brown", "character.accessory.straw.black");
                BuildOrRebuild();
            });
            foreach(var button in panel.GetComponentsInChildren<Button>(true))
            {
                var option=(RectTransform)button.transform;
                var position=option.anchoredPosition;
                option.anchorMin=option.anchorMax=new Vector2(0.5f,0.5f);
                option.anchoredPosition=new Vector2(position.x,0f);
            }
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

        private static void AddGrowingPreviewLayer(Transform parent, string name, Rootborn.Game.Characters.Growing.GrowingCharacterSheetDefinition sheet)
        {
            var sprite = sheet != null ? sheet.GetFrame(Rootborn.Game.Characters.Growing.GrowingCharacterVisualView.RowDown, Rootborn.Game.Characters.Growing.GrowingCharacterVisualView.StandingColumn) : null;
            if (sprite == null) return;
            var layer = new GameObject(name, typeof(RectTransform), typeof(Image));
            layer.transform.SetParent(parent, false);
            var rect = (RectTransform)layer.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = layer.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        internal static void BuildCharacterPreviewImage(Transform parent, SaveSlotMetadata metadata)
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
            background.raycastTarget = false;
            var character = metadata != null ? metadata.Character : null;
            int bodyVariant = character != null ? character.BodyVariant : 0;
            int hairVariant = character != null ? character.HairVariant : 0;
            int outfitVariant = character != null ? character.OutfitVariant : 0;
            background.color = CharacterPreviewColor(bodyVariant, hairVariant, outfitVariant);

            // Draw the same layered character the game plays with.
            if (GameDataRegistryProvider.TryGet(out var gameData) && gameData != null)
            {
                var growing = Rootborn.Game.Characters.Growing.GrowingCharacterRuntimeInstaller.FindDefault(gameData);
                if (growing != null)
                {
                    background.color = Color.clear;
                    AddGrowingPreviewLayer(go.transform, "PreviewGrowing_Body", growing.Body);
                    AddGrowingPreviewLayer(go.transform, "PreviewGrowing_Outfit", growing.Outfit);
                    AddGrowingPreviewLayer(go.transform, "PreviewGrowing_Head", growing.Head);
                    return;
                }
            }
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
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                if (eventSystem.GetComponent<BaseInputModule>() == null)
                {
                    AddUiInputModule(eventSystem.gameObject);
                }
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem));
            AddUiInputModule(go);
        }

        private static void AddUiInputModule(GameObject go)
        {
            UiInputModuleInstaller.AddPreferredInputModule(go);
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
            label.color = Ink;
            label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
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
            go.GetComponent<Image>().raycastTarget = true;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var text = MakeText(go.transform, "Label", label, Vector2.zero, Vector2.zero, size.y < 54f ? 17 : 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            text.rectTransform.offsetMin = new Vector2(14f, 5f);
            text.rectTransform.offsetMax = new Vector2(-14f, -5f);
            StyleSurface(go, ModernUiRecipes.CommonPanel);
            return button;
        }
        private static void StyleSurface(GameObject go, ModernUiTileRecipe recipe)
        {
            var skin = go.GetComponent<ModernUiTileImage>();
            if (skin == null) skin = go.AddComponent<ModernUiTileImage>();
            skin.SetRecipe(recipe); skin.Rebuild();
        }

        private static string Local(string key, string korean, string english)
        {
            string value = GameText.Get(key);
            return value == key ? (GameText.Language == "ko" ? korean : english) : value;
        }

        private void OnDestroy()
        {
            CloseDeleteConfirmation();
            CloseSpumCreator();
            if (_root != null) { _root.SetActive(false); DestroyUiObject(_root); _root = null; }
        }
    }
}
