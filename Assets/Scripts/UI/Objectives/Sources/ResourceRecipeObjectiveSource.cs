using System;
using System.Collections.Generic;
using Rootborn.Game.Crafting;
using Rootborn.Game.Player;
using Rootborn.Game.StudentLife;

namespace Rootborn.UI.Objectives
{
    public sealed class ResourceRecipeObjectiveSource : IObjectiveJournalSource
    {
        private static readonly ObjectiveJournalCategory CategoryValue = new ObjectiveJournalCategory("Hints", "Hints");

        public string SourceId => "resource-recipes";
        public ObjectiveJournalCategory Category => CategoryValue;

        public ObjectiveJournalItem[] BuildItems(ObjectiveJournalContext context)
        {
            var recipes = context.Registry != null ? context.Registry.Recipes : Array.Empty<RecipeDefinition>();
            if (recipes == null || recipes.Length == 0 || context.Player == null) return Array.Empty<ObjectiveJournalItem>();

            var student = context.Player.GetComponent<StudentLifeProgressComponent>()?.EnsureProgress();
            if (student == null) return Array.Empty<ObjectiveJournalItem>();

            string playerId = ResolvePlayerId(context, student);
            var progress = ResourceRecipeProgressPersistence.LoadOrCreate(student.SaveSlot, playerId);
            var items = new List<ObjectiveJournalItem>();
            for (int i = 0; i < recipes.Length; i++)
            {
                var recipe = recipes[i];
                if (recipe == null || string.IsNullOrEmpty(recipe.Id) || !progress.IsRecipeRevealed(recipe)) continue;
                items.Add(new ObjectiveJournalItem(
                    "resource-recipe:" + recipe.Id,
                    Category.CategoryId,
                    string.IsNullOrEmpty(recipe.DisplayNameKey) ? recipe.Id : recipe.DisplayNameKey,
                    ObjectiveJournalSourceText.Localized("ui.journal.recipe.subtitle", "Resource recipe progress"),
                    "Revealed",
                    ObjectiveJournalSourceText.Localized("ui.journal.recipe.discovered", "Recipe discovered"),
                    string.IsNullOrEmpty(recipe.NextHintKey) ? ObjectiveJournalSourceText.Localized("ui.journal.recipe.next-hint", "Gather the listed resources, then craft when the surface and timing requirements are satisfied.") : recipe.NextHintKey,
                    false,
                    35 + i));
            }

            return items.ToArray();
        }

        private static string ResolvePlayerId(ObjectiveJournalContext context, StudentLifeProgress student)
        {
            var identity = context.Player != null ? context.Player.GetComponent<PlayerIdentity>() : null;
            if (identity != null && !string.IsNullOrEmpty(identity.PlayerId)) return identity.PlayerId;
            return string.IsNullOrEmpty(student.PlayerId) ? PlayerIdentity.DefaultPlayerId : student.PlayerId;
        }
    }
}
