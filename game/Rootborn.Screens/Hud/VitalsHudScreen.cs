using System.Globalization;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Hud
{
    /// <summary>생활 수치 HUD의 표시 모델: 체력과 집중(Unity UrbanLifeVitalsHud가 보이던 두 값). 게임 한 판이 없으면 보이지 않는다.</summary>
    public readonly record struct VitalsModel(bool Visible, int Energy, int Focus)
    {
        public static VitalsModel From(GameSession session) =>
            session == null ? default : new VitalsModel(true, session.Student.Energy, session.Student.Focus);
    }

    /// <summary>
    /// 생활 수치 HUD: 시계 오른쪽의 체력·집중 칸 두 개(칸마다 아이콘, 이름, 값). Unity 판처럼 게이지 없이 숫자만 보인다.
    /// 이름 키 ui.hud.energy·ui.hud.focus는 Unity 판에서도 현지화 파일에 없어 코드의 한글·영어로 대신했다(보고서의 빠진 키 목록).
    /// </summary>
    public sealed class VitalsHudScreen : GameScreen
    {
        /// <summary>배치(칸 안, 위 기준). Unity: 아이콘 위에서 20, 48x48 / 이름 위에서 68, 높이 28, 18 / 값 위에서 96, 높이 36, 24. 좌우 16 안쪽.</summary>
        private static class Layout
        {
            public const float IconTop = 20f, IconSize = 48f;
            public const float LabelTop = 68f, LabelHeight = 28f, LabelFont = 18f;
            public const float ValueTop = 96f, ValueHeight = 36f, ValueFont = 24f;
            public const float SideInset = 16f;
        }

        /// <summary>칸 하나의 정의: id, 아이콘 키, 이름 키, 오른쪽(집중) 칸인지.</summary>
        private readonly record struct Widget(string Id, string Icon, string LabelKey, bool Focus);

        private static readonly Widget[] Widgets =
        {
            new Widget("hud.energy", "icon:energy", "ui.hud.energy", false),
            new Widget("hud.focus", "icon:focus", "ui.hud.focus", true),
        };

        private readonly UiNode[] _panels = new UiNode[2];
        private readonly UiLabel[] _names = new UiLabel[2];
        private readonly UiLabel[] _values = new UiLabel[2];
        private string _shownLanguage;

        public VitalsHudScreen(ScreenServices services) : base(services) { }

        public VitalsModel Model { get; private set; }

        public static string ValueId(string widgetId) => widgetId + ".value";

        protected override UiNode Build()
        {
            var root = new UiNode { Id = "hud.vitals" };
            for (int i = 0; i < Widgets.Length; i++)
            {
                Widget w = Widgets[i];
                var panel = HudLayout.PlaceResource(root.Add(new UiPanel { Id = w.Id, Skin = UiSkin.Slot, Interactive = false }), w.Focus);
                panel.Add(new UiImage { Image = w.Icon }).Place(0.5f, 0f, -Layout.IconSize / 2f, Layout.IconTop, Layout.IconSize, Layout.IconSize);
                _names[i] = Text(panel, w.Id + ".name", Layout.LabelTop, Layout.LabelHeight, Layout.LabelFont);
                _values[i] = Text(panel, ValueId(w.Id), Layout.ValueTop, Layout.ValueHeight, Layout.ValueFont);
                _panels[i] = panel;
            }
            return root;
        }

        private static UiLabel Text(UiNode panel, string id, float top, float height, float size)
        {
            var label = panel.Add(new UiLabel { Id = id, FontSize = size, Color = HudLayout.Ink, Align = UiAlign.Center });
            label.AnchorMinX = 0f; label.AnchorMaxX = 1f; label.AnchorMinY = label.AnchorMaxY = 0f;
            label.OffsetMinX = Layout.SideInset; label.OffsetMaxX = -Layout.SideInset;
            label.OffsetMinY = top; label.OffsetMaxY = top + height;
            return label;
        }

        protected internal override void OnOpened(ScreenOpenContext context) => Refresh(true);
        protected internal override void OnChanged(string topic) => Refresh(false);

        private void Refresh(bool force)
        {
            VitalsModel model = VitalsModel.From(Services.Session);
            bool language = _shownLanguage != Services.Text.Language;
            if (!force && !language && model == Model) return;
            Model = model;
            foreach (UiNode panel in _panels) panel.Visible = model.Visible;
            if (!model.Visible) return;
            if (force || language)
            {
                _shownLanguage = Services.Text.Language;
                for (int i = 0; i < Widgets.Length; i++) _names[i].Text = T(Widgets[i].LabelKey);
            }
            _values[0].Text = model.Energy.ToString(CultureInfo.InvariantCulture);
            _values[1].Text = model.Focus.ToString(CultureInfo.InvariantCulture);
        }
    }
}
