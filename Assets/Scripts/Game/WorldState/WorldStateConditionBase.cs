using Rootborn.Game.Dialogue;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.WorldState
{
    public readonly struct WorldStateConditionContext
    {
        public readonly WorldStateProgress Progress;
        public readonly LocationDefinition Location;
        public readonly NpcDefinition Npc;
        public readonly ScriptableObject Shop;
        public readonly ScriptableObject EventDefinition;

        public WorldStateConditionContext(WorldStateProgress progress, LocationDefinition location, NpcDefinition npc, ScriptableObject shop, ScriptableObject eventDefinition)
        {
            Progress = progress;
            Location = location;
            Npc = npc;
            Shop = shop;
            EventDefinition = eventDefinition;
        }
    }

    public abstract class WorldStateConditionBase : ScriptableObject
    {
        public abstract bool IsSatisfied(in WorldStateConditionContext context);
    }

    [CreateAssetMenu(fileName = "WorldStateCondition_FlagActive", menuName = "Rootborn/World State/Conditions/Flag Active")]
    public sealed class WorldStateFlagActiveCondition : WorldStateConditionBase
    {
        [SerializeField] private WorldStateFlagDefinition _flag;
        [SerializeField] private bool _expectedActive = true;

        public override bool IsSatisfied(in WorldStateConditionContext context)
        {
            if (context.Progress == null || _flag == null) return !_expectedActive;
            return context.Progress.IsActive(_flag) == _expectedActive;
        }

        public void ConfigureForTests(WorldStateFlagDefinition flag, bool expectedActive)
        {
            _flag = flag;
            _expectedActive = expectedActive;
        }
    }
}
