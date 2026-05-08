using Rootborn.Game.Story;
using UnityEngine;

namespace Rootborn.Game.ModernSociety
{
    [CreateAssetMenu(fileName = "ModernRoutineFlagRule_New", menuName = "Rootborn/Modern Society/Routine Flag Rule")]
    public sealed class ModernRoutineFlagRule : ScriptableObject
    {
        [SerializeField] private ModernActivityDefinition[] _requiredActivities = System.Array.Empty<ModernActivityDefinition>();
        [SerializeField] private StoryFlagDefinition _flag;

        public bool IsSatisfied(System.Collections.Generic.IReadOnlyList<ModernActivityDefinition> performed)
        {
            if (_requiredActivities == null || _requiredActivities.Length == 0 || performed == null)
            {
                return false;
            }

            for (int i = 0; i < _requiredActivities.Length; i++)
            {
                var required = _requiredActivities[i];
                if (required == null) return false;

                bool found = false;
                for (int j = 0; j < performed.Count; j++)
                {
                    if (performed[j] == required)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found) return false;
            }

            return true;
        }

        public void Apply(StoryFlagSet flags)
        {
            flags?.Set(_flag);
        }
    }
}
