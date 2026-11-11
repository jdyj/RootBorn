using System;
using System.Collections.Generic;
using Rootborn.Core.Characters;
using Rootborn.Core.Dialogue;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Time;

namespace Rootborn.Game
{
    /// <summary>추적 중인 체인의 지금 단계(화면 상시 HUD가 그린다).</summary>
    public readonly struct TrackedObjectiveView
    {
        public TrackedObjectiveView(string chainId, string stepId, string stepNameKey, QuestChainState state, ObjectiveLine[] objectives, string blockedSummary)
        {
            ChainId = chainId ?? string.Empty;
            StepId = stepId ?? string.Empty;
            StepNameKey = stepNameKey ?? string.Empty;
            State = state;
            Objectives = objectives ?? Array.Empty<ObjectiveLine>();
            BlockedSummary = blockedSummary ?? string.Empty;
        }

        public bool HasChain => !string.IsNullOrEmpty(ChainId);
        public string ChainId { get; }
        public string StepId { get; }
        public string StepNameKey { get; }
        public QuestChainState State { get; }
        public ObjectiveLine[] Objectives { get; }
        public string BlockedSummary { get; }
    }

    public readonly struct ObjectiveLine
    {
        public ObjectiveLine(string displayKey, int current, int required)
        {
            DisplayKey = displayKey ?? string.Empty;
            Current = current;
            Required = required;
        }

        public string DisplayKey { get; }
        public int Current { get; }
        public int Required { get; }
    }

    /// <summary>읽기 전용 조회. Core의 요약기와 판정을 그대로 쓴다.</summary>
    public sealed partial class GameSession
    {
        /// <summary>추적 중인 체인 하나와 그 단계의 목표 진행. 없으면 빈 값.</summary>
        public TrackedObjectiveView TrackedObjective()
        {
            foreach (var chain in _chains)
            {
                if (chain == null || !QuestChains.IsTracked(chain)) continue;
                var step = QuestChains.GetCurrentStep(chain);
                if (step == null) continue;
                var lines = new ObjectiveLine[step.Objectives.Count];
                for (int i = 0; i < lines.Length; i++)
                    lines[i] = new ObjectiveLine(step.Objectives[i].DisplayKey, QuestChains.GetObjectiveProgress(chain, i), step.Objectives[i].RequiredCount);
                return new TrackedObjectiveView(chain.Id, step.Id, step.DisplayNameKey, QuestChains.GetState(chain), lines, QuestChains.GetBlockedSummary(chain));
            }

            return default;
        }

        /// <summary>장소 활동을 지금 할 수 없는 이유(시간·오늘 안 됨·오늘 했음·기력). None이면 비용으로는 할 수 있다(요구 조건은 따로 본다).</summary>
        public StudentActivityRefusalReason ExplainLocationActivity(string activityId)
            => _definitions.TryGet<LocationActivity>(activityId, out var activity)
                ? StudentActivityRefusal.Explain(Student, activity.TimePolicy, activity.EnergyCost, activity.FocusCost)
                : StudentActivityRefusalReason.None;

        public StudentActivityRefusalReason ExplainLifeActivity(string activityId)
            => _definitions.TryGet<LifeActivity>(activityId, out var activity)
                ? StudentActivityRefusal.Explain(Student, activity.TimePolicy, activity.EnergyCost, activity.FocusCost)
                : StudentActivityRefusalReason.None;

        /// <summary>그 장소의 장소 활동과 각 활동을 지금 못 하는 이유(정의 순서).</summary>
        public IReadOnlyList<(LocationActivity Activity, StudentActivityRefusalReason Refusal, bool RequirementsMet)> LocationActivitiesAt(string locationId)
        {
            var list = new List<(LocationActivity, StudentActivityRefusalReason, bool)>();
            foreach (var activity in _definitions.All<LocationActivity>())
            {
                if (activity == null || activity.LocationId != locationId) continue;
                list.Add((activity, StudentActivityRefusal.Explain(Student, activity.TimePolicy, activity.EnergyCost, activity.FocusCost), activity.HasSatisfiedRequirements(Student)));
            }

            return list;
        }

        /// <summary>지금 날·시각에 그 장소에 있는 NPC id(일정 정의 순서). 시각은 장소 상태와 같은 시간대 규칙으로 바꾼다.</summary>
        public IReadOnlyList<string> NpcsAt(string locationId)
        {
            var result = new List<string>();
            string slot = LocationStateTimeSlots.FromProgress(Student);
            foreach (var schedule in _definitions.All<NpcSchedule>())
            {
                if (schedule?.Npc == null || result.Contains(schedule.Npc.Id)) continue;
                if (_npcSchedules.ResolveLocationId(schedule.Id, Student.CurrentDay, slot, Student) == locationId) result.Add(schedule.Npc.Id);
            }

            return result;
        }

        /// <summary>장래희망마다 지금 고를 수 있는지와 그 이유(정의 순서). 장래희망 화면이 그린다. 판정은 Core CareerInterest가 한다.</summary>
        public IReadOnlyList<(CareerInterest Interest, CareerInterestSelectionEvaluation Evaluation)> CareerInterestChoices()
        {
            var list = new List<(CareerInterest, CareerInterestSelectionEvaluation)>();
            foreach (var interest in _definitions.All<CareerInterest>())
                if (interest != null) list.Add((interest, interest.Evaluate(CareerInterests, CareerCandidates, Student, Student.CurrentDay)));
            return list;
        }

        /// <summary>지금 학생 날(1일째부터).</summary>
        public int StudentDay => Student.CurrentDay;

        /// <summary>지금 학생 시각(0시부터 분). 활동으로만 흐르고 세이브에 들어간다(Unity LifeTimePresentation.Minutes).</summary>
        public int StudentTimeMinutes => Student.TimeMinutes;

        /// <summary>학생 시각을 0~24시로(Unity LifeTimePresentation.Hour). 마을 낮밤과 시계 화면이 쓴다.</summary>
        public float StudentHour => DayParts.WrapHour(Student.TimeMinutes / 60f);

        /// <summary>
        /// 캐릭터의 성장 단계 id(Unity WearableLook.StageFor → Core WearableRules.SelectStage): 성인 직업을 가졌으면 첫 성인 단계,
        /// 학생이면 도달한 날 중 가장 늦은 학생 단계. 맞는 단계가 없으면 빈 문자열.
        /// </summary>
        public string GrowthStageId() => WearableRules.SelectStage(_definitions.All<GrowthStageRule>(), Student.CurrentDay, Adult.IsAdult);

        /// <summary>오늘 할 수 있는 시간 포인트와 쓴 것.</summary>
        public (int Remaining, int Budget) TimePoints => (Student.TimePointsRemaining, Student.DailyTimePointBudget);
    }
}
