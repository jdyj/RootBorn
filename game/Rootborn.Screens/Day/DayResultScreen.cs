using System;
using System.Collections.Generic;
using Rootborn.Core.StudentLife;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Day
{
    /// <summary>하루 결과 화면의 배치 수치. Unity StudentDayResultPanel(1160x900 가운데, 뒤 전체를 55% 검정으로 가림).</summary>
    public static class DayResultLayout
    {
        public const float Width = 1160, Height = 900;
        public static readonly UiColor Backdrop = new UiColor(0, 0, 0, 140);
        public static readonly (float X, float Y, float W, float H) Title = (0, 390, 1040, 56);
        public const float TitleFont = 36;
        /// <summary>결산 글 영역: 패널 안쪽 왼쪽 42, 위·아래 112, 오른쪽 34(스크롤 막대 18 포함, Unity 막대 -52..-34).</summary>
        public const float BodyLeft = 42, BodyTop = 112, BodyRight = 34, BodyBottom = 112;
        public const float BodyFont = 20, SectionSpacing = 22;
        public static readonly (float X, float Y, float W, float H) NextDay = (0, -390, 300, 58);
        public const float ButtonFont = 22;
    }

    /// <summary>
    /// 하루 결과(하루 마감 지점 상호작용 "student-day-end"). Unity StudentDayEndInteractor → StudentDayResultPanel 순서:
    /// 하루가 진행 중이면 잠들어 하루를 끝내고(GameSession.Sleep — 취침 시각 전이어도 막지 않는다, 다음 날 07:00), 결산 구역을 위에서부터 보인다 — 한 활동, 결과(성장·학교 밖·진로 후보·장래희망·일과 칸),
    /// 퀘스트, 관계, 상태, 보상, 물건, 다음 날 안내, 장소 상태 기록, 캠페인, 체인. 다음 날 버튼이 다음 날을 시작하고 닫는다.
    /// 이미 끝난 날이면 끝내지 않고 그 날의 결산만 다시 보인다. 모달, 이동을 막는다. 닫기 버튼은 없다(Unity 판과 같음).
    /// </summary>
    public sealed class DayResultScreen : GameScreen
    {
        public const string InteractionKind = "student-day-end";
        public const string RootId = "day-result", PanelId = "day-result.panel", TitleId = "day-result.title", BodyId = "day-result.body", NextDayId = "day-result.next-day";

        private UiLabel _title;
        private UiSectionScrollView _body;
        private UiButton _next;
        private Dictionary<string, string> _displayKeys;
        private GameDayResult _result;

        public DayResultScreen(ScreenServices services) : base(services) { }

        public DayResultModel Model { get; private set; }
        public UiSectionScrollView Body => _body;

        /// <summary>키보드 처음 포커스: 다음 날 단추에서 시작한다(Enter 한 번으로 넘긴다).</summary>
        protected internal override UiNode InitialFocus => _next;

        protected override UiNode Build()
        {
            var root = new UiNode { Id = RootId };
            root.Stretch(0, 0, 0, 0);
            root.Add(new UiBox { Color = DayResultLayout.Backdrop, Interactive = true }).Stretch(0, 0, 0, 0);
            var panel = root.Add(new UiPanel { Id = PanelId, Skin = UiSkin.Panel });
            panel.Centered(0, 0, DayResultLayout.Width, DayResultLayout.Height);
            var t = DayResultLayout.Title;
            var area = new PanelArea(DayResultLayout.Width, DayResultLayout.Height, Services.PaddingOf(UiSkin.Panel));
            _title = panel.Add(new UiLabel { Id = TitleId, FontSize = DayResultLayout.TitleFont, Align = UiAlign.Center }).Centered(t.X, Math.Min(t.Y, area.CenterYFromTop(t.H)), t.W, t.H);
            _body = panel.Add(new UiSectionScrollView { Id = BodyId, FontSize = DayResultLayout.BodyFont, SectionSpacing = DayResultLayout.SectionSpacing });
            _body.Stretch(DayResultLayout.BodyLeft, DayResultLayout.BodyTop, DayResultLayout.BodyRight, DayResultLayout.BodyBottom);
            var n = DayResultLayout.NextDay;
            _next = panel.Add(new UiButton { Id = NextDayId, FontSize = DayResultLayout.ButtonFont }).Centered(n.X, n.Y, n.W, n.H);
            _next.Clicked += StartNextDay;
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            if (Services.Session == null) { Close(); return; }
            GameSession session = Services.Session;
            DayBaseline.For(session);
            _displayKeys ??= DayResultText.BuildDisplayKeys(session);
            if (session.Student.DayState == StudentDayState.InProgress)
                _result = Services.Session.Sleep(); // 잠자리에서 잔다(흐르는 시간 설계 3.3 — 하루 마감 흐름 그대로, 회복은 잔 시각으로)
            else
            {
                GameDayResult last = session.LastDayResult;
                _result = last.Ended && last.Summary.DayNumber == session.Student.CurrentDay ? last : default;
            }
            _body.ScrollOffset = 0;
            Render();
        }

        protected internal override void OnChanged(string topic)
        {
            if (Services.Session != null && Model != null) Render();
        }

        private void StartNextDay()
        {
            if (Model == null || !Model.CanStartNextDay) return;
            GameSession session = Services.Session;
            if (!Services.Session.StartNextDay()) return;
            DayBaseline.For(session).Capture();
            Close();
        }

        private string PeopleText(IReadOnlyList<string> names)
        {
            string separator = T("ui.hud.room-separator"), text = string.Empty;
            for (int i = 0; i < names.Count; i++)
                text += (i > 0 ? separator : string.Empty) + (string.IsNullOrEmpty(names[i]) ? T("ui.room.guest-name") : names[i]);
            return text;
        }

        private void Render()
        {
            GameSession session = Services.Session;
            Model = DayResultModel.From(session, _result, DayBaseline.For(session).Summary());
            var text = new DayResultText(T, Services.Text, _displayKeys);
            _title.Text = text.Title(Model.Summary.DayNumber);
            _next.Text = text.Ui(Model.Sleeping ? "sleeping" : Model.WaitingForOthers ? "waiting-others" : "next-day");
            _next.Enabled = Model.CanStartNextDay;
            var sections = new List<string>(17);
            // 자는 중 화면(흐르는 시간 설계 3.3절): 방에 아직 깨어 있는 사람. 모두 잠들거나 07:00이 되면 다음 날 단추가 켜진다.
            // 사람은 이름으로 보인다(방의 신원 id가 아니다). 이름이 없으면 기본 말.
            if (Model.Sleeping && Model.AwakeOthers.Count > 0) sections.Add(F("day-result.sleeping-awake-others", PeopleText(Model.AwakeOtherNames)));
            sections.AddRange(new[]
            {
                text.List("activities", Model.Summary.CompletedActivityIds, "empty-activities"),
                text.Results(Model),
                text.Quests(Model.QuestInventory.QuestEntries),
                text.Relationships(Model.RelationshipCondition.RelationshipEntries),
                text.Conditions(Model.RelationshipCondition.StatusEntries),
                text.Rewards(Model.QuestInventory.RewardEntries),
                text.Inventory(Model.QuestInventory.InventoryDeltas),
                text.NextGuide(Model),
            });
            sections.AddRange(text.LocationStates(session, Model.LocationStates));
            sections.Add(text.Campaign(Model.Campaign));
            sections.Add(text.Chain(Model));
            _body.SetSections(sections);
        }
    }
}
