using System;
using System.Collections.Generic;
using Rootborn.Game.Characters;
using Rootborn.Game.Characters.Growing;
using Rootborn.Game.Localization;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.UI.MainMenu
{
    /// <summary>
    /// New-game character creator for the growing character: a name, the hair style and the outfit the character
    /// starts in at each growth stage. Every choice comes from the appearance assets; adding a look is data only.
    /// The wardrobe can change the outfit later; the creator only seeds the starting choice per body.
    /// </summary>
    public sealed class SpumCharacterCreatorPanel : MonoBehaviour
    {
        public const int MaxNameLength = 12;

        private readonly List<List<GrowingCharacterAppearanceDefinition>> _stages = new List<List<GrowingCharacterAppearanceDefinition>>();
        private readonly List<int> _choices = new List<int>();
        private readonly List<Text> _choiceLabels = new List<Text>();
        private readonly List<Image> _stageRowBackgrounds = new List<Image>();
        private int _previewStage;
        private InputField _nameField;
        private RectTransform _previewCharacter;
        private Text _previewCaption;
        private Action<CharacterAppearanceSnapshot, string> _confirm;
        private Action _cancel;

        public ModernUiTileRecipe RootPanelRecipe => ModernUiRecipes.CommonPanel;
        public int StageCount => _stages.Count;
        public int PreviewStage => _previewStage;
        public string PlayerName => _nameField != null ? CleanName(_nameField.text) : string.Empty;

        public GrowingCharacterAppearanceDefinition ChoiceAt(int stage)
        {
            return stage >= 0 && stage < _stages.Count ? _stages[stage][_choices[stage]] : null;
        }

        public void Build(CharacterAppearanceDefinition[] appearances, string defaultName, Action<CharacterAppearanceSnapshot, string> onConfirm, Action onCancel)
        {
            _confirm = onConfirm;
            _cancel = onCancel;
            _stages.Clear();
            _choices.Clear();
            _choiceLabels.Clear();
            _stageRowBackgrounds.Clear();
            _stages.AddRange(GrowingCharacterStageSelector.CreatorStages(appearances));
            for (int i = 0; i < _stages.Count; i++) _choices.Add(0);
            _previewStage = 0;

            ClearGeneratedChildren(transform);
            EnsureModalBackdrop();
            GameObject root = CreatePanel(transform, "SpumCharacterCreatorRoot", new Vector2(1296f, 864f));
            CreateHeader(root.transform);
            CreatePreview(root.transform);
            CreateChoices(root.transform, defaultName);
            CreateFooter(root.transform);
            RefreshAll();
        }

        public void SetName(string value)
        {
            if (_nameField != null) _nameField.text = value ?? string.Empty;
        }

        public void StepChoice(int stage, int offset)
        {
            if (stage < 0 || stage >= _stages.Count || _stages[stage].Count == 0) return;
            int count = _stages[stage].Count;
            _choices[stage] = ((_choices[stage] + offset) % count + count) % count;
            _previewStage = stage;
            RefreshAll();
        }

        public void PreviewStageAt(int stage)
        {
            if (stage < 0 || stage >= _stages.Count) return;
            _previewStage = stage;
            RefreshAll();
        }

        public void RandomizeSelection()
        {
            RandomizeSelection(Environment.TickCount);
        }

        public void RandomizeSelection(int seed)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < _stages.Count; i++) _choices[i] = rng.Next(_stages[i].Count);
            RefreshAll();
        }

        public void Confirm()
        {
            _confirm?.Invoke(CreateSnapshot(), PlayerName);
        }

        public CharacterAppearanceSnapshot CreateSnapshot()
        {
            var selections = new List<CharacterAppearancePartSelection>(_stages.Count);
            for (int i = 0; i < _stages.Count; i++)
            {
                var look = ChoiceAt(i);
                if (look != null && look.Body != null)
                    selections.Add(new CharacterAppearancePartSelection(GrowingCharacterStageSelector.OutfitCategoryPrefix + look.Body.Id, look.Id));
            }

            var first = ChoiceAt(0);
            return new CharacterAppearanceSnapshot(1, GrowingCharacterAppearanceDefinition.VisualKindName, first != null ? first.Id : string.Empty,
                GrowingCharacterStageSelector.CreatorCatalogId, selections.ToArray());
        }

        private void CreateHeader(Transform root)
        {
            Transform header = CreateContainer(root, "Header", new Vector2(1180f, 64f), new Vector2(0f, 360f)).transform;
            MakeText(header, "Title", L("ui.creator.title", "Create your character"), new Vector2(-380f, 0f), new Vector2(420f, 52f), 32, TextAnchor.MiddleLeft);

            GameObject back = CreateButton(header, "BackButton", L("ui.creator.back", "Back"), new Vector2(120f, 52f));
            back.GetComponent<Button>().onClick.AddListener(() => _cancel?.Invoke());
            ((RectTransform)back.transform).anchoredPosition = new Vector2(170f, 0f);

            GameObject random = CreateButton(header, "RandomButton", L("ui.creator.random", "Random"), new Vector2(140f, 52f));
            random.GetComponent<Button>().onClick.AddListener(RandomizeSelection);
            ((RectTransform)random.transform).anchoredPosition = new Vector2(318f, 0f);

            GameObject confirm = CreateButton(header, "ConfirmButton", L("ui.creator.confirm", "Start"), new Vector2(150f, 52f));
            confirm.GetComponent<Button>().onClick.AddListener(Confirm);
            ((RectTransform)confirm.transform).anchoredPosition = new Vector2(478f, 0f);
        }

        private void CreatePreview(Transform root)
        {
            GameObject preview = CreateContainer(root, "Preview", new Vector2(380f, 560f), new Vector2(-400f, 0f));
            var background = preview.AddComponent<Image>();
            background.color = new Color(0.13f, 0.16f, 0.22f, 1f);
            background.raycastTarget = false;

            var character = new GameObject("CharacterPreviewImage", typeof(RectTransform));
            character.transform.SetParent(preview.transform, false);
            _previewCharacter = (RectTransform)character.transform;
            _previewCharacter.anchorMin = _previewCharacter.anchorMax = new Vector2(0.5f, 0.5f);
            _previewCharacter.anchoredPosition = new Vector2(0f, 30f);
            _previewCharacter.sizeDelta = new Vector2(300f, 300f);

            _previewCaption = MakeText(preview.transform, "PreviewCaption", string.Empty, new Vector2(0f, -230f), new Vector2(340f, 48f), 22, TextAnchor.MiddleCenter);
            _previewCaption.color = Color.white;
        }

        private void CreateChoices(Transform root, string defaultName)
        {
            Transform choices = CreateContainer(root, "Choices", new Vector2(700f, 560f), new Vector2(210f, 0f)).transform;
            float y = 230f;

            MakeText(choices, "NameLabel", L("ui.creator.name", "Name"), new Vector2(-260f, y), new Vector2(160f, 48f), 22, TextAnchor.MiddleLeft);
            _nameField = CreateNameField(choices, new Vector2(90f, y), new Vector2(480f, 56f));
            _nameField.text = CleanName(defaultName);
            y -= 90f;

            MakeText(choices, "HairLabel", L("ui.creator.hair", "Hair"), new Vector2(-260f, y), new Vector2(160f, 48f), 22, TextAnchor.MiddleLeft);
            CreateStepRow(choices, "Hair", y, L("ui.creator.hair.short_brown", "Short brown hair"), null, false);
            y -= 90f;

            for (int i = 0; i < _stages.Count; i++)
            {
                int stage = i;
                var rowBackground = new GameObject("Stage" + i + "_Row", typeof(RectTransform), typeof(Image), typeof(Button));
                rowBackground.transform.SetParent(choices, false);
                var rowRect = (RectTransform)rowBackground.transform;
                rowRect.anchorMin = rowRect.anchorMax = new Vector2(0.5f, 0.5f);
                rowRect.anchoredPosition = new Vector2(0f, y);
                rowRect.sizeDelta = new Vector2(690f, 72f);
                var image = rowBackground.GetComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0f);
                rowBackground.GetComponent<Button>().targetGraphic = image;
                rowBackground.GetComponent<Button>().onClick.AddListener(() => PreviewStageAt(stage));
                _stageRowBackgrounds.Add(image);

                MakeText(choices, "Stage" + i + "_Label", StageLabel(_stages[i][0]), new Vector2(-260f, y), new Vector2(170f, 48f), 22, TextAnchor.MiddleLeft);
                _choiceLabels.Add(CreateStepRow(choices, "Stage" + i, y, string.Empty, offset => StepChoice(stage, offset), _stages[i].Count > 1));
                y -= 90f;
            }
        }

        // ◀ label ▶ row. Arrows are hidden when there is only one option, so a single choice never looks broken.
        private Text CreateStepRow(Transform parent, string prefix, float y, string text, Action<int> step, bool canStep)
        {
            GameObject previous = CreateButton(parent, prefix + "_Previous", "<", new Vector2(56f, 52f));
            ((RectTransform)previous.transform).anchoredPosition = new Vector2(-120f, y);
            GameObject next = CreateButton(parent, prefix + "_Next", ">", new Vector2(56f, 52f));
            ((RectTransform)next.transform).anchoredPosition = new Vector2(300f, y);
            if (step != null)
            {
                previous.GetComponent<Button>().onClick.AddListener(() => step(-1));
                next.GetComponent<Button>().onClick.AddListener(() => step(1));
            }

            previous.SetActive(canStep);
            next.SetActive(canStep);
            return MakeText(parent, prefix + "_Choice", text, new Vector2(90f, y), new Vector2(340f, 52f), 22, TextAnchor.MiddleCenter);
        }

        private void CreateFooter(Transform root)
        {
            Transform footer = CreateContainer(root, "Footer", new Vector2(1180f, 64f), new Vector2(0f, -350f)).transform;
            MakeText(footer, "Hint", L("ui.creator.hint", "You can change outfits later in the wardrobe."), Vector2.zero, new Vector2(1120f, 40f), 20, TextAnchor.MiddleCenter);
        }

        private void RefreshAll()
        {
            for (int i = 0; i < _choiceLabels.Count; i++)
            {
                var look = ChoiceAt(i);
                _choiceLabels[i].text = look != null ? L(look.DisplayNameKey, look.Id) : string.Empty;
                _stageRowBackgrounds[i].color = i == _previewStage ? new Color(0.35f, 0.45f, 0.70f, 0.25f) : new Color(1f, 1f, 1f, 0f);
            }

            var shown = ChoiceAt(_previewStage);
            if (_previewCaption != null)
                _previewCaption.text = shown != null ? string.Format(System.Globalization.CultureInfo.CurrentCulture, L("ui.creator.preview", "{0}"), StageLabel(shown)) : string.Empty;
            if (_previewCharacter == null) return;
            ClearGeneratedChildren(_previewCharacter);
            if (shown == null) return;
            AddPreviewLayer(_previewCharacter, "PreviewGrowing_Body", shown.Body);
            AddPreviewLayer(_previewCharacter, "PreviewGrowing_Outfit", shown.Outfit);
            AddPreviewLayer(_previewCharacter, "PreviewGrowing_Head", shown.Head);
        }

        private static string StageLabel(GrowingCharacterAppearanceDefinition look)
        {
            if (look == null) return string.Empty;
            if (look.RequiresAdult) return L("ui.creator.stage.adult", "Adult");
            return look.MinimumStudentDay <= 1 ? L("ui.creator.stage.child", "Child") : L("ui.creator.stage.student", "Student");
        }

        private static void AddPreviewLayer(Transform parent, string name, GrowingCharacterSheetDefinition sheet)
        {
            var sprite = sheet != null ? sheet.GetFrame(GrowingCharacterVisualView.RowDown, GrowingCharacterVisualView.StandingColumn) : null;
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

        private InputField CreateNameField(Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject("NameField", typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = Color.white;
            var text = MakeText(go.transform, "Text", string.Empty, Vector2.zero, size - new Vector2(24f, 8f), 22, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            var placeholder = MakeText(go.transform, "Placeholder", L("ui.creator.name.placeholder", "Enter a name"), Vector2.zero, size - new Vector2(24f, 8f), 22, TextAnchor.MiddleLeft);
            placeholder.color = new Color(0.45f, 0.48f, 0.55f, 1f);
            var field = go.GetComponent<InputField>();
            field.targetGraphic = image;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.characterLimit = MaxNameLength;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        private static string CleanName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string trimmed = value.Trim();
            return trimmed.Length > MaxNameLength ? trimmed.Substring(0, MaxNameLength) : trimmed;
        }

        private void EnsureModalBackdrop()
        {
            var backdrop = GetComponent<Image>();
            if (backdrop == null) backdrop = gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.06f, 0.08f, 0.12f, 1f);
            backdrop.raycastTarget = true;
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            // Configure geometry before OnEnable builds the tiled chrome. One root surface, no per-cell tile grids.
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var backplate = go.GetComponent<Image>();
            backplate.color = new Color(0.90f, 0.89f, 0.94f, 1f);
            backplate.raycastTarget = true;
            var tile = go.AddComponent<ModernUiTileImage>();
            tile.SetRecipe(ModernUiRecipes.CommonPanel);
            tile.SetTileSize(new Vector2(48f, 48f));
            go.SetActive(true);
            return go;
        }

        private static GameObject CreateContainer(Transform parent, string name, Vector2 size, Vector2 position)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return go;
        }

        private static GameObject CreateButton(Transform parent, string name, string label, Vector2 size)
        {
            GameObject button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(parent, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var image = button.GetComponent<Image>();
            image.color = new Color(0.19f, 0.23f, 0.31f, 1f);
            image.raycastTarget = true;
            button.GetComponent<Button>().targetGraphic = image;
            var text = MakeText(button.transform, "Label", label, Vector2.zero, new Vector2(size.x - 12f, size.y - 8f), 20, TextAnchor.MiddleCenter);
            text.color = Color.white;
            return button;
        }

        private static Text MakeText(Transform parent, string name, string text, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            GameObject textGo = new GameObject(name, typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(parent, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = position;
            textRect.sizeDelta = size;
            var label = textGo.GetComponent<Text>();
            label.text = text;
            label.alignment = alignment;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.color = new Color(0.10f, 0.13f, 0.20f, 1f);
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static void ClearGeneratedChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static string L(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            string value = GameText.Get(key);
            return string.IsNullOrEmpty(value) || value == key ? fallback : value;
        }
    }
}
