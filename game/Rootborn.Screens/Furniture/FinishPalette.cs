using System;
using System.Collections.Generic;
using Rootborn.Core.Building;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.World;
using Rootborn.Game;
using Rootborn.Ui;
using M = Rootborn.Screens.Furniture.FurniturePlacementScreen.Metrics;
using S = Rootborn.Screens.Furniture.FurniturePlacementScreen;

namespace Rootborn.Screens.Furniture
{
    /// <summary>
    /// 가구 배치 화면의 "꾸미기" 분류(짓는 집의 방마다 벽지·바닥): 팔레트 안의 묶음(자리 탭, 도구 줄, 카드 격자, 쪽 넘기기)과 오른쪽 패널의 꾸밈 정보,
    /// 그리고 월드 위 미리 보기. 노드는 한 번 만들고 값만 고친다. 규칙(대상 칸, 바뀌는 칸, 비용, 잠김, 거절 이유)은 GameSession(Core)이 정한다 —
    /// 여기서는 포인터 칸이 바뀔 때만 <see cref="GameSession.PreviewHouseFinish"/>를 묻고, 누르면 <see cref="GameSession.ApplyHouseFinish"/> 하나를 부른다.
    ///
    /// 월드 위: 적용할 수 있는 미리 보기는 월드의 꾸밈 그림을 덮어 그리고(IPlacementView.ShowFinishPreview) 바뀔 칸을 밝힌다.
    /// 거절된 미리 보기(잠김·돈 모자람)는 월드 그림을 덮지 않고(null) 바뀔 칸만 안 되는 색으로 밝히며 이유는 오른쪽 패널에 보인다.
    /// 바뀔 칸이 없으면(이미 이 꾸밈, 대상 없음) 밝히지 않는다. 미리 보기가 앞의 것과 같으면(<see cref="HouseFinishPreview.SameAs"/>) 아무것도 바꾸지 않는다.
    /// </summary>
    internal sealed class FinishPalette
    {
        private readonly ScreenServices _services;
        private readonly Func<string, string> _t;
        private readonly Func<string, object[], string> _f;
        private readonly Func<string, string> _itemName;
        private readonly FinishPaletteModel _model = new FinishPaletteModel(M.PageSize);

        private UiNode _group, _info;
        private FurnitureCategoryButton _tabWall, _tabFloor, _toolRoom, _toolBrush;
        private readonly UiSlot[] _cards = new UiSlot[M.PageSize];
        private readonly UiFittedImage[] _upper = new UiFittedImage[M.PageSize];
        private readonly UiFittedImage[] _lower = new UiFittedImage[M.PageSize];
        private UiButton _previous, _next;
        private UiTextBlock _pageText;
        private UiTextBlock _name, _price, _cells, _cost, _money, _reason;

        private IPlacementView _view;
        private string _tool = HouseFinishTools.Room;
        private bool _hasHover;
        private GridPoint _hover;
        private bool _stale;
        private HouseFinishPreview _held;
        private HouseFinishPreview _shown;
        private readonly List<GridPoint> _highlight = new List<GridPoint>(128);
        private UiColor _highlightColor;

        public FinishPalette(ScreenServices services, Func<string, string> t, Func<string, object[], string> f, Func<string, string> itemName)
        {
            _services = services;
            _t = t;
            _f = f;
            _itemName = itemName;
        }

        public FinishPaletteModel Model => _model;
        public bool Shown { get; private set; }
        public RoomFinishDefinition Picked { get; private set; }
        public string Tool => _tool;
        /// <summary>지금 들고 있는 미리 보기(포인터가 방 위에 있고 꾸밈을 골랐을 때). 없으면 null.</summary>
        public HouseFinishPreview Preview => _held;
        /// <summary>PreviewHouseFinish를 물은 횟수(시험용 — 포인터 칸이 바뀔 때만 오른다).</summary>
        public int PreviewQueries { get; private set; }
        /// <summary>마지막 누름이 한 일(시험용): applied, refused, none.</summary>
        public string LastAction { get; private set; } = "none";

        private GameSession Session => _services.Session;

        public void Attach(IPlacementView view)
        {
            if (!ReferenceEquals(view, _view)) { _shown = null; _held = null; _highlight.Clear(); }
            _view = view;
        }

        // ---------- 노드 ----------

        public void Build(UiNode palette, UiNode settings)
        {
            _group = palette.Add(new UiNode { Id = S.FinishGroupId, Visible = false }).Centered(0f, 0f, M.PaletteW, M.PaletteH);
            _tabWall = Tab(S.FinishTabWallId, 0, 2, () => SetSlot(RoomFinishSlots.Wall));
            _tabFloor = Tab(S.FinishTabFloorId, 1, 2, () => SetSlot(RoomFinishSlots.Floor));
            _toolRoom = _group.Add(new FurnitureCategoryButton { Id = S.FinishToolRoomId, FontSize = M.FinishToolFont })
                .Centered(-M.FinishToolPitch / 2f, M.FinishToolY, M.FinishToolW, M.FinishToolH);
            _toolRoom.Clicked += () => SetTool(HouseFinishTools.Room);
            _toolBrush = _group.Add(new FurnitureCategoryButton { Id = S.FinishToolBrushId, FontSize = M.FinishToolFont })
                .Centered(M.FinishToolPitch / 2f, M.FinishToolY, M.FinishToolW, M.FinishToolH);
            _toolBrush.Clicked += () => SetTool(HouseFinishTools.Brush);

            for (int i = 0; i < M.PageSize; i++)
            {
                int card = i;
                float x = M.SlotX0 + (i % M.Columns) * M.SlotPitchX, y = M.FinishCardY0 - (i / M.Columns) * M.SlotPitchY;
                _cards[i] = _group.Add(new UiSlot { Id = S.FinishCardId(i) }).Centered(x, y, M.SlotSize, M.SlotSize);
                _upper[i] = _cards[i].Add(new UiFittedImage { Id = S.FinishCardId(i) + ".icon" }).Centered(0f, M.FinishPieceOffset, M.FinishPiece, M.FinishPiece);
                _lower[i] = _cards[i].Add(new UiFittedImage { Id = S.FinishCardId(i) + ".icon2" }).Centered(0f, -M.FinishPieceOffset, M.FinishPiece, M.FinishPiece);
                _cards[i].Clicked += () => Pick(_model.At(card));
            }
            _previous = _group.Add(new UiButton { Id = S.FinishPreviousId, FontSize = M.PageButtonFont })
                .Centered(-M.PageButtonX, M.PageButtonY, M.PageButtonW, M.PageButtonH);
            _previous.Clicked += () => ShowPage(_model.Page - 1);
            _next = _group.Add(new UiButton { Id = S.FinishNextId, FontSize = M.PageButtonFont })
                .Centered(M.PageButtonX, M.PageButtonY, M.PageButtonW, M.PageButtonH);
            _next.Clicked += () => ShowPage(_model.Page + 1);
            _pageText = _group.Add(new UiTextBlock { Id = S.FinishPageTextId, FontSize = M.PageTextFont, Color = M.Ink, Align = UiAlign.Center })
                .Centered(M.PageTextX, M.PageTextY, M.PageTextW, M.PageTextH);

            _info = settings.Add(new UiNode { Id = S.FinishInfoId, Visible = false }).Centered(0f, 0f, M.SettingsW, M.SettingsH);
            _name = Line(S.FinishNameId, M.FinishNameY, M.FinishNameH, M.FinishNameFont);
            _price = Line(S.FinishPriceId, M.FinishPriceY, M.FinishLineH, M.FinishLineFont);
            _cells = Line(S.FinishCellsId, M.FinishCellsY, M.FinishLineH, M.FinishLineFont);
            _cost = Line(S.FinishCostId, M.FinishCostY, M.FinishLineH, M.FinishLineFont);
            _money = Line(S.FinishMoneyId, M.FinishMoneyY, M.FinishLineH, M.FinishLineFont);
            _reason = Line(S.FinishReasonId, M.FinishReasonY, M.FinishReasonH, M.FinishLineFont);
        }

        private FurnitureCategoryButton Tab(string id, int index, int count, Action click)
        {
            float x = (index - (count - 1) / 2f) * M.FinishTabPitch;
            var tab = _group.Add(new FurnitureCategoryButton { Id = id, FontSize = M.FinishTabFont }).Centered(x, M.FinishTabY, M.FinishTabW, M.FinishTabH);
            tab.Clicked += click;
            return tab;
        }

        private UiTextBlock Line(string id, float y, float h, float font) =>
            _info.Add(new UiTextBlock { Id = id, FontSize = font, Color = M.Ink, Align = UiAlign.Center, FitHeight = true })
                .Centered(0f, y, M.FinishInfoW, h);

        // ---------- 켜고 끄기, 알림 ----------

        /// <summary>분류를 켜거나 끈다. 켤 때 카드를 다시 읽고, 끌 때 고른 꾸밈과 미리 보기를 버린다(다른 분류에 미리 보기가 남지 않게).</summary>
        public void Show(bool on)
        {
            if (_group == null) return;
            _group.Visible = on;
            _info.Visible = on;
            if (on == Shown && on) return;
            Shown = on;
            if (on) { RefreshCards(); ApplyTexts(); return; }
            Picked = null;
            ClearHover();
        }

        public void Closed() => ClearHover();

        public void OnChanged(string topic)
        {
            if (!Shown) return;
            if (topic == ChangeTopics.Language) { ApplyTexts(); return; }
            // 미리 보기 결과를 바꿀 수 있는 알림만 낡음으로 본다(다음 포인터 프레임 또는 누름에서 한 번 다시 묻는다). 시간·학생·퀘스트 같은
            // 무관한 알림은 보지 않는다 — 흐르는 시간은 게임 1분마다 알린다.
            if (!AffectsPreview(topic)) return;
            _stale = true;
            if (topic == ChangeTopics.All || topic == ChangeTopics.Furniture || topic == ChangeTopics.Career || topic == ChangeTopics.HouseBuild || topic == ChangeTopics.Map)
                RefreshCards();
            if (topic == ChangeTopics.Inventory || topic == ChangeTopics.All) ApplyInfo();
        }

        /// <summary>
        /// 미리 보기 결과(대상·바뀌는 칸·비용·이유)에 닿는 알림: 꾸밈(HouseBuild), 돈(Inventory), 해금(Furniture·Career), 맵·일터(집이 열렸는가),
        /// 같이 하는 방(같이 쓰는 집인가), 모름(All).
        /// </summary>
        private static bool AffectsPreview(string topic) =>
            topic == ChangeTopics.HouseBuild || topic == ChangeTopics.Inventory || topic == ChangeTopics.Furniture || topic == ChangeTopics.Career
            || topic == ChangeTopics.Map || topic == ChangeTopics.Workspace || topic == GameChangeTopics.Room || topic == ChangeTopics.All;

        private void RefreshCards()
        {
            var session = Session;
            _model.Set(_model.Slot, session?.HouseFinishOptions(_model.Slot), d => session != null && session.IsFinishUnlocked(d));
            if (Picked != null && _model.Find(Picked) == null) { Picked = null; _held = null; }
            RefreshPage();
            ApplyInfo();
        }

        private void RefreshPage()
        {
            bool wall = string.Equals(_model.Slot, RoomFinishSlots.Wall, StringComparison.Ordinal);
            for (int i = 0; i < M.PageSize; i++)
            {
                FinishCard card = _model.At(i);
                _cards[i].Visible = card != null;
                if (card == null) continue;
                _cards[i].Dimmed = !card.Unlocked;
                _cards[i].Selected = ReferenceEquals(card.Definition, Picked);
                UiColor tint = card.Unlocked ? UiTheme.White : UiTheme.ButtonDisabled; // 잠긴 카드는 못 쓰는 칸과 같은 칠
                if (card.LowerImage != null)
                {
                    _upper[i].Centered(0f, M.FinishPieceOffset, M.FinishPiece, M.FinishPiece);
                    _upper[i].Show(card.UpperImage, 0, 0);
                    _lower[i].Show(card.LowerImage, 0, 0);
                    _lower[i].Visible = true;
                    _lower[i].Tint = tint;
                }
                else
                {
                    _upper[i].Centered(0f, 0f, M.SlotIcon, M.SlotIcon);
                    _upper[i].Show(card.UpperImage, 0, 0);
                    _lower[i].Clear();
                    _lower[i].Visible = false;
                }
                _upper[i].Tint = tint;
                _upper[i].Layout(_cards[i].Rect); // 자리를 바꿨으면 지금 칸 안에서 다시 잡는다(화면 쌓기는 크기가 바뀔 때만 배치한다)
            }
            _previous.Enabled = _model.HasPrevious;
            _next.Enabled = _model.HasNext;
            _previous.Visible = _next.Visible = _model.PageCount > 1;
            _pageText.Text = _model.PageCount > 1 ? _f("ui.furniture.page", new object[] { _model.Page + 1, _model.PageCount, _model.Count }) : string.Empty;
            _tabWall.Selected = wall;
            _tabFloor.Selected = !wall;
            _toolRoom.Selected = string.Equals(_tool, HouseFinishTools.Room, StringComparison.Ordinal);
            _toolBrush.Selected = string.Equals(_tool, HouseFinishTools.Brush, StringComparison.Ordinal);
        }

        public void ApplyTexts()
        {
            if (_group == null) return;
            _tabWall.Text = _t("ui.house-build.tab.wall");
            _tabFloor.Text = _t("ui.house-build.tab.floor");
            _toolRoom.Text = _t("ui.house-build.tool.room");
            _toolBrush.Text = _t("ui.house-build.tool.brush");
            _previous.Text = _t("ui.furniture.page.previous");
            _next.Text = _t("ui.furniture.page.next");
            RefreshPage();
            ApplyInfo();
        }

        /// <summary>오른쪽 패널: 이름, 칸당 값(또는 무료), 바뀌는 칸, 비용, 가진 돈, 이유. 값은 고른 꾸밈과 들고 있는 미리 보기에서만 온다.</summary>
        private void ApplyInfo()
        {
            if (_info == null) return;
            RoomFinishDefinition picked = Picked;
            _name.Text = picked == null ? _t("ui.house-build.pick") : _t(picked.NameKey);
            if (picked == null) _price.Text = string.Empty;
            else if (picked.Cost.Count == 0) _price.Text = _t("ui.house-build.free");
            else
            {
                ItemAmount<string> per = picked.Cost[0];
                _price.Text = _f(per.Count == 1 ? "ui.house-build.price.one" : "ui.house-build.price", new object[] { per.Count, _itemName(per.Item) });
            }

            HouseFinishPreview p = _held;
            bool target = p != null && p.HasTarget;
            int changed = target ? p.ChangedCells.Count : 0;
            _cells.Text = !target ? string.Empty : changed == 1 ? _f("ui.house-build.cells.one", new object[] { 1 }) : _f("ui.house-build.cells", new object[] { changed });
            _cost.Text = target && p.Cost.Count > 0 ? _f("ui.house-build.cost", new object[] { p.Cost[0].Count, _itemName(p.Cost[0].Item) }) : string.Empty;

            string item = picked != null && picked.Cost.Count > 0 ? picked.Cost[0].Item : null;
            _money.Text = item == null ? string.Empty : _f("ui.house-build.money", new object[] { Owned(item), _itemName(item) });

            string reason = p != null ? p.ReasonKey
                : picked != null && Session != null && !Session.IsFinishUnlocked(picked) ? HouseFinishReasons.Locked : null;
            _reason.Text = reason == null ? string.Empty : _t(reason);
        }

        private int Owned(string itemId)
        {
            var session = Session;
            if (session == null || session.Definitions == null || !session.Definitions.TryGet(itemId, out ItemDefinition item)) return 0;
            return session.Inventory.CountOf(item);
        }

        // ---------- 고르기 ----------

        private void SetSlot(string slot)
        {
            if (string.Equals(slot, _model.Slot, StringComparison.Ordinal)) return;
            _model.Set(slot, null, null); // 자리만 바꾸고 첫 쪽으로(카드는 바로 아래에서 읽는다)
            Picked = null;
            ResetPreview();
            RefreshCards();
        }

        private void SetTool(string tool)
        {
            if (string.Equals(tool, _tool, StringComparison.Ordinal)) return;
            _tool = tool;
            ResetPreview();
            RefreshPage();
            ApplyInfo();
        }

        private void ShowPage(int page)
        {
            _model.ShowPage(page);
            RefreshPage();
        }

        private void Pick(FinishCard card)
        {
            if (card == null) return;
            Picked = card.Definition;
            ResetPreview();
            RefreshPage();
            ApplyInfo();
        }

        // 고른 것·도구가 바뀌었다: 들고 있는 미리 보기를 버리고, 포인터가 방 위면 바로 다시 묻는다.
        private void ResetPreview()
        {
            _held = null;
            _highlight.Clear();
            if (_hasHover) Requery();
            else ShowOnWorld(null);
        }

        // ---------- 월드 ----------

        /// <summary>
        /// 포인터(Host가 프레임마다): 칸이 바뀌었거나 상태가 낡았을 때만 미리 보기를 묻는다. UI 위·방 밖이면 미리 보기를 지운다.
        /// pressed면(누른 그 프레임) 그 칸에 적용한다. 같은 칸에서는 새 객체를 만들지 않는다.
        /// </summary>
        public void WorldPointer(float x, float y, bool pressed, bool overUi)
        {
            if (overUi || _view == null || _view.Room == null || !_view.TryCellAt(x, y, out GridPoint cell))
            {
                ClearHover();
                return;
            }
            if (!_hasHover || !_hover.Equals(cell) || _stale)
            {
                _hover = cell;
                _hasHover = true;
                Requery();
            }
            if (pressed) Press();
        }

        /// <summary>상호작용 키: 바라보는 칸을 누른 것과 같다. 고른 꾸밈이 없거나 바라보는 칸이 없으면 가져가지 않는다.</summary>
        public bool OnInteract()
        {
            if (Picked == null || _view == null || _view.Room == null || !_view.TryFacingCell(out GridPoint cell)) return false;
            if (!_hasHover || !_hover.Equals(cell) || _stale)
            {
                _hover = cell;
                _hasHover = true;
                Requery();
            }
            Press();
            return true;
        }

        private void Press()
        {
            var session = Session;
            if (Picked == null || !_hasHover || session == null || _view?.Room == null) return;
            CellPos target = _view.Room.ToMap(_hover);
            bool applied = session.ApplyHouseFinish(Picked.Id, _tool, target);
            LastAction = applied ? "applied" : "refused";
            // 적용한 그 업데이트에서 다시 묻는다(이제 바뀔 칸이 없다) — 월드에서 옛 미리 보기를 지우고, 다음 그리기에서 미리 보기를 다시 조합하지 않게.
            if (_stale || applied) Requery();
        }

        private void Requery()
        {
            _stale = false;
            var session = Session;
            if (Picked == null || session == null || !_hasHover || _view?.Room == null) { SetPreview(null); return; }
            PreviewQueries++;
            SetPreview(session.PreviewHouseFinish(Picked.Id, _tool, _view.Room.ToMap(_hover)));
        }

        private void ClearHover()
        {
            _hasHover = false;
            SetPreview(null);
        }

        private void SetPreview(HouseFinishPreview preview)
        {
            if (preview == null && _held == null) { ShowOnWorld(null); return; }
            if (preview != null && _held != null && preview.SameAs(_held)) return; // 같은 방 안에서 칸만 움직였다
            _held = preview;
            _highlight.Clear();
            if (preview != null && _view?.Room != null)
            {
                foreach (CellPos c in preview.HighlightCells) _highlight.Add(_view.Room.ToPlacement(c));
                _highlightColor = preview.CanApply ? M.FinishValidCell : M.InvalidFootprint;
            }
            ShowOnWorld(preview != null && preview.CanApply ? preview : null);
            ApplyInfo();
        }

        // 월드에는 적용할 수 있는 미리 보기만 넘긴다. 앞에 넘긴 것과 같으면 다시 넘기지 않는다.
        private void ShowOnWorld(HouseFinishPreview preview)
        {
            if (ReferenceEquals(preview, _shown)) return;
            _shown = preview;
            _view?.ShowFinishPreview(preview);
        }

        /// <summary>월드 위: 바뀔 칸 밝힘(칸 목록은 미리 보기가 바뀔 때 만들었다). 새 객체를 만들지 않는다.</summary>
        public void EmitWorldOverlay(DrawList list)
        {
            if (!Shown || _view == null || _view.Room == null) return;
            for (int i = 0; i < _highlight.Count; i++) list.Fill(_view.CellRect(_highlight[i]), _highlightColor);
        }
    }
}
