using Rootborn.Game.Crafting;
using Rootborn.Game.Resources;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "LocationActivityOutcome_ResourceRecipe", menuName = "Rootborn/Student Life/Location Identity/Outcomes/Resource Recipe")]
    public sealed class LocationActivityResourceRecipeOutcome : LocationActivityOutcomeBase
    {
        [SerializeField] private ResourceNodeDefinition _resource;
        [SerializeField] private RecipeDefinition _recipe;
        [SerializeField] private string _nextHintKey;

        public ResourceNodeDefinition Resource => _resource;
        public RecipeDefinition Recipe => _recipe;
        public string NextHintKey => string.IsNullOrEmpty(_nextHintKey) ? "objective.resourceRecipe.next" : _nextHintKey;

        public override string Apply(StudentLifeProgress progress, LocationActivityDefinition activity)
        {
            if (progress == null || activity == null || _recipe == null) return string.Empty;
            var resourceRecipeProgress = ResourceRecipeProgressPersistence.LoadOrCreate(progress.SaveSlot, progress.PlayerId);
            bool changed = resourceRecipeProgress.TryReveal(_resource, _recipe, activity.Id);
            if (!changed) return string.Empty;
            ResourceRecipeProgressPersistence.Save(resourceRecipeProgress);
            return LocationActivityLogCodec.EncodeUnlock(activity.Id, _recipe.Id);
        }

        public void ConfigureForTests(ResourceNodeDefinition resource, RecipeDefinition recipe, string nextHintKey)
        {
            _resource = resource;
            _recipe = recipe;
            _nextHintKey = string.IsNullOrEmpty(nextHintKey) ? "objective.resourceRecipe.next" : nextHintKey;
        }
    }
}
