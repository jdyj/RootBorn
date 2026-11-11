using System;
using System.Collections.Generic;

namespace Rootborn.Core.Quests
{
    /// <summary>Same names and order as the game's QuestChainState; saves store the name.</summary>
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

    /// <summary>Same field names and types as the game's save file (written by JsonUtility).</summary>
    [Serializable]
    public sealed class QuestChainLogSaveData : Save.IVersionedSave
    {
        /// <summary>The highest format version this program reads. Version 0 is a file written by the Unity build.</summary>
        public const int KnownVersion = 1;

        public int Version;
        public QuestChainProgressSaveData[] Chains = Array.Empty<QuestChainProgressSaveData>();

        int Save.IVersionedSave.Version { get => Version; set => Version = value; }
        int Save.IVersionedSave.KnownVersion => KnownVersion;
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

    /// <summary>
    /// One chain's progress: state, current step, objective counts, processed event keys and claimed reward ids.
    /// A step's rewards are paid once (reward id "step:&lt;step id&gt;"), together with moving to the next step.
    /// </summary>
    public sealed class QuestChainProgress
    {
        private const string StepRewardPrefix = "step:";
        private readonly HashSet<string> _completedStepIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _processedEventKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _rewardClaimedIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _transitionHistory = new List<string>();
        private int[] _objectiveProgressValues;
        private bool _isApplyingStep;

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

        public bool SetAvailable() => State == QuestChainState.Available;

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
            if (!TryCreditEvent(chain, in questEvent)) return false;
            TryCompleteCurrentStep(chain, default, default);
            return true;
        }

        internal bool TryCreditEvent(QuestChainDefinition chain, in QuestEvent questEvent)
        {
            if (_isApplyingStep || chain == null || (State != QuestChainState.Active && State != QuestChainState.Tracked)) return false;
            // Checked before the key is used, so an event without a positive count cannot use up a key.
            if (questEvent.Count <= 0) return false;
            if (!string.IsNullOrEmpty(questEvent.EventKey) && !_processedEventKeys.Add(questEvent.EventKey)) return false;
            var step = FindStep(chain, CurrentStepId);
            if (step == null || step.Objectives.Count == 0) return false;

            bool changed = false;
            for (int i = 0; i < step.Objectives.Count; i++)
            {
                var objective = step.Objectives[i];
                if (objective == null || !objective.Matches(in questEvent)) continue;
                int required = objective.RequiredCount;
                int current = _objectiveProgressValues[i];
                if (current >= required) continue;
                int next = Math.Min(required, current + objective.GetDelta(in questEvent));
                if (next == current) continue;
                _objectiveProgressValues[i] = next;
                changed = true;
            }

            return changed;
        }

        public bool MarkRewardClaimed(string rewardId) => !string.IsNullOrEmpty(rewardId) && _rewardClaimedIds.Add(rewardId);

        public bool IsRewardClaimed(string rewardId) => !string.IsNullOrEmpty(rewardId) && _rewardClaimedIds.Contains(rewardId);

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
            // Saves store the state's name. Numbers and unknown names are not states (the game accepted e.g. "9").
            if (!string.IsNullOrEmpty(saveData.ChainState) && Array.IndexOf(Enum.GetNames(typeof(QuestChainState)), saveData.ChainState) >= 0)
                progress.State = (QuestChainState)Enum.Parse(typeof(QuestChainState), saveData.ChainState);
            progress.IsTracked = saveData.IsTracked && !IsTerminalState(progress.State);
            progress.CurrentStepId = string.IsNullOrEmpty(saveData.CurrentStepId) ? FirstStepId(chain) : ResolveSavedStepId(chain, saveData.CurrentStepId);
            progress._objectiveProgressValues = CreateObjectiveProgressValues(chain, progress.CurrentStepId);
            var restoredStep = FindStep(chain, progress.CurrentStepId);
            if (saveData.ObjectiveProgressValues != null && restoredStep != null)
            {
                int count = Math.Min(progress._objectiveProgressValues.Length, saveData.ObjectiveProgressValues.Length);
                for (int i = 0; i < count; i++)
                {
                    int required = restoredStep.Objectives[i] != null ? restoredStep.Objectives[i].RequiredCount : 0;
                    progress._objectiveProgressValues[i] = Math.Min(Math.Max(saveData.ObjectiveProgressValues[i], 0), required);
                }
            }

            AddSavedStepIds(progress._completedStepIds, chain, saveData.CompletedStepIds);
            AddAll(progress._processedEventKeys, saveData.ProcessedEventKeys);
            AddSavedRewardIds(progress._rewardClaimedIds, chain, saveData.RewardClaimedIds);
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

        public QuestStepDefinition GetCurrentStep(QuestChainDefinition chain) => FindStep(chain, CurrentStepId);

        internal void RecordAcceptedDay(int dayIndex)
        {
            if (AcceptedDayIndex < 0) AcceptedDayIndex = dayIndex;
        }

        internal bool TryCompleteCurrentStep(QuestChainDefinition chain, QuestRuntimeContext context, RewardRuntimeContext rewards)
        {
            if (_isApplyingStep || (State != QuestChainState.Active && State != QuestChainState.Tracked)) return false;
            var step = GetCurrentStep(chain);
            if (!AreCurrentObjectivesComplete(step) || !ConditionsSatisfied(step.CompletionConditions, in context)) return false;
            if (!ConditionsSatisfied(step.BlockConditions, in context)) return false;
            if (!TryResolveNextStep(chain, step, in context, out var nextStep)) return false;

            string rewardId = StepRewardPrefix + step.Id;
            int dayIndex = context.StudentLifeProgress != null ? context.StudentLifeProgress.CurrentDay : -1;
            _isApplyingStep = true;
            try
            {
                Action commit = () => CommitStep(step, nextStep, rewardId, dayIndex);
                if (IsRewardClaimed(rewardId))
                {
                    commit();
                    return true;
                }

                return QuestRewardBatch.TryApply(step.Rewards, in rewards, commit);
            }
            finally
            {
                _isApplyingStep = false;
            }
        }

        private bool TryResolveNextStep(QuestChainDefinition chain, QuestStepDefinition step, in QuestRuntimeContext context, out QuestStepDefinition nextStep)
        {
            nextStep = null;
            if (chain == null || step == null) return false;
            if (step.Transitions.Count > 0)
            {
                for (int i = 0; i < step.Transitions.Count; i++)
                {
                    var transition = step.Transitions[i];
                    if (transition == null || !transition.TryResolveNextStep(chain, this, in context, out string nextId)) continue;
                    if (string.IsNullOrEmpty(nextId)) return true;
                    nextStep = FindStep(chain, nextId);
                    return nextStep != null && nextStep.Id != step.Id && !_completedStepIds.Contains(nextStep.Id);
                }

                return false;
            }

            for (int i = 0; i < chain.Steps.Count; i++)
            {
                if (chain.Steps[i] == null || chain.Steps[i].Id != step.Id) continue;
                if (i == chain.Steps.Count - 1) return true;
                nextStep = chain.Steps[i + 1];
                return nextStep != null && !string.IsNullOrEmpty(nextStep.Id) && !_completedStepIds.Contains(nextStep.Id);
            }

            return false;
        }

        private void CommitStep(QuestStepDefinition step, QuestStepDefinition nextStep, string rewardId, int dayIndex)
        {
            _completedStepIds.Add(step.Id);
            _rewardClaimedIds.Add(rewardId);
            _transitionHistory.Add(step.Id + "->" + (nextStep != null ? nextStep.Id : "completed"));
            TodayQuestChainSummary = step.DisplayNameKey;
            BlockedReasonId = string.Empty;
            LastEvaluatedConditionSummary = string.Empty;
            if (nextStep == null)
            {
                State = QuestChainState.Completed;
                IsTracked = false;
                CompletedDayIndex = dayIndex;
                return;
            }

            CurrentStepId = nextStep.Id;
            _objectiveProgressValues = new int[nextStep.Objectives.Count];
        }

        private static bool ConditionsSatisfied(IReadOnlyList<QuestCondition> conditions, in QuestRuntimeContext context)
        {
            if (conditions == null) return true;
            for (int i = 0; i < conditions.Count; i++)
                if (conditions[i] == null || !conditions[i].IsSatisfied(in context)) return false;
            return true;
        }

        internal bool AreCurrentObjectivesComplete(QuestChainDefinition chain) => AreCurrentObjectivesComplete(GetCurrentStep(chain));

        private bool AreCurrentObjectivesComplete(QuestStepDefinition step)
        {
            if (step == null || step.Objectives.Count == 0) return false;
            for (int i = 0; i < step.Objectives.Count; i++)
            {
                var objective = step.Objectives[i];
                int required = objective != null ? objective.RequiredCount : 1;
                if (i >= _objectiveProgressValues.Length || _objectiveProgressValues[i] < required) return false;
            }

            return true;
        }

        private static bool IsTerminalState(QuestChainState state)
            => state == QuestChainState.Completed || state == QuestChainState.Failed || state == QuestChainState.Expired;

        private static string FirstStepId(QuestChainDefinition chain)
            => chain != null && chain.Steps.Count > 0 && chain.Steps[0] != null ? chain.Steps[0].Id : string.Empty;

        private static QuestStepDefinition FindStep(QuestChainDefinition chain, string stepId)
        {
            if (chain == null) return null;
            for (int i = 0; i < chain.Steps.Count; i++)
            {
                var step = chain.Steps[i];
                if (step != null && step.Id == stepId) return step;
            }

            return null;
        }

        // A renamed step lists its earlier ids as data, so a save that stopped there keeps going.
        private static string ResolveSavedStepId(QuestChainDefinition chain, string savedStepId)
        {
            if (chain == null || FindStep(chain, savedStepId) != null) return savedStepId;
            for (int i = 0; i < chain.Steps.Count; i++)
            {
                var step = chain.Steps[i];
                if (step == null) continue;
                var legacyIds = step.LegacyStepIds;
                for (int j = 0; j < legacyIds.Count; j++) if (legacyIds[j] == savedStepId) return step.Id;
            }

            return savedStepId;
        }

        private static void AddSavedStepIds(HashSet<string> target, QuestChainDefinition chain, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++) if (!string.IsNullOrEmpty(values[i])) target.Add(ResolveSavedStepId(chain, values[i]));
        }

        // Step reward ids embed the step id. Translate them too, or a renamed step could pay out twice.
        private static void AddSavedRewardIds(HashSet<string> target, QuestChainDefinition chain, string[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (string.IsNullOrEmpty(value)) continue;
                target.Add(value.StartsWith(StepRewardPrefix, StringComparison.Ordinal)
                    ? StepRewardPrefix + ResolveSavedStepId(chain, value.Substring(StepRewardPrefix.Length))
                    : value);
            }
        }

        private static int[] CreateObjectiveProgressValues(QuestChainDefinition chain, string stepId)
        {
            var step = FindStep(chain, stepId);
            return new int[step != null ? step.Objectives.Count : 0];
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

    /// <summary>
    /// Every quest chain the player knows. Only one chain is tracked at a time; tracking another pauses it.
    /// A definition with the same id as a known chain finds that chain's progress.
    /// </summary>
    public sealed class QuestChainLog
    {
        private const string CompletionRewardId = "completion";
        private readonly Dictionary<QuestChainDefinition, QuestChainProgress> _progressByChain = new Dictionary<QuestChainDefinition, QuestChainProgress>();
        private readonly Dictionary<string, QuestChainProgress> _progressByChainId = new Dictionary<string, QuestChainProgress>(StringComparer.Ordinal);
        private readonly HashSet<string> _claimingChainIds = new HashSet<string>(StringComparer.Ordinal);

        public QuestChainLog(IEnumerable<QuestChainDefinition> chains)
        {
            if (chains == null) return;
            foreach (var chain in chains) AddChain(chain);
        }

        public bool AddChain(QuestChainDefinition chain)
        {
            if (chain == null || _progressByChain.ContainsKey(chain)) return false;
            var progress = new QuestChainProgress("default", "player", chain);
            _progressByChain.Add(chain, progress);
            CacheProgress(chain, progress);
            return true;
        }

        public QuestChainState GetState(QuestChainDefinition chain) => TryGetProgress(chain, out var progress) ? progress.State : QuestChainState.Available;

        public int GetObjectiveProgress(QuestChainDefinition chain, int objectiveIndex)
            => TryGetProgress(chain, out var progress) ? progress.GetObjectiveProgress(objectiveIndex) : 0;

        public string GetBlockedReasonId(QuestChainDefinition chain) => TryGetProgress(chain, out var progress) ? progress.BlockedReasonId : string.Empty;

        public string GetBlockedSummary(QuestChainDefinition chain) => TryGetProgress(chain, out var progress) ? progress.LastEvaluatedConditionSummary : string.Empty;

        public bool IsTracked(QuestChainDefinition chain) => TryGetProgress(chain, out var progress) && progress.IsTracked;

        public bool IsRewardClaimed(QuestChainDefinition chain, string rewardId) => TryGetProgress(chain, out var progress) && progress.IsRewardClaimed(rewardId);

        public bool EvaluateAvailability(QuestChainDefinition chain, QuestRuntimeContext context)
            => IsValidDefinition(chain) && TryGetProgress(chain, out var progress) && progress.SetAvailable() && AreAllSatisfied(chain.StartConditions, in context);

        public QuestStepDefinition GetCurrentStep(QuestChainDefinition chain) => TryGetProgress(chain, out var progress) ? progress.GetCurrentStep(chain) : null;

        public bool EvaluateTerminalState(QuestChainDefinition chain, QuestRuntimeContext context)
        {
            if (!TryGetProgress(chain, out var progress)) return false;
            if (progress.State == QuestChainState.Available || progress.State == QuestChainState.Completed || progress.State == QuestChainState.Failed || progress.State == QuestChainState.Expired) return false;

            var failure = FirstSatisfied(chain.FailureConditions, in context);
            if (failure != null) return progress.Fail(failure.BlockedReasonId, failure.BlockedSummary, ResolveDayIndex(in context));

            var expiration = FirstSatisfied(chain.ExpirationConditions, in context);
            if (expiration != null) return progress.Expire(expiration.BlockedReasonId, expiration.BlockedSummary, ResolveDayIndex(in context));

            return false;
        }

        public bool EvaluateBlockState(QuestChainDefinition chain, QuestRuntimeContext context)
        {
            if (!TryGetProgress(chain, out var progress)) return false;
            if (progress.State == QuestChainState.Available || progress.State == QuestChainState.Completed || progress.State == QuestChainState.Failed || progress.State == QuestChainState.Expired) return false;
            var step = progress.GetCurrentStep(chain);
            if (step == null) return progress.Block("questchain.invalid-step", "The saved quest step is not present in this chain.");
            var chainConditions = progress.State == QuestChainState.Blocked && chain.UnblockConditions.Count > 0 ? chain.UnblockConditions : chain.BlockConditions;
            var missing = FirstUnsatisfied(chainConditions, in context) ?? FirstUnsatisfied(step.BlockConditions, in context);
            if (missing != null) return progress.Block(missing.BlockedReasonId, missing.BlockedSummary);
            if (progress.State == QuestChainState.Blocked) return progress.Unblock();
            return false;
        }

        public bool Accept(QuestChainDefinition chain) => AcceptWithContext(chain, default);

        public bool AcceptWithContext(QuestChainDefinition chain, QuestRuntimeContext context)
        {
            if (!IsValidDefinition(chain) || !TryGetProgress(chain, out var progress) || !AreAllSatisfied(chain.StartConditions, in context)) return false;
            if (!progress.Accept(chain)) return false;
            progress.RecordAcceptedDay(ResolveDayIndex(in context));
            return true;
        }

        public bool Track(QuestChainDefinition chain)
        {
            if (!TryGetProgress(chain, out var target)) return false;
            foreach (var pair in _progressByChain)
                if (!ReferenceEquals(pair.Value, target) && pair.Value.State == QuestChainState.Tracked) pair.Value.Pause();
            return target.Track();
        }

        public bool Block(QuestChainDefinition chain, string reasonId, string summary) => TryGetProgress(chain, out var progress) && progress.Block(reasonId, summary);

        public bool Unblock(QuestChainDefinition chain) => TryGetProgress(chain, out var progress) && progress.Unblock();

        public void RecordEvent(QuestEvent questEvent) => RecordEventWithContext(questEvent, default, default);

        public void RecordEventWithContext(QuestEvent questEvent, QuestRuntimeContext context, RewardRuntimeContext rewards)
        {
            foreach (var pair in _progressByChain)
            {
                EvaluateTerminalState(pair.Key, context);
                EvaluateBlockState(pair.Key, context);
                if (pair.Value.TryCreditEvent(pair.Key, in questEvent)) pair.Value.TryCompleteCurrentStep(pair.Key, context, rewards);
            }
        }

        public bool AreCurrentObjectivesComplete(QuestChainDefinition chain) => TryGetProgress(chain, out var progress) && progress.AreCurrentObjectivesComplete(chain);

        public bool EvaluateStepProgress(QuestChainDefinition chain, QuestRuntimeContext context, RewardRuntimeContext rewards)
        {
            if (!TryGetProgress(chain, out var progress)) return false;
            EvaluateTerminalState(chain, context);
            EvaluateBlockState(chain, context);
            return progress.TryCompleteCurrentStep(chain, context, rewards);
        }

        public bool MarkRewardClaimed(QuestChainDefinition chain, string rewardId) => TryGetProgress(chain, out var progress) && progress.MarkRewardClaimed(rewardId);

        public bool CanClaimCompletionRewards(QuestChainDefinition chain, in RewardRuntimeContext context)
        {
            if (!TryGetProgress(chain, out var progress)) return false;
            if (progress.State != QuestChainState.Completed || progress.IsRewardClaimed(CompletionRewardId) || _claimingChainIds.Contains(chain.Id)) return false;
            return QuestRewardBatch.CanApply(chain.CompletionRewards, in context);
        }

        public bool ClaimCompletionRewards(QuestChainDefinition chain, in RewardRuntimeContext context)
        {
            if (!TryGetProgress(chain, out var progress)) return false;
            if (!CanClaimCompletionRewards(chain, in context) || !_claimingChainIds.Add(chain.Id)) return false;
            try
            {
                return QuestRewardBatch.TryApply(chain.CompletionRewards, in context, () => progress.MarkRewardClaimed(CompletionRewardId));
            }
            finally
            {
                _claimingChainIds.Remove(chain.Id);
            }
        }

        public QuestChainLogSaveData ToSaveData()
        {
            var list = new List<QuestChainProgressSaveData>(_progressByChain.Count);
            foreach (var pair in _progressByChain)
            {
                if (string.IsNullOrEmpty(pair.Key.Id)) continue;
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

        private static bool IsValidDefinition(QuestChainDefinition chain)
        {
            if (chain == null || string.IsNullOrEmpty(chain.Id) || chain.Steps.Count == 0) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < chain.Steps.Count; i++)
            {
                var step = chain.Steps[i];
                if (step == null || string.IsNullOrEmpty(step.Id) || !ids.Add(step.Id)) return false;
            }

            return true;
        }

        private static QuestCondition FirstUnsatisfied(IReadOnlyList<QuestCondition> conditions, in QuestRuntimeContext context)
        {
            if (conditions == null) return null;
            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition != null && !condition.IsSatisfied(in context)) return condition;
            }

            return null;
        }

        private static QuestCondition FirstSatisfied(IReadOnlyList<QuestCondition> conditions, in QuestRuntimeContext context)
        {
            if (conditions == null) return null;
            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition != null && condition.IsSatisfied(in context)) return condition;
            }

            return null;
        }

        private static bool AreAllSatisfied(IReadOnlyList<QuestCondition> conditions, in QuestRuntimeContext context) => FirstUnsatisfied(conditions, in context) == null;

        private static int ResolveDayIndex(in QuestRuntimeContext context) => context.StudentLifeProgress != null ? context.StudentLifeProgress.CurrentDay : -1;

        private void CacheProgress(QuestChainDefinition chain, QuestChainProgress progress)
        {
            if (string.IsNullOrEmpty(chain.Id)) return;
            _progressByChainId[chain.Id] = progress;
        }

        private bool TryGetDefinition(string chainId, out QuestChainDefinition definition)
        {
            foreach (var pair in _progressByChain)
            {
                if (pair.Key.Id != chainId) continue;
                definition = pair.Key;
                return true;
            }

            definition = null;
            return false;
        }

        private bool TryGetProgress(QuestChainDefinition chain, out QuestChainProgress progress)
        {
            if (chain != null)
            {
                if (_progressByChain.TryGetValue(chain, out progress)) return true;
                if (!string.IsNullOrEmpty(chain.Id) && _progressByChainId.TryGetValue(chain.Id, out progress)) return true;
            }

            progress = null;
            return false;
        }
    }
}
