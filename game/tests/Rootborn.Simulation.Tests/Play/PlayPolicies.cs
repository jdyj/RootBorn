using System;
using System.Collections.Generic;
using Rootborn.Screens;
using Rootborn.Screens.Day;
using Rootborn.Screens.Objectives;
using Rootborn.Screens.Start;

namespace Rootborn.Simulation
{
    /// <summary>
    /// 지금 할 수 있는 것 중 하나를 고르는 방법. 정책은 화면 요소 id, 행동의 분류, 상호작용 종류, 추적 목표의 id 묶음 같은 일반 정보만 본다.
    /// 엔티티 id·맵 id를 모른다. 난수는 달리는 쪽이 씨앗으로 만든 것만 쓴다(같은 씨앗은 같은 플레이).
    /// </summary>
    public interface IPlayPolicy
    {
        string Name { get; }
        /// <summary>고를 것이 없으면 null.</summary>
        PlayAction Choose(PlayView view, PlayMemory memory, Random random);
    }

    /// <summary>이 판에서 한 일의 기억(안 해 본 것 우선, 같은 단계에서 같은 목표 행동 되풀이 막기).</summary>
    public sealed class PlayMemory
    {
        private readonly Dictionary<string, int> _tried = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _triedThisStep = new Dictionary<string, int>(StringComparer.Ordinal);
        private string _step;

        public int Tried(string key) => _tried.TryGetValue(key, out int n) ? n : 0;
        public int TriedThisStep(string key) => _triedThisStep.TryGetValue(key, out int n) ? n : 0;

        public void Note(PlayView view, PlayAction action)
        {
            if (view.TrackedStep != _step) { _step = view.TrackedStep; _triedThisStep.Clear(); }
            _tried[action.Key] = Tried(action.Key) + 1;
            _triedThisStep[action.Key] = TriedThisStep(action.Key) + 1;
        }

        /// <summary>그 단계 안에서 고른 추적 행동(목표 줄을 누른 뒤 추적 단추처럼 순서가 있는 일)을 기억한다.</summary>
        public readonly HashSet<string> Flags = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>정책 등록 표. 새 정책은 Register에 한 줄.</summary>
    public sealed class PlayPolicyCatalog
    {
        private readonly Dictionary<string, Func<PlayAtlas, IPlayPolicy>> _make = new Dictionary<string, Func<PlayAtlas, IPlayPolicy>>(StringComparer.Ordinal);
        private readonly List<string> _names = new List<string>();

        public const string Random = "random", MainChain = "main-chain", OffSchool = "off-school", Novelty = "novelty";

        public static void Register(PlayPolicyCatalog r)
        {
            r.Add(Random, _ => new RandomPolicy());
            r.Add(MainChain, atlas => new MainChainPolicy(atlas));
            r.Add(OffSchool, _ => new PreferKindsPolicy(OffSchool, OffSchoolKinds));
            r.Add(Novelty, _ => new NoveltyPolicy());
        }

        /// <summary>
        /// 학교 밖 길(rules/game-design.md 자유 경로): 마을의 장소·알바·연습·탐색·시설 지점을 먼저 하고, 거기서 연 화면의 활동 줄을 고른다.
        /// 종류 이름은 상호작용 데이터의 종류다(엔티티 id가 아니다).
        /// </summary>
        public static readonly string[] OffSchoolKinds = { LocationScreen.InteractionKind, "location-visit", "part-time-work", "career-practice", "exploration", "facility" };

        public void Add(string name, Func<PlayAtlas, IPlayPolicy> make)
        {
            if (_make.ContainsKey(name)) throw new InvalidOperationException("이미 등록된 정책: " + name);
            _make[name] = make;
            _names.Add(name);
        }

        public IReadOnlyList<string> Names => _names;
        public IPlayPolicy Create(string name, PlayAtlas atlas) => _make.TryGetValue(name, out var make) ? make(atlas) : throw new ArgumentException("등록되지 않은 정책: " + name);

        public static PlayPolicyCatalog Default()
        {
            var catalog = new PlayPolicyCatalog();
            Register(catalog);
            return catalog;
        }
    }

    internal static class Pick
    {
        public static PlayAction Any(List<PlayAction> from, Random random) => from.Count == 0 ? null : from[random.Next(from.Count)];

        /// <summary>점수가 가장 큰 것들 중 하나(같으면 난수).</summary>
        public static PlayAction Best(IReadOnlyList<PlayAction> actions, Func<PlayAction, double> score, Random random)
        {
            var best = new List<PlayAction>();
            double top = double.NegativeInfinity;
            foreach (PlayAction a in actions)
            {
                double s = score(a);
                if (double.IsNegativeInfinity(s)) continue;
                if (s > top + 1e-9) { top = s; best.Clear(); best.Add(a); }
                else if (Math.Abs(s - top) <= 1e-9) best.Add(a);
            }
            return Any(best, random);
        }

        /// <summary>닫는 단추처럼 보이는 요소(요소 id가 close로 끝나거나 close-를 품는다).</summary>
        public static bool IsClose(PlayAction a) => a.Category == "button" && a.Element != null
            && (a.Element.EndsWith("close", StringComparison.Ordinal) || a.Element.Contains(".close"));
    }

    /// <summary>무작위: 할 수 있는 것 중 하나를 고르게(씨앗 고정).</summary>
    public sealed class RandomPolicy : IPlayPolicy
    {
        public string Name => PlayPolicyCatalog.Random;
        public PlayAction Choose(PlayView view, PlayMemory memory, Random random) => Pick.Any(view.Actions, random);
    }

    /// <summary>안 해 본 것 우선: 가장 적게 고른 행동(가 본 적 없는 지점, 누른 적 없는 버튼). 같으면 난수.</summary>
    public sealed class NoveltyPolicy : IPlayPolicy
    {
        public string Name => PlayPolicyCatalog.Novelty;
        public PlayAction Choose(PlayView view, PlayMemory memory, Random random) => Pick.Best(view.Actions, a => -memory.Tried(a.Key), random);
    }

    /// <summary>
    /// 정해 준 상호작용 종류를 먼저: 그 종류의 지점으로 걸어가고, 그 지점이 연 화면에서는 줄·카드(활동)를 고른다. 없으면 안 해 본 것.
    /// 미니게임은 메인 체인 정책처럼 풀이로 푼다.
    /// </summary>
    public sealed class PreferKindsPolicy : IPlayPolicy
    {
        private readonly HashSet<string> _kinds;
        private readonly MinigamePlayer _minigame = new MinigamePlayer();

        public PreferKindsPolicy(string name, IEnumerable<string> kinds)
        {
            Name = name;
            _kinds = new HashSet<string>(kinds, StringComparer.Ordinal);
        }

        public string Name { get; }

        public PlayAction Choose(PlayView view, PlayMemory memory, Random random) => _minigame.Choose(view, random) ?? Pick.Best(view.Actions, a =>
        {
            double novelty = -Math.Min(memory.Tried(a.Key), 20) * 0.5;
            if (a.Category == "interaction" && _kinds.Contains(a.InteractionKind)) return 10 + novelty;
            if ((a.Category == "card" || a.Category == "row") && view.TopScreen != null) return 6 + novelty;
            if (a.Category == "door") return 2 + novelty;
            return novelty;
        }, random);
    }

    /// <summary>
    /// 목표 종류(Core 목표 클래스 이름)마다 사람이 보통 찾아가는 곳: 상호작용 종류나 화면 요소 id. 목표가 id를 가리키지 않을 때
    /// (예: "하루 계획 고르기", "장래희망 고르기")만 쓴다. 새 목표 종류는 한 줄로 더한다.
    /// </summary>
    public static class ObjectiveHints
    {
        private static readonly Dictionary<string, (string[] Kinds, string[] Elements)> Hints = new Dictionary<string, (string[], string[])>(StringComparer.Ordinal);

        static ObjectiveHints()
        {
            Register("DailyPlanSelectedQuestObjective", new[] { DailyPlanScreen.InteractionKind }, new[] { DailyPlanScreen.RouteGridId });
            Register("CareerInterestSelectedQuestObjective", Array.Empty<string>(), new[] { "hud.button.dream", Rootborn.Screens.Career.CareerInterestScreen.CardsId });
            Register("FurniturePlacedQuestObjective", Array.Empty<string>(), new[] { "hud.button.placement" });
        }

        public static void Register(string objectiveKind, string[] interactionKinds, string[] elements) => Hints[objectiveKind] = (interactionKinds, elements);

        public static bool Matches(PlayView view, PlayAction a)
        {
            foreach (string kind in view.TrackedKinds)
            {
                if (!Hints.TryGetValue(kind, out var h)) continue;
                if (a.InteractionKind != null && Array.IndexOf(h.Kinds, a.InteractionKind) >= 0) return true;
                if (a.Element != null && Array.IndexOf(h.Elements, a.Element) >= 0) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 메인 체인 우선: 수첩에서 목표를 추적하고(없으면 Tab → 퀘스트 분류 → 첫 줄 → 추적), 추적 목표가 가리키는 것(NPC, 장소, 활동 줄, 일터,
    /// 가구)을 찾아간다. 다른 맵에 있으면 문을 따라간다(지도의 문 연결). 목표를 다 채웠는데 단계가 그대로면 하루를 마감한다.
    /// 미니게임은 도전 종류의 풀이(ChallengeSolvers)로 화면에 보이는 것만 보고 푼다. 대본은 없다 — 못 가면 어디서 막히는지가 결과다.
    /// </summary>
    public sealed class MainChainPolicy : IPlayPolicy
    {
        private readonly PlayAtlas _atlas;
        private readonly MinigamePlayer _minigame = new MinigamePlayer();

        public MainChainPolicy(PlayAtlas atlas) { _atlas = atlas; }

        public string Name => PlayPolicyCatalog.MainChain;

        public PlayAction Choose(PlayView view, PlayMemory memory, Random random)
        {
            PlayAtlas atlas = _atlas;
            PlayAction a;
            if ((a = _minigame.Choose(view, random)) != null) return a;
            if (!view.HasTracked && view.InWorld && (a = TrackSomething(view, memory)) != null) return a;
            if (view.HasTracked && view.TrackedObjectivesDone && (a = DayEndRoutine.Choose(view, atlas)) != null) return a;

            // 추적 목표가 가리키는 것: 같은 단계에서 두 번까지만(해도 목표가 안 오르면 다른 것을 해 본다). 같은 종류의 지점이 여럿이면
            // 그 단계에서 아직 안 해 본 곳을 먼저(알바 지점 9곳 중 하나가 실패하면 다음 날은 다른 곳).
            a = Pick.Best(view.Actions, x =>
            {
                bool target = x.Subject != null && view.TrackedTargets.Contains(x.Subject) || x.Category == "cell" || ObjectiveHints.Matches(view, x);
                if (!target) return double.NegativeInfinity;
                if (memory.TriedThisStep(x.Key) >= 2) return double.NegativeInfinity;
                return (x.Category == "cell" ? 3 : x.Category == "interaction" ? 2 : 1) - 0.5 * memory.TriedThisStep(x.Key);
            }, random);
            if (a != null) return a;

            // 화면이 떠 있고 할 목표가 없으면 닫는다(대화, 장소, 하루 이벤트).
            if (view.TopScreen != null && (a = DayEndRoutine.Close(view)) != null) return a;

            // 목표가 다른 맵에 있으면 그쪽 문.
            if (atlas != null && view.InWorld && view.TrackedTargets.Count > 0)
            {
                string door = atlas.FirstDoorToward(view.MapId, map => AnyTargetIn(atlas, map, view));
                if (!string.IsNullOrEmpty(door) && (a = view.Find("door:" + door)) != null) return a;
            }
            // 목표 종류 힌트가 다른 맵에 있으면(예: 하루 계획 판) 그쪽 문.
            if (atlas != null && view.InWorld)
                foreach (PlayAction hinted in HintedKindsElsewhere(view, atlas))
                    return hinted;
            return Pick.Best(view.Actions, x => -memory.Tried(x.Key) - (x.Category == "key" ? 5 : 0), random);
        }

        private static bool AnyTargetIn(PlayAtlas atlas, string map, PlayView view)
        {
            foreach (string id in view.TrackedTargets) if (atlas.Holds(map, id, view.Day, view.Minutes / 60f)) return true;
            return false;
        }

        private static IEnumerable<PlayAction> HintedKindsElsewhere(PlayView view, PlayAtlas atlas)
        {
            foreach (string kind in new[] { DailyPlanScreen.InteractionKind })
            {
                var probe = new PlayAction { InteractionKind = kind };
                if (!ObjectiveHints.Matches(view, probe)) continue;
                string door = atlas.FirstDoorToward(view.MapId, map => atlas.HasThing(map, kind, null));
                PlayAction a = string.IsNullOrEmpty(door) ? null : view.Find("door:" + door);
                if (a != null) yield return a;
            }
        }

        private int _journalRow, _exploreUntil = -1, _steps;
        private bool _pressedTrack;

        /// <summary>
        /// 추적할 것을 고른다: Tab → 퀘스트 분류 → 줄 하나 → 추적. 추적해도 목표가 생기지 않으면(아직 받지 않은 체인) 다음 줄. 줄을 다 해 봤으면
        /// 한동안 안 해 본 것을 하다가(대화로 체인을 받을 수 있다) 처음 줄부터 다시.
        /// </summary>
        private PlayAction TrackSomething(PlayView view, PlayMemory memory)
        {
            _steps++;
            if (_steps < _exploreUntil) return null;
            if (_pressedTrack) { _pressedTrack = false; _journalRow++; memory.Flags.Remove("journal.quests"); memory.Flags.Remove("journal.row"); }
            if (!view.OpenScreens.Contains(ScreenCatalog.ObjectiveJournal)) return view.Find("key:" + ScreenKey.Tab);
            string quests = "button:" + ScreenCatalog.ObjectiveJournal + "/" + ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Quests);
            if (memory.Flags.Add("journal.quests")) { PlayAction q = view.Find(quests); if (q != null) return q; }
            if (memory.Flags.Add("journal.row"))
            {
                PlayAction r = view.Find("row:" + ScreenCatalog.ObjectiveJournal + "/" + ObjectiveJournalScreen.ListId + "#" + _journalRow);
                if (r == null)
                {
                    _journalRow = 0; _exploreUntil = _steps + 25;
                    memory.Flags.Remove("journal.quests"); memory.Flags.Remove("journal.row");
                    return DayEndRoutine.Close(view);
                }
                return r;
            }
            PlayAction track = view.Find("button:" + ScreenCatalog.ObjectiveJournal + "/" + ObjectiveJournalScreen.TrackId);
            _pressedTrack = true;
            return track;
        }
    }

    /// <summary>
    /// 하루 마감: 하루 결과의 다음 날 단추 → 떠 있는 화면 닫기 → 하루 마감 지점(student-day-end 종류)으로 걷기 → 그 지점이 있는 맵 쪽 문.
    /// 정책이 하루를 끝내지 않으면 달리는 쪽이 정한 걸음 뒤에 이것으로 끝낸다(여러 날을 플레이하려고).
    /// </summary>
    public static class DayEndRoutine
    {
        public static PlayAction Choose(PlayView view, PlayAtlas atlas)
        {
            PlayAction a = view.Find("button:" + ScreenCatalog.DayResult + "/" + DayResultScreen.NextDayId);
            if (a != null) return a;
            if (view.TopScreen != null || view.MovementBlocked) return Close(view);
            if (!view.InWorld) return null;
            foreach (PlayAction x in view.Actions)
                if (x.Category == "interaction" && x.InteractionKind == DayResultScreen.InteractionKind) return x;
            if (atlas == null) return null;
            string door = atlas.FirstDoorToward(view.MapId, map => atlas.HasThing(map, DayResultScreen.InteractionKind, null));
            return string.IsNullOrEmpty(door) ? null : view.Find("door:" + door);
        }

        /// <summary>
        /// 맨 위 화면을 닫는다: 닫기 단추 → 다음 날 단추(하루 결과는 닫기가 없다) → Esc(이동을 막지 않는 화면만 — 막는 화면에서 Esc는
        /// 키 배정 표로 가지 않는다) → 그 화면의 첫 단추.
        /// </summary>
        public static PlayAction Close(PlayView view)
        {
            foreach (PlayAction x in view.Actions)
                if (x.Screen == view.TopScreen && Pick.IsClose(x)) return x;
            PlayAction a = view.Find("button:" + ScreenCatalog.DayResult + "/" + DayResultScreen.NextDayId);
            if (a != null) return a;
            if (!view.MovementBlocked && (a = view.Find("key:" + ScreenKey.Escape)) != null) return a;
            foreach (PlayAction x in view.Actions)
                if (x.Screen == view.TopScreen && x.Category == "button") return x;
            return view.Find("key:" + ScreenKey.Escape);
        }
    }

    /// <summary>시작: 슬롯 선택 → 첫 칸 새 이야기 → 이름 치기 → 시작하기(모든 정책이 같은 시작을 한다).</summary>
    public static class StartRoutine
    {
        public static PlayAction Choose(PlayView view, PlayMemory memory)
        {
            if (view.InWorld) return null;
            PlayAction a = view.Find("button:" + ScreenCatalog.SaveSlotSelect + "/" + SaveSlotScreen.NewGameId(0));
            if (a != null) return a;
            if (memory.Flags.Add("start.name") && (a = view.Find("text:" + ScreenCatalog.CharacterCreator + "/" + CharacterCreatorScreen.NameFieldId)) != null) return a;
            return view.Find("button:" + ScreenCatalog.CharacterCreator + "/" + CharacterCreatorScreen.ConfirmId);
        }
    }

    /// <summary>기록된 행동 이름을 차례로 다시 고른다. 그 이름이 지금 할 수 있는 것에 없으면 재생이 어긋난 것이다.</summary>
    public sealed class ReplayPolicy : IPlayPolicy
    {
        private readonly IReadOnlyList<string> _keys;
        private int _next;

        public ReplayPolicy(IReadOnlyList<string> keys) { _keys = keys ?? throw new ArgumentNullException(nameof(keys)); }

        public string Name => "replay";
        public bool Finished => _next >= _keys.Count;
        public int Position => _next;

        public PlayAction Choose(PlayView view, PlayMemory memory, Random random)
        {
            if (Finished) return null;
            string key = _keys[_next];
            PlayAction a = view.Find(key) ?? throw new PlayReplayMismatch(_next, key, view);
            _next++;
            return a;
        }
    }

    public sealed class PlayReplayMismatch : Exception
    {
        public PlayReplayMismatch(int step, string key, PlayView view)
            : base("재생 " + step + "번째 행동 '" + key + "'이 지금 할 수 있는 것에 없다. 맵 " + view.MapId + ", 맨 위 화면 " + (view.TopScreen ?? "(없음)")
                + ", 할 수 있는 것 " + view.Actions.Count + "개") { }
    }
}
