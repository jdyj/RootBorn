using System;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Workspaces;
using Rootborn.Game;
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
            /// <summary>제목: 팔레트 안쪽(테마 여백 — PanelArea) 왼쪽 위.</summary>
            public const float TitleW = 270f, TitleH = 42f, TitleFont = 24f;
            public const float SearchX = 0f, SearchY = 248f, SearchW = 368f, SearchH = 64f, SearchFont = 18f;
            public const int SearchMaxLength = 100;
            public const float SlotX0 = -144f, SlotY0 = 120f, SlotPitchX = 72f, SlotPitchY = 88f, SlotSize = 64f, SlotIcon = 48f;
            /// <summary>분류 줄(가구 / 보관): 제목 바로 아래(팔레트 안쪽 위 + 제목 높이 + CategoryGap), 단추 둘을 가운데에 나란히. 가구 분류의 검색·칸은 이 줄 아래로 내렸다.</summary>
            public const float CategoryW = 150f, CategoryH = 56f, CategoryPitch = 164f, CategoryGap = 8f, CategoryFont = 20f;
            /// <summary>보관 분류: 목록(보이는 줄만 그린다), 비었을 때 안내, 같이 쓰는 집의 이유 줄.</summary>
            public const float StoredListX = 0f, StoredListY = 20f, StoredListW = 368f, StoredListH = 460f, StoredRowH = 48f, StoredFont = 18f;
            public const float StoredEmptyY = 20f, StoredEmptyW = 320f, StoredEmptyH = 120f, StoredEmptyFont = 18f;
            public const float StoredReasonY = -286f, StoredReasonW = 368f, StoredReasonH = 100f, StoredReasonFont = 16f;

            public const float PageButtonX = 112f, PageButtonY = -248f, PageButtonW = 128f, PageButtonH = 48f, PageButtonFont = 18f;
            public const float PageTextX = 0f, PageTextY = -324f, PageTextW = 368f, PageTextH = 64f, PageTextFont = 18f;

            /// <summary>Unity 판은 SettingsY 0이었다. 위쪽 체력·집중 HUD와 맞닿아 한 덩어리로 보여 16 내렸다(겹침 검사의 최소 틈 8).</summary>
            public const float SettingsX = 760f, SettingsY = -16f, SettingsW = 360f, SettingsH = 720f;
            /// <summary>설정 패널 제목: 안쪽(테마 여백) 왼쪽 위.</summary>
            public const float SettingsTitleW = 230f, SettingsTitleH = 40f;
            public const float CloseX = 150f, CloseY = 310f, CloseSize = 44f;
            public const float PreviewX = -82f, PreviewY = -288f, PreviewSize = 116f, PreviewIcon = 82f;
            public const float InfoX = 72f, InfoW = 170f;
            public const float NameY = -254f, NameH = 28f, NameFont = 16f;
            public const float SizeY = -292f, DirectionY = -330f, InfoH = 26f, InfoFont = 15f;
            /// <summary>같이 쓰는 집: 방의 허락을 기다리는 중·거절 줄(설정 패널 위쪽, 여러 줄로 줄바꿈).</summary>
            public const float RoomStatusX = 0f, RoomStatusY = 200f, RoomStatusW = 300f, RoomStatusH = 120f, RoomStatusFont = 16f;

            /// <summary>위 도구 줄: 단추 자리는 ToolbarX + DeleteX·RotateX(가운데 기준), 줄 패널은 단추 폭 + 테마 여백으로 줄인다(ToolRow).</summary>
            public const float ToolbarX = 0f, ToolbarY = 430f, ToolbarH = 86f;
            public const float DeleteX = 60f, RotateX = 245f, ToolButtonW = 150f, ToolButtonH = 70f, ToolFont = 18f;
            /// <summary>아래 도구 줄: 단추 자리는 BottomX + MoveX, 줄 패널은 단추 폭 + 테마 여백(ToolRow).</summary>
            public const float BottomX = 0f, BottomY = -490f, BottomH = 86f;
            /// <summary>Unity 글자 13 → 규약의 최소 14.</summary>
            public const float MoveX = -320f, MoveW = 64f, MoveH = 40f, MoveFont = 14f;

            /// <summary>
            /// 일터 점수 줄(일터일 때만, 위 도구 줄 바로 아래). 위 칸: 성질마다 아이콘과 점수, 가구 수, 템플릿 단추. 아래 칸: 못 놓는 이유.
            /// 추천으로 정함(사용자 확인 전).
            /// </summary>
            public const float WorkspaceX = 0f, WorkspaceY = 319f, WorkspaceW = 760f, WorkspaceH = 120f;
            public const float QualityX0 = -320f, QualityPitch = 84f, QualityRowY = 22f, QualityIcon = 32f, QualityIconOffset = -20f;
            public const float QualityLabelOffset = 20f, QualityLabelW = 44f, QualityLabelH = 32f, QualityFont = 18f;
            public const float CapacityX = 120f, CapacityW = 110f, CapacityH = 40f, CapacityFont = 18f;
            public const float TemplatesX = 270f, TemplatesW = 160f, TemplatesH = 52f, TemplatesFont = 18f;
            public const float WorkspaceReasonY = -32f, WorkspaceReasonW = 700f, WorkspaceReasonH = 44f, WorkspaceReasonFont = 16f;

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
        public const string ToolbarId = "furniture.toolbar", BottomBarId = "furniture.bottom";
        public const string RoomStatusId = "furniture.room-status";
        /// <summary>물건이 든 상자를 고른 줄 아래에 보이는 안내의 문구 키(놓으면 다시 꺼낼 수 있다).</summary>
        public const string HoldsItemsNoteKey = "ui.furniture.stored.holds-hint";
        public const string CategoryFurnitureId = "furniture.category.furniture", CategoryStoredId = "furniture.category.stored";
        public const string FurnitureGroupId = "furniture.group.furniture", StoredGroupId = "furniture.group.stored";
        public const string StoredListId = "furniture.stored.list", StoredEmptyId = "furniture.stored.empty", StoredReasonId = "furniture.stored.reason";
        public static string SlotId(int slot) => "furniture.slot." + slot;
        public const string WorkspaceRowId = "workspace-row";
        public const string CapacityId = "workspace-capacity";
        public const string TemplatesId = "workspace-templates";
        public const string WorkspaceReasonId = "workspace-reason";
        /// <summary>성질 점수 글자(아이콘은 이 id + ".icon").</summary>
        public static string QualityId(string qualityId) => "workspace-quality-" + qualityId;

        /// <summary>일터에 가구를 더 놓지 못하는 이유(GameSession.CanAddToActiveWorkspace) → 문구 키. 표 한 곳.</summary>
        private static readonly Dictionary<WorkspaceResultKind, string> AddReasonKeys = new Dictionary<WorkspaceResultKind, string>
        {
            [WorkspaceResultKind.OverCapacity] = "ui.workspace.full",
            [WorkspaceResultKind.NotOwner] = "ui.workspace.not-owner",
        };

        private static readonly FurniturePlacementDirection[] Directions =
            { FurniturePlacementDirection.North, FurniturePlacementDirection.East, FurniturePlacementDirection.South, FurniturePlacementDirection.West };

        private readonly FurniturePaletteModel _model;
        private readonly UiSlot[] _slots = new UiSlot[Metrics.PageSize];
        private readonly UiFittedImage[] _icons = new UiFittedImage[Metrics.PageSize];
        private UiTextField _search;
        private UiButton _previous, _next, _close, _delete, _rotate, _move;
        private UiTextBlock _pageText;
        private UiTextBlock _roomStatus;
        private string _roomStatusKey;
        private UiLabel _title, _settingsTitle, _name, _size, _direction;
        private UiNode _furnitureGroup, _storedGroup;
        private FurnitureCategoryButton _categoryFurniture, _categoryStored;
        private UiScrollList _storedList;
        private UiTextBlock _storedEmpty, _storedReason;
        private StoredFurnitureModel _stored = StoredFurnitureModel.Build(null, f => string.Empty, shared: false);
        private int _storedIndex = -1;
        private UiFittedImage _selectedIcon;

        private UiPanel _workspaceRow;
        private readonly List<(string QualityId, UiLabel Score)> _qualities = new List<(string, UiLabel)>(5);
        private UiLabel _capacity;
        private UiButton _templates;
        private UiTextBlock _workspaceReason;
        private string _workspaceReasonKey;
        private WorkspaceStatus _workspace;
        private bool _cardsEnabled = true;

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
        /// <summary>"보관" 분류가 열려 있는가(아니면 "가구" 분류).</summary>
        public bool StoredShown { get; private set; }
        /// <summary>보관 목록의 표시 모델(GameSession.StoredFurniture 조회).</summary>
        public StoredFurnitureModel Stored => _stored;
        /// <summary>보관 목록에서 고른 줄의 기록 자리(StoredFurniture.Index). 고른 것이 없으면 -1.</summary>
        public int PickedStoredIndex => _storedIndex;
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
        /// <summary>
        /// 마지막 월드 클릭이 한 일(시험용): placed, moved, selected, rejected, none. 같이 쓰는 집이면 waiting(방에 물었다), 뒤이어 방의 답에 따라
        /// placed·moved 또는 refused(방이 거절 — 다른 사람이 먼저 그 칸을 썼다).
        /// </summary>
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
            UiInsets pad = Services.PaddingOf(UiSkin.Panel);
            var paletteArea = new PanelArea(Metrics.PaletteW, Metrics.PaletteH, pad);
            _title = palette.Add(new UiLabel { Id = "furniture.title", FontSize = Metrics.TitleFont, Color = Metrics.Ink });
            _title.Place(0f, 0f, paletteArea.Left, paletteArea.Top, Metrics.TitleW, Metrics.TitleH);
            // 분류마다 따로 노드(ui-standards): 가구 분류의 검색·칸·쪽 넘기기는 한 묶음, 보관 분류의 목록은 다른 묶음. 묶음은 팔레트와 같은 크기라 안의 좌표는 그대로다.
            _furnitureGroup = palette.Add(new UiNode { Id = FurnitureGroupId }).Centered(0f, 0f, Metrics.PaletteW, Metrics.PaletteH);
            _storedGroup = palette.Add(new UiNode { Id = StoredGroupId, Visible = false }).Centered(0f, 0f, Metrics.PaletteW, Metrics.PaletteH);
            _search = _furnitureGroup.Add(new UiTextField { Id = SearchId, FontSize = Metrics.SearchFont, MaxLength = Metrics.SearchMaxLength })
                .Centered(Metrics.SearchX, Metrics.SearchY, Metrics.SearchW, Metrics.SearchH);
            _search.TextChanged += OnSearchChanged;
            for (int i = 0; i < Metrics.PageSize; i++)
            {
                int slot = i;
                float x = Metrics.SlotX0 + (i % Metrics.Columns) * Metrics.SlotPitchX, y = Metrics.SlotY0 - (i / Metrics.Columns) * Metrics.SlotPitchY;
                _slots[i] = _furnitureGroup.Add(new UiSlot { Id = SlotId(i) }).Centered(x, y, Metrics.SlotSize, Metrics.SlotSize);
                _icons[i] = _slots[i].Add(new UiFittedImage { Id = SlotId(i) + ".icon" }).Centered(0f, 0f, Metrics.SlotIcon, Metrics.SlotIcon);
                _slots[i].Clicked += () => SelectFromPalette(_model.At(slot));
            }
            _previous = _furnitureGroup.Add(new UiButton { Id = PreviousId, FontSize = Metrics.PageButtonFont })
                .Centered(-Metrics.PageButtonX, Metrics.PageButtonY, Metrics.PageButtonW, Metrics.PageButtonH);
            _previous.Clicked += () => ShowPage(_model.Page - 1);
            _next = _furnitureGroup.Add(new UiButton { Id = NextId, FontSize = Metrics.PageButtonFont })
                .Centered(Metrics.PageButtonX, Metrics.PageButtonY, Metrics.PageButtonW, Metrics.PageButtonH);
            _next.Clicked += () => ShowPage(_model.Page + 1);
            _pageText = _furnitureGroup.Add(new UiTextBlock { Id = PageTextId, FontSize = Metrics.PageTextFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(Metrics.PageTextX, Metrics.PageTextY, Metrics.PageTextW, Metrics.PageTextH);
            // 분류 단추는 묶음들 뒤에 둔다: 열 때 첫 포커스가 팔레트 첫 칸에 가게(포커스 순서 = 노드 순서).
            float categoryY = paletteArea.CenterYFromTop(Metrics.CategoryH, Metrics.TitleH + Metrics.CategoryGap);
            _categoryFurniture = palette.Add(new FurnitureCategoryButton { Id = CategoryFurnitureId, FontSize = Metrics.CategoryFont })
                .Centered(-Metrics.CategoryPitch / 2f, categoryY, Metrics.CategoryW, Metrics.CategoryH);
            _categoryFurniture.Clicked += () => ShowCategory(false);
            _categoryStored = palette.Add(new FurnitureCategoryButton { Id = CategoryStoredId, FontSize = Metrics.CategoryFont })
                .Centered(Metrics.CategoryPitch / 2f, categoryY, Metrics.CategoryW, Metrics.CategoryH);
            _categoryStored.Clicked += () => ShowCategory(true);

            _storedList = _storedGroup.Add(new UiScrollList { Id = StoredListId, RowHeight = Metrics.StoredRowH, FontSize = Metrics.StoredFont })
                .Centered(Metrics.StoredListX, Metrics.StoredListY, Metrics.StoredListW, Metrics.StoredListH);
            _storedList.RowText = StoredRowText;
            _storedList.RowClicked += PickStoredRow;
            _storedEmpty = _storedGroup.Add(new UiTextBlock { Id = StoredEmptyId, FontSize = Metrics.StoredEmptyFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.StoredEmptyY, Metrics.StoredEmptyW, Metrics.StoredEmptyH);
            _storedReason = _storedGroup.Add(new UiTextBlock { Id = StoredReasonId, FontSize = Metrics.StoredReasonFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.StoredReasonY, Metrics.StoredReasonW, Metrics.StoredReasonH);

            UiPanel settings = root.Add(new UiPanel { Id = "furniture.settings" }).Centered(Metrics.SettingsX, Metrics.SettingsY, Metrics.SettingsW, Metrics.SettingsH);
            var settingsArea = new PanelArea(Metrics.SettingsW, Metrics.SettingsH, pad);
            _settingsTitle = settings.Add(new UiLabel { Id = "furniture.settings.title", FontSize = Metrics.TitleFont, Color = Metrics.Ink });
            _settingsTitle.Place(0f, 0f, settingsArea.Left, settingsArea.Top, Metrics.SettingsTitleW, Metrics.SettingsTitleH);
            _close = settings.Add(new UiButton { Id = CloseId, Icon = Rootborn.Screens.Day.ScreenIcons.Close })
                .Centered(Metrics.CloseX, Metrics.CloseY, Metrics.CloseSize, Metrics.CloseSize);
            _close.Clicked += Close;
            // 아래쪽 무리(미리보기·이름·크기·방향)를 테마 여백 위로 들어 올린다: 가장 아래 요소와 패널 아래 사이가 여백보다 좁으면 그만큼.
            float bottomMargin = Math.Min(Metrics.SettingsH / 2f + Metrics.PreviewY - Metrics.PreviewSize / 2f, Metrics.SettingsH / 2f + Metrics.DirectionY - Metrics.InfoH / 2f);
            float lift = Math.Max(0f, pad.Bottom - bottomMargin);
            UiPanel frame = settings.Add(new UiPanel { Id = "furniture.selected.frame" }).Centered(Metrics.PreviewX, Metrics.PreviewY + lift, Metrics.PreviewSize, Metrics.PreviewSize);
            _selectedIcon = frame.Add(new UiFittedImage { Id = SelectedIconId }).Centered(0f, 0f, Metrics.PreviewIcon, Metrics.PreviewIcon);
            _name = settings.Add(new UiLabel { Id = SelectedNameId, FontSize = Metrics.NameFont, Color = Metrics.Ink })
                .Centered(Metrics.InfoX, Metrics.NameY + lift, Metrics.InfoW, Metrics.NameH);
            _size = settings.Add(new UiLabel { Id = SelectedSizeId, FontSize = Metrics.InfoFont, Color = Metrics.Ink })
                .Centered(Metrics.InfoX, Metrics.SizeY + lift, Metrics.InfoW, Metrics.InfoH);
            _direction = settings.Add(new UiLabel { Id = SelectedDirectionId, FontSize = Metrics.InfoFont, Color = Metrics.Ink })
                .Centered(Metrics.InfoX, Metrics.DirectionY + lift, Metrics.InfoW, Metrics.InfoH);
            _roomStatus = settings.Add(new UiTextBlock { Id = RoomStatusId, FontSize = Metrics.RoomStatusFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(Metrics.RoomStatusX, Metrics.RoomStatusY, Metrics.RoomStatusW, Metrics.RoomStatusH);

            UiPanel toolbar = root.Add(new UiPanel { Id = ToolbarId });
            _delete = toolbar.Add(new UiButton { Id = DeleteId, FontSize = Metrics.ToolFont });
            _delete.Clicked += DeleteSelected;
            _rotate = toolbar.Add(new UiButton { Id = RotateId, FontSize = Metrics.ToolFont });
            _rotate.Clicked += Rotate;
            new ToolRow(toolbar, Metrics.ToolbarY, Metrics.ToolbarH, pad)
                .Add(_delete, Metrics.ToolbarX + Metrics.DeleteX, Metrics.ToolButtonW, Metrics.ToolButtonH)
                .Add(_rotate, Metrics.ToolbarX + Metrics.RotateX, Metrics.ToolButtonW, Metrics.ToolButtonH)
                .Fit();

            UiPanel bottom = root.Add(new UiPanel { Id = BottomBarId });
            _move = bottom.Add(new UiButton { Id = MoveId, FontSize = Metrics.MoveFont });
            _move.Clicked += PickUpSelected;
            new ToolRow(bottom, Metrics.BottomY, Metrics.BottomH, pad)
                .Add(_move, Metrics.BottomX + Metrics.MoveX, Metrics.MoveW, Metrics.MoveH)
                .Fit();

            BuildWorkspaceRow(root);
            ApplyTexts();
            return root;
        }

        // 일터 점수 줄: 성질(데이터 정의 순서)마다 아이콘과 점수 글자 하나. 노드는 한 번 만들고 값만 고친다.
        private void BuildWorkspaceRow(UiNode root)
        {
            _workspaceRow = root.Add(new UiPanel { Id = WorkspaceRowId, Visible = false })
                .Centered(Metrics.WorkspaceX, Metrics.WorkspaceY, Metrics.WorkspaceW, Metrics.WorkspaceH);
            int index = 0;
            IEnumerable<SpaceQuality> qualities = Services.Definitions?.All<SpaceQuality>() ?? Array.Empty<SpaceQuality>();
            foreach (SpaceQuality quality in qualities)
            {
                if (quality == null) continue;
                float x = Metrics.QualityX0 + index++ * Metrics.QualityPitch;
                if (!string.IsNullOrEmpty(quality.IconKey))
                    _workspaceRow.Add(new UiImage { Id = QualityId(quality.Id) + ".icon", Image = Rootborn.Screens.Day.ScreenIcons.Key(quality.IconKey) })
                        .Centered(x + Metrics.QualityIconOffset, Metrics.QualityRowY, Metrics.QualityIcon, Metrics.QualityIcon);
                UiLabel score = _workspaceRow.Add(new UiLabel { Id = QualityId(quality.Id), FontSize = Metrics.QualityFont, Color = Metrics.Ink })
                    .Centered(x + Metrics.QualityLabelOffset, Metrics.QualityRowY, Metrics.QualityLabelW, Metrics.QualityLabelH);
                _qualities.Add((quality.Id, score));
            }
            _capacity = _workspaceRow.Add(new UiLabel { Id = CapacityId, FontSize = Metrics.CapacityFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(Metrics.CapacityX, Metrics.QualityRowY, Metrics.CapacityW, Metrics.CapacityH);
            _templates = _workspaceRow.Add(new UiButton { Id = TemplatesId, FontSize = Metrics.TemplatesFont })
                .Centered(Metrics.TemplatesX, Metrics.QualityRowY, Metrics.TemplatesW, Metrics.TemplatesH);
            _templates.Clicked += () => Services.Screens?.Open(ScreenCatalog.WorkspaceTemplates);
            _workspaceReason = _workspaceRow.Add(new UiTextBlock { Id = WorkspaceReasonId, FontSize = Metrics.WorkspaceReasonFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.WorkspaceReasonY, Metrics.WorkspaceReasonW, Metrics.WorkspaceReasonH);
        }

        /// <summary>
        /// 일터 줄을 GameSession 조회로 고친다: 열린 일터가 없으면(집) 숨긴다. 있으면 성질 점수, 놓은 수/한도, 템플릿 단추(자기 일터일 때만),
        /// 가구를 더 놓지 못하면 카드를 끄고 이유를 보인다. 판단은 GameSession(CanAddToActiveWorkspace)이 한다.
        /// </summary>
        private void RefreshWorkspace()
        {
            var session = Services.Session;
            _workspace = session?.ActiveWorkspaceStatus;
            _workspaceRow.Visible = _workspace != null;
            WorkspaceResultKind add = _workspace == null ? WorkspaceResultKind.Applied : session.CanAddToActiveWorkspace();
            _cardsEnabled = add == WorkspaceResultKind.Applied;
            _workspaceReasonKey = _cardsEnabled ? null : AddReasonKeys.TryGetValue(add, out string key) ? key : null;
            bool owned = _workspace == null || _workspace.Owned;
            if (_workspace != null)
            {
                foreach ((string qualityId, UiLabel score) in _qualities) score.Text = _workspace.Scores.Get(qualityId).ToString(CultureInfo.InvariantCulture);
                _templates.Enabled = _workspace.Owned;
            }
            _capacity.Visible = owned; // 남의 일터 한도는 모른다(GameSession이 0을 준다)
            _delete.Enabled = _rotate.Enabled = _move.Enabled = owned; // 남의 일터는 옮기기·치우기도 안 된다
            // 새로 놓을 수 없으면 팔레트에서 고른 것을 푼다(유령·초록 칸을 보이지 않는다). 자기 일터에서 집어 든 가구는 한도와 무관하게 옮길 수 있어 남긴다.
            if (!owned) { Carrying = false; SelectedInstanceId = null; }
            if (!_cardsEnabled && !Carrying && Active != null)
            {
                Active = null;
                _hasHover = false;
                _validDirty = true;
                RefreshSelected();
            }
            ApplyWorkspaceTexts();
            for (int i = 0; i < Metrics.PageSize; i++) _slots[i].Enabled = _cardsEnabled;
        }

        private void ApplyWorkspaceTexts()
        {
            _templates.Text = T("ui.workspace.templates");
            _capacity.Text = _workspace == null ? string.Empty : F("ui.workspace.capacity", _workspace.Placed, _workspace.Capacity);
            _workspaceReason.Text = _workspaceReasonKey == null ? string.Empty : T(_workspaceReasonKey);
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            _wasDown = true; // 버튼을 눌러 연 그 누름이 월드 클릭이 되지 않게
            _hasHover = false;
            _ghostHeld = false;
            // 열 때마다 같은 상태에서 시작한다: 가구 분류, 보관 줄을 고른 것은 없다. 닫았다 여는 사이 집을 다시 읽으면 기록 자리 번호가 다시
            // 매겨지므로 옛 번호를 들고 있으면 안 보이는 다른 가구를 놓게 된다. 팔레트에서 고른 가구는 남는다.
            StoredShown = false;
            if (_storedIndex >= 0) { _storedIndex = -1; Active = null; }
            _overlayVisible = Active != null;
            _validDirty = true;
            ReloadFurniture();
            RefreshStored();
            RefreshWorkspace();
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
            if (topic == ChangeTopics.Furniture || topic == ChangeTopics.Workspace || topic == ChangeTopics.Map || topic == ChangeTopics.All) RefreshStored();
            if (topic == ChangeTopics.Furniture || topic == ChangeTopics.All) FollowRoomAnswer();
            if (topic == ChangeTopics.Furniture || topic == ChangeTopics.Workspace || topic == ChangeTopics.Career || topic == ChangeTopics.Map || topic == ChangeTopics.All)
                RefreshWorkspace();
            if (topic == ChangeTopics.Furniture || topic == ChangeTopics.World || topic == ChangeTopics.Map || topic == ChangeTopics.All)
            {
                _validDirty = true;
                _hasHover = false;
                if (SelectedInstanceId != null && FindPlaced(SelectedInstanceId) == null) { SelectedInstanceId = null; Carrying = false; }
            }
        }

        // 같이 쓰는 집: 방에 물은 요청이 끝났으면(기다림이 풀림) 결과를 보인다 — 허락이면 줄을 지우고, 거절이면 "다른 사람이 먼저 그 자리를 썼어요".
        private void FollowRoomAnswer()
        {
            var session = Services.Session;
            if (session == null || !session.SharesOpenLayout) { SetRoomStatus(null); return; } // 일터는 방에서도 각자 것이라 공유 중으로 보이지 않는다
            if (session.WaitingForRoom) { SetRoomStatus("ui.furniture.room-waiting"); return; }
            if (LastAction != "waiting") return;
            bool applied = session.LastRoomCommandApplied == true;
            LastAction = applied ? _waitingFor : "refused";
            SetRoomStatus(applied ? null : "ui.furniture.room-refused");
            _overlayVisible = true;
        }

        private string _waitingFor;

        private void SetRoomStatus(string key)
        {
            _roomStatusKey = key;
            _roomStatus.Text = key == null ? string.Empty : T(key);
        }

        // 같이 쓰는 집에서 명령이 방에 물었으면(지금 바뀌지 않고 기다린다) waiting으로 둔다. 아니면 거절.
        private bool AskedRoom(string action)
        {
            var session = Services.Session;
            if (session == null || !session.SharesOpenLayout || !session.WaitingForRoom) return false;
            _waitingFor = action;
            LastAction = "waiting";
            SetRoomStatus("ui.furniture.room-waiting");
            return true;
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
            _categoryFurniture.Text = T("ui.furniture.category.furniture");
            _categoryStored.Text = T("ui.furniture.category.stored");
            _storedEmpty.Text = T("ui.furniture.stored.empty");
            ApplyStoredNote();
            _settingsTitle.Text = T("ui.furniture.title");
            _roomStatus.Text = _roomStatusKey == null ? string.Empty : T(_roomStatusKey);
            _search.Placeholder = T("ui.furniture.search.placeholder");
            _previous.Text = T("ui.furniture.page.previous");
            _next.Text = T("ui.furniture.page.next");
            _delete.Text = T("ui.furniture.delete");
            _rotate.Text = T("ui.furniture.rotate");
            _move.Text = T("ui.furniture.move");
            if (_workspaceRow != null) ApplyWorkspaceTexts();
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
                _slots[i].Enabled = _cardsEnabled;
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

        // ---------- 보관 분류 ----------

        /// <summary>분류를 바꾼다(가구 / 보관). 집어 든 가구가 없으면 고른 가구를 놓고 간다(다른 분류의 미리보기가 남지 않게).</summary>
        public void ShowCategory(bool stored)
        {
            if (StoredShown == stored) return;
            StoredShown = stored;
            if (!Carrying) { Active = null; _storedIndex = -1; _storedList.SelectedIndex = -1; }
            _overlayVisible = Active != null;
            _validDirty = true;
            _hasHover = false;
            ApplyStoredNote();
            ApplyCategoryView();
            RefreshSelected();
        }

        private void ApplyCategoryView()
        {
            _furnitureGroup.Visible = !StoredShown;
            _storedGroup.Visible = StoredShown;
            _categoryFurniture.Selected = !StoredShown;
            _categoryStored.Selected = StoredShown;
        }

        /// <summary>보관 목록을 GameSession 조회로 다시 읽는다(열 때, 가구·일터·맵이 바뀔 때). 고른 줄이 없어졌으면(놓았다) 고른 것을 푼다.</summary>
        private void RefreshStored()
        {
            var session = Services.Session;
            _stored = StoredFurnitureModel.Build(session?.StoredFurniture(), Name, session != null && session.SharesOpenLayout);
            _storedList.RowCount = _stored.Rows.Count;
            _storedList.Visible = !_stored.IsEmpty;
            _storedEmpty.Visible = _stored.IsEmpty;
            int row = _storedIndex < 0 ? -1 : _stored.RowOf(_storedIndex);
            // 줄이 없어졌거나(놓았다) 그 번호가 다른 가구를 가리키게 됐으면(기록 자리가 다시 매겨졌다) 고른 것을 푼다.
            if (_storedIndex >= 0 && (row < 0 || !ReferenceEquals(_stored.Rows[row].Definition, Active)))
            {
                row = -1;
                _storedIndex = -1;
                if (!Carrying) { Active = null; _hasHover = false; _validDirty = true; RefreshSelected(); }
            }
            _storedList.SelectedIndex = row;
            ApplyStoredNote();
            ApplyCategoryView();
        }

        /// <summary>
        /// 보관 목록 아래 안내 한 줄: 고를 수 없는 까닭(같이 쓰는 집)이 먼저, 아니면 고른 줄이 물건이 든 상자일 때 "놓으면 다시 꺼낼 수 있다".
        /// 문구는 키로만 고른다.
        /// </summary>
        private void ApplyStoredNote()
        {
            string key = _stored.ReasonKey;
            if (key == null && _storedIndex >= 0)
            {
                int picked = _stored.RowOf(_storedIndex);
                if (picked >= 0 && _stored.Rows[picked].HoldsItems) key = HoldsItemsNoteKey;
            }
            _storedReason.Text = key == null ? string.Empty : T(key);
            _storedReason.Visible = !_stored.IsEmpty && key != null;
        }

        // 보이는 줄만 묻는다(UiScrollList). 물건이 든 상자는 그렇다고 덧붙인다.
        private string StoredRowText(int row)
        {
            if (row < 0 || row >= _stored.Rows.Count) return string.Empty;
            StoredFurnitureRow r = _stored.Rows[row];
            string name = Name(r.Definition);
            return r.HoldsItems ? F("ui.furniture.stored.row-with-items", name) : name;
        }

        private void PickStoredRow(int row)
        {
            if (!_stored.CanPick || row < 0 || row >= _stored.Rows.Count) { _storedList.SelectedIndex = _storedIndex < 0 ? -1 : _stored.RowOf(_storedIndex); return; }
            StoredFurnitureRow r = _stored.Rows[row];
            _storedIndex = r.Index;
            Active = r.Definition;
            Direction = r.Direction; // 저장된 방향에서 시작한다. 회전으로 바꿀 수 있다.
            _directionChosen = true;
            Carrying = false;
            SelectedInstanceId = null;
            _overlayVisible = true;
            _validDirty = true;
            _hasHover = false;
            ApplyStoredNote();
            RefreshSelected();
        }

        // ---------- 조작 ----------

        private void SelectFromPalette(FurnitureDefinition f)
        {
            if (f == null) return;
            Active = f;
            _storedIndex = -1;
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
            if (Services.Session.WaitingForRoom) return;
            // 물건이 든 상자는 치우지 않는다(Core가 거절한다) — 이유를 알린다. 비우면 치울 수 있다.
            var contents = Services.Session.StorageAt(id);
            if (contents != null && !contents.IsEmpty)
            {
                LastAction = "storage-not-empty";
                SetRoomStatus("ui.furniture.storage-not-empty");
                return;
            }
            bool removed = Services.Session.RemoveFurniture(id);
            if (!removed && AskedRoom("removed")) { SelectedInstanceId = null; Carrying = false; return; }
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
            _storedIndex = -1;
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

        /// <summary>
        /// 키보드: 상호작용 키(E·Space·Ctrl)가 캐릭터가 바라보는 칸을 포인터로 누른 것과 같게 한다(놓기·고르기·옮기기 — 같은 Press).
        /// 그 칸에서 할 일이 없으면(고른 가구도 든 가구도 없고 놓인 가구도 없다) 가져가지 않는다 — 월드 상호작용(문·NPC)으로 간다.
        /// 칸 커서를 따로 두지 않는다(이 화면은 걸으면서 쓴다 — 방향 키는 캐릭터).
        /// </summary>
        protected internal override bool OnKey(ScreenKey key, bool shift)
        {
            if (key != ScreenKey.Interact || _view == null || _view.Room == null || !_view.TryFacingCell(out GridPoint cell)) return false;
            HouseFurnitureLayout house = Services.Session?.House;
            if (house == null) return false;
            bool somethingThere = house.TryFindAt(cell, out _);
            if (!somethingThere && Active == null && !(Carrying && SelectedInstanceId != null)) return false;
            _hover = cell;
            _hasHover = true;
            _hoverValid = Active != null && CanPlace(Active, cell);
            Press(cell);
            return true;
        }

        private bool CanPlace(FurnitureDefinition f, GridPoint anchor)
        {
            if (_storedIndex >= 0) return Services.Session != null && Services.Session.CanPlaceStoredFurniture(_storedIndex, anchor, Direction);
            HouseFurnitureLayout house = Services.Session?.House;
            return house != null && house.CanPlace(f, anchor, Direction);
        }

        private void Press(GridPoint cell)
        {
            HouseFurnitureLayout house = Services.Session?.House;
            if (house == null || Services.Session == null) return;
            if (Services.Session.WaitingForRoom) return; // 같이 쓰는 집: 앞의 요청이 방의 답을 기다린다
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
                if (!moved && AskedRoom("moved")) { Carrying = false; _overlayVisible = false; return; }
                if (!moved) { _overlayVisible = true; return; }
                Carrying = false;
                AfterCommit(cell);
                return;
            }

            if (_storedIndex >= 0)
            {
                // 보관한 가구를 그 칸에 다시 놓는다: 판정과 저장은 GameSession이 한다. 성공하면 Furniture 알림이 와서 목록에서 빠진다.
                bool restored = Services.Session.PlaceStoredFurniture(_storedIndex, cell, direction);
                LastAction = restored ? "placed" : "rejected";
                if (!restored) { _overlayVisible = true; return; }
                AfterCommit(cell);
                return;
            }

            if (Active == null) return;
            string furnitureId = Active.StableId;
            bool placed = Services.Session.PlaceFurniture(furnitureId, cell, direction);
            LastAction = placed ? "placed" : "rejected";
            if (!placed && AskedRoom("placed")) { _overlayVisible = false; return; }
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
            // 한도에 닿은 일터에서 옮기기를 마쳤으면 집어 들었던 가구를 놓기용으로 남기지 않는다(RefreshWorkspace와 같은 규칙).
            if (!_cardsEnabled && !Carrying && Active != null) { Active = null; RefreshSelected(); }
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
