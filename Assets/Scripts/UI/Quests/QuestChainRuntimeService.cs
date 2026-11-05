using System;
using System.Collections.Generic;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Careers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.UI.Quests
{
    /// <summary>One bridge subscriber; the shared player owner commits every command before publication.</summary>
    [DisallowMultipleComponent]
    public sealed class QuestChainRuntimeService : MonoBehaviour
    {
        private readonly QuestChainProgressStore _store = new QuestChainProgressStore();
        private readonly Dictionary<string, AdultCareerRuntimeComponent> _owners = new Dictionary<string, AdultCareerRuntimeComponent>(StringComparer.Ordinal);
        private GameDataRegistry _registry;
        private string _activeSlot;
        public event Action<StudentLifeProgress> ProgressChanged;
        public QuestChainProgressStore Store => _store;

        public static QuestChainRuntimeService Ensure(GameDataRegistry registry)
        {
            var manager = Managers.EnsureExists();
            var service = manager.GetComponent<QuestChainRuntimeService>();
            if (service == null) service = manager.gameObject.AddComponent<QuestChainRuntimeService>();
            service._registry = registry; service.BindEvents(); service.CheckSession(); return service;
        }
        private void OnEnable() => BindEvents();
        private void BindEvents()
        {
            QuestGameplayEventBridge.OnRecorded -= HandleRecorded; QuestGameplayEventBridge.OnRecorded += HandleRecorded;
            SceneManager.sceneLoaded -= HandleSceneLoaded; SceneManager.sceneLoaded += HandleSceneLoaded;
        }
        private void OnDisable()
        {
            QuestGameplayEventBridge.OnRecorded -= HandleRecorded; SceneManager.sceneLoaded -= HandleSceneLoaded;
            ClearSession();
        }
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu") ClearSession();
            CheckSession();
        }
        private void CheckSession()
        {
            string slot = ActiveSaveContext.SlotId;
            if (string.Equals(slot, _activeSlot, StringComparison.Ordinal)) return;
            _activeSlot = slot; ClearSession();
        }
        private void ClearSession() { _store.Clear(); _owners.Clear(); }

        public QuestChainLog GetLog(StudentLifeProgress student)
        {
            var owner = ResolveOwner(student);
            if (owner == null) return null;
            _store.BindLog(owner.StudentComponent.Progress, owner.QuestChains);
            return owner.QuestChains;
        }
        public RewardRuntimeContext ResolveRewards(StudentLifeProgress student)
        {
            var owner = ResolveOwner(student);
            return owner != null ? new RewardRuntimeContext(
                PlayerGlobalState.GetQuestLog(student.SaveSlot, student.PlayerId, _registry.Quests),
                owner.PlayerInventory.Inventory, null, null, owner.StudentComponent.Progress, owner.World, owner.FurnitureUnlocks) : default;
        }

        public bool ExecuteUserCommand(StudentLifeProgress student, Func<QuestChainLog, RewardRuntimeContext, bool> command)
        {
            var owner = ResolveOwner(student);
            if (owner == null || command == null) return false;
            var result = owner.ExecuteSessionCommand((session, service) =>
            {
                var log = DetachedLog(session);
                var rewards = DetachedRewards(session);
                if (!command(log, rewards)) return new AdultCareerResult(AdultCareerResultKind.RequirementFailed);
                session.ReplaceQuestChains(log.ToSaveData());
                return new AdultCareerResult(AdultCareerResultKind.Applied);
            });
            if (result.Applied) NotifyCommitted(owner);
            return result.Applied;
        }

        private void HandleRecorded(QuestEvent questEvent, StudentLifeProgress student)
        {
            var owner = ResolveOwner(student);
            if (owner == null) return;
            var result = owner.ExecuteSessionCommand((session, service) => new AdultCareerResult(
                ApplyEventToSession(session, questEvent) ? AdultCareerResultKind.Applied : AdultCareerResultKind.InvalidRequest));
            if (result.Applied) NotifyCommitted(owner);
        }

        // Used by work settlement so income, step rewards and the event receipt share one save.
        // This mutates only the supplied detached session; the caller persists and publishes it.
        public bool ApplyEventToSession(AdultCareerSession session, QuestEvent questEvent) => ApplyRegisteredEventToSession(_registry, session, questEvent);

        // Detached settlement must not create Managers or scene subscribers.
        public static bool ApplyRegisteredEventToSession(GameDataRegistry registry, AdultCareerSession session, QuestEvent questEvent)
        {
            if (registry == null || session?.Student == null || session.Inventory == null || session.World == null
                || session.FurnitureUnlocks == null || string.IsNullOrEmpty(questEvent.EventKey) || questEvent.Count <= 0) return false;
            var log = DetachedLog(registry, session);
            var rewards = DetachedRewards(registry, session);
            var context = new QuestRuntimeContext(rewards.QuestLog, null, rewards.KnowledgeProgress, rewards.StoryFlags, session.Student);
            if (questEvent.Kind == QuestEventKind.Talk && questEvent.Npc != null)
            {
                foreach (var chain in registry.QuestChains)
                    if (chain != null && chain.StartNpc == questEvent.Npc && log.GetState(chain) == QuestChainState.Available
                        && log.AcceptWithContext(chain, context))
                    {
                        log.Track(chain);
                        CreditPlanChosenBeforeStart(registry, log, session.Student, context, rewards);
                    }
            }
            log.RecordEventWithContext(questEvent, context, rewards);
            foreach (var chain in registry.QuestChains)
                if (chain != null) log.EvaluateStepProgress(chain, context, rewards);
            session.ReplaceQuestChains(log.ToSaveData());
            return true;
        }
        // A plan picked before talking to the chain's starter would otherwise need picking again to count.
        public static bool CreditPlanChosenBeforeStart(GameDataRegistry registry, QuestChainLog log, StudentLifeProgress student, in QuestRuntimeContext context, RewardRuntimeContext rewards)
        {
            string routeId = student != null ? student.DailyPlan.SelectedRouteId : string.Empty;
            if (string.IsNullOrEmpty(routeId) || registry == null || log == null) return false;
            var route = new DailyPlanRouteRunner().FindRoute(registry.DailyPlanRouteSets, routeId);
            if (route == null) return false;
            log.RecordEventWithContext(new QuestEvent(QuestEventKind.DailyPlanSelected, "daily-plan-before-start:" + student.CurrentDay + ":" + routeId, dailyPlanRoute: route), context, rewards);
            return true;
        }
        private QuestChainLog DetachedLog(AdultCareerSession session) => DetachedLog(_registry, session);
        private static QuestChainLog DetachedLog(GameDataRegistry registry, AdultCareerSession session)
        {
            var log = new QuestChainLog(registry.QuestChains);
            var initial = log.ToSaveData();
            foreach (var record in initial.Chains)
            {
                record.SaveSlot = session.Student.SaveSlot; record.PlayerId = session.Student.PlayerId;
            }
            log.LoadFromSaveData(initial);
            log.LoadFromSaveData(session.QuestChains);
            return log;
        }
        private RewardRuntimeContext DetachedRewards(AdultCareerSession session) => DetachedRewards(_registry, session);
        private static RewardRuntimeContext DetachedRewards(GameDataRegistry registry, AdultCareerSession session) => new RewardRuntimeContext(
            PlayerGlobalState.GetQuestLog(session.Student.SaveSlot, session.Student.PlayerId, registry.Quests),
            session.Inventory, null, null, session.Student, session.World, session.FurnitureUnlocks);
        public void NotifyCommitted(AdultCareerRuntimeComponent owner)
        {
            var student = owner.StudentComponent.Progress;
            _store.BindLog(student, owner.QuestChains);
            ProgressChanged?.Invoke(student);
        }

        private AdultCareerRuntimeComponent ResolveOwner(StudentLifeProgress student)
        {
            CheckSession();
            if (student == null || _registry == null || string.IsNullOrEmpty(_activeSlot) || student.SaveSlot != _activeSlot) return null;
            string key = student.SaveSlot.Length + ":" + student.SaveSlot + student.PlayerId;
            if (_owners.TryGetValue(key, out var cached) && cached != null && cached.CanMutate) return cached;
            // Event/startup lookup only, never a per-frame scan. At most the small number of live players.
            var actors = FindObjectsByType<StudentLifeProgressComponent>(FindObjectsSortMode.None);
            foreach (var actor in actors)
            {
                var progress = actor.Progress;
                if (progress == null || progress.SaveSlot != student.SaveSlot || progress.PlayerId != student.PlayerId) continue;
                var owner = AdultCareerRuntimeComponent.Ensure(actor.gameObject, _registry);
                if (owner == null || !owner.CanMutate) continue;
                _owners[key] = owner; _store.BindLog(owner.StudentComponent.Progress, owner.QuestChains); return owner;
            }
            return null;
        }
    }
}
