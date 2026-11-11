using System;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.Core.Minigames;
using Rootborn.Screens;
using Rootborn.Screens.Minigames;
using Rootborn.Ui;

namespace Rootborn.Simulation
{
    /// <summary>미니게임 판에서 화면에 보이는 요소 하나: id, 사각형, 글자, 색, 누를 수 있는지.</summary>
    public sealed class SightElement
    {
        public string Id;
        public UiRect Rect;
        public string Text;
        public UiColor? Color;
        public bool Visible, Enabled;
    }

    /// <summary>
    /// 미니게임 화면에서 플레이어가 보는 것: 제목이 말하는 도전 종류, 안내 글, 진행 줄, 도전 자리의 요소(사각형·글자·색).
    /// 정답(외울 순서, 고르기의 답, 꼭 덮을 칸)은 담지 않는다 — 풀이는 사람처럼 보이는 것으로 푼다.
    /// </summary>
    public sealed class MinigameSight
    {
        public MinigameMechanic Mechanic;
        /// <summary>도전이 바뀔 때마다 오르는 번호(새 도전이면 풀이를 새로 만든다).</summary>
        public int Challenge;
        /// <summary>판이 끝나 결과를 보이고 있다.</summary>
        public bool Complete;
        public string Feedback = string.Empty, Status = string.Empty;
        /// <summary>관찰한 때의 프레임 번호(움직이는 것의 빠르기를 잴 때).</summary>
        public long Frame;
        public GameText Text;
        public readonly List<SightElement> Elements = new List<SightElement>(32);

        public SightElement Get(string id)
        {
            for (int i = 0; i < Elements.Count; i++) if (Elements[i].Id == id) return Elements[i];
            return null;
        }

        /// <summary>id가 prefix + 숫자인 요소들을 숫자 순서로.</summary>
        public List<SightElement> Numbered(string prefix)
        {
            var found = new List<(int, SightElement)>();
            foreach (SightElement e in Elements)
                if (e.Id != null && e.Id.StartsWith(prefix, StringComparison.Ordinal)
                    && int.TryParse(e.Id.Substring(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) found.Add((n, e));
            found.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            var list = new List<SightElement>(found.Count);
            foreach (var f in found) list.Add(f.Item2);
            return list;
        }

        /// <summary>
        /// 문구 키의 모양(자리 표시 하나를 빼고 앞뒤 글)으로 보인 글에서 그 자리의 글을 꺼낸다. args의 \u0001 자리가 꺼낼 자리다.
        /// 언어마다 문구가 달라도 같은 키로 만든 글이면 꺼낸다.
        /// </summary>
        public bool TryUnwrap(string shown, string key, out string inner, params object[] args)
        {
            inner = null;
            if (Text == null || shown == null) return false;
            string shape = Text.Format(key, args);
            int at = shape.IndexOf('\u0001');
            if (at < 0) return false;
            string before = shape.Substring(0, at), after = shape.Substring(at + 1);
            if (shown.Length < before.Length + after.Length || !shown.StartsWith(before, StringComparison.Ordinal) || !shown.EndsWith(after, StringComparison.Ordinal)) return false;
            inner = shown.Substring(before.Length, shown.Length - before.Length - after.Length);
            return true;
        }
    }

    /// <summary>
    /// 도전 하나를 푸는 방법. 도전마다 새로 만든다(그 도전 동안의 기억을 가진다). 보이는 것(MinigameSight)만 보고 지금 할 행동을 고른다.
    /// 보이는 것으로 정답을 알 수 없으면 무작위로 시도하고 실패도 결과로 받는다. 할 것이 없으면 null(기다린다).
    /// </summary>
    public interface IChallengeSolver
    {
        PlayAction Next(MinigameSight sight, PlayView view, Random random);
    }

    /// <summary>도전 종류 → 풀이. 새 도전 종류는 풀이 클래스 하나 + Register 한 줄(없으면 ChallengeSolverTests가 실패한다).</summary>
    public static class ChallengeSolvers
    {
        private static readonly Dictionary<MinigameMechanic, Func<IChallengeSolver>> Make = new Dictionary<MinigameMechanic, Func<IChallengeSolver>>();

        static ChallengeSolvers()
        {
            Register(MinigameMechanic.Sequence, () => new SequenceSolver());
            Register(MinigameMechanic.Choice, () => new ChoiceSolver());
            Register(MinigameMechanic.Timing, () => new TimingSolver());
            Register(MinigameMechanic.Placement, () => new PlacementSolver());
            Register(MinigameMechanic.TargetDodge, () => new TargetDodgeSolver());
        }

        public static void Register(MinigameMechanic mechanic, Func<IChallengeSolver> make) => Make[mechanic] = make;
        public static bool Has(MinigameMechanic mechanic) => Make.ContainsKey(mechanic);
        public static IChallengeSolver Create(MinigameMechanic mechanic) => Make.TryGetValue(mechanic, out var make) ? make() : null;

        internal static PlayAction Press(PlayView view, SightElement e) =>
            e == null ? null : view.Find("button:" + ScreenCatalog.Minigame + "/" + e.Id);
    }

    /// <summary>
    /// 미니게임 화면이 떠 있을 때 정책이 맡기는 쪽: 끝났으면 닫고, 아니면 지금 도전의 풀이에 묻는다. 도전 종류마다의 코드는 풀이에만 있다.
    /// </summary>
    public sealed class MinigamePlayer
    {
        private int _challenge = -1;
        private IChallengeSolver _solver;

        /// <summary>미니게임이 떠 있지 않으면 null.</summary>
        public PlayAction Choose(PlayView view, Random random)
        {
            if (!view.OpenScreens.Contains(ScreenCatalog.Minigame)) { _challenge = -1; _solver = null; return null; }
            MinigameSight sight = view.Minigame;
            if (sight == null) return view.Find("wait");
            if (sight.Complete) return DayEndRoutine.Close(view);
            if (sight.Challenge != _challenge) { _challenge = sight.Challenge; _solver = ChallengeSolvers.Create(sight.Mechanic); }
            PlayAction a = _solver?.Next(sight, view, random);
            return a ?? view.WaitFrames(10) ?? view.Find("wait");
        }
    }

    /// <summary>순서 외우기: 미리 보기 안내 글에서 순서(단추 글자들)를 읽어 외우고, 단추가 켜지면 그 순서대로 누른다. 못 봤으면 무작위.</summary>
    public sealed class SequenceSolver : IChallengeSolver
    {
        private List<int> _order;
        private int _next;

        public PlayAction Next(MinigameSight sight, PlayView view, Random random)
        {
            List<SightElement> buttons = sight.Numbered(SymbolChoiceView.ChoicePrefix);
            bool enabled = buttons.Exists(b => b.Enabled);
            if (!enabled)
            {
                if (_order == null) _order = ReadPreview(sight, buttons);
                return view.WaitFrames(10);
            }
            if (_order != null && _next < _order.Count && _order[_next] < buttons.Count)
            {
                PlayAction a = ChallengeSolvers.Press(view, buttons[_order[_next]]);
                if (a != null) { _next++; return a; }
            }
            return ChoiceSolver.HintOrRandom(sight, view, buttons, random);
        }

        /// <summary>안내 글 "외우기 문구 + 순서" 모양에서 순서 부분을 꺼내 구분 글로 나누고, 각 글자를 단추 글자와 맞춘다.</summary>
        private static List<int> ReadPreview(MinigameSight sight, List<SightElement> buttons)
        {
            if (!sight.TryUnwrap(sight.Feedback, "ui.minigame.preview", out string inner, sight.Text.Get("minigame.memorize"), "\u0001")) return null;
            string separator = sight.Text.Get("ui.minigame.sequence-separator");
            var order = new List<int>();
            foreach (string label in inner.Split(new[] { separator }, StringSplitOptions.None))
            {
                int index = buttons.FindIndex(b => b.Text == label);
                if (index < 0) return null;
                order.Add(index);
            }
            return order.Count > 0 ? order : null;
        }
    }

    /// <summary>고르기: 힌트 표시가 붙은 단추가 있으면 그것, 없으면 무작위(질문 글의 뜻은 읽지 않는다).</summary>
    public sealed class ChoiceSolver : IChallengeSolver
    {
        public PlayAction Next(MinigameSight sight, PlayView view, Random random) =>
            HintOrRandom(sight, view, sight.Numbered(SymbolChoiceView.ChoicePrefix), random);

        internal static PlayAction HintOrRandom(MinigameSight sight, PlayView view, List<SightElement> buttons, Random random)
        {
            var enabled = buttons.FindAll(b => b.Enabled && b.Visible);
            if (enabled.Count == 0) return null;
            foreach (SightElement b in enabled)
                if (sight.TryUnwrap(b.Text, "ui.minigame.hint-choice", out _, "\u0001")) return ChallengeSolvers.Press(view, b);
            return ChallengeSolvers.Press(view, enabled[random.Next(enabled.Count)]);
        }
    }

    /// <summary>
    /// 박자: 지나가는 커서와 박자 칸을 본다. 두 관찰 사이 커서가 움직인 거리로 빠르기를 재고, 다음 박자 칸 가운데에 닿을 때까지 기다렸다가
    /// Space를 누른다. 박자 칸 하나는 한 번만 누른다.
    /// </summary>
    public sealed class TimingSolver : IChallengeSolver
    {
        private readonly HashSet<int> _tapped = new HashSet<int>();
        private float _lastX = float.NaN;
        private long _lastFrame;
        private float _speed;

        public PlayAction Next(MinigameSight sight, PlayView view, Random random)
        {
            SightElement cursor = sight.Get(TimingView.CursorId);
            if (cursor == null) return null;
            float x = cursor.Rect.CenterX;
            if (!float.IsNaN(_lastX) && sight.Frame > _lastFrame && x > _lastX) _speed = (x - _lastX) / (sight.Frame - _lastFrame);
            _lastX = x; _lastFrame = sight.Frame;

            List<SightElement> beats = sight.Numbered("minigame.beat.");
            int next = -1;
            for (int i = 0; i < beats.Count; i++)
                if (!_tapped.Contains(i) && beats[i].Rect.Right > x) { next = i; break; }
            if (next < 0) return view.WaitFrames(10);
            if (_speed <= 0) return view.WaitFrames(2);
            float frames = (beats[next].Rect.CenterX - x) / _speed;
            if (frames <= 1.5f || x >= beats[next].Rect.X)
            {
                _tapped.Add(next);
                return view.Find("key:" + ScreenKey.Space);
            }
            return view.WaitFrames(Math.Max(1, (int)Math.Floor(frames - 1)));
        }
    }

    /// <summary>
    /// 놓기: 칸 색으로 빈 칸을 본다(처음 가장 많은 색이 빈 칸). 조각 단추를 고르고 빈 칸을 차례로 눌러 본다. 안내 글이 "놓을 수 없다"면 다음 칸,
    /// 다 안 되면 돌리기를 누르고 다시. 빈 칸이 줄면 놓인 것이고 다음 조각으로.
    /// </summary>
    public sealed class PlacementSolver : IChallengeSolver
    {
        private UiColor? _free;
        private int _piece, _candidate, _rotations, _freeBefore = -1;
        private bool _selected, _clicked;
        private List<SightElement> _candidates;

        public PlayAction Next(MinigameSight sight, PlayView view, Random random)
        {
            List<SightElement> cells = sight.Elements.FindAll(e => e.Id != null && e.Id.StartsWith("minigame.cell.", StringComparison.Ordinal));
            List<SightElement> pieces = sight.Numbered("minigame.piece.");
            if (cells.Count == 0 || pieces.Count == 0) return null;
            if (_free == null) _free = MostCommonColor(cells);
            int freeNow = cells.FindAll(IsFree).Count;

            if (_clicked)
            {
                _clicked = false;
                bool invalid = sight.Feedback == sight.Text.Get("minigame.placement-invalid");
                if (!invalid && freeNow < _freeBefore) { _piece++; _selected = false; _candidates = null; _rotations = 0; }
                else _candidate++;
            }
            if (_piece >= pieces.Count) _piece = 0; // 다 놓았는데 안 끝났다: 처음 조각부터 다시 옮겨 본다
            if (!_selected)
            {
                _selected = true;
                return ChallengeSolvers.Press(view, pieces[_piece]);
            }
            if (_candidates == null)
            {
                _candidates = cells.FindAll(IsFree);
                _candidates.Sort((a, b) => a.Rect.Bottom != b.Rect.Bottom ? b.Rect.Bottom.CompareTo(a.Rect.Bottom) : a.Rect.X.CompareTo(b.Rect.X));
                _candidate = 0;
            }
            if (_candidate >= _candidates.Count)
            {
                if (_rotations < 3)
                {
                    _rotations++;
                    _candidates = null;
                    return ChallengeSolvers.Press(view, sight.Get(PlacementView.RotateId));
                }
                _rotations = 0; _candidates = null; _piece++; _selected = false; // 이 조각은 안 들어간다: 다음 조각
                return null;
            }
            _clicked = true;
            _freeBefore = freeNow;
            return ChallengeSolvers.Press(view, _candidates[_candidate]);

            bool IsFree(SightElement e) => e.Color.HasValue && e.Color.Value == _free.Value;
        }

        private static UiColor? MostCommonColor(List<SightElement> cells)
        {
            var count = new Dictionary<UiColor, int>();
            UiColor? best = null;
            int top = 0;
            foreach (SightElement c in cells)
            {
                if (!c.Color.HasValue) continue;
                count.TryGetValue(c.Color.Value, out int n);
                count[c.Color.Value] = ++n;
                if (n > top) { top = n; best = c.Color.Value; }
            }
            return best;
        }
    }

    /// <summary>
    /// 조준·피하기: 다가오는 장애물이 지금 줄(색이 다른 줄 단추)에 있으면 다른 줄을 누르고, 아니면 과녁 가운데를 누른다. 과녁이 없으면 기다린다.
    /// </summary>
    public sealed class TargetDodgeSolver : IChallengeSolver
    {
        public PlayAction Next(MinigameSight sight, PlayView view, Random random)
        {
            List<SightElement> lanes = sight.Numbered("minigame.lane.");
            SightElement obstacle = sight.Get(TargetDodgeView.ObstacleId);
            int current = OddOneOut(lanes);
            if (obstacle != null && obstacle.Visible && lanes.Count > 1 && current >= 0)
            {
                int blocked = Nearest(lanes, obstacle.Rect.CenterX);
                if (blocked == current)
                {
                    int to = current + 1 < lanes.Count ? current + 1 : current - 1;
                    PlayAction move = ChallengeSolvers.Press(view, lanes[to]);
                    if (move != null) return move;
                }
            }
            SightElement target = sight.Get(TargetDodgeView.TargetId);
            if (target != null && target.Visible)
                return view.Point(ScreenCatalog.Minigame, TargetDodgeView.SurfaceId, TargetDodgeView.TargetId);
            return view.WaitFrames(10);
        }

        /// <summary>색이 하나만 다른 줄(지금 줄). 모두 같으면 -1.</summary>
        private static int OddOneOut(List<SightElement> lanes)
        {
            for (int i = 0; i < lanes.Count; i++)
            {
                int same = 0;
                for (int j = 0; j < lanes.Count; j++) if (lanes[j].Color == lanes[i].Color) same++;
                if (same == 1) return i;
            }
            return -1;
        }

        private static int Nearest(List<SightElement> lanes, float x)
        {
            int best = 0;
            for (int i = 1; i < lanes.Count; i++)
                if (Math.Abs(lanes[i].Rect.CenterX - x) < Math.Abs(lanes[best].Rect.CenterX - x)) best = i;
            return best;
        }
    }
}
