using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;
using Rootborn.Data;
using Rootborn.Ui;

namespace Rootborn.Screens.Hud
{
    /// <summary>
    /// 상호작용 종류 하나의 안내: 문구 키, 대상 가운데에서 몇 칸 위에 띄우는지, 대상 이름을 끼우는 경우 이름 키를 찾는 법(정의 + 대상 id → 이름 키).
    /// 이름을 끼우는 문구는 {0} 자리에 이름을 넣는다. 이름을 못 찾으면 FallbackNameKey를 이름으로, 그것도 없으면 NoNameTextKey 문구를 쓴다.
    /// </summary>
    public sealed record InteractionPromptSpec(string Kind, string TextKey, float CellsAbove,
        Func<GameDefinitions, string, string> NameKeyOf = null, string FallbackNameKey = null, string NoNameTextKey = null);

    /// <summary>안내 문구에 끼울 대상 이름 키 찾기(정의 종류마다 하나). 대상 id로 그 정의를 찾아 DisplayNameKey를 준다.</summary>
    public static class InteractionPromptNames
    {
        public static string Location(GameDefinitions definitions, string id) => Of<Location>(definitions, id, l => l.DisplayNameKey);
        public static string PartTimeWork(GameDefinitions definitions, string id) => Of<PartTimeWork>(definitions, id, w => w.DisplayNameKey);
        public static string Exploration(GameDefinitions definitions, string id) => Of<ExplorationInteraction>(definitions, id, e => e.DisplayNameKey);
        public static string Discovery(GameDefinitions definitions, string id) => Of<Rootborn.Core.StudentLife.Discovery>(definitions, id, d => d.DisplayNameKey);

        private static string Of<T>(GameDefinitions definitions, string id, Func<T, string> key) where T : class
            => definitions != null && !string.IsNullOrEmpty(id) && definitions.TryGet(id, out T found) ? key(found) : null;
    }

    /// <summary>
    /// 상호작용 안내 글자(Unity PlayerInteractionRouter의 "[E] …"). 앞의 대상 위에 한 줄. 종류별 문구 키와 높이는 Unity 판 상호작용
    /// 구현체의 InteractionPrompt·InteractionPromptOffset 값이다. 표에 없는 종류(자동문 등)는 안내가 없다(Unity 판도 빈 안내).
    /// 이동을 막는 화면이 떠 있거나 글자를 치는 동안은 숨긴다(그동안 상호작용 키가 월드로 가지 않는다). 상호작용이 연 패널·모달
    /// (상자, 대화 등)이 떠 있는 동안에도 숨긴다(그 대상과 이미 상호작용하는 중이다).
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

        private const string GenericLocationKey = "life.location.generic";

        /// <summary>종류 → 안내(등록 표). 새 상호작용 종류는 여기 한 줄. 높이는 Unity 판 상호작용 구현체의 InteractionPromptOffset.</summary>
        public static readonly InteractionPromptSpec[] Prompts =
        {
            new InteractionPromptSpec("npc", "ui.prompt.talk", 1.15f),                                                                  // NpcInteractor
            new InteractionPromptSpec("daily-plan-board", "ui.prompt.plan-day", 1.1f),                                                   // DailyPlanBoardInteractor
            new InteractionPromptSpec("student-day-end", "ui.prompt.sleep", 1.15f),                                                   // StudentDayEndInteractor
            new InteractionPromptSpec("location-visit", "ui.prompt.visit", 1.05f, InteractionPromptNames.Location, GenericLocationKey),          // LocationVisitInteractor
            new InteractionPromptSpec("location-activity", "life.location.explore", 1.25f, InteractionPromptNames.Location, GenericLocationKey), // LocationActivityInteractor
            new InteractionPromptSpec("part-time-work", "ui.prompt.part-time-work", 1.05f, InteractionPromptNames.PartTimeWork,
                NoNameTextKey: "ui.prompt.part-time-work-generic"),                                                                         // PartTimeWorkInteractor
            new InteractionPromptSpec("career-practice", "ui.prompt.career-practice", 1.15f),                                            // StudentLifeSceneInteraction(연습)
            new InteractionPromptSpec("exploration", "ui.prompt.explore", 1.15f, InteractionPromptNames.Exploration,
                NoNameTextKey: "ui.prompt.explore-generic"),                                                                                // ExplorationInteractionPointInteractor
            new InteractionPromptSpec(AdultFacility.FacilityScreen.InteractionKind, "facility.interact", 1.1f),                          // CareerFacilityInteractor
            new InteractionPromptSpec(Discoveries.DiscoveryNoticeScreen.InteractionKind, "ui.prompt.discovery", 1.05f, InteractionPromptNames.Discovery,
                NoNameTextKey: "ui.prompt.discovery-generic"),                                                                              // DiscoveryPointInteractor
            new InteractionPromptSpec(Storage.StorageScreen.InteractionKind, "ui.prompt.open-storage", 1.1f),                            // 놓인 상자(MonoGame 판이 더함)
        };

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
            if (!IsOpen || target == null || screens.MovementBlocked || screens.InteractionScreenOpen || !_byKind.TryGetValue(target.Value.Kind ?? string.Empty, out InteractionPromptSpec spec))
            {
                _label.Visible = false;
                return;
            }

            InteractionTargetView t = target.Value;
            if (t.Kind != _shownKind || t.Id != _shownId || _shownLanguage != Services.Text.Language)
            {
                _label.Text = Layout.KeyHint + PromptText(spec, t.Id);
                _shownKind = t.Kind; _shownId = t.Id; _shownLanguage = Services.Text.Language;
            }
            float y = t.Y - spec.CellsAbove * t.Cell;
            _label.Place(0f, 0f, t.X - Layout.BoxWidth / 2f, y - Layout.BoxHeight / 2f, Layout.BoxWidth, Layout.BoxHeight);
            _label.Visible = true;
            if (_label.Parent != null && _label.Parent.Rect.W > 0) _label.Layout(_label.Parent.Rect);
        }

        private string PromptText(InteractionPromptSpec spec, string targetId)
        {
            if (spec.NameKeyOf == null) return T(spec.TextKey);
            string nameKey = spec.NameKeyOf(Services.Definitions, targetId);
            if (string.IsNullOrEmpty(nameKey)) nameKey = spec.FallbackNameKey;
            if (string.IsNullOrEmpty(nameKey)) return T(spec.NoNameTextKey ?? spec.TextKey);
            return F(spec.TextKey, T(nameKey));
        }
    }
}
