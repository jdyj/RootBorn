using System;
using System.Collections.Generic;
using Rootborn.Core.Quests;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Dialogue
{
    /// <summary>NPC 기본 대화를 쓸 수 있는지 판정한다(원본 NpcDialogueConditionBase).</summary>
    public interface INpcDialogueCondition
    {
        bool IsSatisfied(StudentLifeProgress progress);
    }

    /// <summary>
    /// 대화 조건 하나: 튜토리얼 단계, 관계 최소값, 상태 최대값. 비운 항목은 보지 않는다. 진행이 없으면 요구가 하나도 없을 때만 참.
    /// </summary>
    public sealed class DialogueCondition : INpcDialogueCondition
    {
        public DialogueCondition(string requiredTutorialStageId, string minimumRelationshipId, int minimumRelationshipValue, string maximumStatusId, int maximumStatusValue = int.MaxValue)
        {
            RequiredTutorialStageId = requiredTutorialStageId ?? string.Empty;
            MinimumRelationshipId = minimumRelationshipId ?? string.Empty;
            MinimumRelationshipValue = minimumRelationshipValue;
            MaximumStatusId = maximumStatusId ?? string.Empty;
            MaximumStatusValue = maximumStatusValue;
        }

        public string RequiredTutorialStageId { get; }
        public string MinimumRelationshipId { get; }
        public int MinimumRelationshipValue { get; }
        public string MaximumStatusId { get; }
        public int MaximumStatusValue { get; }

        public bool IsSatisfied(StudentLifeProgress progress)
        {
            if (progress == null) return RequiredTutorialStageId.Length == 0 && MinimumRelationshipId.Length == 0 && MaximumStatusId.Length == 0;
            if (RequiredTutorialStageId.Length > 0 && progress.TutorialStageId != RequiredTutorialStageId) return false;
            if (MinimumRelationshipId.Length > 0 && progress.GetRelationshipValue(MinimumRelationshipId) < MinimumRelationshipValue) return false;
            if (MaximumStatusId.Length > 0 && progress.GetStatusValue(MaximumStatusId) > MaximumStatusValue) return false;
            return true;
        }
    }

    public enum DialogueQuestAction
    {
        None,
        AcceptQuest,
        ClaimReward,
        Close
    }

    public readonly struct DialogueChoiceContext
    {
        public readonly QuestLog QuestLog;
        public readonly RewardRuntimeContext RewardContext;

        public DialogueChoiceContext(QuestLog questLog, RewardRuntimeContext rewardContext)
        {
            QuestLog = questLog;
            RewardContext = rewardContext;
        }
    }

    /// <summary>
    /// 대화 선택지 하나. 퀘스트 받기는 시작 전일 때만 보이고, 받을 때는 퀘스트의 조건을 보상 문맥의 지식·이야기 깃발·학생 진행으로 판정한다
    /// (Core QuestLog.Accept 규칙). 보상 받기는 QuestLog의 사전 확인을 거친다.
    /// </summary>
    public sealed class DialogueChoice
    {
        public DialogueChoice(string labelKey, DialogueQuestAction questAction, QuestDefinition quest)
        {
            LabelKey = labelKey ?? string.Empty;
            QuestAction = questAction;
            Quest = quest;
        }

        public string LabelKey { get; }
        public DialogueQuestAction QuestAction { get; }
        public QuestDefinition Quest { get; }

        public bool IsAvailable(in DialogueChoiceContext context)
        {
            if (QuestAction == DialogueQuestAction.AcceptQuest) return context.QuestLog != null && Quest != null && context.QuestLog.GetState(Quest) == QuestStage.NotStarted;
            if (QuestAction == DialogueQuestAction.ClaimReward)
            {
                var rewardContext = context.RewardContext;
                return context.QuestLog != null && Quest != null && context.QuestLog.CanClaimReward(Quest, in rewardContext);
            }

            return true;
        }

        public bool TryExecute(in DialogueChoiceContext context)
        {
            if (!IsAvailable(in context)) return false;
            if (QuestAction == DialogueQuestAction.AcceptQuest)
            {
                var reward = context.RewardContext;
                var questContext = new QuestRuntimeContext(context.QuestLog, Quest, reward.KnowledgeProgress, reward.StoryFlags, reward.StudentLifeProgress);
                return context.QuestLog.Accept(Quest, in questContext);
            }

            if (QuestAction == DialogueQuestAction.ClaimReward)
            {
                var rewardContext = context.RewardContext;
                return context.QuestLog.ClaimReward(Quest, in rewardContext);
            }

            return QuestAction == DialogueQuestAction.Close || QuestAction == DialogueQuestAction.None;
        }
    }

    /// <summary>대화 하나: 대사 키, 선택지, 필요한 튜토리얼 단계, 조건. id는 데이터 내보내기의 자산 이름이다.</summary>
    public sealed class DialogueDefinition
    {
        private readonly string[] _lineKeys;
        private readonly DialogueChoice[] _choices;
        private readonly DialogueCondition[] _conditions;

        public DialogueDefinition(string id, string[] lineKeys, DialogueChoice[] choices, string requiredTutorialStageId, DialogueCondition[] conditions)
        {
            Id = id ?? string.Empty;
            _lineKeys = lineKeys ?? Array.Empty<string>();
            _choices = choices ?? Array.Empty<DialogueChoice>();
            RequiredTutorialStageId = requiredTutorialStageId ?? string.Empty;
            _conditions = conditions ?? Array.Empty<DialogueCondition>();
        }

        public string Id { get; }
        public IReadOnlyList<string> LineKeys => _lineKeys;
        public IReadOnlyList<DialogueChoice> Choices => _choices;
        public string RequiredTutorialStageId { get; }
        public IReadOnlyList<DialogueCondition> Conditions => _conditions;

        public bool IsAvailable(StudentLifeProgress progress)
        {
            if (RequiredTutorialStageId.Length > 0 && (progress == null || progress.TutorialStageId != RequiredTutorialStageId)) return false;
            for (int i = 0; i < _conditions.Length; i++)
            {
                var condition = _conditions[i];
                if (condition != null && !condition.IsSatisfied(progress)) return false;
            }

            return true;
        }
    }

    /// <summary>지금 열린 대화 하나.</summary>
    public sealed class DialogueSession
    {
        public DialogueDefinition Current { get; private set; }
        public bool IsOpen => Current != null;
        public void Open(DialogueDefinition dialogue) => Current = dialogue;
        public void Close() => Current = null;
    }

    public sealed class NpcRole
    {
        public NpcRole(string id, string displayName, int sortOrder)
        {
            Id = id ?? string.Empty;
            DisplayName = string.IsNullOrEmpty(displayName) ? Id : displayName;
            SortOrder = sortOrder;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public int SortOrder { get; }
    }

    /// <summary>
    /// NPC의 규칙 쪽 정의. 집 장소는 id로, 역할·대화·퀘스트는 정의로 가진다. 월드 그림·외형(원본 Texture2D·Rect·외형 정의)은 화면 쪽이라 없다.
    /// </summary>
    public sealed class NpcDefinition
    {
        private readonly NpcRole[] _roles;
        private readonly INpcDialogueCondition[] _defaultDialogueConditions;
        private readonly DialogueDefinition[] _stageDialogues;
        private readonly QuestDefinition[] _quests;

        public NpcDefinition(string id, string displayNameKey, string introductionKey, string homeLocationId, NpcRole[] roles, DialogueDefinition defaultDialogue,
            INpcDialogueCondition[] defaultDialogueConditions, DialogueDefinition[] stageDialogues, QuestDefinition[] quests)
        {
            Id = id ?? string.Empty;
            DisplayNameKey = displayNameKey ?? string.Empty;
            IntroductionKey = string.IsNullOrEmpty(introductionKey) ? DisplayNameKey : introductionKey;
            HomeLocationId = homeLocationId ?? string.Empty;
            _roles = roles ?? Array.Empty<NpcRole>();
            DefaultDialogue = defaultDialogue;
            _defaultDialogueConditions = defaultDialogueConditions ?? Array.Empty<INpcDialogueCondition>();
            _stageDialogues = stageDialogues ?? Array.Empty<DialogueDefinition>();
            _quests = quests ?? Array.Empty<QuestDefinition>();
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public string IntroductionKey { get; }
        public string HomeLocationId { get; }
        public IReadOnlyList<NpcRole> Roles => _roles;
        public DialogueDefinition DefaultDialogue { get; }
        public IReadOnlyList<INpcDialogueCondition> DefaultDialogueConditions => _defaultDialogueConditions;
        public IReadOnlyList<DialogueDefinition> StageDialogues => _stageDialogues;
        public IReadOnlyList<QuestDefinition> Quests => _quests;

        public bool CanUseDefaultDialogue(StudentLifeProgress progress)
        {
            for (int i = 0; i < _defaultDialogueConditions.Length; i++)
            {
                var condition = _defaultDialogueConditions[i];
                if (condition != null && !condition.IsSatisfied(progress)) return false;
            }

            return true;
        }

        /// <summary>쓸 수 있는 첫 단계 대화, 없으면 조건이 맞을 때 기본 대화, 그것도 아니면 null.</summary>
        public DialogueDefinition ResolveDialogue(StudentLifeProgress progress)
        {
            for (int i = 0; i < _stageDialogues.Length; i++)
            {
                var dialogue = _stageDialogues[i];
                if (dialogue != null && dialogue.IsAvailable(progress)) return dialogue;
            }

            return CanUseDefaultDialogue(progress) ? DefaultDialogue : null;
        }
    }
}
