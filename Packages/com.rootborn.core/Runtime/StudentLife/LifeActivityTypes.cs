using System;

namespace Rootborn.Core.StudentLife
{
    public enum LifeActivityCategory
    {
        School,
        Work,
        Hobby,
        Errand,
        Social,
        Rest,
        SelfStudy
    }

    public enum LifeActivityResultKind
    {
        Applied,
        InvalidRequest,
        InsufficientResources,
        RequirementFailed,
        DuplicateRequest
    }

    public enum StudentDayState
    {
        InProgress,
        ResultReady
    }

    public readonly struct LifeActivityResult
    {
        public readonly LifeActivityResultKind Kind;
        public readonly string SaveSlot;
        public readonly string PlayerId;
        public readonly string ActivityId;
        public readonly string RequestId;
        public readonly string ChoiceId;
        public readonly string[] ChangedTraitIds;

        public LifeActivityResult(LifeActivityResultKind kind, string saveSlot, string playerId, string activityId, string requestId)
        {
            Kind = kind;
            SaveSlot = string.IsNullOrEmpty(saveSlot) ? "default" : saveSlot;
            PlayerId = string.IsNullOrEmpty(playerId) ? "player" : playerId;
            ActivityId = string.IsNullOrEmpty(activityId) ? string.Empty : activityId;
            RequestId = string.IsNullOrEmpty(requestId) ? string.Empty : requestId;
            ChoiceId = string.Empty;
            ChangedTraitIds = Array.Empty<string>();
        }

        public LifeActivityResult(LifeActivityResultKind kind, string saveSlot, string playerId, string activityId, string requestId, string choiceId, string[] changedTraitIds)
            : this(kind, saveSlot, playerId, activityId, requestId)
        {
            ChoiceId = string.IsNullOrEmpty(choiceId) ? string.Empty : choiceId;
            ChangedTraitIds = changedTraitIds ?? Array.Empty<string>();
        }
    }

    public readonly struct StudentDaySummary
    {
        public readonly int DayNumber;
        public readonly string[] CompletedActivityIds;
        public readonly string[] ResultLogIds;
        public readonly string NextDayEntryPointId;
        public readonly string TutorialStageId;
        public readonly string NextObjectiveId;
        public readonly string NextGuideText;

        public StudentDaySummary(int dayNumber, string[] completedActivityIds, string[] resultLogIds, string nextDayEntryPointId, string tutorialStageId, string nextObjectiveId, string nextGuideText)
        {
            DayNumber = Math.Max(1, dayNumber);
            CompletedActivityIds = completedActivityIds ?? Array.Empty<string>();
            ResultLogIds = resultLogIds ?? Array.Empty<string>();
            NextDayEntryPointId = string.IsNullOrEmpty(nextDayEntryPointId) ? string.Empty : nextDayEntryPointId;
            TutorialStageId = string.IsNullOrEmpty(tutorialStageId) ? string.Empty : tutorialStageId;
            NextObjectiveId = string.IsNullOrEmpty(nextObjectiveId) ? string.Empty : nextObjectiveId;
            NextGuideText = string.IsNullOrEmpty(nextGuideText) ? string.Empty : nextGuideText;
        }
    }
}
