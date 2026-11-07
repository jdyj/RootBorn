using System;
using Rootborn.Game.Common;
using Rootborn.Game.Dialogue;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.Story;
using Rootborn.UI.Careers;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.UI.Quests
{
    /// <summary>One scene panel and one explicitly bound player own NPC presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class NpcDialoguePresenter : MonoBehaviour
    {
        private DialoguePanel _panel;
        private AdultCareerRuntimeComponent _owner;
        private PlayerQuestSession _session;
        private QuestLog _questLog;
        private StoryFlagSet _storyFlags;
        private NpcInteractor _lastNpc;
        private long _lastSequence = -1;
        public bool IsBound => _owner != null && _questLog != null;

        public static NpcDialoguePresenter Ensure(Scene scene, DialoguePanel sharedPanel)
        {
            if (!scene.IsValid() || !scene.isLoaded || sharedPanel == null || sharedPanel.gameObject.scene != scene) return null;
            var presenter = sharedPanel.GetComponent<NpcDialoguePresenter>();
            if (presenter == null) presenter = sharedPanel.gameObject.AddComponent<NpcDialoguePresenter>();
            presenter._panel = sharedPanel;
            sharedPanel.OnChoiceExecuted -= presenter.HandleChoiceExecuted;
            sharedPanel.OnChoiceExecuted += presenter.HandleChoiceExecuted;
            return presenter;
        }

        public static bool BindSceneOwner(Scene scene, AdultCareerRuntimeComponent owner)
        {
            if (owner == null || owner.gameObject.scene != scene || !owner.CanMutate) return false;
            var panel = LocationNpcRuntimeInstaller.EnsureDialoguePanelForScene(scene);
            var presenter = Ensure(scene, panel);
            return presenter != null && presenter.Bind(owner, owner.Registry);
        }

        public bool Bind(AdultCareerRuntimeComponent owner, GameDataRegistry registry)
        {
            if (owner == null || registry == null || owner.gameObject.scene != gameObject.scene || !owner.CanMutate) return false;
            if (_owner != owner)
            {
                _lastNpc = null; _lastSequence = -1;
                // The player's one quest session, loaded from the slot once and carried between scenes.
                var session = PlayerQuestRuntime.Get(owner.Progress.SaveSlot, owner.Progress.PlayerId, registry.Quests);
                if (session.LoadFailed) return false;
                _session = session;
                _questLog = session.QuestLog;
                _storyFlags = new StoryFlagSet();
            }
            _owner = owner;
            BindSceneNpcQuestEvents();
            NpcInteractor.BindSceneAuthority(gameObject.scene,this,actor=>this!=null && _owner!=null && _owner.CanMutate && actor==_owner.gameObject && actor.scene==gameObject.scene);
            return true;
        }

        // Talking to an NPC counts toward talk objectives in every scene with NPCs, not only Town.
        private void BindSceneNpcQuestEvents()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var npc in root.GetComponentsInChildren<NpcInteractor>(true))
                    npc.BindQuestEvents(_questLog);
        }

        public bool Owns(NpcInteractor npc, GameObject actor)
        {
            return IsBound && _owner.CanMutate && actor == _owner.gameObject
                && actor.scene == gameObject.scene && npc != null && npc.gameObject.scene == gameObject.scene;
        }

        public bool TryShow(NpcInteractor npc, GameObject actor)
        {
            if (_panel == null || !Owns(npc, actor) || npc.Session.Current == null) return false;
            if (_lastNpc == npc && _lastSequence == npc.InteractionSequence) return _panel.IsOpen;
            _lastNpc = npc; _lastSequence = npc.InteractionSequence;
            var context = new RewardRuntimeContext(_questLog, _owner.PlayerInventory.Inventory, null, _storyFlags,
                _owner.StudentComponent.Progress, _owner.World, _owner.FurnitureUnlocks);
            var sequence=npc.InteractionSequence;
            var student=_owner.StudentComponent.Progress;
            var inventory=_owner.PlayerInventory.Inventory;
            _panel.Open(npc.Session.Current, new DialogueChoiceContext(_questLog, context),
                ()=>this!=null && Owns(npc,actor) && npc.InteractionSequence==sequence
                && ReferenceEquals(student,_owner.StudentComponent.Progress) && ReferenceEquals(inventory,_owner.PlayerInventory.Inventory));
            return true;
        }

        // The quest session already saved on each state change; this keeps the owner's world and inventory in step.
        // Legacy claims are not yet part of the aggregate command.
        private void HandleChoiceExecuted(bool success)
        {
            if (!success || !IsBound || !_owner.CanMutate) return;
            _session?.Save();
            _owner.SaveCurrent();
        }

        private void OnDestroy()
        {
            NpcInteractor.ReleaseSceneAuthority(gameObject.scene,this);
            if (_panel != null) _panel.OnChoiceExecuted -= HandleChoiceExecuted;
        }
    }
}
