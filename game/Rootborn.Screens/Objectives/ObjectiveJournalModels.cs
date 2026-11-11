using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Rootborn.Game;

namespace Rootborn.Screens.Objectives
{
    /// <summary>수첩의 분류 하나: id, 이름 키, 아이콘 키(Unity ObjectiveJournalPanel.BuildCategoryRail).</summary>
    public sealed record JournalCategory(string Id, string LabelKey, string IconKey);

    /// <summary>수첩의 분류 목록(Unity DefaultCategoryIds 순서). 새 분류는 여기에 한 줄.</summary>
    public static class JournalCategories
    {
        public const string Goals = "Goals";
        public const string Quests = "Quests";
        public const string Campaign = "Campaign";
        public const string Hints = "Hints";

        public static readonly IReadOnlyList<JournalCategory> All = new[]
        {
            new JournalCategory(Goals, "ui.objective.category.goals", "journal"),
            new JournalCategory(Quests, "ui.objective.category.quests", "plan"),
            new JournalCategory(Campaign, "ui.objective.category.campaign", "town_map"),
            new JournalCategory(Hints, "ui.objective.category.hints", "discovery"),
        };

        public static JournalCategory Find(string id)
        {
            foreach (JournalCategory c in All) if (c.Id == id) return c;
            return null;
        }
    }

    /// <summary>상세의 행동 단추가 돌려주는 것: 무언가 바뀌었는지와 보일 문구.</summary>
    public readonly record struct JournalActionResult(bool Succeeded, JournalText Message);

    /// <summary>
    /// 항목 하나의 상세 행동 단추(예: 끝낸 퀘스트의 보상 받기). 모으는 쪽이 데이터와 상태로 정한다(엔티티 id로 고르지 않는다).
    /// 화면은 이름을 보이고 누르면 실행할 뿐이다. 실행은 SessionCommands로 게임 명령을 부른다.
    /// </summary>
    public sealed class JournalAction
    {
        private readonly Func<SessionCommands, JournalActionResult> _run;

        public JournalAction(JournalText label, Func<SessionCommands, JournalActionResult> run)
        {
            Label = label ?? JournalText.Empty;
            _run = run;
        }

        public JournalText Label { get; }
        public JournalActionResult Run(SessionCommands commands) => _run != null && commands != null ? _run(commands) : new JournalActionResult(false, JournalText.Empty);
    }

    /// <summary>
    /// 수첩 항목 하나(Unity ObjectiveJournalItem): 바뀌지 않는 키(추적 저장용), 분류, 제목, 부제, 상태, 진행, 다음 할 일, 정렬 순서, 행동.
    /// 글자는 모두 JournalText(키와 값)다.
    /// </summary>
    public sealed class JournalItem
    {
        public JournalItem(string stableKey, string categoryId, JournalText title, JournalText subtitle, JournalText state, JournalText progress,
            JournalText nextHint, bool isTracked, int sortPriority, JournalAction action = null)
        {
            StableKey = stableKey ?? string.Empty;
            CategoryId = categoryId ?? string.Empty;
            Title = title == null || title.IsEmpty ? JournalText.Raw(StableKey) : title;
            Subtitle = subtitle ?? JournalText.Empty;
            State = state ?? JournalText.Empty;
            Progress = progress ?? JournalText.Empty;
            NextHint = nextHint ?? JournalText.Empty;
            IsTracked = isTracked;
            SortPriority = sortPriority;
            Action = action;
        }

        public string StableKey { get; }
        public string CategoryId { get; }
        public JournalText Title { get; }
        public JournalText Subtitle { get; }
        public JournalText State { get; }
        public JournalText Progress { get; }
        public JournalText NextHint { get; }
        public bool IsTracked { get; }
        public int SortPriority { get; }
        public JournalAction Action { get; }

        /// <summary>같은 글자를 보이는지(행동은 비교하지 않는다).</summary>
        public bool SameText(JournalItem other) => other != null && StableKey == other.StableKey && Title.Equals(other.Title) && Subtitle.Equals(other.Subtitle)
            && State.Equals(other.State) && Progress.Equals(other.Progress) && NextHint.Equals(other.NextHint);
    }

    /// <summary>수첩에 항목을 대는 쪽 하나(Unity IObjectiveJournalSource). GameSession 조회만 쓴다.</summary>
    public interface IObjectiveJournalSource
    {
        string SourceId { get; }
        string CategoryId { get; }
        IReadOnlyList<JournalItem> BuildItems(GameSession session);
    }

    /// <summary>
    /// 모으는 쪽들의 항목을 분류별로 모은다(Unity ObjectiveJournalAggregator.Build): 같은 키는 처음 것만, 분류 안에서 정렬 순서 → 제목 키 → 키 순.
    /// </summary>
    public sealed class ObjectiveJournalAggregator
    {
        private readonly IReadOnlyList<IObjectiveJournalSource> _sources;

        public ObjectiveJournalAggregator(IReadOnlyList<IObjectiveJournalSource> sources) { _sources = sources ?? Array.Empty<IObjectiveJournalSource>(); }

        public Dictionary<string, JournalItem[]> Build(GameSession session)
        {
            var byCategory = new Dictionary<string, List<JournalItem>>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (IObjectiveJournalSource source in _sources)
            {
                if (source == null || string.IsNullOrEmpty(source.CategoryId)) continue;
                if (!byCategory.TryGetValue(source.CategoryId, out List<JournalItem> items)) byCategory[source.CategoryId] = items = new List<JournalItem>();
                IReadOnlyList<JournalItem> built = session != null ? source.BuildItems(session) : null;
                if (built == null) continue;
                foreach (JournalItem item in built)
                    if (item != null && !string.IsNullOrEmpty(item.StableKey) && seen.Add(item.StableKey)) items.Add(item);
            }
            var result = new Dictionary<string, JournalItem[]>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<JournalItem>> pair in byCategory)
            {
                pair.Value.Sort(Compare);
                result[pair.Key] = pair.Value.ToArray();
            }
            return result;
        }

        /// <summary>그 키의 항목(없으면 null).</summary>
        public static JournalItem Find(Dictionary<string, JournalItem[]> items, string stableKey, out string categoryId)
        {
            categoryId = null;
            if (items == null || string.IsNullOrEmpty(stableKey)) return null;
            foreach (KeyValuePair<string, JournalItem[]> pair in items)
                foreach (JournalItem item in pair.Value)
                    if (item.StableKey == stableKey) { categoryId = pair.Key; return item; }
            return null;
        }

        private static int Compare(JournalItem a, JournalItem b)
        {
            int priority = a.SortPriority.CompareTo(b.SortPriority);
            if (priority != 0) return priority;
            int title = string.Compare(a.Title.ToString(), b.Title.ToString(), StringComparison.Ordinal);
            return title != 0 ? title : string.Compare(a.StableKey, b.StableKey, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 화면 쌓기 하나가 쓰는 모으는 쪽 목록. 처음에 ObjectiveJournalSourceCatalog를 채운다. 시험이나 확장은 등록 표의 AddListener에서
    /// For(services).Add(...)로 더한다. 수첩과 추적 목표 HUD가 같은 목록을 쓴다.
    /// </summary>
    public static class ObjectiveJournalSources
    {
        private static readonly ConditionalWeakTable<ScreenServices, List<IObjectiveJournalSource>> _bySession = new ConditionalWeakTable<ScreenServices, List<IObjectiveJournalSource>>();

        public static List<IObjectiveJournalSource> For(ScreenServices services) =>
            _bySession.GetValue(services, _ => { var list = new List<IObjectiveJournalSource>(16); ObjectiveJournalSourceCatalog.Register(list); return list; });
    }

    /// <summary>
    /// 수첩에서 "추적"한 항목 하나(Unity ObjectiveJournalTrackingStore). 추적 목표 HUD는 이것만 보인다.
    /// 세이브 파일(objective-journal-tracking.json)의 모양과 이름이 Core에 없어 지금은 메모리에만 둔다(보고서 "필요한 변경").
    /// 바뀌면 화면 쌓기에 Topic 알림을 낸다.
    /// </summary>
    public sealed class ObjectiveJournalTracking
    {
        public const string Topic = "objective-tracking";
        private static readonly ConditionalWeakTable<ScreenServices, ObjectiveJournalTracking> _bySession = new ConditionalWeakTable<ScreenServices, ObjectiveJournalTracking>();
        private readonly ScreenServices _services;

        private ObjectiveJournalTracking(ScreenServices services) { _services = services; }

        public static ObjectiveJournalTracking For(ScreenServices services) => _bySession.GetValue(services, s => new ObjectiveJournalTracking(s));

        public string CategoryId { get; private set; } = string.Empty;
        public string StableKey { get; private set; } = string.Empty;
        public bool HasTracked => !string.IsNullOrEmpty(StableKey);

        public void Track(string categoryId, string stableKey)
        {
            if (string.IsNullOrEmpty(stableKey)) return;
            bool changed = stableKey != StableKey || (categoryId ?? string.Empty) != CategoryId;
            CategoryId = categoryId ?? string.Empty;
            StableKey = stableKey;
            if (changed) _services.Screens?.NotifyChanged(Topic);
        }
    }
}
