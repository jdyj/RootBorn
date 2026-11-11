using System.Collections.Generic;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Quests
{
    /// <summary>
    /// 행동 이벤트 하나를 퀘스트 체인에 적용하는 순서(원본 Assets/Scripts/UI/Quests/QuestChainRuntimeService.cs의
    /// ApplyRegisteredEventToSession과 CreditPlanChosenBeforeStart).
    /// 1) 대화 이벤트면 그 상대가 시작 NPC이고 아직 받지 않은 체인을 받고 추적하며, 받기 전에 오늘 고른 일과를 소급해 센다.
    /// 2) 이벤트를 기록한다. 3) 모든 체인의 단계 진행을 평가한다.
    /// 일반 퀘스트 로그(QuestLog)는 여기서 다루지 않는다. 부르는 쪽이 필요하면 따로 센다.
    /// </summary>
    public static class QuestChainEvents
    {
        /// <summary>키가 있고 개수가 1 이상인 이벤트만 적용한다(원본과 같다).</summary>
        public static bool IsApplicable(in QuestEvent questEvent) => !string.IsNullOrEmpty(questEvent.EventKey) && questEvent.Count > 0;

        /// <summary>적용했으면 true. 적용할 수 없는 이벤트나 빈 체인 기록이면 아무것도 바꾸지 않고 false.</summary>
        public static bool Apply(QuestChainLog log, IReadOnlyList<QuestChainDefinition> chains, IReadOnlyList<DailyPlanRouteSet> routeSets,
            QuestEvent questEvent, QuestRuntimeContext context, RewardRuntimeContext rewards)
        {
            if (log == null || !IsApplicable(in questEvent)) return false;
            if (chains == null) chains = System.Array.Empty<QuestChainDefinition>();
            if (questEvent.Kind == QuestEventKind.Talk && !string.IsNullOrEmpty(questEvent.SubjectId))
            {
                for (int i = 0; i < chains.Count; i++)
                {
                    var chain = chains[i];
                    if (chain == null || !chain.IsStartedByTalkingTo(questEvent.SubjectId) || log.GetState(chain) != QuestChainState.Available
                        || !log.AcceptWithContext(chain, context)) continue;
                    log.Track(chain);
                    CreditPlanChosenBeforeStart(log, routeSets, context, rewards);
                }
            }

            log.RecordEventWithContext(questEvent, context, rewards);
            for (int i = 0; i < chains.Count; i++) if (chains[i] != null) log.EvaluateStepProgress(chains[i], context, rewards);
            return true;
        }

        /// <summary>
        /// 체인을 받기 전에 오늘 고른 일과를 일과 선택 이벤트로 센다(키 "daily-plan-before-start:&lt;날&gt;:&lt;길&gt;", 같은 날 같은 길은 한 번).
        /// 학생 진행이 없거나, 고른 길이 없거나, 그 길이 길 묶음에 없으면 false.
        /// </summary>
        public static bool CreditPlanChosenBeforeStart(QuestChainLog log, IReadOnlyList<DailyPlanRouteSet> routeSets, QuestRuntimeContext context, RewardRuntimeContext rewards)
        {
            var student = context.StudentLifeProgress;
            string routeId = student != null ? student.DailyPlan.SelectedRouteId : string.Empty;
            if (log == null || string.IsNullOrEmpty(routeId) || routeSets == null) return false;
            var sets = new DailyPlanRouteSet[routeSets.Count];
            for (int i = 0; i < sets.Length; i++) sets[i] = routeSets[i];
            if (new DailyPlanRouteRunner().FindRoute(sets, routeId) == null) return false;
            log.RecordEventWithContext(new QuestEvent(QuestEventKind.DailyPlanSelected, "daily-plan-before-start:" + student.CurrentDay + ":" + routeId, routeId), context, rewards);
            return true;
        }
    }
}
