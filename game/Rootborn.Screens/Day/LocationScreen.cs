using System;
using System.Globalization;
using Rootborn.Core.Placement;
using Rootborn.Core.StudentLife;
using Rootborn.Ui;
using Rootborn.Screens.Hud;

namespace Rootborn.Screens.Day
{
    /// <summary>
    /// 장소 화면의 배치 수치. Unity LocationIdentityPanel(1120x800 가운데)의 anchoredPosition·sizeDelta(가운데 기준, y 위로)를 그대로 모았다.
    /// </summary>
    public static class LocationLayout
    {
        public const float Width = 1120, Height = 800;
        /// <summary>제목(W, H). 자리는 안쪽 위(테마 여백 — PanelArea), 가운데 맞춤.</summary>
        public const float TitleW = 880, TitleH = 48;
        public const float TitleFont = 32;
        public static readonly (float X, float Y, float W, float H) Description = (0, 272, 944, 64);
        public const float DescriptionFont = 22;
        public static readonly (float X, float Y, float W, float H) TimeBudget = (0, 216, 944, 36);
        public const float TimeBudgetFont = 22;
        public static readonly (float X, float Y, float W, float H) State = (0, 154, 944, 64);
        public const float StateFont = 20;
        public static readonly (float X, float Y, float W, float H) HintFrame = (-318, -56, 340, 336);
        // 도움말·활동 틀 안의 글자 자리는 틀 − 테마 여백(PanelArea).
        public const float HintsFont = 20;
        public static readonly (float X, float Y, float W, float H) ActivityFrame = (176, -56, 568, 336);
        /// <summary>활동 목록 보이는 영역(Unity 504x272). 오른쪽에 스크롤 막대 자리(14)를 더했다.</summary>
        public const float RowHeight = 76, RowPitch = 92, RowFont = 20, RowSubFont = 18, RowReasonFont = 16;
        /// <summary>줄 글자의 좌우 안쪽 여백. 긴 글자(다른 언어)는 이 안에서 말줄임한다.</summary>
        public const float RowTextInset = 12;
        public static readonly (float X, float Y, float W, float H) Result = (0, -296, 976, 112);
        public const float ResultFont = 20;
        public static readonly (float X, float Y, float W, float H) Close = (488, 344, 48, 48);
    }

    /// <summary>
    /// 장소(장소 지점 상호작용 "location-activity", 이벤트 id = 장소 id). Unity LocationIdentityPanel: 이름, 설명, 시간 줄(가게면 용돈),
    /// 장소 상태, 왼쪽 힌트 틀, 오른쪽 활동·물건 목록(스크롤), 아래 결과 글, 닫기(×). 열 때 장소 방문을 기록한다(GameSession.VisitLocation,
    /// Unity LocationActivityInteractor 순서). 들어갈 수 없는 장소(Core Location.CanVisit)면 열지 않는다. 이동을 막는다(모달).
    /// 활동이 미니게임을 요구하는 경우는 없다(Unity 판 장소 화면은 미니게임 없는 장소 활동과 가구 구매만 했다).
    /// </summary>
    public sealed class LocationScreen : GameScreen
    {
        public const string InteractionKind = "location-activity";
        public const string RootId = "location", TitleId = "location.title", DescriptionId = "location.description", TimeBudgetId = "location.time-budget",
            StateId = "location.state", HintsId = "location.hints", RowsId = "location.rows", ResultId = "location.result", CloseId = "location.close";

        private UiLabel _title, _timeBudget;
        private UiTextBlock _description, _state, _hints, _result;
        private UiButton _close;
        private UiCardGrid _rows;
        private string _locationId;
        private int _requestSequence;
        private string _resultText = string.Empty;

        public LocationScreen(ScreenServices services) : base(services) { }

        public LocationModel Model { get; private set; }

        public UiRect RowRect(int index) => _rows.CellRect(index);

        protected override UiNode Build()
        {
            var root = new UiPanel { Id = RootId, Skin = UiSkin.Panel };
            root.Centered(0f, 0f, LocationLayout.Width, LocationLayout.Height);
            UiInsets pad = Services.PaddingOf(UiSkin.Panel);
            var area = new PanelArea(LocationLayout.Width, LocationLayout.Height, pad);
            _title = root.Add(new UiLabel { Id = TitleId, FontSize = LocationLayout.TitleFont, Align = UiAlign.Center });
            _title.Centered(0f, area.CenterYFromTop(LocationLayout.TitleH), LocationLayout.TitleW, LocationLayout.TitleH);
            _description = At(root.Add(new UiTextBlock { Id = DescriptionId, FontSize = LocationLayout.DescriptionFont, Align = UiAlign.Center }), LocationLayout.Description);
            _timeBudget = At(root.Add(new UiLabel { Id = TimeBudgetId, FontSize = LocationLayout.TimeBudgetFont, Align = UiAlign.Center }), LocationLayout.TimeBudget);
            _state = At(root.Add(new UiTextBlock { Id = StateId, FontSize = LocationLayout.StateFont, Align = UiAlign.Center, Visible = false }), LocationLayout.State);
            UiPanel hintFrame = At(root.Add(new UiPanel { Skin = UiSkin.Panel, Interactive = false }), LocationLayout.HintFrame);
            var hintArea = new PanelArea(LocationLayout.HintFrame.W, LocationLayout.HintFrame.H, pad);
            _hints = hintFrame.Add(new UiTextBlock { Id = HintsId, FontSize = LocationLayout.HintsFont });
            _hints.Place(0f, 0f, hintArea.Left, hintArea.Top, hintArea.InnerWidth, hintArea.InnerHeight);
            var activityArea = new PanelArea(LocationLayout.ActivityFrame.W, LocationLayout.ActivityFrame.H, pad);
            UiPanel activityFrame = At(root.Add(new UiPanel { Skin = UiSkin.Panel, Interactive = false }), LocationLayout.ActivityFrame);
            _rows = activityFrame.Add(new UiCardGrid
            {
                Id = RowsId, Padding = 0, DrawBackground = false,
                CellWidth = activityArea.InnerWidth - UiCardScroller.ScrollbarWidth - UiCardScroller.ScrollbarGap, CellHeight = LocationLayout.RowHeight, Spacing = LocationLayout.RowPitch - LocationLayout.RowHeight,
                StepsPerNotch = 32f / LocationLayout.RowPitch, // Unity scrollSensitivity 32
            });
            _rows.Place(0f, 0f, activityArea.Left, activityArea.Top, activityArea.InnerWidth, activityArea.InnerHeight);
            _rows.DrawCell = DrawRow;
            _rows.CellClicked += Press;
            _result = At(root.Add(new UiTextBlock { Id = ResultId, FontSize = LocationLayout.ResultFont }), LocationLayout.Result);
            _close = At(root.Add(new UiButton { Id = CloseId, Icon = ScreenIcons.Close }), LocationLayout.Close);
            _close.Clicked += Close;
            return root;
        }

        private static T At<T>(T node, (float X, float Y, float W, float H) r) where T : UiNode => node.Centered(r.X, r.Y, r.W, r.H);

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            string locationId = context.Interaction?.TargetId;
            if (Services.Session == null || !LocationModel.CanOpen(Services.Session, locationId)) { Close(); return; }
            _locationId = locationId;
            _resultText = string.Empty;
            _rows.ScrollOffset = 0;
            _rows.SelectedIndex = -1;
            Services.Session.VisitLocation(locationId); // OnChanged가 다시 그린다
            Render();
        }

        protected internal override void OnChanged(string topic)
        {
            if (Services.Session == null || _locationId == null) return;
            FinishWaitedPurchase();
            Render();
        }

        // 방의 허락을 기다리던 구매(돈을 같이 쓰는 방, 설계 3.2절). 끝나면 결과 문구로 바꾼다.
        private string _waitingPurchase;

        private void FinishWaitedPurchase()
        {
            var session = Services.Session;
            if (_waitingPurchase == null || session.WaitingForRoom || !session.LastRoomCommandApplied.HasValue) return;
            string id = _waitingPurchase;
            _waitingPurchase = null;
            if (session.LastRoomCommandApplied.Value)
            {
                _resultText = string.Format(CultureInfo.InvariantCulture, T("dream.shop.bought"), TryFindRow(id, out LocationRow row) ? Name(row) : string.Empty);
            }
            else _resultText = session.LastRoomRefusal ? T(RoomMoneyText.RefusedKey) : T("dream.shop.failed");
        }

        private bool TryFindRow(string id, out LocationRow found)
        {
            found = default;
            if (Model == null) return false;
            foreach (LocationRow row in Model.Rows)
            {
                if (row.Id != id) continue;
                found = row;
                return true;
            }

            return false;
        }

        private void Press(int index)
        {
            if (Model == null || index < 0 || index >= Model.Rows.Count) return;
            LocationRow row = Model.Rows[index];
            if (!row.Enabled) return; // Unity 판처럼 못 하는 줄은 눌리지 않는다(이유는 줄에 보인다)
            if (Services.Session.WaitingForRoom) return; // 방의 허락을 기다리는 동안은 다시 누르지 않는다
            if (row.Kind == LocationRowKind.Activity) PerformActivity(row);
            else Purchase(row);
        }

        private void PerformActivity(LocationRow row)
        {
            var session = Services.Session;
            string requestId = "location-activity:" + row.Id + ":" + session.Student.GetActivityLogIds().Length + ":" + _requestSequence++;
            LocationActivityResult result = Services.Session.PerformLocationActivity(row.Id, requestId);
            bool applied = result.Kind == LifeActivityResultKind.Applied;
            _resultText = applied
                ? string.Format(CultureInfo.InvariantCulture, T("life.time.applied"), StudentTimeText.ClockMinutes(session.Student.TimeMinutes) / 60,
                    StudentTimeText.ClockMinutes(session.Student.TimeMinutes) % 60)
                : T("life.time.unavailable");
            if (applied && session.Definitions.TryGet<LocationActivity>(row.Id, out LocationActivity activity) && !string.IsNullOrEmpty(activity.ResultTextKey))
                _resultText += "\n" + T(activity.ResultTextKey);
            _result.Text = _resultText;
        }

        private void Purchase(LocationRow row)
        {
            FurniturePurchaseResult result = Services.Session.PurchaseFurniture(row.Id);
            if (Services.Session.WaitingForRoom)
            {
                _waitingPurchase = row.Id;
                _resultText = T(RoomMoneyText.WaitingKey);
                _result.Text = _resultText;
                return;
            }

            string name = Name(row);
            _resultText = result == FurniturePurchaseResult.Purchased ? string.Format(CultureInfo.InvariantCulture, T("dream.shop.bought"), name)
                : result == FurniturePurchaseResult.NotEnoughMoney ? T("dream.shop.not-enough")
                : result == FurniturePurchaseResult.AlreadyOwned ? string.Format(CultureInfo.InvariantCulture, T("dream.shop.owned"), name)
                : T("dream.shop.failed");
            _result.Text = _resultText;
        }

        private string Name(LocationRow row) => row.NameKey.Length > 0 ? T(row.NameKey) : row.LiteralName;

        private void Render()
        {
            var session = Services.Session;
            Model = LocationModel.From(session, _locationId);
            _title.Text = Model.NameKey.Length > 0 ? T(Model.NameKey) : string.Empty;
            _description.Text = T(Model.DescriptionKey);
            string time = StudentTimeText.Format(T, session.Student);
            string balanceKey = session.SharesWallet ? "life.location.time-with-room-balance" : "life.location.time-with-balance"; // 돈을 같이 쓰는 방이면 방 지갑
            _timeBudget.Text = Model.OfferBalance >= 0 ? string.Format(CultureInfo.InvariantCulture, T(balanceKey), time, Model.OfferBalance) : time;
            _state.Visible = Model.ShowsState;
            _state.Text = Model.ShowsState ? T(Model.StateNameKey) + (Model.StateDescriptionKey.Length == 0 ? string.Empty : "\n" + T(Model.StateDescriptionKey)) : string.Empty;
            string hints = T(LocationModel.HintsTitleKey);
            if (!Model.HasIdentity || Model.HintKeys.Count == 0) hints += "\n" + T(LocationModel.NoHintsKey);
            else foreach (string key in Model.HintKeys) hints += "\n" + T(key);
            _hints.Text = hints;
            _result.Text = _resultText;
            _rows.ItemCount = Model.Rows.Count;
            foreach (LocationRow row in Model.Rows) RowLines(row); // 쓰는 키를 기록해 둔다
        }

        /// <summary>줄의 글자: 이름과 비용(활동) 또는 살/가진 문구(물건), 그리고 못 하는 이유.</summary>
        private (string Main, string Reason) RowLines(LocationRow row)
        {
            if (row.Kind == LocationRowKind.Offer)
            {
                string text = row.Owned ? string.Format(CultureInfo.InvariantCulture, T("dream.shop.owned"), Name(row))
                    : string.Format(CultureInfo.InvariantCulture, T("dream.shop.buy"), Name(row), row.Price);
                return (text, row.Owned || row.Affordable ? string.Empty : T("dream.shop.not-enough"));
            }
            string main = T(row.NameKey) + "\n" + StudentTimeText.FormatCostAndEnd(T, Services.Session, row.TimeCostMinutes);
            string reason = row.Refusal != StudentActivityRefusalReason.None ? T(StudentActivityRefusal.LocalizationKey(row.Refusal))
                : !row.RequirementsMet ? T("life.location.requirements-unmet") : string.Empty;
            return (main, reason);
        }

        private void DrawRow(int index, UiRect rect, CardState state, DrawList list, ITextMeasurer text)
        {
            if (Model == null || index >= Model.Rows.Count) return;
            LocationRow row = Model.Rows[index];
            bool active = state.Active && row.Enabled;
            list.Panel(rect, UiSkin.Button, !active ? UiTheme.ButtonDisabled : state.Hovered ? UiTheme.ButtonHovered : UiTheme.ButtonNormal);
            (string main, string reason) = RowLines(row);
            string[] lines = main.Split('\n');
            float line1 = text.LineHeight(LocationLayout.RowFont), line2 = lines.Length > 1 ? text.LineHeight(LocationLayout.RowSubFont) : 0;
            float line3 = reason.Length > 0 ? text.LineHeight(LocationLayout.RowReasonFont) : 0;
            float total = line1 + line2 + line3 + (line2 > 0 ? 2 : 0) + (line3 > 0 ? 2 : 0);
            float y = rect.Y + (rect.H - total) / 2f;
            UiColor ink = active ? UiTheme.Ink : UiTheme.InkDisabled;
            y = Line(list, text, rect, y, lines[0], LocationLayout.RowFont, ink) + 2;
            if (lines.Length > 1) y = Line(list, text, rect, y, lines[1], LocationLayout.RowSubFont, ink) + 2;
            if (reason.Length > 0) Line(list, text, rect, y, reason, LocationLayout.RowReasonFont, ink);
        }

        private static float Line(DrawList list, ITextMeasurer text, UiRect rect, float y, string value, float size, UiColor color)
        {
            value = TextFit.Ellipsize(value, rect.W - LocationLayout.RowTextInset * 2f, text, size); // 긴 글자(다른 언어)가 줄 밖으로 나가지 않게
            list.Text(rect.X + (rect.W - text.Width(value, size)) / 2f, y, value, size, color, UiSkin.Button);
            return y + text.LineHeight(size);
        }
    }
}
