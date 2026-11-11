using Rootborn.Screens.Objectives;
using Rootborn.Ui;

namespace Rootborn.Screens.Hud
{
    /// <summary>
    /// 추적 목표 HUD의 표시 모델: 목표 수첩에서 "추적"한 항목 하나(ObjectiveJournalTracking). Unity TrackedObjectiveHud.Show와 같은 규칙:
    /// 제목은 항목 제목(없으면 "추적 중인 목표"), 진행은 항목 진행(없으면 상태), 힌트는 다음 할 일(없으면 부제).
    /// 추적한 것이 없거나 그 항목이 목록에서 사라지면 없음(Unity: 추적 지정이 없으면 HUD를 띄우지 않고, 사라지면 숨긴다).
    /// </summary>
    public sealed class TrackedObjectiveModel
    {
        public static readonly TrackedObjectiveModel None = new TrackedObjectiveModel();

        public bool HasItem { get; private set; }
        public string StableKey { get; private set; } = string.Empty;
        public JournalText Title { get; private set; } = JournalText.Empty;
        public JournalText Progress { get; private set; } = JournalText.Empty;
        public JournalText Hint { get; private set; } = JournalText.Empty;

        public static TrackedObjectiveModel From(ScreenServices services)
        {
            if (services?.Session == null) return None;
            ObjectiveJournalTracking tracking = ObjectiveJournalTracking.For(services);
            if (!tracking.HasTracked) return None;
            var items = new ObjectiveJournalAggregator(ObjectiveJournalSources.For(services)).Build(services.Session);
            JournalItem item = ObjectiveJournalAggregator.Find(items, tracking.StableKey, out _);
            if (item == null) return None;
            return new TrackedObjectiveModel
            {
                HasItem = true,
                StableKey = item.StableKey,
                Title = item.Title.IsEmpty ? JournalText.Key("ui.objectives.tracked") : item.Title,
                Progress = item.Progress.IsEmpty ? item.State : item.Progress,
                Hint = item.NextHint.IsEmpty ? item.Subtitle : item.NextHint,
            };
        }

        public bool SameAs(TrackedObjectiveModel other) => other != null && HasItem == other.HasItem && StableKey == other.StableKey
            && Title.Equals(other.Title) && Progress.Equals(other.Progress) && Hint.Equals(other.Hint);
    }

    /// <summary>
    /// 추적 목표 HUD(Unity TrackedObjectiveHud): 위쪽 줄 아래 오른쪽의 640x200 칸, 수첩 아이콘과 제목·진행·힌트 세 줄.
    /// 수첩에서 추적한 항목 하나만 보인다(목표 수첩 규칙: 상시 목표 HUD는 이것 하나). 이동을 막는 화면이 떠 있는 동안 숨긴다
    /// (Unity: 입력 잠금 중 투명). 게임 진행이 바뀌거나 추적이 바뀌면 그 항목을 다시 받는다.
    /// </summary>
    public sealed class TrackedObjectiveHudScreen : GameScreen, IJournalTextResolver
    {
        /// <summary>배치(칸 안, 왼쪽 위 기준). Unity: 아이콘 (40, 52) 80x80 / 글자 x 136, 오른쪽 36 안쪽: 제목 y 36 높이 32 22, 진행 y 76 높이 28 18, 힌트 y 110 높이 54 18.</summary>
        private static class Layout
        {
            public const float IconX = 40f, IconY = 52f, IconSize = 80f;
            public const float TextX = 136f, TextRight = 36f;
            public const float TitleY = 36f, TitleHeight = 32f, TitleFont = 22f;
            public const float ProgressY = 76f, ProgressHeight = 28f, ProgressFont = 18f;
            public const float HintY = 110f, HintHeight = 54f, HintFont = 18f;
            public const string Icon = "icon:journal";
        }

        public const string PanelId = "hud.tracked";
        public const string TitleId = "hud.tracked.title";
        public const string ProgressId = "hud.tracked.progress";
        public const string HintId = "hud.tracked.hint";

        private UiPanel _panel;
        private UiLabel _title, _progress;
        private UiTextBlock _hint;
        private string _shownLanguage;
        private bool _subscribed;

        public TrackedObjectiveHudScreen(ScreenServices services) : base(services) { }

        public TrackedObjectiveModel Model { get; private set; } = TrackedObjectiveModel.None;
        public bool IsShown => _panel != null && _panel.Visible;
        /// <summary>보이는 세 줄(제목, 진행, 힌트).</summary>
        public string VisibleText => IsShown ? (_title.Text + "\n" + _progress.Text + "\n" + _hint.Text).Trim() : string.Empty;

        protected override UiNode Build()
        {
            var root = new UiNode { Id = "hud.tracked.root" };
            _panel = HudLayout.PlaceTracked(root.Add(new UiPanel { Id = PanelId, Skin = UiSkin.Panel, Interactive = false }));
            _panel.Add(new UiImage { Image = Layout.Icon }).Place(0f, 0f, Layout.IconX, Layout.IconY, Layout.IconSize, Layout.IconSize);
            _title = Line(new UiLabel { Id = TitleId, FontSize = Layout.TitleFont, Color = HudLayout.Ink }, Layout.TitleY, Layout.TitleHeight);
            _progress = Line(new UiLabel { Id = ProgressId, FontSize = Layout.ProgressFont, Color = HudLayout.Ink }, Layout.ProgressY, Layout.ProgressHeight);
            _hint = Line(new UiTextBlock { Id = HintId, FontSize = Layout.HintFont, Color = HudLayout.Ink, LineSpacing = 2 }, Layout.HintY, Layout.HintHeight);
            _panel.Visible = false;
            return root;
        }

        private T Line<T>(T node, float y, float height) where T : UiNode
        {
            _panel.Add(node);
            node.AnchorMinX = 0f; node.AnchorMaxX = 1f; node.AnchorMinY = node.AnchorMaxY = 0f;
            node.OffsetMinX = Layout.TextX; node.OffsetMaxX = -Layout.TextRight;
            node.OffsetMinY = y; node.OffsetMaxY = y + height;
            return node;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            if (!_subscribed && Services.Screens != null)
            {
                Services.Screens.Opened += _ => UpdateVisibility();
                Services.Screens.Closed += _ => UpdateVisibility();
                _subscribed = true;
            }
            Refresh(true);
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Map) return;
            Refresh(false);
        }

        private void Refresh(bool force)
        {
            TrackedObjectiveModel model = TrackedObjectiveModel.From(Services);
            bool language = _shownLanguage != Services.Text.Language;
            if (force || language || !model.SameAs(Model))
            {
                Model = model;
                _shownLanguage = Services.Text.Language;
                if (model.HasItem)
                {
                    _title.Text = model.Title.Resolve(this);
                    _progress.Text = model.Progress.Resolve(this);
                    _hint.Text = model.Hint.Resolve(this);
                }
            }
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            if (_panel == null) return;
            bool blocked = false;
            if (Services.Screens != null)
                foreach (GameScreen screen in Services.Screens.OpenScreens)
                    if (screen.BlocksMovement) { blocked = true; break; }
            _panel.Visible = IsOpen && Model.HasItem && !blocked;
        }

        string IJournalTextResolver.T(string key) => T(key);
        string IJournalTextResolver.F(string key, object[] args) => F(key, args);
        string IJournalTextResolver.Shown(string keyOrText) => Shown(keyOrText);
    }
}
