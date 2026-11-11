using System.Collections.Generic;
using Rootborn.Ui;

namespace Rootborn.Screens.Hud
{
    /// <summary>
    /// 상호작용 종류 하나의 안내: 문구 키, 대상 이름을 끼우는 형식인지(대상 id가 장소 id), 대상 가운데에서 몇 칸 위에 띄우는지.
    /// </summary>
    public sealed record InteractionPromptSpec(string Kind, string TextKey, bool NamesLocation, float CellsAbove);

    /// <summary>
    /// 상호작용 안내 글자(Unity PlayerInteractionRouter의 "[E] …"). 앞의 대상 위에 한 줄. 종류별 문구 키와 높이는 Unity 판 상호작용
    /// 구현체의 InteractionPrompt·InteractionPromptOffset 값이다. 표에 없는 종류(자동문 등)는 안내가 없다(Unity 판도 빈 안내).
    /// 이동을 막는 화면이 떠 있거나 글자를 치는 동안은 숨긴다(그동안 상호작용 키가 월드로 가지 않는다).
    /// 대상이 바뀔 때만 글자를 다시 만들고, 움직일 때는 자리만 옮긴다.
    /// </summary>
    public sealed class InteractionPromptScreen : GameScreen
    {
        /// <summary>배치: 글자 크기, 글자 상자 폭(가운데 정렬), 색(Unity 안내 글자 색 (1, 0.96, 0.68)). 키 표시 "[E] "는 언어와 무관한 기호다.</summary>
        private static class Layout
        {
            public const float FontSize = 24f;
            public const float BoxWidth = 600f;
            public const float BoxHeight = 32f;
            public static readonly UiColor Color = UnityLayout.Color(1f, 0.96f, 0.68f);
            public const string KeyHint = "[E] ";
        }

        /// <summary>종류 → 안내. 새 상호작용 종류는 여기 한 줄.</summary>
        public static readonly InteractionPromptSpec[] Prompts =
        {
            new InteractionPromptSpec("npc", "ui.prompt.talk", false, 1.15f),                     // NpcInteractor
            new InteractionPromptSpec("daily-plan-board", "ui.prompt.plan-day", false, 1.1f),      // DailyPlanBoardInteractor
            new InteractionPromptSpec("student-day-end", "day-result.end-day", false, 1.15f),      // StudentDayEndInteractor
            new InteractionPromptSpec("location-visit", "ui.prompt.visit", true, 1.05f),           // LocationVisitInteractor
            new InteractionPromptSpec("location-activity", "life.location.explore", true, 1.25f),  // LocationActivityInteractor
        };

        private const string LocationType = "LocationDefinition";
        private const string GenericLocationKey = "life.location.generic";

        public const string LabelId = "hud.prompt";

        private readonly Dictionary<string, InteractionPromptSpec> _byKind = new Dictionary<string, InteractionPromptSpec>();
        private UiLabel _label;
        private string _shownKind, _shownId, _shownLanguage;
        private bool _subscribed;

        public InteractionPromptScreen(ScreenServices services) : base(services)
        {
            foreach (InteractionPromptSpec spec in Prompts) _byKind[spec.Kind] = spec;
        }

        public string Text => _label != null && _label.Visible ? _label.Text : string.Empty;

        protected override UiNode Build()
        {
            var root = new UiNode { Id = "hud.prompt.root" };
            _label = root.Add(new UiLabel { Id = LabelId, FontSize = Layout.FontSize, Color = Layout.Color, Align = UiAlign.Center, Visible = false });
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            if (!_subscribed && Services.Screens != null)
            {
                Services.Screens.TargetChanged += Refresh;
                Services.Screens.Opened += _ => Refresh();
                Services.Screens.Closed += _ => Refresh();
                _subscribed = true;
            }
            Refresh();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language || topic == ChangeTopics.All) Refresh();
        }

        private void Refresh()
        {
            ScreenStack screens = Services.Screens;
            InteractionTargetView? target = screens?.Target;
            if (!IsOpen || target == null || screens.MovementBlocked || !_byKind.TryGetValue(target.Value.Kind ?? string.Empty, out InteractionPromptSpec spec))
            {
                _label.Visible = false;
                return;
            }

            InteractionTargetView t = target.Value;
            if (t.Kind != _shownKind || t.Id != _shownId || _shownLanguage != Services.Text.Language)
            {
                _label.Text = Layout.KeyHint + (spec.NamesLocation ? F(spec.TextKey, LocationName(t.Id)) : T(spec.TextKey));
                _shownKind = t.Kind; _shownId = t.Id; _shownLanguage = Services.Text.Language;
            }
            float y = t.Y - spec.CellsAbove * t.Cell;
            _label.Place(0f, 0f, t.X - Layout.BoxWidth / 2f, y - Layout.BoxHeight / 2f, Layout.BoxWidth, Layout.BoxHeight);
            _label.Visible = true;
            if (_label.Parent != null && _label.Parent.Rect.W > 0) _label.Layout(_label.Parent.Rect);
        }

        private string LocationName(string locationId)
        {
            string key = Services.Names.Get(LocationType, locationId);
            return T(string.IsNullOrEmpty(key) ? GenericLocationKey : key);
        }
    }
}
