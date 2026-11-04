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

        public bool Bind(AdultCareerRuntimeComponent owner, GameDataRegistry registry, QuestLog questLog = null, StoryFlagSet storyFlags = null)
        {
            if (owner == null || registry == null || owner.gameObject.scene != gameObject.scene || !owner.CanMutate) return false;
            bool changed = _owner != owner;
            if (changed)
            {
                _lastNpc = null; _lastSequence = -1;
                _questLog = questLog ?? PlayerGlobalState.GetQuestLog(owner.Progress.SaveSlot, owner.Progress.PlayerId, registry.Quests);
                _storyFlags = storyFlags ?? new StoryFlagSet();
                if (questLog == null && !LoadLegacyLog(owner, _questLog)) return false;
            }
            else
            {
                if (questLog != null) _questLog = questLog;
                if (storyFlags != null) _storyFlags = storyFlags;
            }
            _owner = owner;
            NpcInteractor.BindSceneAuthority(gameObject.scene,this,actor=>this!=null && _owner!=null && _owner.CanMutate && actor==_owner.gameObject && actor.scene==gameObject.scene);
            return true;
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

        // Compatibility persistence only. Legacy claims are not yet part of the aggregate command.
        private void HandleChoiceExecuted(bool success)
        {
            if (!success || !IsBound || !_owner.CanMutate) return;
            new SaveService(_owner.Progress.SaveSlot).WriteJson(LogFileName(_owner.Progress.PlayerId), JsonUtility.ToJson(_questLog.ToSaveData(), true));
            _owner.SaveCurrent();
        }

        private static bool LoadLegacyLog(AdultCareerRuntimeComponent owner, QuestLog log)
        {
            var json = new SaveService(owner.Progress.SaveSlot).ReadJson(LogFileName(owner.Progress.PlayerId));
            if (string.IsNullOrEmpty(json)) return true;
            try
            {
                var data = JsonUtility.FromJson<QuestLogSaveData>(json);
                if (data?.Quests == null) return false;
                log.LoadFromSaveData(data);
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        private static string LogFileName(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || playerId == PlayerIdentity.DefaultPlayerId) return "quest-log.json";
            var chars = playerId.ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            return "quest-log-" + new string(chars) + ".json";
        }

        private void OnDestroy()
        {
            NpcInteractor.ReleaseSceneAuthority(gameObject.scene,this);
            if (_panel != null) _panel.OnChoiceExecuted -= HandleChoiceExecuted;
        }
    }
}
