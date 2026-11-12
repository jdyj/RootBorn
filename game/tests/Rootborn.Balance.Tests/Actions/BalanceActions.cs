using System;
using System.Collections.Generic;
using Rootborn.Core.Careers;

namespace Rootborn.Balance.Tests
{
    /// <summary>행동 종류의 등록부. 판마다 새 행동을 만든다(돌아가며 하는 알바처럼 판 안의 차례를 기억하는 종류가 있다).</summary>
    internal static class BalanceActions
    {
        private static readonly Dictionary<string, Func<IBalanceAction>> Kinds = Register(
            () => new UpgradeFacilityAction(),
            () => new BestFacilityWorkAction(),
            () => new AutoSkipFacilityWorkAction(),
            () => new PartTimeWorkAction(),
            () => new AnyPartTimeWorkAction());

        public static bool Knows(string kind) => kind != null && Kinds.ContainsKey(kind);

        public static IBalanceAction Get(string kind)
        {
            if (kind != null && Kinds.TryGetValue(kind, out var make)) return make();
            throw new ArgumentException("모르는 행동 종류다: " + kind, nameof(kind));
        }

        /// <summary>명령 결과를 행동 결과로(Applied가 아니면 Done이 아니다. 가방이 차서 거절되면 BagFull).</summary>
        public static StepOutcome Refused(AdultCareerResult result)
            => new StepOutcome { Done = false, BagFull = result.Kind == AdultCareerResultKind.InsufficientCapacity };

        private static Dictionary<string, Func<IBalanceAction>> Register(params Func<IBalanceAction>[] makers)
        {
            var kinds = new Dictionary<string, Func<IBalanceAction>>(makers.Length, StringComparer.Ordinal);
            foreach (var make in makers) kinds.Add(make().Kind, make);
            return kinds;
        }
    }
}
