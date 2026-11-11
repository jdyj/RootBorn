using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Hud
{
    /// <summary>
    /// 시계 HUD의 표시 모델: 학생 날, 시각, 용돈, 시간 경고, (같이 하는 방이면) 남은 때와 기다리는 사람.
    /// 시각은 GameSession.RoomShownMinutes — 혼자면 흐르는 시계, 같이 하는 방이면 때의 대표 시각(4단계에서 흐르는 시각으로 바뀐다).
    /// 용돈은 가게가 받는 화폐(판매 가구의 가격 아이템 중 첫 것)의 가방 수량. 화폐를 모르면 용돈은 보이지 않는다(-1).
    /// Alert는 시간 경고(취침 시각이 가까움·지남·지침 — <see cref="ClockAlert"/>). 혼자 하기에는 시간 포인트 줄이 없다(흐르는 시간 설계 4절).
    /// Together는 같이 하는 방이다: 그때만 남은 때와 Waiting(지금 때를 아직 안 끝낸 다른 사람)을 보인다.
    /// </summary>
    public readonly record struct ClockModel(bool HasSession, int Day, int Hour, int Minute, int Allowance, ClockAlert Alert,
        bool Together, int TimePointsRemaining, int TimePointsToday, IReadOnlyList<string> Waiting, bool RoomWallet = false)
    {
        /// <summary>취침 시각이 몇 분 남았을 때부터 "곧 잘 시간"을 보이는가(게임 분, 표시 선택 — 규칙 수치가 아니다).</summary>
        public const int BedtimeWarningMinutes = 60;

        public static ClockModel From(GameSession session, ItemDefinition currency)
        {
            if (session == null) return default;
            int minutes = ((session.RoomShownMinutes % (24 * 60)) + 24 * 60) % (24 * 60);
            int allowance = currency == null ? -1 : session.Inventory.CountOf(currency);
            bool together = !session.IsSoloRoom;
            var waiting = new List<string>(4);
            if (together) session.RoomWaitingFor(waiting);
            return new ClockModel(true, session.Student.CurrentDay, minutes / 60, minutes % 60, allowance, AlertFor(session),
                together, together ? session.RoomTimePointsRemaining : 0, together ? session.Room.PartsToday : 0, waiting, session.SharesWallet);
        }

        /// <summary>지금의 시간 경고. 시간 규칙이 없으면(시험 데이터) 지침만 본다.</summary>
        public static ClockAlert AlertFor(GameSession session)
        {
            if (session == null) return ClockAlert.None;
            if (session.IsExhausted) return ClockAlert.Exhausted;
            int left = session.MinutesUntilBedtime;
            if (left < 0) return ClockAlert.None;
            if (left == 0) return ClockAlert.PastBedtime;
            return left <= BedtimeWarningMinutes ? ClockAlert.BedtimeSoon : ClockAlert.None;
        }

        /// <summary>가게가 받는 화폐: 정의 순서로 첫 판매 가구의 가격 아이템(Unity TimeHud.FindShopCurrency).</summary>
        public static ItemDefinition ShopCurrency(GameSession session)
        {
            if (session == null) return null;
            foreach (FurnitureDefinition furniture in session.Definitions.All<FurnitureDefinition>())
                if (furniture != null && furniture.IsForSale && furniture.PriceItem != null) return furniture.PriceItem;
            return null;
        }
    }

    /// <summary>시계 HUD의 시간 경고. 문구 키와 아이콘 키는 <see cref="ClockHudScreen"/>의 표 한 곳에서 고른다.</summary>
    public enum ClockAlert { None, BedtimeSoon, PastBedtime, Exhausted }

    /// <summary>
    /// 시계 HUD(Unity TownTimeHudRuntimeInstaller + TimeHud). 위쪽 줄의 시계 칸에 시계 아이콘, 첫 줄 "1일  14:00 · 용돈 0",
    /// 둘째 줄은 시간 경고("곧 잘 시간이에요", "잘 시간이 지났어요", "지쳤어요 — 자야 해요")이고 그때 시계 아이콘도 바뀐다(icon:bedtime, icon:exhausted).
    /// 같이 하는 방이면 둘째 줄에 남은 때와 " · 아직: 누구"를 잇는다(4단계에서 바뀐다). GameSession이 바뀌었다는 알림(시각은 분마다 "time")을 받을 때만
    /// 다시 읽는다 — 매 프레임 읽지 않는다.
    /// 게임 한 판이 없으면 불러오는 중 문구.
    /// </summary>
    public sealed class ClockHudScreen : GameScreen
    {
        /// <summary>
        /// 배치(시계 칸 안, 왼쪽 위 기준). Unity: 아이콘 (40, 40) 64x64, 글자는 왼쪽 128·위아래 36·오른쪽 36 안쪽, 24 굵게.
        /// 시간 포인트 줄을 더하면서 그 글자 칸을 위(날·시각·용돈)와 아래(시간 포인트·기다리는 사람)로 나눴다.
        /// </summary>
        private static class Layout
        {
            public const float IconX = 40f, IconY = 40f, IconSize = 64f;
            public const float TextLeft = 128f, TextRight = 36f;
            public const float TopLineTop = 30f, TopLineBottom = 72f;
            public const float RoomLineTop = 76f, RoomLineBottom = 30f;
            public const float FontSize = 24f, RoomFontSize = 18f;
            public const string Icon = "icon:time";
        }

        /// <summary>경고 → (둘째 줄 문구 키, 시계 아이콘). 경고가 없으면 문구 없이 시계 아이콘.</summary>
        private static (string TextKey, string Icon) AlertLook(ClockAlert alert)
        {
            switch (alert)
            {
                case ClockAlert.BedtimeSoon: return ("ui.hud.bedtime-soon", BedtimeIcon);
                case ClockAlert.PastBedtime: return ("ui.hud.past-bedtime", BedtimeIcon);
                case ClockAlert.Exhausted: return ("ui.hud.exhausted", ExhaustedIcon);
                default: return (null, Layout.Icon);
            }
        }

        public const string BedtimeIcon = "icon:bedtime", ExhaustedIcon = "icon:exhausted";
        public const string IconId = "hud.clock.icon";

        public const string PanelId = "hud.clock";
        public const string LabelId = "hud.clock.label";
        public const string RoomLabelId = "hud.clock.room";

        private UiLabel _label, _room;
        private UiImage _icon;
        private ItemDefinition _currency;
        private bool _currencyResolved;

        public ClockHudScreen(ScreenServices services) : base(services) { }

        public ClockModel Model { get; private set; }

        protected override UiNode Build()
        {
            var root = new UiNode { Id = "hud.clock.root" };
            var panel = HudLayout.PlaceClock(root.Add(new UiPanel { Id = PanelId, Skin = UiSkin.Panel, Interactive = false }));
            _icon = panel.Add(new UiImage { Id = IconId, Image = Layout.Icon });
            _icon.Place(0f, 0f, Layout.IconX, Layout.IconY, Layout.IconSize, Layout.IconSize);
            _label = panel.Add(new UiLabel { Id = LabelId, FontSize = Layout.FontSize, Color = HudLayout.Ink });
            _label.Stretch(Layout.TextLeft, Layout.TopLineTop, Layout.TextRight, Layout.TopLineBottom);
            _room = panel.Add(new UiLabel { Id = RoomLabelId, FontSize = Layout.RoomFontSize, Color = HudLayout.Ink, Text = string.Empty });
            _room.Stretch(Layout.TextLeft, Layout.RoomLineTop, Layout.TextRight, Layout.RoomLineBottom);
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context) => Refresh();
        protected internal override void OnChanged(string topic) => Refresh();

        private void Refresh()
        {
            GameSession session = Services.Session;
            if (!_currencyResolved) { _currency = ClockModel.ShopCurrency(session); _currencyResolved = true; }
            ClockModel model = ClockModel.From(session, _currency);
            if (SameAsShown(model) && _label.Text != null && !LanguageChangedSinceLastText) return;
            Model = model;
            _shownLanguage = Services.Text.Language;
            if (!model.HasSession) { _label.Text = T("life.tp.loading"); _room.Text = string.Empty; _icon.Image = Layout.Icon; return; }
            string text = F("life.time.hud", model.Day, model.Hour, model.Minute);
            if (model.Allowance >= 0) text += F(model.RoomWallet ? "ui.hud.room-balance" : "dream.shop.balance", model.Allowance); // 돈을 같이 쓰는 방이면 방 지갑
            _label.Text = text;
            (string alertKey, string icon) = AlertLook(model.Alert);
            _icon.Image = icon;
            string room = alertKey == null ? string.Empty : T(alertKey);
            if (model.Together)
            {
                // 같이 하는 방의 때·기다리는 사람(멀티플레이 설계 2.1절). 흐르는 시간 4단계에서 바뀐다.
                if (room.Length > 0) room += T("ui.hud.room-separator");
                room += F("ui.hud.time-points", model.TimePointsRemaining, model.TimePointsToday);
                if (model.Waiting.Count > 0) room += F("ui.hud.room-waiting", string.Join(T("ui.hud.room-separator"), model.Waiting));
            }
            _room.Text = room;
        }

        // 기다리는 사람 목록은 새로 만든 목록이라 레코드 비교로는 늘 다르다. 내용을 비교한다.
        private bool SameAsShown(ClockModel model)
        {
            ClockModel shown = Model;
            if (model.Waiting == null || shown.Waiting == null) return model == shown;
            if ((model with { Waiting = null }) != (shown with { Waiting = null }) || model.Waiting.Count != shown.Waiting.Count) return false;
            for (int i = 0; i < model.Waiting.Count; i++)
                if (model.Waiting[i] != shown.Waiting[i]) return false;
            return true;
        }

        private string _shownLanguage;
        private bool LanguageChangedSinceLastText => _shownLanguage != Services.Text.Language;
    }
}
