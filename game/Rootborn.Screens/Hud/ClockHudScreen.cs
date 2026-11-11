using Rootborn.Core.Inventory;
using Rootborn.Core.Placement;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Hud
{
    /// <summary>
    /// 시계 HUD의 표시 모델: 학생 날, 시각(하루 안의 분), 용돈. Unity TimeHud의 학생 진행 경로와 같다: 날과 시각은 학생 진행에서,
    /// 용돈은 가게가 받는 화폐(판매 가구의 가격 아이템 중 첫 것)의 가방 수량. 화폐를 모르면 용돈은 보이지 않는다(-1).
    /// Unity 판 학생 HUD는 시간대와 시간 포인트를 보이지 않았다(시간대는 멀티플레이 공유 시간에서만).
    /// </summary>
    public readonly record struct ClockModel(bool HasSession, int Day, int Hour, int Minute, int Allowance)
    {
        public static ClockModel From(GameSession session, ItemDefinition currency)
        {
            if (session == null) return default;
            int minutes = ((session.Student.TimeMinutes % (24 * 60)) + 24 * 60) % (24 * 60);
            int allowance = currency == null ? -1 : session.Inventory.CountOf(currency);
            return new ClockModel(true, session.Student.CurrentDay, minutes / 60, minutes % 60, allowance);
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

    /// <summary>
    /// 시계 HUD(Unity TownTimeHudRuntimeInstaller + TimeHud). 위쪽 줄의 시계 칸에 시계 아이콘과 "1일  08:00 · 용돈 0".
    /// GameSession이 바뀌었다는 알림을 받을 때만 다시 읽는다. 게임 한 판이 없으면 불러오는 중 문구.
    /// </summary>
    public sealed class ClockHudScreen : GameScreen
    {
        /// <summary>배치(시계 칸 안, 왼쪽 위 기준). Unity: 아이콘 (40, 40) 64x64, 글자는 왼쪽 128·위아래 36·오른쪽 36 안쪽, 24 굵게.</summary>
        private static class Layout
        {
            public const float IconX = 40f, IconY = 40f, IconSize = 64f;
            public const float TextLeft = 128f, TextRight = 36f, TextTop = 36f, TextBottom = 36f;
            public const float FontSize = 24f;
            public const string Icon = "icon:time";
        }

        public const string PanelId = "hud.clock";
        public const string LabelId = "hud.clock.label";

        private UiLabel _label;
        private ItemDefinition _currency;
        private bool _currencyResolved;

        public ClockHudScreen(ScreenServices services) : base(services) { }

        public ClockModel Model { get; private set; }

        protected override UiNode Build()
        {
            var root = new UiNode { Id = "hud.clock.root" };
            var panel = HudLayout.PlaceClock(root.Add(new UiPanel { Id = PanelId, Skin = UiSkin.Panel, Interactive = false }));
            panel.Add(new UiImage { Image = Layout.Icon }).Place(0f, 0f, Layout.IconX, Layout.IconY, Layout.IconSize, Layout.IconSize);
            _label = panel.Add(new UiLabel { Id = LabelId, FontSize = Layout.FontSize, Color = HudLayout.Ink });
            _label.Stretch(Layout.TextLeft, Layout.TextTop, Layout.TextRight, Layout.TextBottom);
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context) => Refresh();
        protected internal override void OnChanged(string topic) => Refresh();

        private void Refresh()
        {
            GameSession session = Services.Session;
            if (!_currencyResolved) { _currency = ClockModel.ShopCurrency(session); _currencyResolved = true; }
            ClockModel model = ClockModel.From(session, _currency);
            if (model == Model && _label.Text != null && !LanguageChangedSinceLastText) return;
            Model = model;
            _shownLanguage = Services.Text.Language;
            if (!model.HasSession) { _label.Text = T("life.tp.loading"); return; }
            string text = F("life.time.hud", model.Day, model.Hour, model.Minute);
            if (model.Allowance >= 0) text += F("dream.shop.balance", model.Allowance);
            _label.Text = text;
        }

        private string _shownLanguage;
        private bool LanguageChangedSinceLastText => _shownLanguage != Services.Text.Language;
    }
}
