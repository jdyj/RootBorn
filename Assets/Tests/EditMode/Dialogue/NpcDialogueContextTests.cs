using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Quests.Objectives;
using Rootborn.Game.Quests.Rewards;
using Rootborn.Game.Save;
using Rootborn.Game.Story;
using Rootborn.Game.StudentLife;
using Rootborn.Game.World;
using Rootborn.Network.Session;
using Rootborn.UI.Careers;
using Rootborn.UI.Modern;
using Rootborn.UI.Quests;
using Rootborn.UI.StudentLife;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Dialogue
{
    // The real installer callbacks are wired directly, avoiding Start/Managers boot and scene scans.
    // NpcInteractor.TryInteract still controls actual instance-then-global event dispatch.
    public sealed class NpcDialogueContextTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _objects = new List<Object>();
        private readonly List<MonoBehaviour> _binders = new List<MonoBehaviour>();
        private Scene _scene;
        private string _root;
        private AppConfig _oldConfig;
        private SaveSlotMetadata _oldMetadata;
        private INetworkSession _oldNetwork;
        private GameDataRegistry _registry, _oldRegistry;
        private FieldInfo _globalNpcEvent, _questBridgeEvent;
        private object _previousGlobalNpcEvent, _previousQuestBridgeEvent;
        private AdultCareerRuntimeComponent _owner;
        private NpcInteractor _npc;
        private NpcDefinition _definition;
        private DialogueDefinition _defaultDialogue;
        private DialoguePanel _panel;
        private QuestDefinition _quest;
        private QuestLog _log;
        private StoryFlagSet _flags;

        [SetUp]
        public void Setup()
        {
            _objects.Clear(); _binders.Clear();
            _root = Path.Combine(Path.GetTempPath(), "rb-dialogue-" + Guid.NewGuid().ToString("N"));
            SaveService.SetRootDirectoryForTests(_root);
            _oldConfig = GameBootstrap.Config; _oldMetadata = ActiveSaveContext.Metadata;
            _oldNetwork = NetworkBootstrap.ActiveSession; GameDataRegistryProvider.TryGet(out _oldRegistry);
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, new AppConfig { Mode = SessionMode.Single });
            typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, null);
            string slot = "dlg-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = slot });
            _globalNpcEvent = typeof(NpcInteractor).GetField("OnAnyInteracted", BindingFlags.Static | BindingFlags.NonPublic);
            _questBridgeEvent = typeof(QuestGameplayEventBridge).GetField("OnRecorded", BindingFlags.Static | BindingFlags.NonPublic);
            _previousGlobalNpcEvent = _globalNpcEvent.GetValue(null); _globalNpcEvent.SetValue(null, null);
            _previousQuestBridgeEvent = _questBridgeEvent.GetValue(null); _questBridgeEvent.SetValue(null, null);

            _scene = EditorSceneManager.NewPreviewScene(); _scene.name = "Town";
            Go("AuthoredWorld").AddComponent<AuthoredWorldScene>();
            var calendar = Make<LifeCalendarDefinition>(); calendar.ConfigureForTests(3, 3);
            _defaultDialogue = MakeDialogue();
            _definition = Make<NpcDefinition>();
            _definition.ConfigureForTests("npc.context", "npc.context.name", "npc.context.intro", null,
                Array.Empty<NpcRoleDefinition>(), _defaultDialogue, Array.Empty<NpcDialogueConditionBase>());
            var objective = Make<TalkQuestObjective>(); objective.ConfigureForRuntime("objective.talk", 1); Set(objective, "_targetNpc", _definition);
            var coin = Make<ItemDefinition>(); Set(coin, "_id", "item.context"); Set(coin, "_maxStack", 999);
            var reward = Make<ItemQuestReward>(); Set(reward, "_item", coin); Set(reward, "_count", 2);
            _quest = Make<QuestDefinition>(); _quest.ConfigureForRuntime("quest.context", "Quest", "Talk",
                new QuestObjectiveBase[] { objective }, new QuestRewardBase[] { reward }, null);
            _registry = Make<GameDataRegistry>(); Set(_registry, "_studentLifeCalendar", calendar);
            Set(_registry, "_items", new[] { coin }); Set(_registry, "_quests", new[] { _quest }); Set(_registry, "_npcs", new[] { _definition });
            GameDataRegistryProvider.SetOverrideForTests(_registry);
            var actor = Go("CanonicalActor"); actor.AddComponent<PlayerIdentity>().Configure("actor");
            var student = actor.AddComponent<StudentLifeProgressComponent>(); student.ConfigureForTests(slot, "actor", 12, 10, 0, 480); student.Progress.ConfigureCalendar(calendar);
            actor.AddComponent<PlayerInventory>(); _owner = AdultCareerRuntimeComponent.Ensure(actor, _registry);
            Assert.That(_owner != null && _owner.CanMutate, Is.True, "Fixture requires the real canonical offline owner.");
            _log = PlayerGlobalState.GetQuestLog(slot, "actor", _registry.Quests); _flags = new StoryFlagSet();
            _npc = Go("Npc").AddComponent<NpcInteractor>(); _npc.Bind(_definition); _npc.BindQuestEvents(_log);

            var panelObject = Go("SharedDialoguePanel", typeof(RectTransform)); panelObject.SetActive(false);
            _panel = panelObject.AddComponent<DialoguePanel>();
            var background = Go("Background", typeof(RectTransform)); background.SetActive(false); background.transform.SetParent(panelObject.transform, false);
            var tiles = background.AddComponent<ModernUiTileImage>(); Set(tiles, "_rebuildOnEnable", false);
            Set(_panel, "_backgroundTiles", tiles); background.SetActive(true);
        }

        [TearDown]
        public void Cleanup()
        {
            // Ordinary EditMode components do not reliably receive the normal Play lifecycle.
            foreach (var binder in _binders) if (binder != null) binder.GetType().GetMethod("OnDestroy", PrivateInstance)?.Invoke(binder, null);
            if (_owner != null) typeof(AdultCareerRuntimeComponent).GetMethod("OnDestroy", PrivateInstance).Invoke(_owner, null);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); _binders.Clear();
            if (_scene.IsValid() && _scene.isLoaded) EditorSceneManager.ClosePreviewScene(_scene);
            _globalNpcEvent?.SetValue(null, _previousGlobalNpcEvent); _questBridgeEvent?.SetValue(null, _previousQuestBridgeEvent);
            SaveService.SetRootDirectoryForTests(null); ActiveSaveContext.Set(_oldMetadata);
            GameDataRegistryProvider.SetOverrideForTests(_oldRegistry);
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, _oldConfig); typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, _oldNetwork);
            if (!string.IsNullOrEmpty(_root) && Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void NPC_CONTEXT_001_TryInteractActuallyDispatchesInstanceThenGlobalCallbacks()
        {
            var order = new List<string>();
            _npc.OnInteracted += _ => order.Add("instance");
            NpcInteractor.OnAnyInteracted += (_, progress) => { Assert.That(progress, Is.SameAs(_owner.StudentComponent.Progress)); order.Add("global"); };
            Assert.That(_npc.TryInteract(_owner.gameObject), Is.True);
            CollectionAssert.AreEqual(new[] { "instance", "global" }, order);
        }

        [Test]
        public void NPC_CONTEXT_002_TownCallbacksKeepRichOwnerContextAfterGlobalDispatch()
        {
            WireLegacyQuestBinder();
            DialogueChoiceContext beforeGlobal = default;
            _npc.OnInteracted += _ => beforeGlobal = Context;
            WireLocationRunner();
            Assert.That(_npc.TryInteract(_owner.gameObject), Is.True);
            Assert.That(beforeGlobal.QuestLog, Is.SameAs(_log), "The first production callback supplies the rich context.");
            AssertOwnerContext(Context);
            AssertSinglePanel();
        }

        [Test]
        public void NPC_CONTEXT_003_AcceptChoiceRemainsAvailableAfterBothProductionCallbacks()
        {
            WireLegacyQuestBinder(); WireLocationRunner();
            Assert.That(_npc.TryInteract(_owner.gameObject), Is.True);
            var context = Context;
            Assert.That(Choice(DialogueQuestAction.AcceptQuest).IsAvailable(in context), Is.True,
                "A shared panel must still have the quest log after all callbacks complete.");
            Assert.That(_log.GetState(_quest), Is.EqualTo(QuestState.NotStarted));
        }

        [Test]
        public void NPC_CONTEXT_004_CompletedRewardKeepsCanonicalInventoryForPreflight()
        {
            Assert.That(_log.Accept(_quest), Is.True); WireLegacyQuestBinder(); WireLocationRunner();
            Assert.That(_npc.TryInteract(_owner.gameObject), Is.True);
            Assert.That(_log.GetState(_quest), Is.EqualTo(QuestState.Completed), "The actual NPC talk event completes the real objective.");
            var context = Context;
            Assert.That(Choice(DialogueQuestAction.ClaimReward).IsAvailable(in context), Is.True);
            Assert.That(context.RewardContext.Inventory, Is.SameAs(_owner.PlayerInventory.Inventory));
            // This slice checks presentation/preflight, not legacy claim persistence atomicity.
        }

        [Test]
        public void NPC_CONTEXT_005_ScheduleSelectedDialogueAndRichContextSurviveTogether()
        {
            var scheduled = MakeDialogue(); _npc.BindSchedule(null, "morning", scheduled, null, null);
            WireLegacyQuestBinder(); WireLocationRunner();
            Assert.That(_npc.TryInteract(_owner.gameObject), Is.True);
            Assert.That(_npc.Session.Current, Is.SameAs(scheduled));
            Assert.That(Get(_panel, "_dialogue"), Is.SameAs(scheduled), "Do not replace schedule selection with the NPC default.");
            AssertOwnerContext(Context); AssertSinglePanel();
        }

        [Test]
        public void NPC_CONTEXT_006_AuthoredSchoolGetsOwnerQuestContextWithoutTownQuestHudBinder()
        {
            _scene.name = "School"; WireLocationRunner();
            Assert.That(_npc.TryInteract(_owner.gameObject), Is.True);
            AssertOwnerContext(Context); AssertSinglePanel();
        }

        [Test]
        public void NPC_CONTEXT_007_UnboundForeignActorCannotOpenTheCanonicalOwnersConversation()
        {
            WireLegacyQuestBinder(); WireLocationRunner();
            var foreign = Go("ForeignActor"); foreign.AddComponent<PlayerIdentity>().Configure("foreign");
            _npc.TryInteract(foreign);
            Assert.That(_panel.IsOpen, Is.False, "An unrelated actor cannot borrow the bound owner's quest/reward context.");
            Assert.That(_log.GetState(_quest), Is.EqualTo(QuestState.NotStarted));
        }

        private DialogueChoiceContext Context => (DialogueChoiceContext)Get(_panel, "_context");
        private void AssertOwnerContext(DialogueChoiceContext context)
        {
            Assert.That(context.QuestLog, Is.SameAs(_log));
            Assert.That(context.RewardContext.QuestLog, Is.SameAs(_log));
            Assert.That(context.RewardContext.Inventory, Is.SameAs(_owner.PlayerInventory.Inventory));
            Assert.That(context.RewardContext.StudentLifeProgress, Is.SameAs(_owner.StudentComponent.Progress));
            Assert.That(context.RewardContext.WorldStateProgress, Is.SameAs(_owner.World));
        }
        private void AssertSinglePanel()
        {
            int count = 0; foreach (var root in _scene.GetRootGameObjects()) count += root.GetComponentsInChildren<DialoguePanel>(true).Length;
            Assert.That(count, Is.EqualTo(1), "Reuse the existing scene panel.");
        }
        private void WireLegacyQuestBinder()
        {
            var type = typeof(QuestHudAutoFiller).GetNestedType("QuestDialogueUiBinder", BindingFlags.NonPublic);
            var binder = (MonoBehaviour)Go("QuestDialogueBinder").AddComponent(type); _binders.Add(binder);
            type.GetMethod("Bind", new[] { typeof(DialoguePanel), typeof(QuestLog), typeof(PlayerInventory), typeof(StudentLifeProgressComponent), typeof(StoryFlagSet), typeof(string), typeof(Rootborn.Game.WorldState.WorldStateProgress) })
                .Invoke(binder, new object[] { _panel, _log, _owner.PlayerInventory, _owner.StudentComponent, _flags, "actor", _owner.World });
            var callback = (Action<NpcInteractor>)Delegate.CreateDelegate(typeof(Action<NpcInteractor>), binder, type.GetMethod("HandleNpcInteracted", PrivateInstance));
            _npc.OnInteracted += callback; Set(binder, "_attached", new[] { _npc });
        }
        private void WireLocationRunner()
        {
            var type = typeof(LocationNpcRuntimeInstaller).GetNestedType("Runner", BindingFlags.NonPublic);
            var runner = (MonoBehaviour)Go("[LocationNpcRuntimeInstaller]").AddComponent(type); _binders.Add(runner);
            Set(runner, "_scene", _scene); Set(runner, "_registry", _registry); Set(runner, "_dialoguePanel", _panel);
            type.GetMethod("BindStudent").Invoke(runner, new object[] { _owner.StudentComponent });
            var callback = (Action<NpcInteractor, StudentLifeProgress>)Delegate.CreateDelegate(typeof(Action<NpcInteractor, StudentLifeProgress>), runner, type.GetMethod("HandleNpcInteracted", PrivateInstance));
            NpcInteractor.OnAnyInteracted += callback;
        }
        private DialogueChoiceDefinition Choice(DialogueQuestAction action)
        {
            var choice = Make<DialogueChoiceDefinition>(); Set(choice, "_questAction", action); Set(choice, "_quest", _quest); return choice;
        }
        private DialogueDefinition MakeDialogue()
        {
            var dialogue = Make<DialogueDefinition>();
            dialogue.ConfigureForTests(Array.Empty<string>(), Array.Empty<DialogueChoiceDefinition>(), Array.Empty<DialogueCondition>());
            return dialogue;
        }
        private GameObject Go(string name, params Type[] components)
        {
            var value = new GameObject(name, components); _objects.Add(value); SceneManager.MoveGameObjectToScene(value, _scene); return value;
        }
        private T Make<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static object Get(object target, string field) => target.GetType().GetField(field, PrivateInstance).GetValue(target);
        private static void Set(object target, string field, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                var member = type.GetField(field, PrivateInstance);
                if (member == null) continue; member.SetValue(target, value); return;
            }
            Assert.Fail("Missing fixture field: " + field);
        }
    }
}
