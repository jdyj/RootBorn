using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.Quests
{
    public enum QuestChainState
    {
        Available,
        Active,
        Tracked,
        Paused,
        Blocked,
        Completed,
        Failed,
        Expired
    }

    [Serializable]
    public sealed class QuestChainLogSaveData
    {
        public QuestChainProgressSaveData[] Chains = Array.Empty<QuestChainProgressSaveData>();
    }

    [Serializable]
    public sealed class QuestChainProgressSaveData
    {
        public string SaveSlot;
        public string PlayerId;
        public string ChainId;
        public string ChainState;
        public bool IsTracked;
        public string CurrentStepId;
        public string[] CompletedStepIds = Array.Empty<string>();
        public int[] ObjectiveProgressValues = Array.Empty<int>();
        public string[] ProcessedEventKeys = Array.Empty<string>();
        public string[] RewardClaimedIds = Array.Empty<string>();
        public string BlockedReasonId;
        public string LastEvaluatedConditionSummary;
        public string[] TransitionHistory = Array.Empty<string>();
        public string RelatedCareerInterestId;
        public string TodayQuestChainSummary;
        public int AcceptedDayIndex = -1;
        public int CompletedDayIndex = -1;
        public int FailedDayIndex = -1;
        public int ExpiredDayIndex = -1;
    }

    public sealed class QuestChainProgress
    {
        private readonly HashSet<string> _completedStepIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _processedEventKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _rewardClaimedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _transitionHistory = new List<string>();
        private int[] _objectiveProgressValues;

        public QuestChainProgress(string saveSlot, string playerId, QuestChainDefinition chain)
        {
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            ChainId = chain != null ? chain.Id : string.Empty;
            RelatedCareerInterestId = chain != null ? chain.RelatedCareerInterestId : string.Empty;
            State = QuestChainState.Available;
            CurrentStepId = FirstStepId(chain);
            AcceptedDayIndex = -1;
            CompletedDayIndex = -1;
            FailedDayIndex = -1;
            ExpiredDayIndex = -1;
            _objectiveProgressValues = CreateObjectiveProgressValues(chain, CurrentStepId);
        }

        public string SaveSlot { get; }
        public string PlayerId { get; }
        public string ChainId { get; private set; }
        public QuestChainState State { get; private set; }
        public bool IsTracked { get; private set; }
        public string CurrentStepId { get; private set; }
        public string BlockedReasonId { get; private set; }
        public string LastEvaluatedConditionSummary { get; private set; }
        public string RelatedCareerInterestId { get; private set; }
        public string TodayQuestChainSummary { get; private set; }
        public int AcceptedDayIndex { get; private set; }
        public int CompletedDayIndex { get; private set; }
        public int FailedDayIndex { get; private set; }
        public int ExpiredDayIndex { get; private set; }

        public bool SetAvailable()
        {
            if (State != QuestChainState.Available) return false;
            return true;
        }

        public bool Accept(QuestChainDefinition chain)
        {
            if (State != QuestChainState.Available && State != QuestChainState.Paused) return false;
            State = QuestChainState.Active;
            if (string.IsNullOrEmpty(CurrentStepId)) CurrentStepId = FirstStepId(chain);
            if (_objectiveProgressValues == null || _objectiveProgressValues.Length == 0) _objectiveProgressValues = CreateObjectiveProgressValues(chain, CurrentStepId);
            return true;
        }

        public bool Track()
        {
            if (State != QuestChainState.Active && State != QuestChainState.Paused && State != QuestChainState.Tracked) return false;
            IsTracked = true;
            State = QuestChainState.Tracked;
            return true;
        }

        public bool Pause()
        {
            if (State != QuestChainState.Active && State != QuestChainState.Tracked) return false;
            IsTracked = false;
            State = QuestChainState.Paused;
            return true;
        }

        public bool Block(string reasonId, string summary)
        {
            if (IsTerminalState(State)) return false;
            BlockedReasonId = reasonId ?? string.Empty;
            LastEvaluatedConditionSummary = summary ?? string.Empty;
            State = QuestChainState.Blocked;
            return true;
        }

        public bool Unblock()
        {
            if (State != QuestChainState.Blocked) return false;
            State = IsTracked ? QuestChainState.Tracked : QuestChainState.Active;
            BlockedReasonId = string.Empty;
            LastEvaluatedConditionSummary = string.Empty;
            return true;
        }

        public bool Fail(string reasonId, string summary, int dayIndex)
        {
            if (State == QuestChainState.Available || IsTerminalState(State)) return false;
            State = QuestChainState.Failed;
            IsTracked = false;
            BlockedReasonId = reasonId ?? string.Empty;
            LastEvaluatedConditionSummary = summary ?? string.Empty;
            FailedDayIndex = dayIndex;
            return true;
        }

        public bool Expire(string reasonId, string summary, int dayIndex)
        {
            if (State == QuestChainState.Available || IsTerminalState(State)) return false;
            State = QuestChainState.Expired;
            IsTracked = false;
            BlockedReasonId = reasonId ?? string.Empty;
            LastEvaluatedConditionSummary = summary ?? string.Empty;
            ExpiredDayIndex = dayIndex;
            return true;
        }

        public int GetObjectiveProgress(int objectiveIndex)
        {
            if (objectiveIndex < 0 || objectiveIndex >= _objectiveProgressValues.Length) throw new ArgumentOutOfRangeException(nameof(objectiveIndex));
            return _objectiveProgressValues[objectiveIndex];
        }

        public bool TryRecordEvent(QuestChainDefinition chain, in QuestEvent questEvent)
        {
            if (chain == null || (State != QuestChainState.Active && State != QuestChainState.Tracked)) return false;
            if (!string.IsNullOrEmpty(questEvent.EventKey) && !_processedEventKeys.Add(questEvent.EventKey)) return false;
            var step = FindStep(chain, CurrentStepId);
            if (step == null || step.Objectives == null || step.Objectives.Length == 0) return false;

            bool changed = false;
            for (int i = 0; i < step.Objectives.Length; i++)
            {
                var objective = step.Objectives[i];
                if (objective == null || !objective.Matches(in questEvent)) continue;
                int required = objective.RequiredCount;
                int current = _objectiveProgressValues[i];
                if (current >= required) continue;
                int next = Mathf.Min(required, current + objective.GetDelta(in questEvent));
                if (next == current) continue;
                _objectiveProgressValues[i] = next;
                changed = true;
            }

            if (changed && AreCurrentObjectivesComplete(step)) CompleteCurrentStep(step);
            return changed;
        }

        public bool MarkRewardClaimed(string rewardId)
        {
            return !string.IsNullOrEmpty(rewardId) && _rewardClaimedIds.Add(rewardId);
        }

        public bool IsRewardClaimed(string rewardId)
        {
            return !string.IsNullOrEmpty(rewardId) && _rewardClaimedIds.Contains(rewardId);
        }

        public QuestChainProgressSaveData ToSaveData()
        {
            return new QuestChainProgressSaveData
            {
                SaveSlot = SaveSlot,
                PlayerId = PlayerId,
                ChainId = ChainId,
                ChainState = State.ToString(),
                IsTracked = IsTracked,
                CurrentStepId = CurrentStepId,
                CompletedStepIds = ToArray(_completedStepIds),
                ObjectiveProgressValues = (int[])_objectiveProgressValues.Clone(),
                ProcessedEventKeys = ToArray(_processedEventKeys),
                RewardClaimedIds = ToArray(_rewardClaimedIds),
                BlockedReasonId = BlockedReasonId,
                LastEvaluatedConditionSummary = LastEvaluatedConditionSummary,
                TransitionHistory = _transitionHistory.ToArray(),
                RelatedCareerInterestId = RelatedCareerInterestId,
                TodayQuestChainSummary = TodayQuestChainSummary,
                AcceptedDayIndex = AcceptedDayIndex,
                CompletedDayIndex = CompletedDayIndex,
                FailedDayIndex = FailedDayIndex,
                ExpiredDayIndex = ExpiredDayIndex,
            };
        }

        public static QuestChainProgress FromSaveData(QuestChainProgressSaveData saveData, QuestChainDefinition chain)
        {
            var progress = new QuestChainProgress(saveData != null ? saveData.SaveSlot : "default", saveData != null ? saveData.PlayerId : "player", chain);
            if (saveData == null) return progress;
            progress.ChainId = string.IsNullOrEmpty(saveData.ChainId) ? (chain != null ? chain.Id : string.Empty) : saveData.ChainId;
            if (!string.IsNullOrEmpty(saveData.ChainState) && Enum.TryParse(saveData.ChainState, out QuestChainState state)) progress.State = state;
            progress.IsTracked = saveData.IsTracked && !IsTerminalState(progress.State);
            progress.CurrentStepId = string.IsNullOrEmpty(saveData.CurrentStepId) ? FirstStepId(chain) : saveData.CurrentStepId;
            progress._objectiveProgressValues = saveData.ObjectiveProgressValues != null ? (int[])saveData.ObjectiveProgressValues.Clone() : CreateObjectiveProgressValues(chain, progress.CurrentStepId);
            AddAll(progress._completedStepIds, saveData.CompletedStepIds);
            AddAll(progress._processedEventKeys, saveData.ProcessedEventKeys);
            AddAll(progress._rewardClaimedIds, saveData.RewardClaimedIds);
            progress.BlockedReasonId = saveData.BlockedReasonId ?? string.Empty;
            progress.LastEvaluatedConditionSummary = saveData.LastEvaluatedConditionSummary ?? string.Empty;
            progress.RelatedCareerInterestId = string.IsNullOrEmpty(saveData.RelatedCareerInterestId) ? (chain != null ? chain.RelatedCareerInterestId : string.Empty) : saveData.RelatedCareerInterestId;
            progress.TodayQuestChainSummary = saveData.TodayQuestChainSummary ?? string.Empty;
            progress.AcceptedDayIndex = saveData.AcceptedDayIndex;
            progress.CompletedDayIndex = saveData.CompletedDayIndex;
            progress.FailedDayIndex = saveData.FailedDayIndex;
            progress.ExpiredDayIndex = saveData.ExpiredDayIndex;
            if (saveData.TransitionHistory != null) progress._transitionHistory.AddRange(saveData.TransitionHistory);
            return progress;
        }

        private void CompleteCurrentStep(QuestStepDefinition step)
        {
            if (step != null && !string.IsNullOrEmpty(step.Id)) _completedStepIds.Add(step.Id);
            State = QuestChainState.Completed;
        }

        private bool AreCurrentObjectivesComplete(QuestStepDefinition step)
        {
            if (step == null || step.Objectives == null || step.Objectives.Length == 0) return false;
            for (int i = 0; i < step.Objectives.Length; i++)
            {
                var objective = step.Objectives[i];
                int required = objective != null ? objective.RequiredCount : 1;
                if (i >= _objectiveProgressValues.Length || _objectiveProgressValues[i] < required) return false;
            }

            return true;
        }

        private static bool IsTerminalState(QuestChainState state)
        {
            return state == QuestChainState.Completed || state == QuestChainState.Failed || state == QuestChainState.Expired;
        }

        private static string FirstStepId(QuestChainDefinition chain)
        {
            return chain != null && chain.Steps != null && chain.Steps.Length > 0 && chain.Steps[0] != null ? chain.Steps[0].Id : string.Empty;
        }

        private static QuestStepDefinition FindStep(QuestChainDefinition chain, string stepId)
        {
            if (chain == null || chain.Steps == null) return null;
            for (int i = 0; i < chain.Steps.Length; i++)
            {
                var step = chain.Steps[i];
                if (step != null && step.Id == stepId) return step;
            }

            return chain.Steps.Length > 0 ? chain.Steps[0] : null;
        }

        private static int[] CreateObjectiveProgressValues(QuestChainDefinition chain, string stepId)
        {
            var step = FindStep(chain, stepId);
            int count = step != null && step.Objectives != null ? step.Objectives.Length : 0;
            return new int[count];
        }

        private static void AddAll(HashSet<string> target, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(values[i]);
        }

        private static string[] ToArray(HashSet<string> values)
        {
            var result = new string[values.Count];
            values.CopyTo(result);
            return result;
        }
    }

    public sealed class QuestChainLog
    {
        private const string CompletionRewardId = "completion";
        private readonly Dictionary<QuestChainDefinition, QuestChainProgress> _progressByChain = new Dictionary<QuestChainDefinition, QuestChainProgress>();
        private readonly Dictionary<string, QuestChainProgress> _progressByChainId = new Dictionary<string, QuestChainProgress>(StringComparer.Ordinal);

        public QuestChainLog(IEnumerable<ScriptableObject> chains)
        {
            if (chains == null) return;
            foreach (var chain in chains)
            {
                if (chain is QuestChainDefinition definition) AddChain(definition);
            }
        }

        public bool AddChain(QuestChainDefinition chain)
        {
            if (chain == null) return false;
            if (_progressByChain.ContainsKey(chain)) return false;
            var progress = new QuestChainProgress("default", "player", chain);
            _progressByChain.Add(chain, progress);
            CacheProgress(chain, progress);
            return true;
        }

        public QuestChainState GetState(ScriptableObject chain)
        {
            return TryGetProgress(chain, out var progress) ? progress.State : QuestChainState.Available;
        }

        public int GetObjectiveProgress(ScriptableObject chain, int objectiveIndex)
        {
            return TryGetProgress(chain, out var progress) ? progress.GetObjectiveProgress(objectiveIndex) : 0;
        }

        public string GetBlockedReasonId(ScriptableObject chain)
        {
            return TryGetProgress(chain, out var progress) ? progress.BlockedReasonId : string.Empty;
        }

        public string GetBlockedSummary(ScriptableObject chain)
        {
            return TryGetProgress(chain, out var progress) ? progress.LastEvaluatedConditionSummary : string.Empty;
        }

        public bool IsTracked(ScriptableObject chain)
        {
            return TryGetProgress(chain, out var progress) && progress.IsTracked;
        }

        public bool IsRewardClaimed(ScriptableObject chain, string rewardId)
        {
            return TryGetProgress(chain, out var progress) && progress.IsRewardClaimed(rewardId);
        }

        public bool EvaluateAvailability(ScriptableObject chain, QuestRuntimeContext context)
        {
            return TryGetProgress(chain, out var progress) && progress.SetAvailable();
        }

        public bool EvaluateTerminalState(ScriptableObject chain, QuestRuntimeContext context)
        {
            if (!(chain is QuestChainDefinition definition) || !TryGetProgress(chain, out var progress)) return false;
            if (progress.State == QuestChainState.Available || progress.State == QuestChainState.Completed || progress.State == QuestChainState.Failed || progress.State == QuestChainState.Expired) return false;

            var failure = FirstSatisfied(definition.FailureConditions, in context);
            if (failure != null) return progress.Fail(failure.BlockedReasonId, failure.BlockedSummary, ResolveDayIndex(in context));

            var expiration = FirstSatisfied(definition.ExpirationConditions, in context);
            if (expiration != null) return progress.Expire(expiration.BlockedReasonId, expiration.BlockedSummary, ResolveDayIndex(in context));

            return false;
        }

        public bool EvaluateBlockState(ScriptableObject chain, QuestRuntimeContext context)
        {
            if (!(chain is QuestChainDefinition definition) || !TryGetProgress(chain, out var progress)) return false;
            if (progress.State == QuestChainState.Available || progress.State == QuestChainState.Completed || progress.State == QuestChainState.Failed || progress.State == QuestChainState.Expired) return false;

            if (progress.State == QuestChainState.Blocked)
            {
                var reopenConditions = definition.UnblockConditions != null && definition.UnblockConditions.Length > 0 ? definition.UnblockConditions : definition.BlockConditions;
                if (AreAllSatisfied(reopenConditions, in context)) return progress.Unblock();
                return true;
            }

            var missing = FirstUnsatisfied(definition.BlockConditions, in context);
            if (missing == null) return false;
            return progress.Block(missing.BlockedReasonId, missing.BlockedSummary);
        }

        public bool Accept(ScriptableObject chain)
        {
            return chain is QuestChainDefinition definition && TryGetProgress(chain, out var progress) && progress.Accept(definition);
        }

        public bool Track(ScriptableObject chain)
        {
            if (!TryGetProgress(chain, out var target)) return false;
            foreach (var pair in _progressByChain)
            {
                if (!ReferenceEquals(pair.Value, target) && pair.Value.State == QuestChainState.Tracked)
                {
                    pair.Value.Pause();
                }
            }

            return target.Track();
        }

        public bool Block(ScriptableObject chain, string reasonId, string summary)
        {
            return TryGetProgress(chain, out var progress) && progress.Block(reasonId, summary);
        }

        public bool Unblock(ScriptableObject chain)
        {
            return TryGetProgress(chain, out var progress) && progress.Unblock();
        }

        public void RecordEvent(QuestEvent questEvent)
        {
            foreach (var pair in _progressByChain)
            {
                pair.Value.TryRecordEvent(pair.Key, in questEvent);
            }
        }

        public bool MarkRewardClaimed(ScriptableObject chain, string rewardId)
        {
            return TryGetProgress(chain, out var progress) && progress.MarkRewardClaimed(rewardId);
        }

        public bool CanClaimCompletionRewards(ScriptableObject chain, in RewardRuntimeContext context)
        {
            if (!(chain is QuestChainDefinition definition) || !TryGetProgress(chain, out var progress)) return false;
            if (progress.State != QuestChainState.Completed || progress.IsRewardClaimed(CompletionRewardId)) return false;
            if (definition.CompletionRewards == null) return true;
            for (int i = 0; i < definition.CompletionRewards.Length; i++)
            {
                var reward = definition.CompletionRewards[i];
                if (reward != null && !reward.CanApply(in context)) return false;
            }

            return true;
        }

        public bool ClaimCompletionRewards(ScriptableObject chain, in RewardRuntimeContext context)
        {
            if (!(chain is QuestChainDefinition definition) || !TryGetProgress(chain, out var progress)) return false;
            if (!CanClaimCompletionRewards(chain, in context)) return false;
            if (definition.CompletionRewards != null)
            {
                for (int i = 0; i < definition.CompletionRewards.Length; i++) definition.CompletionRewards[i]?.Apply(in context);
            }

            return progress.MarkRewardClaimed(CompletionRewardId);
        }

        public QuestChainLogSaveData ToSaveData()
        {
            var list = new List<QuestChainProgressSaveData>(_progressByChain.Count);
            foreach (var pair in _progressByChain)
            {
                if (pair.Key == null || string.IsNullOrEmpty(pair.Key.Id)) continue;
                list.Add(pair.Value.ToSaveData());
            }

            return new QuestChainLogSaveData { Chains = list.ToArray() };
        }

        public void LoadFromSaveData(QuestChainLogSaveData saveData)
        {
            if (saveData == null || saveData.Chains == null) return;
            for (int i = 0; i < saveData.Chains.Length; i++)
            {
                var saved = saveData.Chains[i];
                if (saved == null || string.IsNullOrEmpty(saved.ChainId)) continue;
                if (!TryGetDefinition(saved.ChainId, out var definition)) continue;
                var progress = QuestChainProgress.FromSaveData(saved, definition);
                _progressByChain[definition] = progress;
                CacheProgress(definition, progress);
            }
        }

        private static QuestConditionBase FirstUnsatisfied(QuestConditionBase[] conditions, in QuestRuntimeContext context)
        {
            if (conditions == null) return null;
            for (int i = 0; i < conditions.Length; i++)
            {
                var condition = conditions[i];
                if (condition != null && !condition.IsSatisfied(in context)) return condition;
            }
            return null;
        }

        private static QuestConditionBase FirstSatisfied(QuestConditionBase[] conditions, in QuestRuntimeContext context)
        {
            if (conditions == null) return null;
            for (int i = 0; i < conditions.Length; i++)
            {
                var condition = conditions[i];
                if (condition != null && condition.IsSatisfied(in context)) return condition;
            }
            return null;
        }

        private static bool AreAllSatisfied(QuestConditionBase[] conditions, in QuestRuntimeContext context)
        {
            return FirstUnsatisfied(conditions, in context) == null;
        }

        private static int ResolveDayIndex(in QuestRuntimeContext context)
        {
            return context.StudentLifeProgress != null ? context.StudentLifeProgress.CurrentDay : -1;
        }

        private void CacheProgress(QuestChainDefinition chain, QuestChainProgress progress)
        {
            if (chain == null || string.IsNullOrEmpty(chain.Id) || progress == null) return;
            _progressByChainId[chain.Id] = progress;
        }

        private bool TryGetDefinition(string chainId, out QuestChainDefinition definition)
        {
            foreach (var pair in _progressByChain)
            {
                if (pair.Key != null && pair.Key.Id == chainId)
                {
                    definition = pair.Key;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        private bool TryGetProgress(ScriptableObject chain, out QuestChainProgress progress)
        {
            if (chain is QuestChainDefinition definition)
            {
                if (_progressByChain.TryGetValue(definition, out progress)) return true;
                if (!string.IsNullOrEmpty(definition.Id) && _progressByChainId.TryGetValue(definition.Id, out progress)) return true;
            }

            progress = null;
            return false;
        }
    }
}
