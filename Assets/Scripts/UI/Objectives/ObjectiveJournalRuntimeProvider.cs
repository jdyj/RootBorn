using System.Collections.Generic;
using Rootborn.Game.Common;
using Rootborn.Game.Save;
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

        public ObjectiveJournalPanel Panel => _panel;
        public TrackedObjectiveHud TrackedHud => _trackedHud;

        public void Bind(ObjectiveJournalPanel panel, TrackedObjectiveHud trackedHud, GameObject player)
        {
            _panel = panel;
            _trackedHud = trackedHud;
            _player = player;
            if (_panel != null)
            {
                _panel.BindTrackedHud(_trackedHud);
            }

            RebuildAggregator();
            RefreshNow();
        }

        public void RefreshNow()
        {
            if (_panel == null)
            {
                return;
            }

            RebuildAggregator();
            GameDataRegistryProvider.TryGet(out var registry);
            var metadata = ActiveSaveContext.Metadata;
            string trackedStableKey = string.Empty;
            if (ObjectiveJournalTrackingStore.TryLoad(metadata, out _, out var savedStableKey))
            {
                trackedStableKey = savedStableKey;
            }

            _aggregator.Refresh(_panel, new ObjectiveJournalContext(registry, _player, metadata), trackedStableKey);
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
