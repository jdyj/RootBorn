using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Effects;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.Resources;
using Rootborn.Game.Story;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Quests;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.Quests
{
    public sealed class QuestDialogueScenarioTests
    {
        [UnityTest]
        public IEnumerator QUEST_001_NpcInteraction_OpensAndClosesDialogue()
        {
            var npc = new GameObject("NPC");
            var interactor = npc.AddComponent<NpcInteractor>();
            var dialogue = ScriptableObject.CreateInstance<DialogueDefinition>();
            var npcDef = ScriptableObject.CreateInstance<NpcDefinition>();
            SetField(npcDef, "_defaultDialogue", dialogue);
            interactor.Bind(npcDef);

            interactor.Interact();
            yield return null;

            Assert.IsTrue(interactor.Session.IsOpen);
            Assert.AreSame(dialogue, interactor.Session.Current);

            interactor.Close();
            yield return null;

            Assert.IsFalse(interactor.Session.IsOpen);
            Object.Destroy(npc);
        }

        [UnityTest]
        public IEnumerator QUEST_PM_UI_001_TownNpcChoiceButtonClickAcceptsQuest()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForTownQuestLoopRuntime();

            var player = GameObject.Find("Player");
            var gather = player.GetComponent<GatherInteractor>();
            var router = player.GetComponent<PlayerInteractionRouter>();
            var npc = GameObject.Find("GuideNpc").GetComponent<NpcInteractor>();
            var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
            var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            var firstQuest = npc.Npc.DefaultDialogue.Choices[0].Quest;

            yield return OpenGuideDialogue(player, npc, gather, router, dialoguePanel);

            var acceptButton = FindChoiceButton(dialoguePanel, 0);
            Assert.IsNotNull(acceptButton, "DialoguePanel should expose actual clickable choice buttons in PlayMode.");
            yield return ClickButton(acceptButton);

            Assert.AreEqual(QuestState.Active, questLogPanel.QuestLog.GetState(firstQuest));
        }

        [UnityTest]
        public IEnumerator QUEST_STUDENT_PM_001_TownNpcPromptEQuestClaimRaisesAllCareerTraits()
        {
            yield return SceneManager.LoadSceneAsync("Town", LoadSceneMode.Single);
            yield return WaitForTownQuestLoopRuntime();

            var player = GameObject.Find("Player");
            var progress = player.GetComponent<StudentLifeProgressComponent>();
            var gather = player.GetComponent<GatherInteractor>();
            var router = player.GetComponent<PlayerInteractionRouter>();
            var npc = GameObject.Find("GuideNpc").GetComponent<NpcInteractor>();
            var provider = npc.GetComponent<QuestProvider>();
            var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
            var questLogPanel = Object.FindFirstObjectByType<QuestLogPanel>(FindObjectsInactive.Include);
            var routes = FindTraitQuests(provider.Quests);
            Assert.AreEqual(4, routes.Count);

            for (int i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                Assert.IsTrue(FindChoiceIndexes(npc.Npc.DefaultDialogue, route.Quest, out int acceptIndex, out int claimIndex), route.Quest.Id);
                int beforeTrait = progress.Progress.GetTraitValue(route.Effect.Trait);

                yield return OpenGuideDialogue(player, npc, gather, router, dialoguePanel);
                yield return ClickButton(FindChoiceButton(dialoguePanel, acceptIndex));
                Assert.AreEqual(QuestState.Active, questLogPanel.QuestLog.GetState(route.Quest), route.Quest.Id);

                yield return OpenGuideDialogue(player, npc, gather, router, dialoguePanel);
                Assert.AreEqual(QuestState.Completed, questLogPanel.QuestLog.GetState(route.Quest), route.Quest.Id);
                yield return ClickButton(FindChoiceButton(dialoguePanel, claimIndex));

                Assert.Greater(progress.Progress.GetTraitValue(route.Effect.Trait), beforeTrait, route.Quest.Id);
                Assert.IsTrue(progress.Progress.IsCareerHintUnlocked(route.Effect.CareerHint), route.Quest.Id);
            }
        }

        [UnityTest]
        public IEnumerator QUEST_015_NpcQuest_FullFlow_ClaimReward_SetsStoryFlag()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            SetField(item, "_maxStack", 99);
            var resource = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            var objective = ScriptableObject.CreateInstance<GatherQuestObjective>();
            SetField(objective, "_targetResource", resource);
            SetField(objective, "_requiredCount", 1);
            var reward = ScriptableObject.CreateInstance<ItemQuestReward>();
            SetField(reward, "_item", item);
            SetField(reward, "_count", 1);
            var flag = ScriptableObject.CreateInstance<StoryFlagDefinition>();
            var effect = ScriptableObject.CreateInstance<SetStoryFlagCompletionEffect>();
            SetField(effect, "_flag", flag);
            var quest = ScriptableObject.CreateInstance<QuestDefinition>();
            SetField(quest, "_objectives", new QuestObjectiveBase[] { objective });
            SetField(quest, "_rewards", new QuestRewardBase[] { reward });
            SetField(quest, "_completionEffects", new QuestCompletionEffectBase[] { effect });

            var log = new QuestLog(new[] { quest });
            var inventory = new Inventory();
            var flags = new StoryFlagSet();
            var rewardContext = new RewardRuntimeContext(log, inventory, null, flags);

            Assert.IsTrue(log.Accept(quest));
            log.RecordEvent(new QuestEvent(QuestEventKind.Gather, "g1", resource: resource));
            Assert.AreEqual(QuestState.Completed, log.GetState(quest));
            Assert.IsTrue(log.ClaimReward(quest, in rewardContext));

            yield return null;

            Assert.AreEqual(1, inventory.CountOf(item));
            Assert.IsTrue(flags.IsSet(flag));
            Assert.AreEqual(QuestState.RewardClaimed, log.GetState(quest));
            Assert.IsFalse(log.ClaimReward(quest, in rewardContext));
            Assert.AreEqual(1, inventory.CountOf(item));
        }

        private static IEnumerator OpenGuideDialogue(GameObject player, NpcInteractor npc, GatherInteractor gather, PlayerInteractionRouter router, DialoguePanel dialoguePanel)
        {
            player.transform.position = npc.transform.position + new Vector3(0.75f, 0f, 0f);
            Physics2D.SyncTransforms();
            yield return null;

            float elapsed = 0f;
            while (elapsed < 3f)
            {
                router.RefreshPromptNow();
                if (router.PromptVisible && router.PromptText.Contains("Talk"))
                {
                    break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(router.PromptVisible, "Guide interaction prompt should be visible before dialogue interaction.");
            StringAssert.Contains("Talk", router.PromptText);

            gather.TriggerInteract();
            elapsed = 0f;
            while (!dialoguePanel.IsOpen && elapsed < 3f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(dialoguePanel.IsOpen, "Guide dialogue should open through the player interaction path.");
        }

        private static List<RouteQuest> FindTraitQuests(QuestDefinition[] quests)
        {
            var routes = new List<RouteQuest>();
            for (int i = 0; i < quests.Length; i++)
            {
                var candidate = quests[i];
                if (candidate == null || candidate.CompletionEffects == null)
                {
                    continue;
                }

                for (int j = 0; j < candidate.CompletionEffects.Length; j++)
                {
                    if (candidate.CompletionEffects[j] is TraitDeltaCompletionEffect traitEffect && traitEffect.CareerHint != null)
                    {
                        routes.Add(new RouteQuest(candidate, traitEffect));
                        break;
                    }
                }
            }

            return routes;
        }

        private readonly struct RouteQuest
        {
            public readonly QuestDefinition Quest;
            public readonly TraitDeltaCompletionEffect Effect;

            public RouteQuest(QuestDefinition quest, TraitDeltaCompletionEffect effect)
            {
                Quest = quest;
                Effect = effect;
            }
        }

        private static bool FindChoiceIndexes(DialogueDefinition dialogue, QuestDefinition quest, out int acceptIndex, out int claimIndex)
        {
            acceptIndex = -1;
            claimIndex = -1;
            var choices = dialogue != null ? dialogue.Choices : null;
            if (choices == null)
            {
                return false;
            }

            for (int i = 0; i < choices.Length; i++)
            {
                var choice = choices[i];
                if (choice == null || choice.Quest != quest)
                {
                    continue;
                }

                if (choice.QuestAction == DialogueQuestAction.AcceptQuest)
                {
                    acceptIndex = i;
                }
                else if (choice.QuestAction == DialogueQuestAction.ClaimReward)
                {
                    claimIndex = i;
                }
            }

            return acceptIndex >= 0 && claimIndex >= 0;
        }

        private static Button FindChoiceButton(DialoguePanel panel, int index)
        {
            var root = panel.transform.Find("ChoiceButtons");
            var child = root != null ? root.Find("ChoiceButton_" + index) : null;
            return child != null ? child.GetComponent<Button>() : null;
        }

        private static IEnumerator ClickButton(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy, button.name + " must be active for user click.");
            var eventData = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static IEnumerator WaitForTownQuestLoopRuntime()
        {
            for (int i = 0; i < 600; i++)
            {
                var player = GameObject.Find("Player");
                var npc = GameObject.Find("GuideNpc");
                var dialoguePanel = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
                if (player != null &&
                    player.GetComponent<GatherInteractor>() != null &&
                    player.GetComponent<PlayerInteractionRouter>() != null &&
                    player.GetComponent<StudentLifeProgressComponent>() != null &&
                    npc != null &&
                    npc.GetComponent<NpcInteractor>() != null &&
                    npc.GetComponent<QuestProvider>() != null &&
                    npc.GetComponent<QuestProvider>().Quests.Length >= 4 &&
                    dialoguePanel != null &&
                    EventSystem.current != null)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Town quest loop runtime did not expose Player, GuideNpc, QuestProvider, and DialoguePanel within timeout.");
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }

                type = type.BaseType;
            }

            Assert.Fail(fieldName);
        }
    }
}
