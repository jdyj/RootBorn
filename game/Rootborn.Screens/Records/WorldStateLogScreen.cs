using System;
using System.Collections.Generic;
using System.Text;
using Rootborn.Core.Dialogue;
using Rootborn.Core.StudentLife;
using Rootborn.Core.WorldState;
using Rootborn.Game;
using Rootborn.Ui;

namespace Rootborn.Screens.Records
{
    /// <summary>마을 기록 카드 하나: 월드 상태 id와 문구 키들(이름, 설명, 관련 장소, 관련 NPC, 다음에 할 일), 확인했는지, 범위, 배지.</summary>
    public readonly record struct WorldLogCard(string FlagId, string DisplayNameKey, string DescriptionKey, string LocationNameKey, string NpcNameKey,
        string NextActionKey, bool Seen, WorldStateScopeKind Scope, WorldStateBadgeKind[] Badges);

    /// <summary>
    /// 마을 기록의 표시 모델: 켜진 월드 상태 중 "월드 기록" 화면에 보이는 것을 우선순위 순으로(Core WorldStateSummaryBuilder).
    /// 관련 장소·NPC 이름 키는 데이터(displayNameKey)에서 읽는다(Core 정의가 아직 이름 키를 갖지 않는다).
    /// </summary>
    public sealed class WorldStateLogModel
    {
        public static readonly WorldStateLogModel Empty = new WorldStateLogModel(Array.Empty<WorldLogCard>());

        public WorldStateLogModel(WorldLogCard[] cards) { Cards = cards; }

        public IReadOnlyList<WorldLogCard> Cards { get; }

        public static WorldStateLogModel From(GameSession session)
        {
            if (session == null) return Empty;
            WorldStateSummaryModel[] summaries = WorldStateSummaryBuilder.BuildForSurface(
                session.Definitions.All<WorldStateFlag>(), session.World, WorldStateSummarySurface.WorldLog, new Lookup(session.Definitions));
            var cards = new WorldLogCard[summaries.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                WorldStateSummaryModel s = summaries[i];
                cards[i] = new WorldLogCard(s.FlagId, s.DisplayName, s.Description, s.LocationName, s.NpcName, s.NextAction, s.SeenNotification, s.Scope, s.Badges);
            }
            return new WorldStateLogModel(cards);
        }

        private sealed class Lookup : IWorldStateDisplayNames
        {
            private readonly Rootborn.Data.GameDefinitions _definitions;
            public Lookup(Rootborn.Data.GameDefinitions definitions) { _definitions = definitions; }
            public string LocationName(string locationId) => _definitions.TryGet(locationId ?? string.Empty, out Location location) ? location.DisplayNameKey : string.Empty;
            public string NpcName(string npcId) => _definitions.TryGet(npcId ?? string.Empty, out NpcDefinition npc) ? npc.DisplayNameKey : string.Empty;
        }
    }

    /// <summary>
    /// 마을 기록(HUD 마을 기록 버튼). Unity WorldStateLogPanel을 같은 수치로 옮겼다: 가운데 1120x800 패널, 위에 아이콘·제목·닫기,
    /// 카드 목록(992x208, 간격 16, 세로 스크롤)과 카드가 한 화면을 넘으면 아래 스크롤 안내. 기록이 없으면 안내 글자.
    /// 카드: 제목, 본문(설명 / 장소 · NPC / 다음에 할 일), 배지 줄(새 변화·확인함, 범위, 그 밖의 배지). 보이는 카드만 그린다.
    /// 이동을 막는다(Unity: UiGameplayInputLock). 닫기는 × 버튼, HUD 마을 기록 버튼은 열기만(Unity도 열기만), Esc(Unity에는 없던 키).
    /// Unity 판의 월드 상태 사용 기록 패널(WorldStateUsageLogPanel)은 어디서도 열리지 않아(하루 결과 화면의 카드만 쓴다) 옮기지 않았다.
    /// </summary>
    public sealed class WorldStateLogScreen : GameScreen
    {
        /// <summary>배치 — 새 디자인에서는 여기만 고친다. 가운데 기준(y는 위로 +)과 카드 안 왼쪽 위 기준.</summary>
        internal static class Metrics
        {
            public const float PanelW = 1120f, PanelH = 800f;
            public const float IconX = -488f, IconY = 332f, IconSize = 64f;
            public const float TitleY = 332f, TitleW = 832f, TitleH = 48f, TitleFont = 32f;
            public const float CloseX = 484f, CloseY = 334f, CloseSize = 56f;
            public const float EmptyY = 160f, EmptyW = 960f, EmptyH = 48f, EmptyFont = 22f;
            public const float ListY = -40f, ListW = 1024f, ListH = 592f, ListPadding = 8f;
            public const float CardW = 992f, CardH = 208f, CardGap = 16f;
            public const float HintY = -352f, HintW = 960f, HintH = 24f, HintFont = 18f;
            // 카드 안(왼쪽 위 기준)
            public const float CardInsetX = 32f;
            public const float TitleTop = 22f, CardTitleH = 36f, CardTitleFont = 26f;
            public const float BodyTop = 62f, BodyH = 76f, BodyFont = 20f;
            public const float BadgeTop = 147f, BadgeH = 26f, BadgeFont = 18f;
            public const float LineSpacing = 4f;
            public const string Icon = "icon:journal";
            public static readonly UiColor Ink = UnityLayout.Color(48f / 255f, 71f / 255f, 83f / 255f);
        }

        public const string PanelId = "world-state-log";
        public const string TitleId = "world-state-log.title";
        public const string CloseId = "world-state-log.close";
        public const string EmptyId = "world-state-log.empty";
        public const string ListId = "world-state-log.cards";
        public const string HintId = "world-state-log.hint";

        private UiLabel _title, _empty, _hint;
        private UiCardGrid _grid;
        private ResolvedCard[] _resolved = Array.Empty<ResolvedCard>();
        private readonly List<string> _wrap = new List<string>(6);

        /// <summary>카드 글자(문구 키를 지금 언어로 푼 것). 열 때와 언어가 바뀔 때만 만든다.</summary>
        private sealed class ResolvedCard
        {
            public string Title, Body, Badges;
            public List<string> BodyLines;
            public float WrappedWidth = -1f;
        }

        public WorldStateLogScreen(ScreenServices services) : base(services) { }

        public WorldStateLogModel Model { get; private set; } = WorldStateLogModel.Empty;

        /// <summary>키보드 처음 포커스: 기록 카드에서 시작한다.</summary>
        protected internal override UiNode InitialFocus => _grid;

        protected override UiNode Build()
        {
            var root = new UiNode { Id = "world-state-log.root" };
            var panel = root.Add(new UiPanel { Id = PanelId, Skin = UiSkin.Panel });
            panel.Centered(0f, 0f, Metrics.PanelW, Metrics.PanelH);
            panel.Add(new UiImage { Id = "world-state-log.icon", Image = Metrics.Icon }).Centered(Metrics.IconX, Metrics.IconY, Metrics.IconSize, Metrics.IconSize);
            _title = panel.Add(new UiLabel { Id = TitleId, FontSize = Metrics.TitleFont, Align = UiAlign.Center, Color = Metrics.Ink });
            _title.Centered(0f, Metrics.TitleY, Metrics.TitleW, Metrics.TitleH);
            var close = panel.Add(new UiButton { Id = CloseId, Icon = Rootborn.Screens.Day.ScreenIcons.Close });
            close.Centered(Metrics.CloseX, Metrics.CloseY, Metrics.CloseSize, Metrics.CloseSize);
            close.Clicked += Close;
            _empty = panel.Add(new UiLabel { Id = EmptyId, FontSize = Metrics.EmptyFont, Align = UiAlign.Center, Color = Metrics.Ink });
            _empty.Centered(0f, Metrics.EmptyY, Metrics.EmptyW, Metrics.EmptyH);
            _grid = panel.Add(new UiCardGrid
            {
                Id = ListId, Padding = Metrics.ListPadding, DrawBackground = false, CellWidth = Metrics.CardW, CellHeight = Metrics.CardH,
                Spacing = Metrics.CardGap, StepsPerNotch = 0.5f,
            });
            _grid.Centered(0f, Metrics.ListY, Metrics.ListW, Metrics.ListH);
            _grid.DrawCell = DrawCard;
            _hint = panel.Add(new UiLabel { Id = HintId, FontSize = Metrics.HintFont, Align = UiAlign.Center, Color = Metrics.Ink });
            _hint.Centered(0f, Metrics.HintY, Metrics.HintW, Metrics.HintH);
            return root;
        }

        protected internal override void OnOpened(ScreenOpenContext context) => Refresh();

        protected internal override void OnChanged(string topic)
        {
            if (topic == ChangeTopics.Language || topic == ChangeTopics.World || topic == ChangeTopics.Quests || topic == ChangeTopics.All) Refresh();
        }

        protected internal override bool OnKey(ScreenKey key, bool shift)
        {
            if (key != ScreenKey.Escape) return false;
            Close();
            return true;
        }

        private void Refresh()
        {
            _ = Root;
            _title.Text = T("ui.world-log.title");
            _empty.Text = T("ui.world-log.empty");
            _hint.Text = T("ui.world-log.scroll");
            Model = WorldStateLogModel.From(Services.Session);
            int count = Model.Cards.Count;
            _resolved = new ResolvedCard[count];
            for (int i = 0; i < count; i++) _resolved[i] = Resolve(Model.Cards[i]);
            _grid.ItemCount = count;
            _grid.ScrollOffset = 0f;
            _empty.Visible = count == 0;
            _hint.Visible = count * (Metrics.CardH + Metrics.CardGap) > Metrics.ListH;
        }

        private ResolvedCard Resolve(WorldLogCard card)
        {
            var lines = new List<string>(3);
            if (!string.IsNullOrEmpty(card.DescriptionKey)) lines.Add(Shown(card.DescriptionKey));
            if (!string.IsNullOrEmpty(card.LocationNameKey) || !string.IsNullOrEmpty(card.NpcNameKey))
            {
                var place = new StringBuilder(Shown(card.LocationNameKey));
                if (!string.IsNullOrEmpty(card.NpcNameKey)) place.Append(" / ").Append(Shown(card.NpcNameKey));
                lines.Add(place.ToString());
            }
            if (!string.IsNullOrEmpty(card.NextActionKey)) lines.Add(Shown(card.NextActionKey));
            return new ResolvedCard { Title = Shown(card.DisplayNameKey), Body = string.Join("\n", lines), Badges = Badges(card) };
        }

        /// <summary>배지 줄: 새 변화/확인함, 범위, 그 밖의 배지. 같은 글자는 한 번만(Unity ComposeBadges).</summary>
        private string Badges(WorldLogCard card)
        {
            var labels = new List<string>(4);
            void Add(string key)
            {
                string label = T(key);
                if (!labels.Contains(label)) labels.Add(label);
            }
            Add("day-result.world-badge." + (card.Seen ? WorldStateBadgeKind.Seen : WorldStateBadgeKind.New));
            Add("day-result.world-scope." + card.Scope);
            if (card.Badges != null) foreach (WorldStateBadgeKind badge in card.Badges) Add("day-result.world-badge." + badge);
            return string.Join(" · ", labels);
        }

        private void DrawCard(int index, UiRect rect, CardState state, DrawList list, ITextMeasurer text)
        {
            if (index >= _resolved.Length) return;
            ResolvedCard card = _resolved[index];
            list.Panel(rect, UiSkin.Panel, UiTheme.White);
            float x = rect.X + Metrics.CardInsetX, width = rect.W - Metrics.CardInsetX * 2f;
            DrawLine(list, text, card.Title, x, rect.Y + Metrics.TitleTop, Metrics.CardTitleH, Metrics.CardTitleFont);

            if (card.WrappedWidth != width)
            {
                TextWrap.Wrap(card.Body, width, text, Metrics.BodyFont, _wrap);
                card.BodyLines = new List<string>(_wrap);
                card.WrappedWidth = width;
            }
            var body = new UiRect(x, rect.Y + Metrics.BodyTop, width, Metrics.BodyH);
            list.PushClip(body); // 본문이 길면 칸 안에서 자른다(Unity: VerticalWrapMode.Truncate)
            float lineHeight = text.LineHeight(Metrics.BodyFont), y = body.Y;
            foreach (string line in card.BodyLines)
            {
                list.Text(x, y, line, Metrics.BodyFont, Metrics.Ink);
                y += lineHeight + Metrics.LineSpacing;
            }
            list.PopClip();

            DrawLine(list, text, card.Badges, x, rect.Y + Metrics.BadgeTop, Metrics.BadgeH, Metrics.BadgeFont);
        }

        private static void DrawLine(DrawList list, ITextMeasurer text, string value, float x, float top, float height, float fontSize) =>
            list.Text(x, top + (height - text.LineHeight(fontSize)) / 2f, value, fontSize, Metrics.Ink);
    }
}
