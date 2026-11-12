using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Rootborn.Data;
using Rootborn.Game;
using Rootborn.Game.Tests;

namespace Rootborn.Balance.Tests
{
    /// <summary>
    /// 모든 경우 훑기(설계 13.2절): 축의 곱을 판마다 독립으로 병렬로 돌리고, 판마다 시점별 요약을 낸다.
    /// 판마다 저장소·GameSession·행동·모델을 새로 만든다(BalanceRunner.Run). 판끼리 함께 쓰는 것은 바뀌지 않는 정의(TestWorld.Definitions)와 설정뿐이다.
    /// 결과 순서는 계획 순서 × 시점 순서로 고정이다 — 병렬로 돌려도 같은 입력은 같은 줄.
    /// </summary>
    internal static class SweepRunner
    {
        /// <summary>
        /// 축의 곱. 순서: 전략(설정 순서) → 대상(id 순) → 자는 때 → 점수(점수를 쓰는 전략) 또는 알바 성공(아닌 전략) → 걷기 → 몫.
        /// 그 전략이 쓰지 않는 축(점수 또는 알바 성공)은 기준 값 하나다.
        /// </summary>
        public static IReadOnlyList<(StrategySpec Strategy, object Target, RunSetup Setup)> Plan(BalanceConfig config)
        {
            var a = config.Axes;
            var b = config.BaselineSetup;
            var plan = new List<(StrategySpec, object, RunSetup)>();
            foreach (var strategy in config.Strategies)
            {
                var outcomes = strategy.Scored ? a.Score : a.PartTime;
                foreach (var target in RunTargets.All(strategy.Each, TestWorld.Definitions))
                    foreach (var sleep in a.Sleep)
                        foreach (var outcome in outcomes)
                            foreach (var travel in a.TravelMinutes)
                                foreach (var share in a.MinigameShare)
                                    plan.Add((strategy, target, new RunSetup
                                    {
                                        Sleep = sleep,
                                        Score = strategy.Scored ? outcome : b.Score,
                                        PartTime = strategy.Scored ? b.PartTime : outcome,
                                        TravelMinutes = travel,
                                        MinigameShare = share,
                                    }));
            }
            return plan;
        }

        /// <summary>모든 축이 기준 값인가.</summary>
        public static bool IsBaseline(BalanceConfig config, RunSetup setup)
        {
            var b = config.BaselineSetup;
            return setup.Sleep == b.Sleep && setup.Score == b.Score && setup.PartTime == b.PartTime
                && setup.TravelMinutes == b.TravelMinutes && setup.MinigameShare == b.MinigameShare;
        }

        /// <summary>
        /// 계획의 판을 모두 axes.days일 돌려 시점마다 한 줄. maxParallel이 없으면 프로세서 수.
        /// 판이 예외로 멈추면 훑기를 멈추지 않고 그 판의 줄에 까닭을 적는다(조용히 버리지 않는다 — 보고서 맨 위와 failed 칸에 나온다).
        /// </summary>
        public static IReadOnlyList<SweepRow> RunAll(BalanceConfig config, int? maxParallel = null)
        {
            var plan = Plan(config);
            var checkpoints = config.Axes.Checkpoints;
            int days = config.Axes.Days;
            var perRun = new SweepRow[plan.Count][];

            var definitions = TestWorld.Definitions;
            SharedDefinitions.FillCache(definitions);
            int cached = SharedDefinitions.CachedTypes(definitions);

            var options = new ParallelOptions { MaxDegreeOfParallelism = maxParallel ?? Environment.ProcessorCount };
            Parallel.For(0, plan.Count, options, i =>
            {
                var (strategy, target, setup) = plan[i];
                var rows = new SweepRow[checkpoints.Length];
                RunResult run = null;
                string failure = null;
                try
                {
                    run = BalanceRunner.Run(strategy, target, setup, config, days);
                }
                catch (Exception error)
                {
                    failure = error.GetType().Name + ": " + error.Message;
                }
                for (int k = 0; k < checkpoints.Length; k++)
                {
                    rows[k] = new SweepRow
                    {
                        StrategyId = strategy.Id,
                        TargetId = RunTargets.IdOf(target),
                        Setup = setup,
                        Checkpoint = checkpoints[k],
                        Summary = run != null ? RunSummary.From(run, checkpoints[k]) : new RunSummary(),
                        IsBaseline = IsBaseline(config, setup),
                        Failure = failure,
                    };
                }
                perRun[i] = rows;
            });

            // 판 도중에 함께 쓰는 정의의 조회 캐시가 늘었다면 여러 판이 한 사전에 동시에 썼을 수 있다(GameDefinitions.All<T>의 캐시는 잠그지 않는다).
            if (SharedDefinitions.CachedTypes(definitions) != cached)
                throw new InvalidOperationException("판을 도는 동안 GameDefinitions.All<T>의 캐시에 새 형이 들어갔다(미리 채우지 못한 형). 병렬 판이 한 사전에 동시에 썼을 수 있다: "
                    + cached + " → " + SharedDefinitions.CachedTypes(definitions));

            var all = new List<SweepRow>(plan.Count * checkpoints.Length);
            foreach (var rows in perRun) all.AddRange(rows);
            return all;
        }
    }

    /// <summary>
    /// 판끼리 함께 쓰는 정의(GameDefinitions)에서 판 도중에 바뀌는 것: <c>All&lt;T&gt;()</c>가 처음 부를 때 채우는 형별 캐시(잠그지 않는 Dictionary).
    /// 병렬로 판을 돌리기 전에 게임 쪽 어셈블리의 모든 형으로 미리 채워 판 도중에는 읽기만 하게 한다(결과는 언제 채워도 같다).
    /// 게임 코드는 고치지 않는다 — 캐시의 크기는 리플렉션으로 읽어 판 뒤에 늘지 않았는지만 본다.
    /// </summary>
    internal static class SharedDefinitions
    {
        private const string CacheField = "_allCache";
        private static readonly object Gate = new object();   // 훑기 둘이 동시에 채우지 않게(채우는 동안은 판이 돌지 않는다)

        public static void FillCache(GameDefinitions definitions)
        {
            lock (Gate)
            {
                var all = typeof(GameDefinitions).GetMethod(nameof(GameDefinitions.All), BindingFlags.Public | BindingFlags.Instance);
                var assemblies = new[] { typeof(Rootborn.Core.Careers.Facility).Assembly, typeof(GameDefinitions).Assembly, typeof(GameSession).Assembly };
                foreach (var assembly in assemblies)
                    foreach (var type in Types(assembly))
                    {
                        if (type == null || type.IsValueType || type.ContainsGenericParameters || type.IsPointer || type.IsByRef) continue;
                        if (!(type.IsClass || type.IsInterface)) continue;
                        all.MakeGenericMethod(type).Invoke(definitions, null);
                    }
            }
        }

        public static int CachedTypes(GameDefinitions definitions)
        {
            var field = typeof(GameDefinitions).GetField(CacheField, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("GameDefinitions." + CacheField + "가 없다 — 정의의 캐시가 바뀌었으면 SharedDefinitions를 다시 본다");
            return ((ICollection)field.GetValue(definitions)).Count;
        }

        // 읽지 못하는 형(참조 어셈블리가 없는 것)은 건너뛴다 — 정의가 그 형일 수는 없다.
        private static Type[] Types(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException error)
            {
                return error.Types;
            }
        }
    }
}
