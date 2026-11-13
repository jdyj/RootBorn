using System;
using System.Collections.Generic;
using Rootborn.Core.Careers;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 행동 종류의 등록부. 전략의 "do" 항목은 "종류" 또는 "종류 인자"다. 판마다 새 행동을 만든다(돌아가며 하는 알바처럼 판 안의 차례를 기억하는 종류가 있다).
    /// 새 종류는 클래스 하나와 등록 한 줄로 더한다.
    /// </summary>
    internal static class BalanceActions
    {
        // (인자 — 없으면 null, 설정 글자) → 행동. 인자가 틀리면 ArgumentException.
        private static readonly Dictionary<string, Func<string, string, IBalanceAction>> Kinds = Register(
            Plain(() => new UpgradeFacilityAction()),
            Plain(() => new BestFacilityWorkAction()),
            Plain(() => new AutoSkipFacilityWorkAction()),
            Plain(() => new AutoSkipBestFacilityWorkAction()),
            Plain(() => new PartTimeWorkAction()),
            Plain(() => new AnyPartTimeWorkAction()),
            (FacilityWorkAtLevelAction.KindName, (arg, text) => new FacilityWorkAtLevelAction(Models.Number(arg, text, 1, int.MaxValue))));

        public static bool Knows(string text) => Models.Knows(() => Get(text));

        public static IBalanceAction Get(string text)
        {
            SleepRules.Split(text, out string kind, out string arg);
            if (kind != null && Kinds.TryGetValue(kind, out var make)) return make(arg, text);
            throw new ArgumentException("모르는 행동 종류다: " + text, nameof(text));
        }

        /// <summary>명령 결과를 행동 결과로(Applied가 아니면 Done이 아니다. 가방이 차서 거절되면 BagFull).</summary>
        public static StepOutcome Refused(AdultCareerResult result)
            => new StepOutcome { Done = false, BagFull = result.Kind == AdultCareerResultKind.InsufficientCapacity };

        // 인자 없는 종류: 이름은 그 행동의 Kind.
        private static (string, Func<string, string, IBalanceAction>) Plain(Func<IBalanceAction> make)
            => (make().Kind, (arg, text) => arg == null ? make() : throw new ArgumentException("인자가 없는 행동 종류다: " + text));

        private static Dictionary<string, Func<string, string, IBalanceAction>> Register(params (string Kind, Func<string, string, IBalanceAction> Make)[] kinds)
        {
            var map = new Dictionary<string, Func<string, string, IBalanceAction>>(kinds.Length, StringComparer.Ordinal);
            foreach (var (kind, make) in kinds) map.Add(kind, make);
            return map;
        }
    }
}
