using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.Localization;
using Rootborn.UI.Careers;
using UnityEngine;

namespace Rootborn.UI.Objectives
{
    [DisallowMultipleComponent]
    public sealed class ObjectiveJournalRuntimeProvider : MonoBehaviour
    {
        private ObjectiveJournalPanel _panel;
        private TrackedObjectiveHud _trackedHud;
        private GameObject _player;
        private ObjectiveJournalAggregator _aggregator;

        private (object Student, object Adult, int Day, int Energy, int Focus, int Time, int DayState, string Stage, string Objective, string Hint, bool Pending, bool AdultPhase, bool PendingWork, string Language, string TrackedKey, string Slot, int QuestRevision) _observedState;
        private bool _hasObservedState;

        public ObjectiveJournalPanel Panel => _panel;
        public TrackedObjectiveHud TrackedHud => _trackedHud;

        public void Bind(ObjectiveJournalPanel panel, TrackedObjectiveHud trackedHud, GameObject player)
        {
            if (_panel != null) _panel.ItemActionCompleted -= RefreshAfterAction;
            _panel = panel;
            // A detail action (such as claiming a reward) changed game state: rebuild the items so the detail shows it.
            if (_panel != null) _panel.ItemActionCompleted += RefreshAfterAction;
            _trackedHud = trackedHud;
            // A later install pass may not know the player yet; it must not forget the one already bound.
            if (player != null) _player = player;
            _hasObservedState = false;
            if (_panel != null)
            {
                _panel.BindTrackedHud(_trackedHud);
            }

            RebuildAggregator();
            RefreshNow();
        }

        private GameObject FindPlayerInScene()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                var student = root.GetComponentInChildren<StudentLifeProgressComponent>(true);
                if (student != null) return student.gameObject;
            }
            return null;
        }

        public void RefreshNow()
        {
            if (_panel == null)
            {
                return;
            }

            if (_aggregator == null)
            {
                RebuildAggregator();
            }

            if (_player == null) _player = FindPlayerInScene();
            GameDataRegistryProvider.TryGet(out var registry);
            var metadata = ActiveSaveContext.Metadata;
            string trackedStableKey = string.Empty;
            if (ObjectiveJournalTrackingStore.TryLoad(metadata, out _, out var savedStableKey))
            {
                trackedStableKey = savedStableKey;
            }

            _aggregator.Refresh(_panel, new ObjectiveJournalContext(registry, _player, metadata), trackedStableKey);
        }

        // After a detail action only the items are rebuilt: the player stays on the item they acted on (no re-tracking jump).
        private void RefreshAfterAction()
        {
            if (_panel == null) return;
            if (_aggregator == null) RebuildAggregator();
            if (_player == null) _player = FindPlayerInScene();
            GameDataRegistryProvider.TryGet(out var registry);
            _aggregator.Refresh(_panel, new ObjectiveJournalContext(registry, _player, ActiveSaveContext.Metadata), string.Empty);
        }

        // Observe cheap owner values every frame; rebuild read-only source data only on change.
        // Never refresh the journal here: that would select a row and persist tracking again.
        private void LateUpdate()
        {
            if (_player == null || _trackedHud == null || _aggregator == null) return;
            var student = _player.GetComponent<StudentLifeProgressComponent>()?.Progress;
            var adult = _player.GetComponent<AdultCareerRuntimeComponent>()?.Progress;
            string slot = ActiveSaveContext.Metadata != null ? ActiveSaveContext.Metadata.SlotId : string.Empty;
            if (student != null && !string.IsNullOrEmpty(slot) && student.SaveSlot != slot) return;
            var state = ((object)student, (object)adult, student != null ? student.CurrentDay : 0,
                student != null ? student.Energy : 0, student != null ? student.Focus : 0,
                student != null ? student.TimePointsRemaining : 0, student != null ? (int)student.DayState : 0,
                student?.TutorialStageId, student?.NextObjectiveId, student?.NextGuideText,
                student != null && student.HasPendingMinigame, adult != null && adult.IsAdult,
                adult != null && adult.HasPendingWork, GameText.Language, _trackedHud.TrackedStableKey, slot,
                Rootborn.Game.Player.PlayerQuestRuntime.Revision);
            if (_hasObservedState && _observedState.Equals(state)) return;
            _observedState = state;
            _hasObservedState = true;
            string key = _trackedHud.TrackedStableKey;
            if (string.IsNullOrEmpty(key)) return;
            GameDataRegistryProvider.TryGet(out var registry);
            var items = _aggregator.Build(new ObjectiveJournalContext(registry, _player, ActiveSaveContext.Metadata));
            foreach (var category in items.Values)
                foreach (var item in category)
                    if (item.StableKey == key)
                    {
                        _trackedHud.Show(item);
                        return;
                    }
            _trackedHud.Hide();
        }

        private void OnDestroy()
        {
            if (_panel != null) _panel.ItemActionCompleted -= RefreshAfterAction;
        }

        private void RebuildAggregator()
        {
            _aggregator = new ObjectiveJournalAggregator(BuildSources());
        }

        private static IEnumerable<IObjectiveJournalSource> BuildSources()
        {
            return ObjectiveJournalSourceCatalog.CreateDefaultSources();
        }
    }
}
