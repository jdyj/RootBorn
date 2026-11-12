using System;
using System.Collections.Generic;
using Rootborn.Core.Careers;
using Rootborn.Core.Inventory;
using Rootborn.Core.StudentLife;
using Rootborn.Game;
using Rootborn.Screens.Career;
using Rootborn.Screens.Day;
using Rootborn.Ui;
using Rootborn.Screens.Hud;

namespace Rootborn.Screens.AdultFacility
{
    /// <summary>
    /// 성인 시설 관리 화면의 배치 수치. Unity FacilityManagementPanel(880x680 가운데)의 위쪽 왼쪽 기준 좌표를 그대로 옮겼다
    /// (x는 오른쪽으로, y는 아래로 잰 값). 새 디자인에서는 여기만 고친다.
    /// </summary>
    public static class FacilityLayout
    {
        public const float Width = 880, Height = 680;
        public static readonly (float X, float Y, float W, float H) Title = (32, 20, 700, 44);
        public const float TitleFont = 30;
        public static readonly (float X, float Y, float W, float H) Close = (754, 24, 94, 40);
        public const float CloseFont = 20;
        public static readonly (float X, float Y, float W, float H) Summary = (32, 78, 816, 90);
        public const float SummaryFont = 22;
        public static readonly (float X, float Y, float W, float H) Works = (32, 178, 816, 262);
        /// <summary>
        /// 일 한 줄 = 칸 셋(정보, 직접 일하기, 자동 완료). Unity는 정보 450 + 단추 142 둘이었다. 같은 폭 칸 셋으로 줄을 이룬다.
        /// 칸 셋 + 간격 둘 + 오른쪽 스크롤 막대 자리가 목록 폭(다른 줄과 같은 글 칸 816) 안에 든다. 막대 자리를 폭 밖에 더하면 패널 여백·테두리에 겹쳤다.
        /// </summary>
        public const float CellHeight = 114, CellSpacing = 12;
        public static readonly float CellWidth = MathF.Floor((Works.W - CellSpacing * 2 - UiCardScroller.ScrollbarWidth - UiCardScroller.ScrollbarGap) / 3f);
        /// <summary>칸 안 글자와 칸 가장자리 사이(정보 칸·단추 칸 같게).</summary>
        public const float CellPadding = 10;
        /// <summary>정보 칸 글자. 18이면 이름·시간(끝나는 시각 포함)·최대 보상이 다섯 줄로 늘어 칸 아래로 잘렸다(2026-11-12 스크린샷) — 16이면 칸 안에 든다.</summary>
        public const float InfoFont = 16, ButtonFont = 20, HintFont = 16;
        public static readonly (float X, float Y, float W, float H) Cost = (32, 450, 816, 88); // Unity는 54였다. 비용 글이 길어지는 언어를 위해 세 줄 자리를 둔다
        public const float CostFont = 18;
        public const float ActionY = 544, ActionW = 256, ActionH = 48, ActionFont = 20;
        public const float TransitionX = 32, PracticeX = 312, UpgradeX = 592;
        public static readonly (float X, float Y, float W, float H) Status = (32, 608, 816, 48);
        public const float StatusFont = 19;
        public static readonly UiColor Ink = UnityLayout.Color(0.22f, 0.19f, 0.18f);
    }

    /// <summary>
    /// 성인 시설 관리(Unity FacilityManagementPanel). 시설 앞 상호작용(종류 "career-facility", 대상 id = 시설 id)이 연다.
    /// 제목(단계 이름), 시설 단계·평판, 일 목록(스크롤: 이름·TP·숙련·성공 횟수·최대 보상, 직접 일하기, 자동 완료), 다음 단계 비용,
    /// "이 성인 진로 선택"·진로 체험·시설 확장, 결과 글, 닫기. 이동을 막는다(Unity: UiGameplayInputLock).
    /// 할 수 있는지는 화면이 판단하지 않는다 — 누르면 GameSession 명령이 하거나 거절하고, 그 거절 이유를 결과 글로 보인다.
    /// 직접 일하기는 일을 예약하고(그러면 미니게임 화면이 저절로 열린다), 자동 완료는 바로 정산한다.
    /// </summary>
    public sealed class FacilityScreen : GameScreen
    {
        public const string InteractionKind = "career-facility";
        public const string RootId = "facility", TitleId = "facility.title", CloseId = "facility.close", SummaryId = "facility.summary", WorksId = "facility.works",
            CostId = "facility.cost", TransitionId = "facility.transition", PracticeId = "facility.practice", UpgradeId = "facility.upgrade", StatusId = "facility.status";

        private const int ColumnsPerWork = 3;
        private const int InfoColumn = 0, PlayColumn = 1, SkipColumn = 2;

        private UiTextBlock _summary, _cost, _status;
        private UiLabel _title;
        private UiButton _close, _transition, _practice, _upgrade;
        private UiCardGrid _works;
        private readonly CardLabel _label = new CardLabel();
        private readonly List<string> _lines = new List<string>(8);
        private string _facilityId;
        private string[] _infoTexts = Array.Empty<string>();
        private string[] _bonusTexts = Array.Empty<string>();
        private string _statusText = string.Empty;
        private Func<int> _seeds = () => Guid.NewGuid().GetHashCode();

        public FacilityScreen(ScreenServices services) : base(services) { }

        public FacilityModel Model { get; private set; } = FacilityModel.Empty;
        /// <summary>마지막으로 누른 명령의 결과 종류(없으면 null). 테스트와 로그용.</summary>
        public AdultCareerResultKind? LastResult { get; private set; }
        public string StatusText => _statusText;

        /// <summary>일 목록 칸 번호: 일 번호 * 3 + (0 정보, 1 직접 일하기, 2 자동 완료).</summary>
        public static int PlayCell(int workIndex) => workIndex * ColumnsPerWork + PlayColumn;
        public static int SkipCell(int workIndex) => workIndex * ColumnsPerWork + SkipColumn;
        public UiRect CellRect(int cell) => _works.CellRect(cell);
        public UiCardGrid WorkGrid => _works;

        /// <summary>일 줄의 이득 글자 id(직접 일하기 칸 안의 한 줄, 노드가 아니다). 그 일의 이득이 0이면 줄이 없다.</summary>
        public static string WorkBonusId(string workId) => "work-bonus-" + workId;

        /// <summary>그 일의 지금 이득 글자(ui.facility.work-bonus). 이득이 없으면(둘 다 0) null — 줄을 숨긴다.</summary>
        public string WorkBonusText(string workId)
        {
            for (int i = 0; i < Model.Works.Length; i++)
                if (Model.Works[i].Id == workId) return i < _bonusTexts.Length ? _bonusTexts[i] : null;
            return null;
        }

        /// <summary>미니게임 판 시드를 정하는 쪽(테스트에서 고정한다).</summary>
        public void UseSeeds(Func<int> seeds) => _seeds = seeds ?? _seeds;

        protected override UiNode Build()
        {
            var root = new UiPanel { Id = RootId, Skin = UiSkin.Panel };
            root.Centered(0f, 0f, FacilityLayout.Width, FacilityLayout.Height);
            _title = At(root.Add(new UiLabel { Id = TitleId, FontSize = FacilityLayout.TitleFont, Color = FacilityLayout.Ink }), FacilityLayout.Title);
            _close = At(root.Add(new UiButton { Id = CloseId, FontSize = FacilityLayout.CloseFont }), FacilityLayout.Close);
            _close.Clicked += Close;
            _summary = At(root.Add(new UiTextBlock { Id = SummaryId, FontSize = FacilityLayout.SummaryFont, Color = FacilityLayout.Ink }), FacilityLayout.Summary);
            _works = At(root.Add(new UiCardGrid
            {
                Id = WorksId, Padding = 0, DrawBackground = false, CellWidth = FacilityLayout.CellWidth, CellHeight = FacilityLayout.CellHeight, Spacing = FacilityLayout.CellSpacing,
            }), FacilityLayout.Works);
            _works.DrawCell = PaintCell;
            _works.CellClicked += PressCell;
            _cost = At(root.Add(new UiTextBlock { Id = CostId, FontSize = FacilityLayout.CostFont, Color = FacilityLayout.Ink }), FacilityLayout.Cost);
            _transition = Action(root, TransitionId, FacilityLayout.TransitionX);
            _transition.Clicked += ChooseRoute;
            _practice = Action(root, PracticeId, FacilityLayout.PracticeX);
            _practice.Clicked += Practice;
            _upgrade = Action(root, UpgradeId, FacilityLayout.UpgradeX);
            _upgrade.Clicked += Upgrade;
            _status = At(root.Add(new UiTextBlock { Id = StatusId, FontSize = FacilityLayout.StatusFont, Color = FacilityLayout.Ink }), FacilityLayout.Status);
            return root;
        }

        private static T At<T>(T node, (float X, float Y, float W, float H) r) where T : UiNode
        {
            node.Place(0f, 0f, r.X, r.Y, r.W, r.H);
            return node;
        }

        private static UiButton Action(UiPanel root, string id, float x)
        {
            var button = root.Add(new UiButton { Id = id, FontSize = FacilityLayout.ActionFont });
            button.Place(0f, 0f, x, FacilityLayout.ActionY, FacilityLayout.ActionW, FacilityLayout.ActionH);
            return button;
        }

        protected internal override void OnOpened(ScreenOpenContext context)
        {
            _facilityId = context.Interaction?.TargetId ?? context.Argument as string;
            Model = FacilityModel.From(Services.Session, _facilityId);
            if (!Model.Exists) { Close(); return; }
            LastResult = null;
            _statusText = string.Empty;
            _works.ScrollOffset = 0;
            _works.SelectedIndex = -1;
            Show();
        }

        protected internal override void OnChanged(string topic)
        {
            if (_facilityId == null) return;
            var session = Services.Session;
            if (_waitingForRoom && session != null && !session.WaitingForRoom && session.LastRoomCommandApplied.HasValue)
            {
                // 기다리던 명령이 끝났다: 됐으면 그 결과, 거절이면 방 지갑이 모자란 이유. 예약이 됐으면 미니게임이 저절로 열린다(MinigameTrigger).
                _waitingForRoom = false;
                _statusText = session.LastRoomCommandApplied.Value ? T(FacilityModel.ResultKey(AdultCareerResultKind.Applied))
                    : session.LastRoomRefusal ? T(RoomMoneyText.RefusedKey) : T(FacilityModel.ResultKey(AdultCareerResultKind.InsufficientResources));
            }

            if (topic != ChangeTopics.Career && topic != ChangeTopics.Student && topic != ChangeTopics.Inventory && topic != ChangeTopics.World
                && topic != ChangeTopics.Day && topic != ChangeTopics.Furniture && topic != ChangeTopics.Workspace && topic != ChangeTopics.Language && topic != ChangeTopics.All) return;
            Model = FacilityModel.From(Services.Session, _facilityId);
            Show();
        }

        /// <summary>Esc로 닫는다(Unity 판에는 없던 키. 키보드만으로 닫을 수 있게 더했다).</summary>
        protected internal override bool OnKey(ScreenKey key, bool shift)
        {
            if (key != ScreenKey.Escape) return false;
            Close();
            return true;
        }

        private void Show()
        {
            _title.Text = Name(Model.TitleKey);
            _close.Text = T("facility.close");
            _transition.Text = T("facility.transition");
            _practice.Text = T("facility.practice");
            _upgrade.Text = T("facility.upgrade");
            string time = Services.Session != null ? StudentTimeText.Format(T, Services.Session.Student) : string.Empty;
            string state = Model.Selected ? F("facility.level-reputation", Model.Level, Model.Reputation) : T("facility.choose-route");
            // 줄 구조일 뿐 문장을 이어 붙이는 것이 아니다(줄마다 키 하나).
            _summary.Text = time + "\n" + state + (Model.RouteComplete ? "\n" + T("facility.route-complete") : string.Empty);
            _cost.Text = Model.NextLevel > 0 ? F("facility.upgrade-cost", Model.NextLevel, Costs(Model.NextCosts))
                : T(Model.Selected ? "facility.maximum-level" : "facility.transition-requirements");
            _works.ItemCount = Model.Works.Length * ColumnsPerWork;
            _infoTexts = new string[Model.Works.Length]; // 칸 글자는 값이 바뀔 때만 만든다
            for (int i = 0; i < _infoTexts.Length; i++) _infoTexts[i] = InfoText(Model.Works[i]);
            _bonusTexts = new string[Model.Works.Length];
            for (int i = 0; i < _bonusTexts.Length; i++)
            {
                FacilityWorkRow row = Model.Works[i];
                _bonusTexts[i] = row.HasBonus ? F("ui.facility.work-bonus", row.RewardPercentBonus, row.ReputationBonus) : null;
            }
            _status.Text = _statusText;
        }

        private string InfoText(FacilityWorkRow row)
        {
            string text = Name(row.NameKey) + "\n" + F("facility.work-progress-time", Rootborn.Screens.Day.StudentTimeText.FormatCostAndEnd(T, Services.Session, row.Minutes), row.Mastery, row.MaximumMastery, row.Successes);
            if (row.MaximumRewards.Length > 0) text += "\n" + F("facility.work-reward", Costs(row.MaximumRewards));
            return text;
        }

        private string Costs(ItemAmount<string>[] amounts) => ItemAmountsText(amounts);

        /// <summary>데이터가 넘긴 이름 키. 문구표에 있으면 그 문구, 없으면 키 글자 그대로(빠진 키로 세지 않는다).</summary>
        private string Name(string key) => Shown(key);

        private void PressCell(int cell)
        {
            if (Services.Session != null && Services.Session.WaitingForRoom) return; // 방의 허락을 기다리는 동안은 다시 누르지 않는다
            int work = cell / ColumnsPerWork, column = cell % ColumnsPerWork;
            if (Services.Session == null || work < 0 || work >= Model.Works.Length || column == InfoColumn) return;
            string workId = Model.Works[work].Id;
            string request = "facility-work:" + workId + ":" + Guid.NewGuid().ToString("N");
            if (column == PlayColumn)
            {
                int seed = _seeds();
                AdultCareerResultKind kind = Services.Session.ReserveFacilityWork(workId, request, seed).Kind;
                Report(kind);
                if (kind == AdultCareerResultKind.Applied) Close(); // Unity: 일을 예약하면 패널을 닫고 미니게임 화면이 열린다
            }
            else Report(Services.Session.CompleteFacilityWork(workId, request, 100, true).Kind);
        }

        private void ChooseRoute()
        {
            if (Services.Session != null && Services.Session.WaitingForRoom) return;
            if (Services.Session == null || !Model.Exists) return;
            string facility = Model.FacilityId, request = "facility-transition:" + facility + ":" + Guid.NewGuid().ToString("N");
            Report(Services.Session.ChooseFacility(facility, request).Kind);
        }

        private void Upgrade()
        {
            if (Services.Session != null && Services.Session.WaitingForRoom) return;
            if (Services.Session == null || !Model.Exists) return;
            string request = "facility-upgrade:" + Model.FacilityId + ":" + Guid.NewGuid().ToString("N");
            Report(Services.Session.UpgradeFacility(request).Kind);
        }

        /// <summary>진로 체험(Unity RequestPractice): 연습이 미니게임을 가지면 예약(미니게임 화면이 열린다), 없으면 바로 한다.</summary>
        private void Practice()
        {
            if (Services.Session == null || !Model.Exists) return;
            string practiceId = Model.PracticeId, request = "career-practice:" + Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(practiceId)) { Report(AdultCareerResultKind.InvalidDefinition); return; }
            if (Services.Session.Definitions.TryGet(practiceId, out CareerPractice practice) && practice.Minigame != null)
            {
                int seed = _seeds();
                Report(Services.Session.BeginPractice(practiceId, request, seed).Kind);
            }
            else
            {
                LifeActivityResult result = Services.Session.PerformPractice(practiceId, request);
                Report(result.Kind == LifeActivityResultKind.Applied ? AdultCareerResultKind.Applied : AdultCareerResultKind.RequirementFailed);
            }
        }

        // 방의 허락을 기다리는 명령(돈을 같이 쓰는 방, 설계 3.2절). 끝나면 결과 문구로 바꾼다.
        private bool _waitingForRoom;

        private void Report(AdultCareerResultKind kind)
        {
            LastResult = kind;
            _waitingForRoom = Services.Session != null && Services.Session.WaitingForRoom;
            _statusText = _waitingForRoom ? T(RoomMoneyText.WaitingKey) : T(FacilityModel.ResultKey(kind));
            Model = FacilityModel.From(Services.Session, _facilityId);
            Show();
        }

        private void PaintCell(int cell, UiRect rect, CardState state, DrawList list, ITextMeasurer text)
        {
            int work = cell / ColumnsPerWork, column = cell % ColumnsPerWork;
            if (work >= _infoTexts.Length) return;
            if (column == InfoColumn)
            {
                list.Panel(rect, UiSkin.Field, UiTheme.ButtonDisabled);
                DrawLeft(list, text, rect.Inset(FacilityLayout.CellPadding), _infoTexts[work], FacilityLayout.InfoFont);
                return;
            }
            list.Panel(rect, UiSkin.Button, state.Hovered ? UiTheme.ButtonHovered : UiTheme.ButtonNormal);
            UiRect inner = rect.Inset(FacilityLayout.CellPadding);
            if (column == PlayColumn)
            {
                string bonus = _bonusTexts.Length > work ? _bonusTexts[work] : null;
                if (bonus == null) _label.Draw(list, text, inner, T("facility.play-work"), FacilityLayout.ButtonFont, UiTheme.Ink, centerVertically: true);
                else
                {
                    // 이득 줄(work-bonus-<일 id>): 직접 일하기 아래에 안내 글자 크기로. 이득이 0이면 줄이 없다.
                    float labelHeight = text.LineHeight(FacilityLayout.ButtonFont) + 4f;
                    _label.Draw(list, text, new UiRect(inner.X, inner.Y, inner.W, labelHeight), T("facility.play-work"), FacilityLayout.ButtonFont, UiTheme.Ink);
                    _label.Draw(list, text, new UiRect(inner.X, inner.Y + labelHeight, inner.W, inner.H - labelHeight), bonus, FacilityLayout.HintFont, UiTheme.Ink, lineSpacing: 1f);
                }
            }
            else
            {
                float labelHeight = text.LineHeight(FacilityLayout.ButtonFont) + 4f;
                _label.Draw(list, text, new UiRect(inner.X, inner.Y, inner.W, labelHeight), T("facility.auto-skip"), FacilityLayout.ButtonFont, UiTheme.Ink);
                _label.Draw(list, text, new UiRect(inner.X, inner.Y + labelHeight, inner.W, inner.H - labelHeight), T("facility.auto-skip-requirements"), FacilityLayout.HintFont, UiTheme.Ink, lineSpacing: 1f);
            }
            if (state.Focused && state.Selected) list.Outline(rect, UiTheme.FocusOutline, UiTheme.Accent);
        }

        /// <summary>왼쪽 맞춤 여러 줄 글자(폭에 맞춰 줄바꿈, 영역 밖은 자른다).</summary>
        private void DrawLeft(DrawList list, ITextMeasurer text, UiRect area, string label, float fontSize)
        {
            TextWrap.Wrap(label, area.W, text, fontSize, _lines);
            float lineHeight = text.LineHeight(fontSize), y = area.Y;
            list.PushClip(area);
            foreach (string line in _lines)
            {
                if (line.Length > 0) list.Text(area.X, y, line, fontSize, FacilityLayout.Ink);
                y += lineHeight + 2f;
            }
            list.PopClip();
        }
    }
}
