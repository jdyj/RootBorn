using System;
using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Hud
{
    /// <summary>
    /// 시계 HUD의 표시 모델: 학생 날, 시각, 용돈, 시간 경고, 하는 중이면 끝나는 시각.
    /// 시각은 GameSession.RoomShownMinutes — 혼자면 흐르는 시계, 같이 하는 방이면 방의 시각(개인 시계가 따라간다).
    /// 용돈은 가게가 받는 화폐(판매 가구의 가격 아이템 중 첫 것)의 가방 수량. 화폐를 모르면 용돈은 보이지 않는다(-1).
    /// Alert는 시간 경고(취침 시각이 가까움·지남·지침 — <see cref="ClockAlert"/>). 혼자 하기에는 시간 포인트 줄이 없다(흐르는 시간 설계 4절).
    /// Busy는 같이 하는 방에서 앞의 활동을 하는 중이다(설계 3.2절 — 시계를 건너뛰지 않는다). 그때 끝나는 시각(BusyHour:BusyMinute)을 보인다.
    /// 혼자 하기의 활동은 시계를 건너뛰므로 하는 중이 없다(미니게임 중에는 미니게임 화면이 덮는다). 방의 때·남은 때 줄은 흐르는 시간 4단계에서 없앴다.
    /// </summary>
    public readonly record struct ClockModel(bool HasSession, int Day, int Hour, int Minute, int Allowance, ClockAlert Alert,
        bool Busy, int BusyHour, int BusyMinute, bool RoomWallet = false)
    {
        /// <summary>취침 시각이 몇 분 남았을 때부터 "곧 잘 시간"을 보이는가(게임 분, 표시 선택 — 규칙 수치가 아니다).</summary>
        public const int BedtimeWarningMinutes = 60;

        public static ClockModel From(GameSession session, ItemDefinition currency)
        {
            if (session == null) return default;
            int minutes = ((session.RoomShownMinutes % (24 * 60)) + 24 * 60) % (24 * 60);
            int allowance = currency == null ? -1 : session.Inventory.CountOf(currency);
            bool busy = session.IsBusy && session.Student.DayState == Rootborn.Core.StudentLife.StudentDayState.InProgress;
            int ends = busy ? Rootborn.Core.Time.TimeRules.ClockMinutes(session.ActivityEndsAtMinutes) : 0;
            return new ClockModel(true, session.Student.CurrentDay, minutes / 60, minutes % 60, allowance, AlertFor(session), busy, ends / 60, ends % 60, session.SharesWallet);
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
    /// 같이 하는 방에서 활동을 하는 중이면 둘째 줄에 "하는 중 · HH:MM에 끝나요"를 잇는다. GameSession이 바뀌었다는 알림(시각은 분마다 "time")을 받을 때만
    /// 다시 읽는다 — 매 프레임 읽지 않는다.
    /// 게임 한 판이 없으면 불러오는 중 문구.
    /// </summary>
    public sealed class ClockHudScreen : GameScreen
    {
        /// <summary>
        /// 배치(시계 칸 안, 왼쪽 위 기준). Unity: 아이콘 (40, 40) 64x64, 글자는 왼쪽 128·위아래 36·오른쪽 36 안쪽, 24 굵게.
        /// 그 글자 칸을 위(날·시각·용돈)와 아래(시간 경고·하는 중)로 나눴다.
        /// </summary>
        private static class Layout
        {
            public const float IconX = 40f, IconY = 40f, IconSize = 64f;
            public const float TextLeft = 128f, TextRight = 36f;
            public const float TopLineTop = 30f, TopLineBottom = 72f;
            public const float RoomLineBottom = 30f, LineGap = 4f; // 방 줄은 위 줄 아래 LineGap부터
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
            // 글줄은 테마 여백(PanelArea)보다 안으로 들어가지 않는다: 위 줄은 여백 아래에서, 방 줄은 위 줄 바로 아래에서 여백 위까지.
            UiInsets pad = Services.PaddingOf(UiSkin.Panel);
            float top = Math.Max(Layout.TopLineTop, pad.Top), right = Math.Max(Layout.TextRight, pad.Right);
            float lineH = HudLayout.RowHeight - Layout.TopLineTop - Layout.TopLineBottom;
            _label.Stretch(Layout.TextLeft, top, right, HudLayout.RowHeight - top - lineH);
            _room = panel.Add(new UiLabel { Id = RoomLabelId, FontSize = Layout.RoomFontSize, Color = HudLayout.Ink, Text = string.Empty });
            _room.Stretch(Layout.TextLeft, top + lineH + Layout.LineGap, right, Math.Max(Layout.RoomLineBottom, pad.Bottom));
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
            if (model.Busy)
            {
                if (room.Length > 0) room += T("ui.hud.room-separator");
                room += F("ui.hud.busy-until", model.BusyHour, model.BusyMinute);
            }
            _room.Text = room;
        }

        private bool SameAsShown(ClockModel model) => model == Model;

        private string _shownLanguage;
        private bool LanguageChangedSinceLastText => _shownLanguage != Services.Text.Language;
    }
}
