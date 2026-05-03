using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Crafting
{
    [CreateAssetMenu(fileName = "Recipe_New", menuName = "Rootborn/Crafting/Recipe Definition")]
    public sealed class RecipeDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private CraftStepBase[] _steps = System.Array.Empty<CraftStepBase>();
        [SerializeField] private ToolDefinition _producesTool;

        public string Id => _id;
        public CraftStepBase[] Steps => _steps;
        public ToolDefinition ProducesTool => _producesTool;

        public bool AllStepsSatisfied(in CraftAttemptState state)
        {
            for (int i = 0; i < _steps.Length; i++)
            {
                var s = _steps[i];
                if (s == null) return false;
                if (!s.IsSatisfied(in state)) return false;
            }
            return _steps.Length > 0;
        }
    }
}
