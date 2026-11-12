using System;
using System.Collections.Generic;
using System.Globalization;

namespace Rootborn.Balance.Tests
{
    /// <summary>시설 일 미니게임을 끝낸 점수(0~100)를 차례마다 낸다. 판마다 새 인스턴스다(부른 차례를 센다).</summary>
    internal interface IScoreModel
    {
        string Id { get; }
        int Next();
    }

    /// <summary>알바 미니게임을 성공으로 끝냈는지를 차례마다 낸다. 판마다 새 인스턴스다.</summary>
    internal interface ISuccessModel
    {
        string Id { get; }
        bool Next();
    }

    /// <summary>점수 모델의 등록부. 글자는 "종류 인자": fixed N(늘 N), fail-every N(N번째마다 실패 점수, 나머지는 성공 점수).</summary>
    internal static class ScoreModels
    {
        private static readonly Dictionary<string, Func<string, string, int, int, IScoreModel>> Kinds = new Dictionary<string, Func<string, string, int, int, IScoreModel>>(StringComparer.Ordinal)
        {
            { "fixed", (arg, id, fail, pass) => new FixedScore(id, Models.Number(arg, id, 0, 100)) },
            { "fail-every", (arg, id, fail, pass) => new FailEveryScore(id, Models.Number(arg, id, 1, int.MaxValue), fail, pass) },
        };

        /// <summary>점수 하나를 늘 내는 모델의 글자("fixed 80").</summary>
        public static string Fixed(int score) => "fixed " + score.ToString(CultureInfo.InvariantCulture);

        public static bool Knows(string text) => Models.Knows(() => Create(text, 0, 0));

        public static IScoreModel Create(string text, int failScore, int passScore)
        {
            SleepRules.Split(text, out string kind, out string arg);
            if (kind == null || !Kinds.TryGetValue(kind, out var make)) throw new ArgumentException("모르는 점수 모델이다: " + text, nameof(text));
            return make(arg, text, failScore, passScore);
        }

        private sealed class FixedScore : IScoreModel
        {
            private readonly int _score;
            public FixedScore(string id, int score) { Id = id; _score = score; }
            public string Id { get; }
            public int Next() => _score;
        }

        private sealed class FailEveryScore : IScoreModel
        {
            private readonly int _every, _fail, _pass;
            private int _calls;
            public FailEveryScore(string id, int every, int fail, int pass) { Id = id; _every = every; _fail = fail; _pass = pass; }
            public string Id { get; }
            public int Next() => ++_calls % _every == 0 ? _fail : _pass;
        }
    }

    /// <summary>알바 성공 모델의 등록부. 글자: always(늘 성공), fail-every N(N번째마다 실패).</summary>
    internal static class SuccessModels
    {
        private static readonly Dictionary<string, Func<string, string, ISuccessModel>> Kinds = new Dictionary<string, Func<string, string, ISuccessModel>>(StringComparer.Ordinal)
        {
            { "always", (arg, id) => arg == null ? new AlwaysSucceeds(id) : throw new ArgumentException("인자가 없는 성공 모델이다: " + id) },
            { "fail-every", (arg, id) => new FailEverySuccess(id, Models.Number(arg, id, 1, int.MaxValue)) },
        };

        public static bool Knows(string text) => Models.Knows(() => Create(text));

        public static ISuccessModel Create(string text)
        {
            SleepRules.Split(text, out string kind, out string arg);
            if (kind == null || !Kinds.TryGetValue(kind, out var make)) throw new ArgumentException("모르는 성공 모델이다: " + text, nameof(text));
            return make(arg, text);
        }

        private sealed class AlwaysSucceeds : ISuccessModel
        {
            public AlwaysSucceeds(string id) { Id = id; }
            public string Id { get; }
            public bool Next() => true;
        }

        private sealed class FailEverySuccess : ISuccessModel
        {
            private readonly int _every;
            private int _calls;
            public FailEverySuccess(string id, int every) { Id = id; _every = every; }
            public string Id { get; }
            public bool Next() => ++_calls % _every != 0;
        }
    }

    internal static class Models
    {
        public static bool Knows(Action create)
        {
            try
            {
                create();
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>정수 인자(min~max). 아니면 설정 글자를 말하며 ArgumentException.</summary>
        public static int Number(string arg, string id, int min, int max)
        {
            if (arg == null || !int.TryParse(arg, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n < min || n > max)
                throw new ArgumentException("인자는 " + min + (max == int.MaxValue ? " 이상" : "~" + max) + "의 정수다: " + id);
            return n;
        }
    }
}
