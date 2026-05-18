using System;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.Game.Common;
using Rootborn.Game.Interiors;
using Rootborn.UI.Modern;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Rootborn.UI.Interiors
{
    public readonly struct InteriorFurnitureGenerationResult
    {
        public InteriorFurnitureGenerationResult(bool success, string message, string summary, InteriorGeneratedMap map)
        {
            Success = success;
            Message = message ?? string.Empty;
            Summary = summary ?? string.Empty;
            Map = map;
        }

        public bool Success { get; }
        public string Message { get; }
        public string Summary { get; }
        public InteriorGeneratedMap Map { get; }
    }

    public sealed class InteriorFurniturePlacementRow
    {
        public InteriorFurniturePlacementRow(InteriorObjectKind objectKind)
        {
            ObjectKind = objectKind;
            Count = 0;
            FacingDirection = InteriorFacingDirection.None;
            Preference = objectKind == InteriorObjectKind.Sofa ? InteriorPlacementPreference.NearWall : InteriorPlacementPreference.AvoidCorridor;
            Required = false;
            InteractivePreview = objectKind == InteriorObjectKind.Desk;
        }

        public InteriorObjectKind ObjectKind { get; }
        public int Count { get; private set; }
        public InteriorFacingDirection FacingDirection { get; private set; }
        public InteriorPlacementPreference Preference { get; private set; }
        public bool Required { get; private set; }
        public bool InteractivePreview { get; private set; }

        public void Configure(int count, InteriorFacingDirection facingDirection, InteriorPlacementPreference preference, bool required, bool interactivePreview)
        {
            Count = Mathf.Max(0, count);
            FacingDirection = facingDirection;
            Preference = preference;
            Required = required;
            InteractivePreview = interactivePreview;
        }
    }

    public sealed class InteriorFurniturePlacementUiModel
    {
        private readonly List<InteriorFurniturePlacementRow> _rows = new List<InteriorFurniturePlacementRow>();
        private int _seed = 1205;

        public IReadOnlyList<InteriorFurniturePlacementRow> Rows => _rows;
        public InteriorGeneratedMap CurrentMap { get; private set; }
        public int Seed => _seed;

        public void SetSeed(string seedText)
        {
            if (int.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                _seed = parsed;
            }
        }

        public InteriorFurniturePlacementRow GetRow(InteriorObjectKind objectKind)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].ObjectKind == objectKind)
                {
                    return _rows[i];
                }
            }

            return null;
        }

        public void SetFurniture(InteriorObjectKind objectKind, int count, InteriorFacingDirection facingDirection, InteriorPlacementPreference preference, bool required, bool interactivePreview)
        {
            var row = GetRow(objectKind);
            if (row == null)
            {
                row = new InteriorFurniturePlacementRow(objectKind);
                _rows.Add(row);
            }

            row.Configure(count, facingDirection, preference, required, interactivePreview);
        }

        public InteriorFurniturePlacementRequest[] BuildPlacementRequests()
        {
            var requests = new List<InteriorFurniturePlacementRequest>();
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.Count > 0)
                {
                    requests.Add(InteriorFurniturePlacementRequest.CreateForTests(row.ObjectKind, row.Count, row.Preference, row.FacingDirection, row.Required));
                }
            }

            return requests.ToArray();
        }

        public InteriorFurniturePlacementRequest BuildRequest(InteriorObjectKind objectKind)
        {
            var row = GetRow(objectKind);
            return row != null
                ? InteriorFurniturePlacementRequest.CreateForTests(row.ObjectKind, row.Count, row.Preference, row.FacingDirection, row.Required)
                : InteriorFurniturePlacementRequest.CreateForTests(objectKind, 1, InteriorPlacementPreference.Any, InteriorFacingDirection.None, false);
        }

        public InteriorFurnitureGenerationResult Regenerate(InteriorGenerationProfile sourceProfile)
        {
            var profile = sourceProfile != null ? UnityEngine.Object.Instantiate(sourceProfile) : InteriorGenerationProfile.CreateDefaultOfficeForTests();
            profile.ConfigurePlacementRequestsForTests(BuildPlacementRequests());
            try
            {
                var map = sourceProfile != null ? InteriorGenerator.Generate(profile, _seed) : GenerateManualPlacementMap(profile, _seed);
                CurrentMap = map;
                return new InteriorFurnitureGenerationResult(true, "Generated", BuildSummary(map), map);
            }
            catch (Exception ex) when (ex is InteriorPlacementException || ex is InvalidOperationException)
            {
                return new InteriorFurnitureGenerationResult(false, ex.Message, string.Empty, CurrentMap);
            }
            finally
            {
                if (sourceProfile != null)
                {
                    UnityEngine.Object.DestroyImmediate(profile);
                }
            }
        }

        private static string BuildSummary(InteriorGeneratedMap map)
        {
            if (map == null)
            {
                return string.Empty;
            }

            return "Desk: " + map.CountObjects(InteriorObjectKind.Desk)
                + ", Chair: " + map.CountObjects(InteriorObjectKind.Chair)
                + ", Computer: " + map.CountObjects(InteriorObjectKind.Computer)
                + ", Sofa: " + map.CountObjects(InteriorObjectKind.Sofa)
                + ", Plant: " + map.CountObjects(InteriorObjectKind.Plant);
        }

        private static InteriorGeneratedMap GenerateManualPlacementMap(InteriorGenerationProfile profile, int seed)
        {
            var size = profile.Size;
            var map = new InteriorGeneratedMap(size.x, size.y);
            InteriorLayoutGenerator.GenerateLayout(map, profile, new System.Random(seed));
            if (!InteriorPathValidator.CanReachAnyDoor(map, map.SpawnCell))
            {
                throw new InvalidOperationException("Generated House interior is not connected from spawn to a door.");
            }

            return map;
        }
    }

    public sealed class InteriorFurniturePlacementPanel : MonoBehaviour
    {
        private const int MaxPaletteItemsPerCategory = 60;

        private static readonly InteriorObjectKind[] CategoryOrder =
        {
            InteriorObjectKind.Desk,
            InteriorObjectKind.Chair,
            InteriorObjectKind.Computer,
            InteriorObjectKind.Sofa,
            InteriorObjectKind.Plant,
            InteriorObjectKind.Shelf,
            InteriorObjectKind.Light,
            InteriorObjectKind.OfficeProp,
        };

        private readonly InteriorFurniturePlacementUiModel _model = new InteriorFurniturePlacementUiModel();
        private readonly List<InteriorFurnitureDefinition> _furniture = new List<InteriorFurnitureDefinition>();
        private InteriorTilemapApplier _applier;
        private InteriorPlacementPreviewOverlay _overlay;
        private Text _statusText;
        private Text _summaryText;
        private Text _availabilityText;
        private Text _selectedNameText;
        private Text _selectedSizeText;
        private Text _selectedDirectionText;
        private Image _selectedPreviewImage;
        private InputField _seedInput;
        private InteriorFurnitureDefinition _activeFurniture;
        private bool _isClosing;
        private static Font _font;

        public InteriorFurniturePlacementUiModel Model => _model;

        public static InteriorFurniturePlacementPanel Create(Canvas canvas, InteriorTilemapApplier applier)
        {
            var root = new GameObject("InteriorFurniturePlacementPanel", typeof(RectTransform), typeof(InteriorFurniturePlacementPanel));
            root.transform.SetParent(canvas.transform, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var panel = root.GetComponent<InteriorFurniturePlacementPanel>();
            panel._applier = applier;
            panel.BuildUi();
            panel.RegenerateAndApply();
            return panel;
        }

        private void OnDisable()
        {
            _isClosing = true;
        }

        private void BuildUi()
        {
            _furniture.Clear();
            _furniture.AddRange(InteriorFurnitureCatalog.LoadFurniture());

            var left = ModernUiPanelBuilder.CreateCommonPanel48(transform, "FurniturePalette", new Vector2(-740f, 0f), new Vector2(430f, 860f));
            var right = ModernUiPanelBuilder.CreateCommonPanel48(transform, "SelectedFurnitureSettings", new Vector2(760f, 0f), new Vector2(360f, 720f));
            var toolbar = ModernUiPanelBuilder.CreateCommonPanel48(transform, "InteriorPlacementToolbar", new Vector2(0f, 430f), new Vector2(760f, 86f));
            var bottom = ModernUiPanelBuilder.CreateCommonPanel48(transform, "InteriorPlacementBottomBar", new Vector2(0f, -490f), new Vector2(1760f, 86f));

            CreateLabel(left.transform, "PaletteTitle", "가구 배치", new Vector2(-52f, 386f), new Vector2(270f, 42f), 24).alignment = TextAnchor.MiddleLeft;
            CreateLabel(left.transform, "PaletteDropdownArrow", "v", new Vector2(178f, 386f), new Vector2(36f, 36f), 22);
            CreateFurnitureCategoryButtons(left.transform);

            CreateActionButton(toolbar.transform, "SelectToolButton", "선택", new Vector2(-285f, 0f), new Vector2(150f, 70f), () => { }, 18);
            CreateActionButton(toolbar.transform, "PlaceToolButton", "배치", new Vector2(-125f, 0f), new Vector2(150f, 70f), () => { }, 18);
            CreateActionButton(toolbar.transform, "DeleteSelectedFurnitureButton", "삭제", new Vector2(60f, 0f), new Vector2(150f, 70f), DeleteSelectedFurniture, 18);
            CreateActionButton(toolbar.transform, "RotateFurnitureButton", "회전", new Vector2(245f, 0f), new Vector2(150f, 70f), RotateSelectedFurniture, 18);

            CreateLabel(right.transform, "SettingsTitle", "가구 배치", new Vector2(-34f, 310f), new Vector2(230f, 40f), 24).alignment = TextAnchor.MiddleLeft;
            CreateActionButton(right.transform, "CloseFurniturePanelButton", "X", new Vector2(150f, 310f), new Vector2(44f, 44f), () => gameObject.SetActive(false), 18);
            CreateLabel(right.transform, "PlacementSettingsTitle", "배치 설정", new Vector2(-62f, 258f), new Vector2(220f, 28f), 17).alignment = TextAnchor.MiddleLeft;
            CreateLabel(right.transform, "FurnitureCountLabel", "개수", new Vector2(-120f, 202f), new Vector2(70f, 28f), 16).alignment = TextAnchor.MiddleLeft;
            CreateInput(right.transform, "FurnitureCountInput", "1", new Vector2(58f, 202f), new Vector2(92f, 36f));
            CreateLabel(right.transform, "FurnitureDirectionLabel", "방향", new Vector2(-120f, 146f), new Vector2(70f, 28f), 16).alignment = TextAnchor.MiddleLeft;
            CreateSegment(right.transform, "Direction_N", "N", new Vector2(-30f, 146f));
            CreateSegment(right.transform, "Direction_E", "E", new Vector2(24f, 146f));
            CreateSegment(right.transform, "Direction_S", "S", new Vector2(78f, 146f));
            CreateSegment(right.transform, "Direction_W", "W", new Vector2(132f, 146f));
            CreateSegment(right.transform, "Direction_Auto", "Auto", new Vector2(188f, 146f), new Vector2(68f, 38f));
            CreateLabel(right.transform, "FurnitureRuleLabel", "규칙", new Vector2(-120f, 92f), new Vector2(70f, 28f), 16).alignment = TextAnchor.MiddleLeft;
            CreateDropdown(right.transform, "FurnitureRuleDropdown", new[] { "벽 근처", "아무 곳", "동선 피하기", "창문 근처" }, 0, new Vector2(66f, 92f), new Vector2(210f, 38f));
            CreateLabel(right.transform, "FurnitureOptionsTitle", "상세 옵션", new Vector2(-56f, -6f), new Vector2(230f, 30f), 18).alignment = TextAnchor.MiddleLeft;
            CreateToggle(right.transform, "InteractiveFurnitureToggle", "상호작용 가능 (Interactive)", true, new Vector2(0f, -62f));
            CreateToggle(right.transform, "BlocksMovementToggle", "이동 차단 (Blocks Movement)", true, new Vector2(0f, -120f));
            CreateLabel(right.transform, "FurniturePreviewTitle", "미리보기", new Vector2(-72f, -218f), new Vector2(220f, 30f), 18).alignment = TextAnchor.MiddleLeft;
            var previewFrame = ModernUiPanelBuilder.CreateCommonPanel48(right.transform, "SelectedFurniturePreviewFrame", new Vector2(-82f, -288f), new Vector2(116f, 116f));
            _selectedPreviewImage = CreateImage(previewFrame.transform, "SelectedFurniturePreviewIcon", Vector2.zero, new Vector2(82f, 82f));
            _selectedNameText = CreateLabel(right.transform, "SelectedFurnitureName", "가구를 선택하세요", new Vector2(72f, -254f), new Vector2(170f, 28f), 16);
            _selectedNameText.alignment = TextAnchor.MiddleLeft;
            _selectedSizeText = CreateLabel(right.transform, "SelectedFurnitureSize", string.Empty, new Vector2(72f, -292f), new Vector2(170f, 26f), 15);
            _selectedSizeText.alignment = TextAnchor.MiddleLeft;
            _selectedDirectionText = CreateLabel(right.transform, "SelectedFurnitureDirection", string.Empty, new Vector2(72f, -330f), new Vector2(170f, 26f), 15);
            _selectedDirectionText.alignment = TextAnchor.MiddleLeft;

            CreateLabel(bottom.transform, "SeedLabel", "시드 (Seed)", new Vector2(-780f, 0f), new Vector2(130f, 32f), 20).alignment = TextAnchor.MiddleLeft;
            _seedInput = CreateInput(bottom.transform, "SeedInput", _model.Seed.ToString(CultureInfo.InvariantCulture), new Vector2(-600f, 0f), new Vector2(170f, 40f));
            CreateActionButton(bottom.transform, "RegenerateButton", "R", new Vector2(-470f, 0f), new Vector2(48f, 40f), RegenerateAndApply, 16);
            CreateActionButton(bottom.transform, "ApplyButton", "적용", new Vector2(-410f, 0f), new Vector2(64f, 40f), ApplyCurrentMap, 13);
            CreateActionButton(bottom.transform, "MoveSelectedFurnitureButton", "이동", new Vector2(-320f, 0f), new Vector2(64f, 40f), MoveSelectedFurniture, 13);
            CreateActionButton(bottom.transform, "SaveFurnitureLayoutButton", "저장", new Vector2(-246f, 0f), new Vector2(64f, 40f), SaveFurnitureLayout, 13);
            CreateActionButton(bottom.transform, "ClearFurnitureLayoutButton", "초기화", new Vector2(-164f, 0f), new Vector2(76f, 40f), ClearFurnitureLayout, 13);
            CreateActionButton(bottom.transform, "LoadFurnitureLayoutButton", "불러오기", new Vector2(-66f, 0f), new Vector2(96f, 40f), LoadFurnitureLayout, 13);
            CreateLabel(bottom.transform, "ValidationLabel", "검증 (Validation)", new Vector2(150f, 0f), new Vector2(190f, 32f), 20).alignment = TextAnchor.MiddleLeft;
            _statusText = CreateLabel(bottom.transform, "InteriorPlacementStatusText", "Walkable OK", new Vector2(380f, 0f), new Vector2(260f, 40f), 17);
            ConfigureStatusLabel(_statusText);
            _summaryText = CreateLabel(bottom.transform, "InteriorPlacementSummaryText", string.Empty, new Vector2(650f, 0f), new Vector2(340f, 40f), 13);
            ConfigureStatusLabel(_summaryText);
            _availabilityText = CreateLabel(right.transform, "PlacementAvailabilityText", "가구를 선택하세요", new Vector2(0f, 224f), new Vector2(250f, 26f), 12);
            ConfigureStatusLabel(_availabilityText);
        }

        private void CreateFurnitureCategoryButtons(Transform parent)
        {
            float y = 318f;
            for (int i = 0; i < CategoryOrder.Length; i++)
            {
                var category = CategoryOrder[i];
                var items = CollectFurniture(category);
                if (items.Count == 0)
                {
                    continue;
                }

                CreateLabel(parent, "FurnitureCategory_" + category, GetCategoryLabel(category), new Vector2(-172f, y), new Vector2(96f, 28f), 17).alignment = TextAnchor.MiddleLeft;
                y -= 48f;
                var displayCount = Mathf.Min(items.Count, MaxPaletteItemsPerCategory);
                for (int item = 0; item < displayCount; item++)
                {
                    var position = new Vector2(-150f + (item % 5) * 76f, y - (item / 5) * 76f);
                    CreateFurnitureDefinitionButton(parent, items[item], position);
                }

                y -= Mathf.CeilToInt(displayCount / 5f) * 76f + 18f;
            }
        }

        private List<InteriorFurnitureDefinition> CollectFurniture(InteriorObjectKind kind)
        {
            var items = new List<InteriorFurnitureDefinition>();
            for (int i = 0; i < _furniture.Count; i++)
            {
                if (_furniture[i] != null && _furniture[i].ObjectKind == kind)
                {
                    items.Add(_furniture[i]);
                }
            }

            return items;
        }

        private void CreateFurnitureDefinitionButton(Transform parent, InteriorFurnitureDefinition furniture, Vector2 position)
        {
            var go = ModernUiPanelBuilder.CreateCommonPanel48Button(parent, "PaletteFurniture_" + furniture.Id, position, new Vector2(68f, 68f));
            var icon = CreateImage(go.transform, "Icon", Vector2.zero, new Vector2(52f, 52f));
            icon.sprite = GetPrimarySprite(furniture);
            icon.preserveAspect = true;
            icon.color = icon.sprite != null ? Color.white : new Color(0.2f, 0.22f, 0.26f, 1f);
            go.GetComponent<Button>().onClick.AddListener(() => SelectFurnitureDefinition(furniture));
        }

        private static Sprite GetPrimarySprite(InteriorFurnitureDefinition furniture)
        {
            if (furniture == null || furniture.Tiles == null)
            {
                return null;
            }

            for (int i = 0; i < furniture.Tiles.Count; i++)
            {
                if (furniture.Tiles[i].Tile is Tile tile && tile.sprite != null)
                {
                    return tile.sprite;
                }
            }

            return null;
        }

        private void SelectFurnitureDefinition(InteriorFurnitureDefinition furniture)
        {
            _activeFurniture = furniture;
            if (_selectedNameText != null)
            {
                _selectedNameText.text = furniture != null ? furniture.DisplayName : "가구를 선택하세요";
            }

            if (_selectedSizeText != null)
            {
                _selectedSizeText.text = furniture != null ? GetFootprintText(furniture) : string.Empty;
            }

            if (_selectedDirectionText != null)
            {
                _selectedDirectionText.text = furniture != null ? "방향: Auto" : string.Empty;
            }

            if (_selectedPreviewImage != null)
            {
                _selectedPreviewImage.sprite = GetPrimarySprite(furniture);
                _selectedPreviewImage.color = _selectedPreviewImage.sprite != null ? Color.white : Color.clear;
            }

            SetStatus("Manual " + (furniture != null ? furniture.DisplayName : "Furniture") + ": click a green cell", _summaryText != null ? _summaryText.text : string.Empty);
            UpdateAvailabilityPreview();
        }

        private static string GetFootprintText(InteriorFurnitureDefinition furniture)
        {
            if (furniture == null || furniture.Footprint == null || furniture.Footprint.Count == 0)
            {
                return string.Empty;
            }

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            for (int i = 0; i < furniture.Footprint.Count; i++)
            {
                var cell = furniture.Footprint[i];
                minX = Mathf.Min(minX, cell.x);
                maxX = Mathf.Max(maxX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxY = Mathf.Max(maxY, cell.y);
            }

            return (maxX - minX + 1) + "x" + (maxY - minY + 1);
        }

        private static string GetCategoryLabel(InteriorObjectKind category)
        {
            switch (category)
            {
                case InteriorObjectKind.Desk: return "책상";
                case InteriorObjectKind.Chair: return "의자";
                case InteriorObjectKind.Computer: return "컴퓨터";
                case InteriorObjectKind.Sofa: return "소파";
                case InteriorObjectKind.Plant: return "화분";
                case InteriorObjectKind.Shelf: return "선반";
                case InteriorObjectKind.Light: return "조명";
                default: return "기타";
            }
        }

        private void RegenerateAndApply()
        {
            if (_isClosing)
            {
                return;
            }

            if (_seedInput != null)
            {
                _model.SetSeed(_seedInput.text);
            }

            var result = _model.Regenerate(null);
            if (result.Success)
            {
                ApplyCurrentMap();
                RestoreSavedFurnitureLayoutIfAvailable();
                SetStatus("Walkable OK", result.Summary);
            }
            else
            {
                SetStatus("Failed: " + result.Message, string.Empty);
            }

            UpdateAvailabilityPreview();
        }

        private void ApplyCurrentMap()
        {
            if (_applier == null)
            {
                _applier = FindFirstObjectByType<InteriorTilemapApplier>();
            }

            if (_applier != null && _model.CurrentMap != null)
            {
                _applier.Apply(_model.CurrentMap);
            }
        }

        private void RestoreSavedFurnitureLayoutIfAvailable()
        {
            if (_model.CurrentMap == null)
            {
                return;
            }

            if (_overlay == null)
            {
                _overlay = InteriorPlacementPreviewOverlay.Ensure();
            }

            if (_overlay == null)
            {
                return;
            }

            _overlay.BindGeneratedMapForPersistence(_model.CurrentMap, _applier);
            if (_overlay.HasSavedFurnitureLayout())
            {
                _overlay.LoadFurnitureLayout();
            }
        }

        private void SaveFurnitureLayout()
        {
            if (_overlay == null)
            {
                _overlay = InteriorPlacementPreviewOverlay.Ensure();
            }

            if (_overlay != null && _overlay.SaveFurnitureLayout())
            {
                SetStatus(_overlay.LastMessage, _summaryText != null ? _summaryText.text : string.Empty);
            }
        }

        private void ClearFurnitureLayout()
        {
            if (_overlay == null)
            {
                _overlay = InteriorPlacementPreviewOverlay.Ensure();
            }

            if (_overlay != null && _overlay.ClearPlacedFurniture())
            {
                SetStatus(_overlay.LastMessage, _summaryText != null ? _summaryText.text : string.Empty);
            }
        }

        private void LoadFurnitureLayout()
        {
            if (_overlay == null)
            {
                _overlay = InteriorPlacementPreviewOverlay.Ensure();
            }

            if (_overlay != null && _overlay.LoadFurnitureLayout())
            {
                SetStatus(_overlay.LastMessage, _summaryText != null ? _summaryText.text : string.Empty);
            }
        }

        private void RotateSelectedFurniture()
        {
            if (_overlay == null)
            {
                _overlay = InteriorPlacementPreviewOverlay.Ensure();
            }

            if (_overlay == null)
            {
                return;
            }

            var direction = _overlay.RotateActiveFurniture();
            if (_selectedDirectionText != null)
            {
                _selectedDirectionText.text = "방향: " + direction;
            }

            SetStatus("Direction " + direction, _summaryText != null ? _summaryText.text : string.Empty);
        }

        private void MoveSelectedFurniture()
        {
            if (_overlay == null)
            {
                _overlay = InteriorPlacementPreviewOverlay.Ensure();
            }

            if (_overlay != null && _overlay.MoveSelectedFurniture())
            {
                SetStatus("Moved furniture", _summaryText != null ? _summaryText.text : string.Empty);
            }
        }

        private void DeleteSelectedFurniture()
        {
            if (_overlay == null)
            {
                _overlay = InteriorPlacementPreviewOverlay.Ensure();
            }

            if (_overlay != null && _overlay.DeleteSelectedFurniture())
            {
                SetStatus("Deleted furniture", _summaryText != null ? _summaryText.text : string.Empty);
            }
        }

        private void UpdateAvailabilityPreview()
        {
            if (_isClosing || !isActiveAndEnabled || _model.CurrentMap == null)
            {
                return;
            }

            if (_activeFurniture == null)
            {
                if (_availabilityText != null)
                {
                    _availabilityText.text = "가구를 선택하세요";
                }

                return;
            }

            var request = InteriorFurniturePlacementRequest.CreateForTests(_activeFurniture.ObjectKind, 1, _activeFurniture.PlacementPreference, InteriorFacingDirection.None, false);
            var summary = InteriorPlacementAvailability.Summarize(_model.CurrentMap, request);
            if (_availabilityText != null)
            {
                _availabilityText.text = _activeFurniture.DisplayName + ": " + summary.Message + (summary.HasEnoughCandidates ? "" : " - not enough");
            }

            if (_overlay == null)
            {
                _overlay = InteriorPlacementPreviewOverlay.Ensure();
            }

            if (_overlay != null)
            {
                _overlay.RefreshFurniture(_model.CurrentMap, request, _applier, _activeFurniture);
            }
        }

        private void SetStatus(string status, string summary)
        {
            if (_statusText != null)
            {
                _statusText.text = status;
            }

            if (_summaryText != null)
            {
                _summaryText.text = summary;
            }
        }

        private static Text CreateLabel(Transform parent, string name, string text, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            ConfigureRect(go.GetComponent<RectTransform>(), position, size);
            var label = go.GetComponent<Text>();
            label.text = text;
            label.font = ResolveFont();
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.95f, 0.93f, 0.84f, 1f);
            label.raycastTarget = false;
            return label;
        }

        private static Image CreateImage(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            ConfigureRect(go.GetComponent<RectTransform>(), position, size);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static void ConfigureStatusLabel(Text label)
        {
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 9;
            label.resizeTextMaxSize = label.fontSize;
        }

        private static Font ResolveFont()
        {
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return _font;
        }

        private static InputField CreateInput(Transform parent, string name, string text, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            ConfigureRect(go.GetComponent<RectTransform>(), position, size);
            go.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.08f, 0.78f);
            var value = CreateLabel(go.transform, "Text", text, Vector2.zero, size - new Vector2(12f, 4f), 15);
            value.alignment = TextAnchor.MiddleLeft;
            var placeholder = CreateLabel(go.transform, "Placeholder", string.Empty, Vector2.zero, size - new Vector2(12f, 4f), 15);
            var input = go.GetComponent<InputField>();
            input.textComponent = value;
            input.placeholder = placeholder;
            input.text = text;
            return input;
        }

        private static Dropdown CreateDropdown(Transform parent, string name, string[] options, int value, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Dropdown));
            go.transform.SetParent(parent, false);
            ConfigureRect(go.GetComponent<RectTransform>(), position, size);
            go.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.12f, 0.86f);
            var label = CreateLabel(go.transform, "Label", string.Empty, Vector2.zero, size - new Vector2(10f, 2f), 12);
            var dropdown = go.GetComponent<Dropdown>();
            dropdown.captionText = label;
            dropdown.options.Clear();
            for (int i = 0; i < options.Length; i++)
            {
                dropdown.options.Add(new Dropdown.OptionData(options[i]));
            }
            dropdown.value = Mathf.Clamp(value, 0, options.Length - 1);
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private static Toggle CreateToggle(Transform parent, string name, string text, bool value, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent, false);
            ConfigureRect(go.GetComponent<RectTransform>(), position, new Vector2(270f, 32f));
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(go.transform, false);
            ConfigureRect(background.GetComponent<RectTransform>(), new Vector2(116f, 0f), new Vector2(42f, 24f));
            background.GetComponent<Image>().color = new Color(0.2f, 0.38f, 0.68f, 1f);
            var checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkmark.transform.SetParent(background.transform, false);
            ConfigureRect(checkmark.GetComponent<RectTransform>(), new Vector2(9f, 0f), new Vector2(18f, 18f));
            checkmark.GetComponent<Image>().color = new Color(0.9f, 0.9f, 0.86f, 1f);
            CreateLabel(go.transform, "Label", text, new Vector2(-42f, 0f), new Vector2(190f, 28f), 15).alignment = TextAnchor.MiddleLeft;
            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = background.GetComponent<Image>();
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.isOn = value;
            return toggle;
        }

        private static void CreateSegment(Transform parent, string name, string text, Vector2 position)
        {
            CreateSegment(parent, name, text, position, new Vector2(52f, 38f));
        }

        private static void CreateSegment(Transform parent, string name, string text, Vector2 position, Vector2 size)
        {
            CreateActionButton(parent, name, text, position, size, () => { }, 13);
        }

        private static void CreateActionButton(Transform parent, string name, string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action, int fontSize = 12)
        {
            var go = ModernUiPanelBuilder.CreateCommonPanel48Button(parent, name, position, size);
            CreateLabel(go.transform, "Label", text, Vector2.zero, size - new Vector2(12f, 6f), fontSize);
            go.GetComponent<Button>().onClick.AddListener(action);
        }

        private static void ConfigureRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }

    public sealed class HouseInteriorPlacementUiRuntimeInstaller : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoaded()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePanelAfterInitialSceneLoad()
        {
            EnsurePanel(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsurePanel(scene);
        }

        private static void EnsurePanel(Scene scene)
        {
            if (scene.name != "House")
            {
                return;
            }

            EnsureEventSystem(scene);
            if (FindFirstObjectByType<InteriorFurniturePlacementPanel>() != null)
            {
                return;
            }

            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("HouseInteriorPlacementCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(canvasGo, scene);
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            else if (canvas.GetComponent<GraphicRaycaster>() == null)
            {
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            var applier = FindFirstObjectByType<InteriorTilemapApplier>();
            InteriorFurniturePlacementPanel.Create(canvas, applier);
        }

        private static void EnsureEventSystem(Scene scene)
        {
            if (EventSystem.current != null)
            {
                UiInputModuleInstaller.AddPreferredInputModule(EventSystem.current.gameObject);
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem));
            SceneManager.MoveGameObjectToScene(go, scene);
            UiInputModuleInstaller.AddPreferredInputModule(go);
        }
    }
}
