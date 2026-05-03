using System;
using Rootborn.Game.Heir;
using Rootborn.Game.Lineage;
using UnityEngine;

namespace Rootborn.Game.Generation
{
    public sealed class GenerationManager : MonoBehaviour
    {
        [SerializeField] private GenerationProfile _startProfile;

        public GenerationProfile CurrentProfile { get; private set; }
        public float ElapsedSec { get; private set; }
        public LineageBook Lineage { get; } = new LineageBook();

        public event Action<GenerationProfile> OnGenerationStarted;
        public event Action<GenerationProfile, GenerationProfile, HeirData> OnGenerationChanged;

        private HeirData _currentHeir;

        public void StartFromProfile(GenerationProfile profile, HeirData initialHeir = null)
        {
            CurrentProfile = profile;
            ElapsedSec = 0f;
            _currentHeir = initialHeir;
            OnGenerationStarted?.Invoke(profile);
        }

        private void Start()
        {
            if (CurrentProfile == null && _startProfile != null)
            {
                StartFromProfile(_startProfile);
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (CurrentProfile == null) return;
            ElapsedSec += deltaSeconds;
            if (ElapsedSec >= CurrentProfile.LifetimeSec)
            {
                AdvanceGeneration();
            }
        }

        private void Update()
        {
            Tick(UnityEngine.Time.deltaTime);
        }

        public void AdvanceGeneration(HeirData heirOverride = null)
        {
            if (CurrentProfile == null) return;

            var prev = CurrentProfile;
            var next = prev.NextGeneration != null ? prev.NextGeneration : prev;

            var record = new AncestorRecord(prev.GenerationIndex, _currentHeir?.Traits, ElapsedSec);
            Lineage.Record(record);

            _currentHeir = heirOverride;
            CurrentProfile = next;
            ElapsedSec = 0f;
            OnGenerationChanged?.Invoke(prev, next, _currentHeir);
            OnGenerationStarted?.Invoke(next);
        }
    }
}
