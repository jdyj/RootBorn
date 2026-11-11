using System;
using System.Collections.Generic;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Ui;

namespace Rootborn.Screens.Furniture
{
    /// <summary>
    /// 가구 배치(Unity InteriorFurniturePlacementPanel + InteriorPlacementPreviewOverlay, 지은 집 방식). 집의 HUD 버튼(가구 배치 켬/끔)이
    /// 열고 닫는다. 열려 있는 동안 배치 카메라가 켜진다(Host). 이동을 막지 않는다(Unity 판: 패널이 입력을 잠그지 않았다).
    ///
    /// 화면 구성(Unity 판 수치, 개발용 조작 — 시드, 다시 만들기, 검증 글자, 저장·초기화·불러오기 — 은 옮기지 않는다):
    /// - 왼쪽 팔레트: 제목, 검색 칸(이름 또는 id), 가구 20칸(5x4, 한 쪽), 이전·다음, 쪽 글자.
    /// - 오른쪽: 제목, 닫기, 고른 가구의 그림·이름·크기·방향.
    /// - 위 도구 줄: 삭제, 회전. 아래 줄: 이동.
    ///
    /// 월드 위(Host가 UI 위가 아닌 포인터를 WorldPointer로 넘긴다): 고른 가구가 있으면 바닥 칸마다 놓을 수 있는지(초록·빨강),
    /// 포인터 칸에 차지 칸과 그림 미리보기(놓을 수 있으면 흰빛, 없으면 붉은빛). 누르면: 놓인 가구 칸이면 그 가구를 고르고, 옮기는 중이면
    /// 그 칸으로 옮기고, 아니면 고른 가구를 놓는다. 놓을 수 있는지는 언제나 Core(House.CanPlace)가 판단한다 — 화면은 다시 판정하지 않는다.
    /// </summary>
    public sealed class FurniturePlacementScreen : GameScreen
    {
        /// <summary>배치 — Unity InteriorFurniturePlacementPanel.BuildUi의 수치(1920x1080, 가운데 기준). 새 디자인에서는 여기만 고친다.</summary>
        public static class Metrics
        {
            public const int PageSize = 20;
            public const int Columns = 5;

            public const float PaletteX = -740f, PaletteY = 0f, PaletteW = 430f, PaletteH = 860f;
            public const float TitleX = -52f, TitleY = 386f, TitleW = 270f, TitleH = 42f, TitleFont = 24f;
            public const float SearchX = 0f, SearchY = 296f, SearchW = 368f, SearchH = 64f, SearchFont = 18f;
            public const int SearchMaxLength = 100;
            public const float SlotX0 = -144f, SlotY0 = 168f, SlotPitchX = 72f, SlotPitchY = 88f, SlotSize = 64f, SlotIcon = 48f;
            public const float PageButtonX = 112f, PageButtonY = -248f, PageButtonW = 128f, PageButtonH = 48f, PageButtonFont = 18f;
            public const float PageTextX = 0f, PageTextY = -324f, PageTextW = 368f, PageTextH = 64f, PageTextFont = 18f;

            public const float SettingsX = 760f, SettingsY = 0f, SettingsW = 360f, SettingsH = 720f;
            public const float SettingsTitleX = -34f, SettingsTitleY = 310f, SettingsTitleW = 230f, SettingsTitleH = 40f;
            public const float CloseX = 150f, CloseY = 310f, CloseSize = 44f;
            public const float PreviewX = -82f, PreviewY = -288f, PreviewSize = 116f, PreviewIcon = 82f;
            public const float InfoX = 72f, InfoW = 170f;
            public const float NameY = -254f, NameH = 28f, NameFont = 16f;
            public const float SizeY = -292f, DirectionY = -330f, InfoH = 26f, InfoFont = 15f;

            public const float ToolbarX = 0f, ToolbarY = 430f, ToolbarW = 760f, ToolbarH = 86f;
            public const float DeleteX = 60f, RotateX = 245f, ToolButtonW = 150f, ToolButtonH = 70f, ToolFont = 18f;
            public const float BottomX = 0f, BottomY = -490f, BottomW = 1760f, BottomH = 86f;
            /// <summary>Unity 글자 13 → 규약의 최소 14.</summary>
            public const float MoveX = -320f, MoveW = 64f, MoveH = 40f, MoveFont = 14f;

            /// <summary>닫기 버튼 기호(언어와 무관).</summary>

            // 미리보기 색(Unity InteriorPlacementPreviewOverlay).
            public static readonly UiColor ValidCell = UnityLayout.Color(0.15f, 0.95f, 0.35f, 0.38f);
            public static readonly UiColor InvalidCell = UnityLayout.Color(0.95f, 0.18f, 0.12f, 0.24f);
            public static readonly UiColor ValidGhost = UnityLayout.Color(1f, 1f, 1f, 0.62f);
            public static readonly UiColor InvalidGhost = UnityLayout.Color(1f, 0.25f, 0.2f, 0.48f);
            public static readonly UiColor ValidFootprint = UnityLayout.Color(0.15f, 0.95f, 0.95f, 0.44f);
            public static readonly UiColor InvalidFootprint = UnityLayout.Color(1f, 0.2f, 0.12f, 0.42f);
            public static readonly UiColor Ink = UnityLayout.Color(0.16f, 0.12f, 0.08f);
        }

        public const string RootId = "furniture";
        public const string PaletteId = "furniture.palette";
        public const string SearchId = "furniture.search";
        public const string PreviousId = "furniture.page.previous";
        public const string NextId = "furniture.page.next";
        public const string PageTextId = "furniture.page.text";
        public const string CloseId = "furniture.close";
        public const string SelectedNameId = "furniture.selected.name";
        public const string SelectedSizeId = "furniture.selected.size";
        public const string SelectedDirectionId = "furniture.selected.direction";
        public const string SelectedIconId = "furniture.selected.icon";
        public const string DeleteId = "furniture.delete";
        public const string RotateId = "furniture.rotate";
        public const string MoveId = "furniture.move";
        public static string SlotId(int slot) => "furniture.slot." + slot;

        private static readonly FurniturePlacementDirection[] Directions =
            { FurniturePlacementDirection.North, FurniturePlacementDirection.East, FurniturePlacementDirection.South, FurniturePlacementDirection.West };

        private readonly FurniturePaletteModel _model;
        private readonly UiSlot[] _slots = new UiSlot[Metrics.PageSize];
        private readonly UiFittedImage[] _icons = new UiFittedImage[Metrics.PageSize];
        private UiTextField _search;
        private UiButton _previous, _next, _close, _delete, _rotate, _move;
        private UiTextBlock _pageText;
        private UiLabel _title, _settingsTitle, _name, _size, _direction;
        private UiFittedImage _selectedIcon;

        private FurnitureArt _art;
        private IPlacementView _view;

        private bool _directionChosen;
        private bool _overlayVisible;
        private bool[] _validCells;
        private bool _validDirty = true;
        private bool _hasHover;
        private GridPoint _hover;
        private bool _hoverValid;
        private bool _ghostHeld;
        private GridPoint _ghostHeldCell;
        private bool _wasDown;

        public FurniturePlacementScreen(ScreenServices services) : base(services)
        {
            _model = new FurniturePaletteModel(Metrics.PageSize, Name);
        }

        public FurniturePaletteModel Palette => _model;
        /// <summary>고른 가구(팔레트에서 고르거나 옮기려고 집은 것). 없으면 null.</summary>
        public FurnitureDefinition Active { get; private set; }
        public FurniturePlacementDirection Direction { get; private set; } = FurniturePlacementDirection.North;
        /// <summary>고른 놓인 가구(House.Placed의 InstanceId). 없으면 null.</summary>
        public string SelectedInstanceId { get; private set; }
        /// <summary>옮기려고 집어 든 중(다음 클릭이 옮길 칸).</summary>
        public bool Carrying { get; private set; }
        /// <summary>바닥 칸마다 놓을 수 있는지 보여 주는 중.</summary>
        public bool OverlayVisible => _overlayVisible && Active != null;
        /// <summary>포인터가 있는 칸(월드 위일 때). 없으면 null.</summary>
        public GridPoint? Hover => _hasHover ? _hover : (GridPoint?)null;
        /// <summary>포인터 칸에 지금 가구를 놓을 수 있는가(Core 판단). 포인터 칸이 없으면 false.</summary>
        public bool HoverValid => _hasHover && _hoverValid;
        /// <summary>바닥 칸 중 놓을 수 있는 칸 수(보여 주는 중일 때).</summary>
        public int ValidCellCount { get; private set; }
        /// <summary>마지막 월드 클릭이 한 일(시험용): placed, moved, selected, rejected, none.</summary>
        public string LastAction { get; private set; } = "none";

        /// <summary>Host가 잇는다: 가구 그림 좌표표와 월드 좌표 바꾸기. 열기 전에 한 번.</summary>
        public void Attach(FurnitureArt art, IPlacementView view)
        {
            _art = art;
            _view = view;
            _validDirty = true;
        }

        /// <summary>가구 이름: 이름 키 → id로 만든 규칙 키(DataTextKeys) → 데이터의 이름 → id. 지금 데이터는 이름 키가 없어 데이터의 한국어 이름이다.</summary>
        private string Name(FurnitureDefinition f) => f == null ? string.Empty : NameOf(f.DisplayNameKey, f.StableId, f.DisplayName);

        protected override UiNode Build()
        {
            var root = new UiNode { Id = RootId };

            UiPanel palette = root.Add(new UiPanel { Id = PaletteId }).Centered(Metrics.PaletteX, Metrics.PaletteY, Metrics.PaletteW, Metrics.PaletteH);
            _title = palette.Add(new UiLabel { Id = "furniture.title", FontSize = Metrics.TitleFont, Color = Metrics.Ink })
                .Centered(Metrics.TitleX, Metrics.TitleY, Metrics.TitleW, Metrics.TitleH);
            _search = palette.Add(new UiTextField { Id = SearchId, FontSize = Metrics.SearchFont, MaxLength = Metrics.SearchMaxLength })
                .Centered(Metrics.SearchX, Metrics.SearchY, Metrics.SearchW, Metrics.SearchH);
            _search.TextChanged += OnSearchChanged;
            for (int i = 0; i < Metrics.PageSize; i++)
            {
                int slot = i;
                float x = Metrics.SlotX0 + (i % Metrics.Columns) * Metrics.SlotPitchX, y = Metrics.SlotY0 - (i / Metrics.Columns) * Metrics.SlotPitchY;
                _slots[i] = palette.Add(new UiSlot { Id = SlotId(i) }).Centered(x, y, Metrics.SlotSize, Metrics.SlotSize);
                _icons[i] = _slots[i].Add(new UiFittedImage { Id = SlotId(i) + ".icon" }).Centered(0f, 0f, Metrics.SlotIcon, Metrics.SlotIcon);
                _slots[i].Clicked += () => SelectFromPalette(_model.At(slot));
            }
            _previous = palette.Add(new UiButton { Id = PreviousId, FontSize = Metrics.PageButtonFont })
                .Centered(-Metrics.PageButtonX, Metrics.PageButtonY, Metrics.PageButtonW, Metrics.PageButtonH);
            _previous.Clicked += () => ShowPage(_model.Page - 1);
            _next = palette.Add(new UiButton { Id = NextId, FontSize = Metrics.PageButtonFont })
                .Centered(Metrics.PageButtonX, Metrics.PageButtonY, Metrics.PageButtonW, Metrics.PageButtonH);
            _next.Clicked += () => ShowPage(_model.Page + 1);
            _pageText = palette.Add(new UiTextBlock { Id = PageTextId, FontSize = Metrics.PageTextFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(Metrics.PageTextX, Metrics.PageTextY, Metrics.PageTextW, Metrics.PageTextH);

            UiPanel settings = root.Add(new UiPanel { Id = "furniture.settings" }).Centered(Metrics.SettingsX, Metrics.SettingsY, Metrics.SettingsW, Metrics.SettingsH);
            _settingsTitle = settings.Add(new UiLabel { Id = "furniture.settings.title", FontSize = Metrics.TitleFont, Color = Metrics.Ink })
                .Centered(Metrics.SettingsTitleX, Metrics.SettingsTitleY, Metrics.SettingsTitleW, Metrics.SettingsTitleH);
            _close = settings.Add(new UiButton { Id = CloseId, Icon = Rootborn.Screens.Day.ScreenIcons.Close })
                .Centered(Metrics.CloseX, Metrics.CloseY, Metrics.CloseSize, Metrics.CloseSize);
            _close.Clicked += Close;
            UiPanel frame = settings.Add(new UiPanel { Id = "furniture.selected.frame" }).Centered(Metrics.PreviewX, Metrics.PreviewY, Metrics.PreviewSize, Metrics.PreviewSize);
            _selectedIcon = frame.Add(new UiFittedImage { Id = SelectedIconId }).Centered(0f, 0f, Metrics.PreviewIcon, Metrics.PreviewIcon);
            _name = settings.Add(new UiLabel { Id = SelectedNameId, FontSize = Metrics.NameFont, Color = Metrics.Ink })
                .Centered(Metrics.InfoX, Metrics.NameY, Metrics.InfoW, Metrics.NameH);
            _size = settings.Add(new UiLabel { Id = SelectedSizeId, FontSize = Metrics.InfoFont, Color = Metrics.Ink })
                .Centered(Metrics.InfoX, Metrics.SizeY, Metrics.InfoW, Metrics.InfoH);
            _direction = settings.Add(new UiLabel { Id = SelectedDirectionId, FontSize = Metrics.InfoFont, Color = Metrics.Ink })
                .Centered(Metrics.InfoX, Metrics.DirectionY, Metrics.InfoW, Metrics.InfoH);

            UiPanel toolbar = root.Add(new UiPanel { Id = "furniture.toolbar" }).Centered(Metrics.ToolbarX, Metrics.ToolbarY, Metrics.ToolbarW, Metrics.ToolbarH);
            _delete = toolbar.Add(new UiButton { Id = DeleteId, FontSize = Metrics.ToolFont }).Centered(Metrics.DeleteX, 0f, Metrics.ToolButtonW, Metrics.ToolButtonH);
            _delete.Clicked += DeleteSelected;
            _rotate = toolbar.Add(new UiButton { Id = RotateId, FontSize = Metrics.ToolFont }).Centered(Metrics.RotateX, 0f, Metrics.ToolButtonW, Metrics.ToolButtonH);
            _rotate.Clicked += Rotate;

            UiPanel bottom = root.Add(new UiPanel { Id = "furniture.bottom" }).Centered(Metrics.BottomX, Metrics.BottomY, Metrics.BottomW, Metrics.BottomH);
            _move = bottom.Add(new UiButton { Id = MoveId, FontSize = Metrics.MoveFont }).Centered(Metrics.MoveX, 0f, Metrics.MoveW, Metrics.MoveH);
            _move.Clicked += PickUpSelected;

            ApplyTexts();
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            _wasDown = true; // 버튼을 눌러 연 그 누름이 월드 클릭이 되지 않게
            _hasHover = false;
            _ghostHeld = false;
            _overlayVisible = Active != null;
            _validDirty = true;
            ReloadFurniture();
        }

        protected internal override void OnClosed()
        {
            // Unity: 끄면 미리보기(고른 놓인 가구, 집어 든 것)를 버린다. 팔레트에서 고른 가구는 남는다.
            SelectedInstanceId = null;
            Carrying = false;
            _hasHover = false;
            if (Services.Screens?.Tree.Focused == _search) Services.Screens.Tree.SetFocus(null);
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language) { ApplyTexts(); return; }
            if (topic == ChangeTopics.All) ReloadFurniture();
            if (topic == ChangeTopics.Furniture || topic == ChangeTopics.World || topic == ChangeTopics.Map || topic == ChangeTopics.All)
            {
                _validDirty = true;
                _hasHover = false;
                if (SelectedInstanceId != null && FindPlaced(SelectedInstanceId) == null) { SelectedInstanceId = null; Carrying = false; }
            }
        }

        private void ReloadFurniture()
        {
            _model.SetFurniture(Services.Session?.AvailableFurniture());
            _model.Filter(_search.Text);
            RefreshPage();
        }

        private void ApplyTexts()
        {
            _title.Text = T("ui.furniture.title");
            _settingsTitle.Text = T("ui.furniture.title");
            _search.Placeholder = T("ui.furniture.search.placeholder");
            _previous.Text = T("ui.furniture.page.previous");
            _next.Text = T("ui.furniture.page.next");
            _delete.Text = T("ui.furniture.delete");
            _rotate.Text = T("ui.furniture.rotate");
            _move.Text = T("ui.furniture.move");
            RefreshPage();
            RefreshSelected();
        }

        private void ShowPage(int page)
        {
            _model.ShowPage(page);
            RefreshPage();
        }

        private void RefreshPage()
        {
            for (int i = 0; i < Metrics.PageSize; i++)
            {
                FurnitureDefinition f = _model.At(i);
                _slots[i].Visible = f != null;
                _slots[i].Selected = f != null && f == Active;
                ShowIcon(_icons[i], f);
            }
            _previous.Enabled = _model.HasPrevious;
            _next.Enabled = _model.HasNext;
            _pageText.Text = _model.IsEmpty ? T("ui.furniture.page.empty") : F("ui.furniture.page", _model.Page + 1, _model.PageCount, _model.FilteredCount);
        }

        private void ShowIcon(UiFittedImage icon, FurnitureDefinition f)
        {
            if (f != null && _art != null && _art.TryPrimary(f, out FurnitureTilePart part, out FurnitureSprite sprite))
                icon.Show(FurnitureArt.ImageId(part), sprite.W, sprite.H);
            else if (f != null && f.TileParts.Count > 0)
                icon.Show(FurnitureArt.ImageId(f.TileParts[0]), 0, 0); // 좌표표가 없으면 칸에 맞춰 늘린다
            else icon.Clear();
        }

        private void RefreshSelected()
        {
            if (_name == null) return;
            _name.Text = Active != null ? Name(Active) : T("ui.furniture.select-prompt");
            if (Active != null)
            {
                (int w, int h) = Extent(Active.FootprintCells);
                _size.Text = F("ui.furniture.size", w, h);
                _direction.Text = F("ui.furniture.direction", T(_directionChosen ? DirectionKey(Direction) : "ui.furniture.direction.auto"));
            }
            else
            {
                _size.Text = string.Empty;
                _direction.Text = string.Empty;
            }
            ShowIcon(_selectedIcon, Active);
            for (int i = 0; i < Metrics.PageSize; i++) _slots[i].Selected = Active != null && _model.At(i) == Active;
        }

        private static string DirectionKey(FurniturePlacementDirection d) => "ui.furniture.direction." + d.ToString().ToLowerInvariant();

        private static (int, int) Extent(IReadOnlyList<GridPoint> cells)
        {
            if (cells == null || cells.Count == 0) return (0, 0);
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (GridPoint c in cells)
            {
                minX = Math.Min(minX, c.X); maxX = Math.Max(maxX, c.X);
                minY = Math.Min(minY, c.Y); maxY = Math.Max(maxY, c.Y);
            }
            return (maxX - minX + 1, maxY - minY + 1);
        }

        // ---------- 조작 ----------

        private void SelectFromPalette(FurnitureDefinition f)
        {
            if (f == null) return;
            Active = f;
            Carrying = false; // Unity는 집어 든 채 다른 가구를 고르면 그 가구로 옮기려 했다 — 고르면 집어 든 것을 내려놓는다
            _overlayVisible = true;
            _validDirty = true;
            _hasHover = false;
            RefreshSelected();
        }

        private void Rotate()
        {
            int index = Array.IndexOf(Directions, Direction);
            Direction = Directions[(index + 1) % Directions.Length];
            _directionChosen = true;
            _overlayVisible = true;
            _validDirty = true;
            _hasHover = false;
            RefreshSelected();
        }

        private void DeleteSelected()
        {
            if (SelectedInstanceId == null || Services.Session == null) return;
            string id = SelectedInstanceId;
            bool removed = Services.Session.RemoveFurniture(id);
            if (!removed) return;
            SelectedInstanceId = null;
            Carrying = false;
            _overlayVisible = true;
        }

        /// <summary>Unity MoveSelectedFurniture: 고른 놓인 가구를 집어 든다. 다음 클릭한 칸으로 옮긴다.</summary>
        private void PickUpSelected()
        {
            PlacedFurniture placed = FindPlaced(SelectedInstanceId);
            if (placed == null) return;
            Active = placed.Definition;
            Direction = placed.Direction;
            _directionChosen = true;
            Carrying = true;
            _overlayVisible = true;
            _validDirty = true;
            _hasHover = false;
            RefreshSelected();
        }

        private PlacedFurniture FindPlaced(string instanceId)
        {
            HouseFurnitureLayout house = Services.Session?.House;
            if (house == null || instanceId == null) return null;
            foreach (PlacedFurniture p in house.Placed) if (p.InstanceId == instanceId) return p;
            return null;
        }

        /// <summary>
        /// Host가 프레임마다(이 화면이 열려 있을 때) 넘기는 포인터. overUi면 포인터가 UI 위에 있다(미리보기를 지우고 누름은 월드로 가지 않는다).
        /// 누름은 누르는 순간 한 번만 처리한다(Unity: 왼쪽 버튼을 누른 프레임).
        /// </summary>
        public void WorldPointer(float x, float y, bool down, bool overUi)
        {
            bool pressed = down && !_wasDown;
            _wasDown = down;
            if (overUi || _view == null || _view.Room == null || !_view.TryCellAt(x, y, out GridPoint cell))
            {
                _hasHover = false;
                return;
            }
            if (!_hasHover || !_hover.Equals(cell))
            {
                _hover = cell;
                _hasHover = true;
                _hoverValid = Active != null && CanPlace(Active, cell);
                if (_ghostHeld && !_ghostHeldCell.Equals(cell)) _ghostHeld = false;
            }
            if (pressed) Press(cell);
        }

        private bool CanPlace(FurnitureDefinition f, GridPoint anchor)
        {
            HouseFurnitureLayout house = Services.Session?.House;
            return house != null && house.CanPlace(f, anchor, Direction);
        }

        private void Press(GridPoint cell)
        {
            HouseFurnitureLayout house = Services.Session?.House;
            if (house == null || Services.Session == null) return;
            if (house.TryFindAt(cell, out PlacedFurniture there))
            {
                SelectedInstanceId = there.InstanceId;
                LastAction = "selected";
                return;
            }

            FurniturePlacementDirection direction = Direction;
            if (Carrying && SelectedInstanceId != null)
            {
                string id = SelectedInstanceId;
                bool moved = Services.Session.MoveFurniture(id, cell, direction);
                LastAction = moved ? "moved" : "rejected";
                if (!moved) { _overlayVisible = true; return; }
                Carrying = false;
                AfterCommit(cell);
                return;
            }

            if (Active == null) return;
            string furnitureId = Active.StableId;
            bool placed = Services.Session.PlaceFurniture(furnitureId, cell, direction);
            LastAction = placed ? "placed" : "rejected";
            if (!placed) { _overlayVisible = true; return; }
            AfterCommit(cell);
        }

        /// <summary>놓거나 옮긴 뒤: 그 가구를 고르고(회전·삭제·이동이 그 가구에), 미리보기는 포인터가 그 칸을 떠날 때까지 숨긴다.</summary>
        private void AfterCommit(GridPoint cell)
        {
            HouseFurnitureLayout house = Services.Session.House;
            SelectedInstanceId = house.TryFindAt(cell, out PlacedFurniture placed) ? placed.InstanceId : null;
            _overlayVisible = false;
            _ghostHeld = true;
            _ghostHeldCell = cell;
            _hasHover = false;
        }

        // ---------- 월드 위 그리기 ----------

        private void RecomputeValidCells()
        {
            _validDirty = false;
            ValidCellCount = 0;
            FurnitureRoom room = _view?.Room;
            if (room == null || Active == null) { _validCells = null; return; }
            IReadOnlyList<GridPoint> floor = room.FloorCells;
            if (_validCells == null || _validCells.Length != floor.Count) _validCells = new bool[floor.Count];
            for (int i = 0; i < floor.Count; i++)
            {
                _validCells[i] = CanPlace(Active, floor[i]);
                if (_validCells[i]) ValidCellCount++;
            }
        }

        /// <summary>검색 칸 글자가 바뀌면 거른다(같은 검색어면 아무것도 하지 않는다).</summary>
        private void OnSearchChanged(string text)
        {
            if (string.Equals((text ?? string.Empty).Trim(), _model.Query, StringComparison.Ordinal)) return;
            _model.Filter(text);
            RefreshPage();
        }

        /// <summary>
        /// 월드 위 미리보기(놓을 수 있는 칸 색, 포인터 칸의 차지 칸과 그림)를 그린다. Host가 월드를 그린 뒤, 화면들(HUD·패널)보다 먼저
        /// 따로 그린다 — Unity 판처럼 월드의 한 층이라 HUD와 패널을 덮지 않는다. 열려 있을 때만 부른다. 좌표는 논리 좌표.
        /// </summary>
        public void EmitWorldOverlay(DrawList list)
        {
            if (!IsOpen) return;
            FurnitureRoom room = _view?.Room;
            if (room == null || Active == null || Services.Session?.House == null) return;
            if (_validDirty) RecomputeValidCells();

            if (_overlayVisible && _validCells != null)
            {
                IReadOnlyList<GridPoint> floor = room.FloorCells;
                for (int i = 0; i < floor.Count; i++)
                    list.Fill(_view.CellRect(floor[i]), _validCells[i] ? Metrics.ValidCell : Metrics.InvalidCell);
            }

            if (!_hasHover || (_ghostHeld && _ghostHeldCell.Equals(_hover))) return;
            foreach (GridPoint local in Active.GetFootprintCells(Direction))
                list.Fill(_view.CellRect(new GridPoint(_hover.X + local.X, _hover.Y + local.Y)), _hoverValid ? Metrics.ValidFootprint : Metrics.InvalidFootprint);
            if (_art == null) return;
            foreach (FurnitureTilePart part in Active.GetTileParts(Direction))
            {
                if (!_art.TryGet(part, out FurnitureSprite sprite)) continue;
                UiRect cellRect = _view.CellRect(new GridPoint(_hover.X + part.LocalCell.X, _hover.Y + part.LocalCell.Y));
                list.Image(SpriteRect(cellRect, sprite), FurnitureArt.ImageId(part), default, _hoverValid ? Metrics.ValidGhost : Metrics.InvalidGhost);
            }
        }

        /// <summary>그림의 기준점을 칸 가운데에 맞춘 화면 사각형(Unity 타일: 스프라이트 기준점이 칸 가운데). 크기는 그림 픽셀 / PPU 칸.</summary>
        public static UiRect SpriteRect(UiRect cell, FurnitureSprite sprite)
        {
            float w = sprite.WidthCells * cell.W, h = sprite.HeightCells * cell.H;
            return new UiRect(cell.CenterX - w * sprite.PivotX, cell.CenterY - h * sprite.PivotY, w, h);
        }
    }
}
