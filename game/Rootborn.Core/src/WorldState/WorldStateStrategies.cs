namespace Rootborn.Core.WorldState
{
    /// <summary>조건을 평가할 때 주어지는 것. 장소·NPC·상점·이벤트는 id로 받는다.</summary>
    public readonly struct WorldStateConditionContext
    {
        public readonly WorldStateProgress Progress;
        public readonly string LocationId;
        public readonly string NpcId;
        public readonly string ShopId;
        public readonly string EventId;

        public WorldStateConditionContext(WorldStateProgress progress, string locationId, string npcId, string shopId, string eventId)
        {
            Progress = progress;
            LocationId = locationId ?? string.Empty;
            NpcId = npcId ?? string.Empty;
            ShopId = shopId ?? string.Empty;
            EventId = eventId ?? string.Empty;
        }
    }

    public interface IWorldStateCondition
    {
        bool IsSatisfied(in WorldStateConditionContext context);
    }

    /// <summary>상태가 켜졌는지가 기대값과 같으면 참. 상태나 진행이 없으면 '켜지지 않음'으로 본다.</summary>
    public sealed class WorldStateFlagActiveCondition : IWorldStateCondition
    {
        public WorldStateFlagActiveCondition(string flagId, bool expectedActive = true)
        {
            FlagId = flagId ?? string.Empty;
            ExpectedActive = expectedActive;
        }

        public string FlagId { get; }
        public bool ExpectedActive { get; }

        public bool IsSatisfied(in WorldStateConditionContext context)
        {
            if (context.Progress == null || string.IsNullOrEmpty(FlagId)) return !ExpectedActive;
            return context.Progress.IsActive(FlagId) == ExpectedActive;
        }
    }

    /// <summary>효과를 받는 월드 물체. 원본은 GameObject였다.</summary>
    public interface IWorldStateEffectTarget
    {
        void SetActive(bool active);
    }

    public readonly struct WorldStateEffectContext
    {
        public readonly WorldStateProgress Progress;
        public readonly IWorldStateEffectTarget Target;

        public WorldStateEffectContext(WorldStateProgress progress, IWorldStateEffectTarget target)
        {
            Progress = progress;
            Target = target;
        }
    }

    public interface IWorldStateEffect
    {
        bool CanApply(in WorldStateEffectContext context, WorldStateFlag flag);
        bool Apply(in WorldStateEffectContext context, WorldStateFlag flag);
    }

    /// <summary>상태가 켜졌고 이 효과 버전을 아직 적용하지 않았으면 물체를 켜거나 끈다.</summary>
    public sealed class WorldStateSetObjectActiveEffect : IWorldStateEffect
    {
        public WorldStateSetObjectActiveEffect(bool active = true) { Active = active; }

        public bool Active { get; }

        public bool CanApply(in WorldStateEffectContext context, WorldStateFlag flag)
        {
            return context.Progress != null && context.Target != null && flag != null && context.Progress.IsActive(flag)
                && context.Progress.GetRecord(flag).AppliedEffectVersion < flag.EffectVersion;
        }

        public bool Apply(in WorldStateEffectContext context, WorldStateFlag flag)
        {
            if (!CanApply(in context, flag)) return false;
            context.Target.SetActive(Active);
            context.Progress.MarkEffectVersionApplied(flag, flag.EffectVersion);
            return true;
        }
    }
}
