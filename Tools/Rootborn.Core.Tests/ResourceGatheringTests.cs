// 옮겨 옴: Assets/Tests/EditMode/GatherInteractorTests.cs(ResourceNode_AccumulatesPower_AndBreaks, IsWalkable),
//          Assets/Tests/EditMode/ToolResourceMatchingTests.cs(TOOL_MATCH_001~004),
//          Assets/Tests/EditMode/Crafting/ResourceRecipeActivitySliceTests.cs(RESOURCE_RECIPE_EDIT_002·003)
// 등록부·에셋 와이어링 검사(WIRING_*, RegistryWiring_*, EDIT_001·004~006)와 지식 기록 테스트는 옮기지 않았다(지식은 게임이 쓰지 않음).
using System;
using NUnit.Framework;
using Rootborn.Core.Gathering;
using Rootborn.Core.Inventory;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;

namespace Rootborn.Core.Tests
{
    public sealed class ResourceGatheringTests
    {
        private static readonly ItemDefinition Wood = new ItemDefinition("Wood", 99);

        private static ResourceNodeDefinition Node(float hits = 3f, float bareHand = 1f, string preferred = null, params ResourceDrop[] drops)
            => new ResourceNodeDefinition("Tree", "resource.tree", hits, preferred, bareHand, drops, "ground", true, 1f, 1f);

        private static ItemDefinition Find(string id) => id == "Wood" ? Wood : null;

        // --- 도구와 힘 (ToolResourceMatchingTests) ---

        [Test]
        public void TOOL_MATCH_001_PreferredTool_AppliesItsFullPower()
            => Assert.That(Node(preferred: "StoneAxe").ComputeEffectivePower("StoneAxe", 1.2f), Is.EqualTo(1.2f).Within(0.0001f));

        [Test]
        public void TOOL_MATCH_002_AnotherTool_AppliesHalfItsPower()
            => Assert.That(Node(preferred: "StonePickaxe").ComputeEffectivePower("StoneAxe", 1.2f), Is.EqualTo(0.6f).Within(0.0001f));

        [Test]
        public void TOOL_MATCH_003_BareHand_AppliesTheBareHandPenalty()
            => Assert.That(Node(bareHand: 0.3f, preferred: "StonePickaxe").ComputeEffectivePower(null, 0f), Is.EqualTo(0.3f).Within(0.0001f));

        [Test]
        public void TOOL_MATCH_004_NoPreferredTool_AnyToolHasFullPower()
            => Assert.That(Node().ComputeEffectivePower("StoneAxe", 1.2f), Is.EqualTo(1.2f).Within(0.0001f));

        // --- 치기와 부수기 ---

        [Test]
        public void Node_AccumulatesPower_AndBreaksOnce()
        {
            var node = Node(3f, 1f);
            var state = new ResourceNodeState();
            var inventory = new ItemInventory();
            Assert.That(ResourceGathering.Hit(node, state, null, 0f, inventory, Find, new Random(1)), Is.EqualTo(GatherHitResult.Hit));
            Assert.That(ResourceGathering.Hit(node, state, null, 0f, inventory, Find, new Random(1)), Is.EqualTo(GatherHitResult.Hit));
            Assert.That(state.IsBroken, Is.False);
            Assert.That(ResourceGathering.Hit(node, state, null, 0f, inventory, Find, new Random(1)), Is.EqualTo(GatherHitResult.Broken));
            Assert.That(state.IsBroken, Is.True);
            Assert.That(ResourceGathering.Hit(node, state, null, 0f, inventory, Find, new Random(1)), Is.EqualTo(GatherHitResult.Ignored));
        }

        [Test]
        public void BreakingGivesTheRolledDrops()
        {
            var node = Node(1f, 1f, null, new ResourceDrop("Wood", 2, 2));
            var inventory = new ItemInventory();
            Assert.That(ResourceGathering.Hit(node, new ResourceNodeState(), null, 0f, inventory, Find, new Random(1)), Is.EqualTo(GatherHitResult.Broken));
            Assert.That(inventory.CountOf(Wood), Is.EqualTo(2));
        }

        [Test]
        public void UnknownDropItemsAndZeroCountsAreSkipped()
        {
            var node = Node(1f, 1f, null, new ResourceDrop("Ghost", 1, 1), new ResourceDrop("Wood", 0, 0), new ResourceDrop("Wood", 1, 1));
            var inventory = new ItemInventory();
            Assert.That(ResourceGathering.Hit(node, new ResourceNodeState(), null, 0f, inventory, Find, new Random(1)), Is.EqualTo(GatherHitResult.Broken));
            Assert.That(inventory.CountOf(Wood), Is.EqualTo(1));
        }

        [Test]
        public void ChangedBehavior_DropsThatDoNotFit_LeaveTheNodeStandingAndGiveNothing()
        {
            var filler = new ItemDefinition("filler", 1);
            var inventory = new ItemInventory();
            inventory.Add(filler, ItemInventory.MaxSlots);
            var node = Node(1f, 1f, null, new ResourceDrop("Wood", 1, 1));
            var state = new ResourceNodeState();

            Assert.That(ResourceGathering.Hit(node, state, null, 0f, inventory, Find, new Random(1)), Is.EqualTo(GatherHitResult.InventoryFull));
            Assert.That(state.IsBroken, Is.False);
            Assert.That(inventory.CountOf(Wood), Is.Zero);

            inventory.Remove(filler, 1);
            Assert.That(ResourceGathering.Hit(node, state, null, 0f, inventory, Find, new Random(1)), Is.EqualTo(GatherHitResult.Broken));
            Assert.That(inventory.CountOf(Wood), Is.EqualTo(1));
        }

        [Test]
        public void WithoutAnInventory_TheNodeBreaksWithoutDrops()
        {
            var state = new ResourceNodeState();
            Assert.That(ResourceGathering.Hit(Node(1f, 1f, null, new ResourceDrop("Wood", 1, 1)), state, null, 0f, null, Find, null), Is.EqualTo(GatherHitResult.Broken));
            Assert.That(state.IsBroken, Is.True);
        }

        [Test]
        public void NullNodeOrState_IsIgnored()
        {
            Assert.That(ResourceGathering.Hit(null, new ResourceNodeState(), null, 0f, null, Find, null), Is.EqualTo(GatherHitResult.Ignored));
            Assert.That(ResourceGathering.Hit(Node(), null, null, 0f, null, Find, null), Is.EqualTo(GatherHitResult.Ignored));
        }

        [Test]
        public void Drop_RollStaysInItsRange_AndWithoutARandomGivesTheMaximum()
        {
            var drop = new ResourceDrop("Wood", 1, 3);
            var rng = new Random(7);
            for (int i = 0; i < 50; i++) Assert.That(drop.Roll(rng), Is.InRange(1, 3));
            Assert.That(drop.Roll(null), Is.EqualTo(3));
            Assert.That(new ResourceDrop("Wood", -2, -5).Roll(null), Is.EqualTo(0), "음수 최소는 0, 최대는 최소 이상");
        }

        [Test]
        public void Walkability_IsData()
        {
            Assert.That(Node().IsWalkable, Is.True);
            Assert.That(new ResourceNodeDefinition("Rock", null, 6f, null, 0.3f, null, null, false, 1f, 1f).IsWalkable, Is.False);
        }

        // --- 레시피 공개 (ResourceRecipeActivitySliceTests) ---

        [Test]
        public void RESOURCE_RECIPE_EDIT_002_ProgressIsKeptPerSaveSlotAndPlayer()
        {
            var store = new InMemorySaveStore();
            var progress = ResourceRecipeSaveFiles.LoadOrCreate(store, "slot-a", "player-a");
            Assert.That(progress.TryRevealRecipe("recipe.town.field-notes", "activity.square.help"), Is.True);
            ResourceRecipeSaveFiles.Save(store, progress);

            Assert.That(ResourceRecipeSaveFiles.LoadOrCreate(store, "slot-a", "player-a").IsRecipeRevealed("recipe.town.field-notes"), Is.True);
            Assert.That(ResourceRecipeSaveFiles.LoadOrCreate(store, "slot-a", "player-b").IsRecipeRevealed("recipe.town.field-notes"), Is.False);
        }

        [Test]
        public void RESOURCE_RECIPE_EDIT_003_TheActivityOutcomeRevealsOnce()
        {
            var store = new InMemorySaveStore();
            var outcome = new LocationActivityResourceRecipeOutcome("resource.town.paper-scrap", "recipe.town.field-notes",
                "objective.recipe.field-notes.next", new ResourceRecipeReveal(store));
            var activity = new LocationActivity("activity.square.help", null, null, null, null, null, 0, 0, 0, 0, null, new ILocationActivityOutcome[] { outcome });
            var progress = new StudentLifeProgress("slot-b", "player-b", 12, 10, 0, 8 * 60);

            string first = outcome.Apply(progress, activity);
            string second = outcome.Apply(progress, activity);
            var restored = ResourceRecipeSaveFiles.LoadOrCreate(store, "slot-b", "player-b");

            Assert.That(first, Is.Not.Empty);
            Assert.That(second, Is.Empty);
            Assert.That(restored.IsRecipeRevealed("recipe.town.field-notes"), Is.True);
            Assert.That(restored.IsResourceSeen("resource.town.paper-scrap"), Is.True);
            Assert.That(restored.SourceActivityIds, Is.EqualTo(new[] { "activity.square.help" }));
            Assert.That(store.Writes, Is.EqualTo(1), "바뀐 것이 없으면 저장하지 않는다");
        }

        [Test]
        public void Reveal_WithoutARecipeChangesNothing_ButSeesTheResource()
        {
            var progress = new ResourceRecipeProgress("slot", "player");
            Assert.That(progress.TryReveal("Rock", "", "a"), Is.True);
            Assert.That(progress.IsResourceSeen("Rock"), Is.True);
            Assert.That(progress.SourceActivityIds, Is.Empty, "레시피가 없으면 출처 활동을 남기지 않는다");
            Assert.That(progress.TryReveal("Rock", null, "a"), Is.False);
        }

        [Test]
        public void SaveData_RoundTripsSortedAndDropsEmptyIds()
        {
            var progress = ResourceRecipeProgress.FromSaveData(new ResourceRecipeProgressSaveData
            {
                SaveSlot = "slot", PlayerId = "p",
                SeenResourceIds = new[] { "b", "a", "", null, "a" },
                RevealedRecipeIds = new[] { "r2", "r1" },
                SourceActivityIds = new[] { "x" },
            }, "fallback", "fallback");
            var data = progress.ToSaveData();
            Assert.That((data.SaveSlot, data.PlayerId), Is.EqualTo(("slot", "p")));
            Assert.That(data.SeenResourceIds, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(data.RevealedRecipeIds, Is.EqualTo(new[] { "r1", "r2" }));

            var empty = ResourceRecipeProgress.FromSaveData(null, "slot", null);
            Assert.That((empty.SaveSlot, empty.PlayerId), Is.EqualTo(("slot", "player")));
        }

        [Test]
        public void FileNames_MatchTheUnityGame()
        {
            Assert.That(SaveFileNames.ResourceRecipe(null), Is.EqualTo("resource-recipe-progress.json"));
            Assert.That(SaveFileNames.ResourceRecipe("player"), Is.EqualTo("resource-recipe-progress.json"));
            // Unity 판은 "local-player"를 기본으로 보지 않았다(다른 진행 파일과 다르다). 이름을 그대로 지킨다.
            Assert.That(SaveFileNames.ResourceRecipe("local-player"), Is.EqualTo("resource-recipe-progress-local-player.json"));
            Assert.That(SaveFileNames.ResourceRecipe("steam:7"), Is.EqualTo("resource-recipe-progress-steam_7.json"));
        }

        [Test]
        public void Recipe_DisplayNameFallsBackToTheId()
        {
            var recipe = new RecipeDefinition("recipe.town.field-notes", null, "objective.recipe.field-notes.next");
            Assert.That((recipe.DisplayNameKey, recipe.NextHintKey), Is.EqualTo(("recipe.town.field-notes", "objective.recipe.field-notes.next")));
        }
    }
}
