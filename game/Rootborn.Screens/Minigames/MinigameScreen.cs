using System;
using Rootborn.Core.Careers;
using Rootborn.Core.Minigames;
using Rootborn.Ui;

namespace Rootborn.Screens.Minigames
{
    /// <summary>
    /// 미니게임(Unity MinigamePanel). 가운데 900x620 패널: 제목(도전 종류), 안내, 도전 자리(800x290), 안내 글, 진행 줄, 닫기.
    /// 예약된 판(MinigameModel)을 받아 Core MinigameSession으로 돌린다. 시간은 화면 쌓기가 넘기는 dt로만 흐른다(OnTick).
    /// 도전 종류마다 그리는 부분은 ChallengeViewCatalog에서 고른다. 판이 끝나면 한 번만 정산하고 결과를 보여 준 채 둔다.
    /// 닫기·Esc는 끝나지 않은 판을 그만두고(포기로 정산) 닫는다. 정산이 거절되면 다시 저장 단추를 보이고 닫지 않는다(Unity와 같음).
    /// 이동을 막는다(Unity: UiGameplayInputLock).
    /// </summary>
    public sealed class MinigameScreen : GameScreen
    {
        /// <summary>배치 — Unity MinigamePanel.Build 수치(가운데 기준, y는 위로). 새 디자인에서는 여기만 고친다.</summary>
        internal static class Metrics
        {
            public const float Width = 900f, Height = 620f;
            public const float TitleY = 250f, TitleW = 720f, TitleH = 50f, TitleFont = 30f;
            public const float InstructionY = 182f, InstructionW = 780f, InstructionH = 76f, InstructionFont = 21f;
            public const float StatusY = -235f, StatusW = 800f, StatusH = 32f, StatusFont = 19f;
            public const float FeedbackY = -192f, FeedbackW = 800f, FeedbackH = 72f, FeedbackFont = 20f;
            public const float BodyW = 800f, BodyH = 290f;
            public const float CloseX = 390f, CloseY = 250f, CloseW = 52f, CloseH = 48f, CloseFont = 19f;
            public const float RetryY = -278f, RetryW = 220f, RetryH = 40f, RetryFont = 19f;
            public static readonly UiColor Ink = UnityLayout.Color(.25f, .18f, .12f);
        }

        public const string PanelId = "minigame";
        public const string TitleId = "minigame.title";
        public const string InstructionId = "minigame.instruction";
        public const string StatusId = "minigame.status";
        public const string FeedbackId = "minigame.feedback";
        public const string BodyId = "minigame.body";
        public const string CloseId = "minigame.close";
        public const string RetryId = "minigame.retry-save";

        private readonly ChallengeViewCatalog _views;
        private readonly ChallengeViewContext _context;
        private UiLabel _title, _status;
        private UiTextBlock _instruction, _feedback;
        private UiNode _body;
        private UiButton _close, _retry;
        private IChallengeView _view;
        private ChallengeSession _shown;
        private (int Stage, int Seconds, int Progress, int Mistakes, bool Preview, bool Complete) _signature;
        private bool _delivered;

        public MinigameScreen(ScreenServices services) : this(services, ChallengeViewCatalog.Default()) { }

        public MinigameScreen(ScreenServices services, ChallengeViewCatalog views) : base(services)
        {
            _views = views ?? throw new ArgumentNullException(nameof(views));
            _context = new ChallengeViewContext(this);
        }

        public MinigameModel Model { get; private set; } = MinigameModel.None;
        public MinigameSession Session { get; private set; }
        /// <summary>정산 명령의 마지막 결과(정산 전이면 null).</summary>
        public AdultCareerResult? LastSettlement { get; private set; }
        /// <summary>정산한 횟수(성공한 것만). 같은 판은 한 번이어야 한다.</summary>
        public int Settlements { get; private set; }
        public IChallengeView View => _view;

        protected override UiNode Build()
        {
            var root = new UiPanel { Id = PanelId, Skin = UiSkin.Panel };
            root.Centered(0f, 0f, Metrics.Width, Metrics.Height);
            _title = root.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.TitleY, Metrics.TitleW, Metrics.TitleH);
            _instruction = root.Add(new UiTextBlock { Id = InstructionId, FontSize = Metrics.InstructionFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.InstructionY, Metrics.InstructionW, Metrics.InstructionH);
            _status = root.Add(new UiLabel { Id = StatusId, FontSize = Metrics.StatusFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.StatusY, Metrics.StatusW, Metrics.StatusH);
            _feedback = root.Add(new UiTextBlock { Id = FeedbackId, FontSize = Metrics.FeedbackFont, Color = Metrics.Ink, Align = UiAlign.Center })
                .Centered(0f, Metrics.FeedbackY, Metrics.FeedbackW, Metrics.FeedbackH);
            _body = root.Add(new UiNode { Id = BodyId }).Centered(0f, 0f, Metrics.BodyW, Metrics.BodyH);
            _close = root.Add(new UiButton { Id = CloseId, FontSize = Metrics.CloseFont }).Centered(Metrics.CloseX, Metrics.CloseY, Metrics.CloseW, Metrics.CloseH);
            _close.Clicked += CloseOrForfeit;
            _retry = root.Add(new UiButton { Id = RetryId, FontSize = Metrics.RetryFont, Visible = false }).Centered(0f, Metrics.RetryY, Metrics.RetryW, Metrics.RetryH);
            _retry.Clicked += RetrySettlement;
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            Model = MinigameModel.From(Services.Session);
            Session = Model.NewSession(Services.Session);
            LastSettlement = null;
            _delivered = false;
            _shown = null;
            _retry.Visible = false;
            _close.Text = T("ui.close-symbol");
            _retry.Text = T("minigame.save-again");
            if (Session == null)
            {
                // 예약이 없거나 정의를 찾지 못했다: 열 판이 없다.
                Close();
                return;
            }
            RefreshView();
        }

        protected internal override void OnChanged(string topic)
        {
            if (topic != ChangeTopics.Language || Session == null) return;
            _close.Text = T("ui.close-symbol");
            _retry.Text = T("minigame.save-again");
            _shown = null; // 글자를 다시 받도록 도전을 다시 그린다(같은 판, 같은 값)
            _signature = default;
            RefreshView();
        }

        protected internal override bool OnTick(float seconds)
        {
            if (Session == null) return false;
            if (!Session.IsComplete) Session.AdvanceTime(seconds);
            bool changed = Signature() != _signature || _shown != Session.Current;
            if (changed) RefreshView();
            return _view != null && !Session.IsComplete && _shown == Session.Current && _view.Animate(Session.Current) || changed;
        }

        protected internal override bool OnKey(ScreenKey key, bool shift)
        {
            if (key == ScreenKey.Escape) { CloseOrForfeit(); return true; }
            if (Session != null && !Session.IsComplete && _view != null && _view.OnKey(key, Session.Current)) return true;
            // 판이 떠 있는 동안 상호작용 키·Space는 월드로 가지 않는다(Space는 Unity에서도 박자 도전에서만 쓴다).
            return key == ScreenKey.Interact || key == ScreenKey.Space || key == ScreenKey.Rotate || key == ScreenKey.AltLeft || key == ScreenKey.AltRight;
        }

        private (int, int, int, int, bool, bool) Signature()
        {
            ChallengeSession c = Session.Current;
            return (Session.StageIndex, (int)Math.Ceiling(c.RemainingSeconds), c.ProgressIndex, c.Mistakes, c.PreviewVisible, Session.IsComplete);
        }

        /// <summary>값을 다시 읽어 그린다(입력 뒤, 값이 바뀐 프레임).</summary>
        internal void RefreshView()
        {
            if (Session == null || !IsOpen) return;
            if (_shown != Session.Current) BuildStage(Session.Current);
            ChallengeSession c = Session.Current;
            _signature = Signature();
            if (Session.IsComplete)
            {
                _view?.Refresh(c);
                _status.Text = F(Session.IsCancelled ? "minigame.cancelled" : "minigame.result", Session.Score);
                _instruction.Text = T(Session.Succeeded ? "minigame.success" : "minigame.retry");
                Deliver();
                return;
            }
            _status.Text = F("minigame.progress", Session.StageIndex + 1, Session.StageCount, c.ProgressIndex, c.TotalSteps, (int)Math.Ceiling(c.RemainingSeconds), c.Mistakes);
            _instruction.Text = T(c.PromptKey);
            _view?.Refresh(c);
            _view?.Animate(c);
        }

        private void BuildStage(ChallengeSession c)
        {
            _body.Children.Clear();
            var stage = _body.Add(new UiNode { Id = "minigame.stage" });
            stage.Stretch(0, 0, 0, 0);
            _context.Stage = stage;
            _shown = c;
            _feedback.Text = string.Empty;
            _title.Text = T("minigame.title." + c.Mechanic.ToString().ToLowerInvariant());
            _view = _views.Create(c.Mechanic);
            _view?.Build(_context, c);
            Services.Screens?.Relayout();
            if (Services.Screens != null && Services.Screens.Tree.Focused != null && !IsInside(Services.Screens.Tree.Focused, Root))
                Services.Screens.Tree.SetFocus(null);
        }

        private static bool IsInside(UiNode node, UiNode root)
        {
            for (UiNode n = node; n != null; n = n.Parent) if (n == root) return true;
            return false;
        }

        /// <summary>끝난 판을 한 번만 정산한다(Unity Deliver).</summary>
        private void Deliver()
        {
            if (_delivered || Session == null || !Session.IsComplete) return;
            _delivered = true;
            Settle();
        }

        private bool Settle()
        {
            if (Services.Commands == null) return false;
            MinigameModel model = Model;
            MinigameSession played = Session;
            AdultCareerResult result = Services.Commands.Run(s => model.Settle(s, played), ChangeTopics.Student);
            LastSettlement = result;
            if (result.Applied)
            {
                Settlements++;
                return true;
            }
            SetFeedback(T("minigame.save-failed"));
            _retry.Visible = true;
            return false;
        }

        private void RetrySettlement()
        {
            if (!_retry.Visible || Session == null || !Session.IsComplete) return;
            if (!Settle()) return;
            _retry.Visible = false;
            SetFeedback(T("minigame.saved"));
        }

        /// <summary>Unity Close: 끝나지 않은 판은 그만두고 정산한 뒤 닫는다. 정산이 거절되어 다시 저장을 기다리면 닫지 않는다.</summary>
        private void CloseOrForfeit()
        {
            if (Session != null && !Session.IsComplete) Session.Cancel();
            if (Session != null) RefreshView();
            if (_retry.Visible) return;
            Close();
        }

        internal string Text(string key) => T(key);
        internal string Format(string key, object[] args) => F(key, args);
        internal string ShownText(string keyOrText) => Shown(keyOrText);
        internal void SetFeedback(string text) { if (_feedback != null) _feedback.Text = text ?? string.Empty; }
    }
}
