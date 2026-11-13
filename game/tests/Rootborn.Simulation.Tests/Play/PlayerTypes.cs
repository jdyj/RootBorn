using System;
using System.Collections.Generic;
using Rootborn.Screens;

namespace Rootborn.Simulation
{
    /// <summary>
    /// 플레이어 유형 넷(속도 설계 4절): 이야기만 따라가는 사람, 돈을 모으는 사람, 이것저것 해 보는 사람, 한 가지만 파는 사람.
    /// 정책 등록 표(PlayPolicyCatalog — 빠른 판과 실제 창 판이 모두 돈다)와 따로 둔다: 속도 비교에만 쓴다. 새 유형은 한 줄.
    /// 유형은 엔티티 id를 모른다(행동의 분류·상호작용 종류·이름, HUD에 보이는 돈만 본다).
    /// </summary>
    public static class PlayerTypeCatalog
    {
        public const string Story = "story", Money = "money", Explorer = "explorer", OneThing = "one-thing";

        private static readonly List<(string Name, string Label, Func<PlayAtlas, IPlayPolicy> Make)> Types = new List<(string, string, Func<PlayAtlas, IPlayPolicy>)>
        {
            (Story, "이야기만 따라가는 사람", atlas => new MainChainPolicy(atlas)),
            (Money, "돈을 모으는 사람", _ => new MoneyPolicy()),
            (Explorer, "이것저것 해 보는 사람", _ => new ExplorerPolicy()),
            (OneThing, "한 가지만 파는 사람", atlas => new OneThingPolicy(atlas)),
        };

        public static IReadOnlyList<string> Names
        {
            get
            {
                var names = new List<string>(Types.Count);
                foreach (var t in Types) names.Add(t.Name);
                return names;
            }
        }

        /// <summary>
        /// 잠자리(하루 마감 지점)는 고르지 않는다 — 자는 때는 달리는 쪽의 하루 마감 순서(잘 시간·지침·할 것 없음)가 정한다.
        /// 이야기만 따라가는 사람은 빼지 않는다(목표를 다 채우면 스스로 하루를 마감한다).
        /// </summary>
        public static PlayView LeaveSleepToBedtime(PlayView view)
        {
            view.Actions.RemoveAll(a => a.InteractionKind == Rootborn.Screens.Day.DayResultScreen.InteractionKind);
            return view;
        }

        public static bool Has(string name) => Types.Exists(t => t.Name == name);

        public static string Label(string name) => Types.Find(t => t.Name == name).Label ?? name;

        public static IPlayPolicy Create(string name, PlayAtlas atlas)
        {
            foreach (var t in Types) if (t.Name == name) return t.Make(atlas);
            throw new ArgumentException("등록되지 않은 플레이어 유형: " + name);
        }
    }

    /// <summary>
    /// 돈을 모으는 사람: 해 본 일마다 그 뒤 HUD의 돈이 얼마나 늘었는지 기억하고(돈이 늘면 마지막으로 한 지점과 화면의 줄·카드에 몫을 단다),
    /// 평균이 가장 큰 일을 되풀이한다. 안 해 본 일은 한 번은 해 본다(알아야 고른다). 미니게임은 풀이로 푼다.
    /// </summary>
    public sealed class MoneyPolicy : IPlayPolicy
    {
        private readonly MinigamePlayer _minigame = new MinigamePlayer();
        private readonly Dictionary<string, int> _gain = new Dictionary<string, int>(StringComparer.Ordinal);
        private int _lastMoney = -1;
        private string _lastSpot, _lastRow;

        public string Name => PlayerTypeCatalog.Money;

        public PlayAction Choose(PlayView view, PlayMemory memory, Random random)
        {
            PlayerTypeCatalog.LeaveSleepToBedtime(view);
            Observe(view);
            PlayAction a = _minigame.Choose(view, random) ?? Pick.Best(view.Actions, x => Score(view, memory, x), random);
            return Remember(a);
        }

        /// <summary>그 행동을 한 것으로 적는다(테스트가 고른 행동을 정해 줄 때).</summary>
        public PlayAction Credit(PlayView view, PlayAction action)
        {
            Observe(view);
            return Remember(action);
        }

        private void Observe(PlayView view)
        {
            if (view.Money < 0) return;
            if (_lastMoney >= 0 && view.Money > _lastMoney)
            {
                int gain = view.Money - _lastMoney;
                if (_lastSpot != null) _gain[_lastSpot] = Gain(_lastSpot) + gain;
                if (_lastRow != null) _gain[_lastRow] = Gain(_lastRow) + gain;
            }
            _lastMoney = view.Money;
        }

        private PlayAction Remember(PlayAction a)
        {
            if (a == null) return null;
            if (a.Category == "interaction") { _lastSpot = a.Key; _lastRow = null; }
            else if ((a.Category == "row" || a.Category == "card") && a.Screen != ScreenCatalog.Minigame) _lastRow = a.Key;
            return a;
        }

        private int Gain(string key) => _gain.TryGetValue(key, out int g) ? g : 0;

        private double Score(PlayView view, PlayMemory memory, PlayAction a)
        {
            int tried = memory.Tried(a.Key);
            bool work = a.Category == "interaction" || (a.Category == "row" || a.Category == "card") && view.TopScreen != null;
            if (work)
            {
                if (tried == 0) return 5;
                double average = Gain(a.Key) / (double)tried;
                return average > 0 ? 3 + average - 0.01 * tried : 1 - 0.1 * tried; // 해 봤는데 돈이 안 늘었으면 문 너머(다른 맵의 일)보다 뒤
            }
            if (a.Category == "door") return 2 - 0.05 * tried;
            if (view.TopScreen != null && Pick.IsClose(a)) return 1;
            if (a.Category == "key") return -5;
            return -0.1 * tried;
        }
    }

    /// <summary>이것저것 해 보는 사람: 해 본 횟수가 가장 적은 일을 먼저(끝이 없다 — 새로운 것이 가장 빨리 나오는 위쪽 한계). 미니게임은 풀이로 푼다.</summary>
    public sealed class ExplorerPolicy : IPlayPolicy
    {
        private readonly MinigamePlayer _minigame = new MinigamePlayer();

        public string Name => PlayerTypeCatalog.Explorer;

        public PlayAction Choose(PlayView view, PlayMemory memory, Random random) => _minigame.Choose(view, random) ?? Pick.Best(PlayerTypeCatalog.LeaveSleepToBedtime(view).Actions, a =>
        {
            double tried = 3.0 * memory.Tried(a.Key);
            if (a.Category == "interaction") return 10 - tried;
            if ((a.Category == "row" || a.Category == "card") && view.TopScreen != null) return 8 - tried;
            if (a.Category == "door") return 4 - tried;
            if (view.TopScreen != null && Pick.IsClose(a)) return 2 - tried * 0.1;
            if (a.Category == "key") return -5 - tried;
            return -tried;
        }, random);
    }

    /// <summary>
    /// 한 가지만 파는 사람: 처음 고른 마을 일(학교 밖 정책의 종류) 하나와 그 화면에서 처음 고른 줄·카드·단추(닫기 빼고)만 되풀이한다(지루함의 아래쪽 한계).
    /// 그 일이 이 맵에 없으면 그쪽 문으로 가고, 지금 할 수 없으면(안 보이거나 오늘 뺐다) 화면을 닫고 서 있는다(할 것 없음).
    /// </summary>
    public sealed class OneThingPolicy : IPlayPolicy
    {
        private readonly PlayAtlas _atlas;
        private readonly MinigamePlayer _minigame = new MinigamePlayer();
        private readonly HashSet<string> _kinds = new HashSet<string>(PlayPolicyCatalog.OffSchoolKinds, StringComparer.Ordinal);
        private PlayAction _focus;
        private string _row;
        private bool _rest;

        public OneThingPolicy(PlayAtlas atlas) { _atlas = atlas; }

        public string Name => PlayerTypeCatalog.OneThing;

        public PlayAction Choose(PlayView view, PlayMemory memory, Random random)
        {
            PlayerTypeCatalog.LeaveSleepToBedtime(view);
            PlayAction a = _minigame.Choose(view, random);
            if (a != null) return a;
            if (view.TopScreen != null && !view.OpenScreens.Contains(ScreenCatalog.ObjectiveJournal))
            {
                if (_row == null)
                {
                    a = Pick.Any(view.Actions.FindAll(x => (x.Category == "row" || x.Category == "card" || x.Category == "button" && !Pick.IsClose(x)) && x.Screen == view.TopScreen), random);
                    if (a != null) { _row = a.Key; return a; }
                }
                else if ((a = view.Find(_row)) != null) return a;
                _rest = true; // 그 줄을 지금 할 수 없다 — 닫고 쉰다
                return DayEndRoutine.Close(view);
            }
            if (_focus == null)
            {
                _focus = Pick.Any(view.Actions.FindAll(x => x.Category == "interaction" && _kinds.Contains(x.InteractionKind)), random);
                if (_focus == null) return Pick.Best(view.Actions, x => x.Category == "door" ? 1 - memory.Tried(x.Key) : x.Category == "key" ? -10 : -1 - memory.Tried(x.Key), random);
                return _focus;
            }
            if (_rest && view.InWorld && view.TopScreen == null)
            {
                _rest = false;
                return view.Idle(PlayRunner.IdleFrames);
            }
            if ((a = view.Find(_focus.Key)) != null) return a;
            if (_atlas != null && view.InWorld)
            {
                string door = _atlas.FirstDoorToward(view.MapId, map => _atlas.Holds(map, _focus.Subject, view.Day, view.Minutes / 60f));
                if (!string.IsNullOrEmpty(door) && (a = view.Find("door:" + door)) != null) return a;
            }
            return view.TopScreen == null && view.InWorld ? view.Idle(PlayRunner.IdleFrames) : DayEndRoutine.Close(view);
        }
    }
}
