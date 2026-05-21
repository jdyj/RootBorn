using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Crafting;
using Rootborn.Game.Resources;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Objectives;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Crafting
{
    public sealed class ResourceRecipeActivitySliceTests
    {
        private const BindingFlags Bind = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string RegistryPath = "Assets/Data/Registry/GameDataRegistry.asset";
        private const string TownFieldNotesRecipePath = "Assets/Data/Recipes/Recipe_Town_FieldNotes.asset";
        private const string SquareHelpActivityPath = "Assets/Data/StudentLife/LocationIdentities/Activities/LocationActivity_Square_HelpRequest.asset";
        private string _saveRoot;

        [SetUp]
        public void SetUp()
        {
            _saveRoot = Path.Combine(Path.GetTempPath(), "rootborn-resource-recipe-edit", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
        }

        [TearDown]
        public void TearDown()
        {
            SaveService.SetRootDirectoryForTests(null);
            if (!string.IsNullOrEmpty(_saveRoot) && Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
        }

        [Test]
        public void RESOURCE_RECIPE_EDIT_001_GameDataRegistryExposesRecipeDefinitions()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var recipe = MakeRecipe("recipe.town.field-notes");
            SetField(registry, "_recipes", new[] { recipe });

            Assert.AreEqual(1, registry.Recipes.Length);
            Assert.AreSame(recipe, registry.Recipes[0]);
        }

        [Test]
        public void RESOURCE_RECIPE_EDIT_002_ProgressPersistsBySaveSlotAndPlayerIdentity()
        {
            var recipe = MakeRecipe("recipe.town.field-notes");
            var progress = ResourceRecipeProgressPersistence.LoadOrCreate("slot-a", "player-a");

            Assert.IsTrue(progress.TryRevealRecipe(recipe, "activity.square.help"));
            ResourceRecipeProgressPersistence.Save(progress);

            var restored = ResourceRecipeProgressPersistence.LoadOrCreate("slot-a", "player-a");
            var otherPlayer = ResourceRecipeProgressPersistence.LoadOrCreate("slot-a", "player-b");
            Assert.IsTrue(restored.IsRecipeRevealed(recipe));
            Assert.IsFalse(otherPlayer.IsRecipeRevealed(recipe));
        }

        [Test]
        public void RESOURCE_RECIPE_EDIT_003_LocationActivityOutcomeRevealsRecipeOnceWithoutDuplicateProgress()
        {
            var recipe = MakeRecipe("recipe.town.field-notes");
            var resource = MakeResource("resource.town.paper-scrap");
            var outcome = ScriptableObject.CreateInstance<LocationActivityResourceRecipeOutcome>();
            outcome.ConfigureForTests(resource, recipe, "objective.recipe.field-notes.next");
            var activity = MakeActivity("activity.square.help", outcome);
            var progress = new StudentLifeProgress("slot-b", "player-b", 12, 10, 0, 8 * 60);

            string first = outcome.Apply(progress, activity);
            string second = outcome.Apply(progress, activity);
            var restored = ResourceRecipeProgressPersistence.LoadOrCreate("slot-b", "player-b");

            Assert.IsNotEmpty(first);
            Assert.IsEmpty(second, "Repeating a recipe reveal must not duplicate progression.");
            Assert.IsTrue(restored.IsRecipeRevealed(recipe));
            Assert.IsTrue(restored.IsResourceSeen(resource));
        }

        [Test]
        public void RESOURCE_RECIPE_EDIT_004_ObjectiveJournalSourceShowsRevealedRecipeHint()
        {
            var recipe = MakeRecipe("recipe.town.field-notes");
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            SetField(registry, "_recipes", new[] { recipe });
            var player = new GameObject("Player");
            try
            {
                var student = player.AddComponent<StudentLifeProgressComponent>();
                student.ConfigureForTests("slot-c", "player-c", 12, 10, 0, 8 * 60);
                var progress = ResourceRecipeProgressPersistence.LoadOrCreate("slot-c", "player-c");
                Assert.IsTrue(progress.TryRevealRecipe(recipe, "activity.square.help"));
                ResourceRecipeProgressPersistence.Save(progress);

                var source = new ResourceRecipeObjectiveSource();
                var items = source.BuildItems(new ObjectiveJournalContext(registry, player, null));

                Assert.AreEqual(1, items.Length);
                Assert.AreEqual("Hints", items[0].CategoryId);
                Assert.AreEqual("resource-recipe:recipe.town.field-notes", items[0].StableKey);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void RESOURCE_RECIPE_EDIT_005_TownFieldNotesRecipeAssetIsRegisteredInGameDataRegistry()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(TownFieldNotesRecipePath);
            var registry = AssetDatabase.LoadAssetAtPath<GameDataRegistry>(RegistryPath);

            Assert.IsNotNull(recipe, "Town resource/recipe slice must create a recipe asset at " + TownFieldNotesRecipePath + ".");
            Assert.IsNotNull(registry, "GameDataRegistry asset missing at " + RegistryPath + ".");
            CollectionAssert.Contains(registry.Recipes, recipe, "Town field-notes recipe must be registered in GameDataRegistry.");
        }

        [Test]
        public void RESOURCE_RECIPE_EDIT_006_TownSquareHelpActivityHasResourceRecipeOutcomeAsset()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(TownFieldNotesRecipePath);
            var activity = AssetDatabase.LoadAssetAtPath<LocationActivityDefinition>(SquareHelpActivityPath);

            Assert.IsNotNull(recipe, "Town resource/recipe recipe asset missing.");
            Assert.IsNotNull(activity, "Town Square help activity asset missing.");
            var outcome = activity.Outcomes.OfType<LocationActivityResourceRecipeOutcome>().FirstOrDefault();
            Assert.IsNotNull(outcome, "Town Square help activity must include a resource/recipe outcome asset.");
            Assert.AreSame(recipe, outcome.Recipe, "Town Square help resource/recipe outcome must reveal the registered Town Field Notes recipe.");
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, Bind).SetValue(target, value);
        }

        private static RecipeDefinition MakeRecipe(string id)
        {
            var recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            SetField(recipe, "_id", id);
            return recipe;
        }

        private static ResourceNodeDefinition MakeResource(string id)
        {
            var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            SetField(resource, "_id", id);
            return resource;
        }

        private static LocationActivityDefinition MakeActivity(string id, LocationActivityOutcomeBase outcome)
        {
            var activity = ScriptableObject.CreateInstance<LocationActivityDefinition>();
            activity.ConfigureForTests(id, id, null, LocationGrowthRoute.Exploration, 0, 0, 0, 0, null, new[] { outcome });
            return activity;
        }
    }
}
