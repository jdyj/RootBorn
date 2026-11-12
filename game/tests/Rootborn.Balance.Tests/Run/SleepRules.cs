using System;
using System.Collections.Generic;
using System.Globalization;
using Rootborn.Core.Time;

namespace Rootborn.Balance.Tests
{
    /// <summary>행동 하나를 시작할지 볼 때의 시각들(그날 0시부터 센 분). RunDay는 판 안에서 몇째 날(1부터).</summary>
    internal readonly struct SleepMoment
    {
        public int RunDay { get; init; }
        public int NowMinutes { get; init; }
        public int WalkMinutes { get; init; }
        public int ActionMinutes { get; init; }
        public int MinigameMinutes { get; init; }   // 그 행동에서 미니게임을 하는 분(올림). 미니게임이 없으면 0
        public int DayEndMinutes { get; init; }

        /// <summary>걷기와 행동이 끝나는 시각.</summary>
        public int EndsAt => NowMinutes + WalkMinutes + ActionMinutes;
    }

    /// <summary>
    /// 잠 규칙 하나(설계 13.1절): 그 밤에 자는지와 지금 그 행동을 시작해도 되는지. 잠·지침·회복 규칙 자체는 게임(GameSession)이 판단한다 —
    /// 여기는 플레이어가 언제 자러 가는지만 정한다. 어느 규칙이든 날이 넘어가는 시각(07:00)을 넘겨 끝나는 행동은 시작하지 않는다.
    /// </summary>
    internal interface ISleepRule
    {
        /// <summary>설정에 적은 글자 그대로("finish-by 22:00", "never" …).</summary>
        string Id { get; }

        /// <summary>false면 그 밤은 자지 않고 날이 넘어가는 시각에 넘어간다.</summary>
        bool SleepsTonight(int runDay);

        bool MayStart(SleepMoment m);

        /// <summary>
        /// 그날 행동이 끝나도 되는 가장 늦은 시각(그날 0시부터 센 분). 행동 뒤 검사에 쓴다(계획 길이 예측이 실제보다 짧았는지).
        /// 끝 제한이 없으면 null(start-before, 넘겨 잇는 밤).
        /// </summary>
        int? LatestEnd(int runDay, int dayEndMinutes);

        /// <summary>그 밤에 07:00을 넘겨 끝나는 행동을 다음 날로 잇는가(" carry" 규칙의 자지 않는 밤).</summary>
        bool CarriesTonight(int runDay);
    }

    /// <summary>
    /// 잠 규칙의 등록부. 설정의 글자는 "종류" 또는 "종류 인자" 한 줄이다. 새 종류는 클래스 하나와 등록 한 줄로 더한다.
    /// 시각 인자 HH:MM은 TimeRules와 같은 방식으로 센다: 하루 시작보다 이르면 자정을 넘긴 같은 날(+1440분).
    /// </summary>
    internal static class SleepRules
    {
        // (인자 — 없으면 null, 시간 규칙, 설정 글자) → 규칙. 인자가 틀리면 ArgumentException.
        private static readonly Dictionary<string, Func<string, TimeRules, string, ISleepRule>> Kinds = new Dictionary<string, Func<string, TimeRules, string, ISleepRule>>(StringComparer.Ordinal)
        {
            { "finish-by", (arg, rules, id) => new FinishByRule(id, Clock(arg, rules, id)) },
            { "start-before", (arg, rules, id) => new StartBeforeRule(id, Clock(arg, rules, id)) },
            { "never", (arg, rules, id) => NoArgument(arg, id, new NeverRule(id)) },
            { "stay-up-every", (arg, rules, id) => StayUpEvery(arg, rules, id) },
        };

        /// <summary>글자를 읽을 수 있는가(종류와 인자 모두). 시각은 기준 시간 규칙으로 센다 — 맞는 글자인지만 본다.</summary>
        public static bool Knows(string text)
        {
            if (text == null) return false;
            try
            {
                Parse(text, TimeRules.Legacy);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// "종류 [인자] [carry]". 끝의 " carry"는 어느 종류에나 붙는다: 자지 않는 밤에는 07:00 전에 시작하기만 하면 되고(끝이 07:00을 넘어도 된다),
        /// 게임이 넘긴 시간을 다음 날 아침으로 잇는다.
        /// </summary>
        public static ISleepRule Parse(string text, TimeRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            string core = text;
            bool carry = text != null && text.EndsWith(CarrySuffix, StringComparison.Ordinal);
            if (carry) core = text.Substring(0, text.Length - CarrySuffix.Length);
            Split(core, out string kind, out string arg);
            if (kind == null || !Kinds.TryGetValue(kind, out var make)) throw new ArgumentException("모르는 잠 규칙이다: " + text, nameof(text));
            var rule = make(arg, rules, text);
            return carry ? new CarryRule(text, rule) : rule;
        }

        private const string CarrySuffix = " carry";

        /// <summary>"종류 인자"를 첫 빈칸에서 나눈다. 인자가 없으면 null.</summary>
        internal static void Split(string text, out string kind, out string arg)
        {
            kind = arg = null;
            if (string.IsNullOrWhiteSpace(text)) return;
            int space = text.IndexOf(' ');
            kind = space < 0 ? text : text.Substring(0, space);
            arg = space < 0 ? null : text.Substring(space + 1);
        }

        private static ISleepRule NoArgument(string arg, string id, ISleepRule rule)
            => arg == null ? rule : throw new ArgumentException("인자가 없는 잠 규칙이다: " + id);

        // HH:MM → 그날 0시부터 센 분. 하루 시작보다 이르면 자정을 넘긴 시각이다.
        private static int Clock(string arg, TimeRules rules, string id)
        {
            var parts = arg?.Split(':');
            if (parts == null || parts.Length != 2 || parts[0].Length != 2 || parts[1].Length != 2
                || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int hour)
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minute)
                || hour > 23 || minute > 59)
                throw new ArgumentException("잠 규칙의 시각은 HH:MM(00:00~23:59)이다: " + id);
            int minutes = hour * 60 + minute;
            return minutes < rules.DayStartMinutes ? minutes + TimeRules.MinutesPerDay : minutes;
        }

        // "N" 또는 "N until HH:MM"(다른 밤은 그 시각까지 끝내고 잔다. 없으면 취침 시각).
        private static ISleepRule StayUpEvery(string arg, TimeRules rules, string id)
        {
            const string Until = " until ";
            if (arg == null) return new StayUpEveryRule(id, Count(null, id), rules.BedtimeMinutes);
            int at = arg.IndexOf(Until, StringComparison.Ordinal);
            if (at < 0) return new StayUpEveryRule(id, Count(arg, id), rules.BedtimeMinutes);
            return new StayUpEveryRule(id, Count(arg.Substring(0, at), id), Clock(arg.Substring(at + Until.Length), rules, id));
        }

        private static int Count(string arg, string id)
        {
            if (arg == null || !int.TryParse(arg, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n < 1)
                throw new ArgumentException("잠 규칙의 날 수는 1 이상의 정수다: " + id);
            return n;
        }

        // 그 시각까지 끝나는 행동만 시작하고 잔다.
        private sealed class FinishByRule : ISleepRule
        {
            private readonly int _by;
            public FinishByRule(string id, int by) { Id = id; _by = by; }
            public string Id { get; }
            public bool SleepsTonight(int runDay) => true;
            public bool MayStart(SleepMoment m) => m.EndsAt <= _by && m.EndsAt <= m.DayEndMinutes;
            public int? LatestEnd(int runDay, int dayEndMinutes) => Math.Min(_by, dayEndMinutes);
            public bool CarriesTonight(int runDay) => false;
        }

        // 그 시각 전이면 길이와 상관없이 시작하고, 끝나면 잔다(날 넘김을 넘기지는 않는다).
        private sealed class StartBeforeRule : ISleepRule
        {
            private readonly int _before;
            public StartBeforeRule(string id, int before) { Id = id; _before = before; }
            public string Id { get; }
            public bool SleepsTonight(int runDay) => true;
            public bool MayStart(SleepMoment m) => m.NowMinutes < _before && m.EndsAt <= m.DayEndMinutes;
            public int? LatestEnd(int runDay, int dayEndMinutes) => null;
            public bool CarriesTonight(int runDay) => false;
        }

        // 자지 않는다. 날 넘김 전에 끝나는 것만 시작한다.
        private sealed class NeverRule : ISleepRule
        {
            public NeverRule(string id) { Id = id; }
            public string Id { get; }
            public bool SleepsTonight(int runDay) => false;
            public bool MayStart(SleepMoment m) => m.EndsAt <= m.DayEndMinutes;
            public int? LatestEnd(int runDay, int dayEndMinutes) => dayEndMinutes;
            public bool CarriesTonight(int runDay) => false;
        }

        // N일에 하루(N, 2N, …째 날) 밤샌다. 나머지 날은 그 시각(until, 없으면 취침 시각 — game/time.json, 지금 22:00)까지 끝내고 잔다.
        private sealed class StayUpEveryRule : ISleepRule
        {
            private readonly int _every, _sleepBy;
            public StayUpEveryRule(string id, int every, int sleepBy) { Id = id; _every = every; _sleepBy = sleepBy; }
            public string Id { get; }
            public bool SleepsTonight(int runDay) => runDay % _every != 0;
            public bool MayStart(SleepMoment m) => m.EndsAt <= m.DayEndMinutes && (!SleepsTonight(m.RunDay) || m.EndsAt <= _sleepBy);
            public int? LatestEnd(int runDay, int dayEndMinutes) => SleepsTonight(runDay) ? Math.Min(_sleepBy, dayEndMinutes) : dayEndMinutes;
            public bool CarriesTonight(int runDay) => false;
        }

        // " carry": 자지 않는 밤에는 07:00 전에 시작하기만 하면 된다. 걷기는 시계의 남은 조각 때문에 1분 더 걸릴 수 있어 걷기 끝이 07:00 1분 전보다 일러야 한다
        // (걷다가 날이 넘어가지 않게). 미니게임이 있는 행동은 걷기 + 미니게임도 07:00 전에 끝나야 한다 — 미니게임 도중에 07:00이 되면 게임이 날을 넘기고
        // 정산을 거절한다(CarryFactsTests). 정산만 07:00을 넘기는 것은 된다. 자는 밤은 안의 규칙 그대로.
        private sealed class CarryRule : ISleepRule
        {
            private const int WalkSlack = 1;
            private readonly ISleepRule _inner;
            public CarryRule(string id, ISleepRule inner) { Id = id; _inner = inner; }
            public string Id { get; }
            public bool SleepsTonight(int runDay) => _inner.SleepsTonight(runDay);
            public bool MayStart(SleepMoment m) => SleepsTonight(m.RunDay) ? _inner.MayStart(m) : m.NowMinutes + m.WalkMinutes + WalkSlack + m.MinigameMinutes < m.DayEndMinutes;
            public int? LatestEnd(int runDay, int dayEndMinutes) => SleepsTonight(runDay) ? _inner.LatestEnd(runDay, dayEndMinutes) : null;
            public bool CarriesTonight(int runDay) => !SleepsTonight(runDay);
        }
    }
}
