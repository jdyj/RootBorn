using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Crafting;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Crafting
{
    public sealed class CraftingRecipeTests
    {
        private const BindingFlags Bind = BindingFlags.Instance | BindingFlags.NonPublic;

        private static T SetField<T>(T target, string name, object value)
        {
            typeof(T).GetField(name, Bind).SetValue(target, value);
            return target;
        }

        private static RecipeDefinition MakeRecipe(params CraftStepBase[] steps)
        {
            var recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            SetField(recipe, "_steps", steps);
            return recipe;
        }

        private static GatherResourceStep MakeGatherStep(string resourceId, int count)
        {
            var step = ScriptableObject.CreateInstance<GatherResourceStep>();
            SetField(step, "_resourceId", resourceId);
            SetField(step, "_requiredCount", count);
            return step;
        }

        private static WaitOnSurfaceStep MakeWaitStep(string surface, float seconds)
        {
            var step = ScriptableObject.CreateInstance<WaitOnSurfaceStep>();
            SetField(step, "_requiredSurface", surface);
            SetField(step, "_requiredSeconds", seconds);
            return step;
        }

        [Test]
        public void CRAFT_001_RecipeWithNoSteps_IsNotSatisfied()
        {
            var recipe = MakeRecipe();
            var state = new CraftAttemptState(new Dictionary<string, int>(), "ground", 10f);

            Assert.IsFalse(recipe.AllStepsSatisfied(in state));
        }

        [Test]
        public void CRAFT_002_RecipeWithNullStep_IsNotSatisfied()
        {
            var recipe = MakeRecipe(MakeGatherStep("Wood", 1), null);
            var state = new CraftAttemptState(new Dictionary<string, int> { ["Wood"] = 1 }, "ground", 10f);

            Assert.IsFalse(recipe.AllStepsSatisfied(in state));
        }

        [Test]
        public void CRAFT_003_AllStepsSatisfied_RequiresGatherCountSurfaceAndElapsedTime()
        {
            var recipe = MakeRecipe(
                MakeGatherStep("Wood", 3),
                MakeWaitStep("ground", 5f));
            var state = new CraftAttemptState(new Dictionary<string, int> { ["Wood"] = 3 }, "ground", 5f);

            Assert.IsTrue(recipe.AllStepsSatisfied(in state));
        }

        [Test]
        public void CRAFT_004_GatherStepFailsWhenInventoryMissingOrCountTooLow()
        {
            var step = MakeGatherStep("Stone", 2);
            var noInventory = new CraftAttemptState(null, "ground", 0f);
            var tooLow = new CraftAttemptState(new Dictionary<string, int> { ["Stone"] = 1 }, "ground", 0f);
            var enough = new CraftAttemptState(new Dictionary<string, int> { ["Stone"] = 2 }, "ground", 0f);

            Assert.IsFalse(step.IsSatisfied(in noInventory));
            Assert.IsFalse(step.IsSatisfied(in tooLow));
            Assert.IsTrue(step.IsSatisfied(in enough));
        }

        [Test]
        public void CRAFT_005_WaitStepRequiresMatchingSurfaceAndEnoughElapsedSeconds()
        {
            var step = MakeWaitStep("forest", 4f);
            var wrongSurface = new CraftAttemptState(new Dictionary<string, int>(), "ground", 4f);
            var tooEarly = new CraftAttemptState(new Dictionary<string, int>(), "forest", 3.99f);
            var satisfied = new CraftAttemptState(new Dictionary<string, int>(), "forest", 4f);

            Assert.IsFalse(step.IsSatisfied(in wrongSurface));
            Assert.IsFalse(step.IsSatisfied(in tooEarly));
            Assert.IsTrue(step.IsSatisfied(in satisfied));
        }
    }
}
