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

    /// <summary>대화 선택지를 지금 못 고르는 이유. None이면 고를 수 있다.</summary>
    public enum DialogueChoiceBlock
    {
        None,
        /// <summary>퀘스트 행동인데 퀘스트나 퀘스트 로그가 없다.</summary>
        MissingQuest,
        /// <summary>받기: 이미 받았거나 끝냈다.</summary>
        QuestAlreadyStarted,
        /// <summary>받기: 퀘스트의 제공 조건이나 선행 조건이 아직 맞지 않는다.</summary>
        QuestConditionsNotMet,
        /// <summary>보상 받기: 퀘스트를 아직 끝내지 않았다(시작 전 포함).</summary>
        QuestNotCompleted,
        /// <summary>보상 받기: 이미 받았다.</summary>
        RewardAlreadyClaimed,
        /// <summary>보상 받기: 보상이 가방에 다 들어가지 않는다(또는 다른 받기가 진행 중이다).</summary>
        RewardDoesNotFit
    }

    /// <summary>
    /// 선택지 판정 결과. Visible은 Unity 판의 숨김 규칙(IsAvailable)과 같고, CanChoose는 지금 골랐을 때 적용되는지다.
    /// 받기 선택지는 조건이 맞지 않아도 보인다(Unity 판과 같음) — 그때 CanChoose는 거짓이고 Block이 이유다.
    /// </summary>
    public readonly struct DialogueChoiceEvaluation
    {
        public DialogueChoiceEvaluation(bool visible, DialogueChoiceBlock block)
        {
            Visible = visible;
            Block = block;
        }

        public bool Visible { get; }
        public bool CanChoose => Block == DialogueChoiceBlock.None;
        public DialogueChoiceBlock Block { get; }
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

        /// <summary>보이는지, 지금 고를 수 있는지와 못 고르는 이유. 아무것도 바꾸지 않는다.</summary>
        public DialogueChoiceEvaluation Evaluate(in DialogueChoiceContext context)
        {
            if (QuestAction != DialogueQuestAction.AcceptQuest && QuestAction != DialogueQuestAction.ClaimReward)
                return new DialogueChoiceEvaluation(true, DialogueChoiceBlock.None);
            if (context.QuestLog == null || Quest == null) return new DialogueChoiceEvaluation(false, DialogueChoiceBlock.MissingQuest);
            var state = context.QuestLog.GetState(Quest);
            if (QuestAction == DialogueQuestAction.AcceptQuest)
            {
                if (state != QuestStage.NotStarted) return new DialogueChoiceEvaluation(false, DialogueChoiceBlock.QuestAlreadyStarted);
                var reward = context.RewardContext;
                var questContext = new QuestRuntimeContext(context.QuestLog, Quest, reward.KnowledgeProgress, reward.StoryFlags, reward.StudentLifeProgress);
                return new DialogueChoiceEvaluation(true, context.QuestLog.CanAccept(Quest, in questContext) ? DialogueChoiceBlock.None : DialogueChoiceBlock.QuestConditionsNotMet);
            }

            if (state == QuestStage.RewardClaimed) return new DialogueChoiceEvaluation(false, DialogueChoiceBlock.RewardAlreadyClaimed);
            if (state != QuestStage.Completed) return new DialogueChoiceEvaluation(false, DialogueChoiceBlock.QuestNotCompleted);
            var rewardContext = context.RewardContext;
            return context.QuestLog.CanClaimReward(Quest, in rewardContext)
                ? new DialogueChoiceEvaluation(true, DialogueChoiceBlock.None)
                : new DialogueChoiceEvaluation(false, DialogueChoiceBlock.RewardDoesNotFit);
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
