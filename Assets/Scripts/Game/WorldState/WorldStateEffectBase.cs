using UnityEngine;

namespace Rootborn.Game.WorldState
{
    public readonly struct WorldStateEffectContext
    {
        public readonly WorldStateProgress Progress;
        public readonly GameObject Target;

        public WorldStateEffectContext(WorldStateProgress progress, GameObject target)
        {
            Progress = progress;
            Target = target;
        }
    }

    public abstract class WorldStateEffectBase : ScriptableObject
    {
        public abstract bool CanApply(in WorldStateEffectContext context, WorldStateFlagDefinition flag);
        public abstract bool Apply(in WorldStateEffectContext context, WorldStateFlagDefinition flag);
    }

    [CreateAssetMenu(fileName = "WorldStateEffect_SetActive", menuName = "Rootborn/World State/Effects/Set Object Active")]
    public sealed class WorldStateSetObjectActiveEffect : WorldStateEffectBase
    {
        [SerializeField] private bool _active = true;

        public override bool CanApply(in WorldStateEffectContext context, WorldStateFlagDefinition flag)
        {
            return context.Progress != null && context.Target != null && flag != null && context.Progress.IsActive(flag) && context.Progress.GetRecord(flag).AppliedEffectVersion < flag.EffectVersion;
        }

        public override bool Apply(in WorldStateEffectContext context, WorldStateFlagDefinition flag)
        {
            if (!CanApply(in context, flag)) return false;
            context.Target.SetActive(_active);
            context.Progress.MarkEffectVersionApplied(flag, flag.EffectVersion);
            return true;
        }

        public void ConfigureForTests(bool active)
        {
            _active = active;
        }
    }
}
