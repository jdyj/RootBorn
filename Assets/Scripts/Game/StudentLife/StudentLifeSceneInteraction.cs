using Rootborn.Game.Save;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [DisallowMultipleComponent]
    public sealed class StudentLifeProgressComponent : MonoBehaviour
    {
        [SerializeField] private string _fallbackPlayerId = "local-player";
        [SerializeField] private int _startingEnergy = 12;
        [SerializeField] private int _startingFocus = 10;
        [SerializeField] private int _startingStress;
        [SerializeField] private int _startingTimeMinutes = 8 * 60;

        public StudentLifeProgress Progress { get; private set; }

        private void Awake()
        {
            EnsureProgress();
        }

        public void ConfigureForTests(string saveSlot, string playerId, int energy, int focus, int stress, int timeMinutes)
        {
            Progress = new StudentLifeProgress(saveSlot, playerId, energy, focus, stress, timeMinutes);
        }

        public StudentLifeProgress EnsureProgress()
        {
            if (Progress != null)
            {
                return Progress;
            }

            string saveSlot = ActiveSaveContext.Metadata != null && !string.IsNullOrEmpty(ActiveSaveContext.Metadata.SlotId)
                ? ActiveSaveContext.Metadata.SlotId
                : "default";
            Progress = new StudentLifeProgress(saveSlot, _fallbackPlayerId, _startingEnergy, _startingFocus, _startingStress, _startingTimeMinutes);
            return Progress;
        }
    }

    [DisallowMultipleComponent]
    public sealed class StudentLifeActivityInteractor : MonoBehaviour
    {
        [SerializeField] private LifeActivityDefinition _activity;
        [SerializeField] private TraitDefinition _primaryTrait;
        [SerializeField] private SkillDefinition _primarySkill;
        [SerializeField] private CareerDefinition _primaryCareer;

        private readonly LifeActivityRunner _runner = new LifeActivityRunner();
        private int _requestSequence;

        public LifeActivityDefinition Activity => _activity;
        public TraitDefinition PrimaryTrait => _primaryTrait;
        public SkillDefinition PrimarySkill => _primarySkill;
        public CareerDefinition PrimaryCareer => _primaryCareer;
        public LifeActivityResult LastResult { get; private set; }

        public void Bind(
            LifeActivityDefinition activity,
            TraitDefinition primaryTrait,
            SkillDefinition primarySkill,
            CareerDefinition primaryCareer)
        {
            _activity = activity;
            _primaryTrait = primaryTrait;
            _primarySkill = primarySkill;
            _primaryCareer = primaryCareer;
        }

        public bool Interact(StudentLifeProgressComponent progressComponent)
        {
            string requestId = _activity != null ? _activity.Id + ":" + _requestSequence++ : "activity:" + _requestSequence++;
            return Interact(progressComponent, requestId);
        }

        public bool Interact(StudentLifeProgressComponent progressComponent, string requestId)
        {
            var progress = progressComponent == null ? null : progressComponent.EnsureProgress();
            bool applied = _runner.TryPerform(_activity, progress, requestId, out var result);
            LastResult = result;
            return applied;
        }
    }
}
