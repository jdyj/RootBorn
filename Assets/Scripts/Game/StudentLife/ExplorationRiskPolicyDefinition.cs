using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "ExplorationRiskPolicy_New", menuName = "Rootborn/Student Life/Exploration Choices/Risk Policy")]
    public sealed class ExplorationRiskPolicyDefinition : ScriptableObject
    {
        [SerializeField] private int _stressDelta;
        [SerializeField] private int _energyCost;
        [SerializeField] private int _focusCost;
        [SerializeField] private StatusDefinition _status;
        [SerializeField] private int _statusDelta;

        public int StressDelta => _stressDelta;
        public int EnergyCost => Mathf.Max(0, _energyCost);
        public int FocusCost => Mathf.Max(0, _focusCost);
        public StatusDefinition Status => _status;
        public int StatusDelta => _statusDelta;

        public bool CanApply(in ExplorationInteractionContext context)
        {
            return context.StudentProgress == null || context.StudentProgress.CanSpend(EnergyCost, FocusCost);
        }

        public void Apply(in ExplorationInteractionContext context, ExplorationOutcomeCollector collector)
        {
            var progress = context.StudentProgress;
            if (progress == null) return;
            progress.Spend(0, EnergyCost, FocusCost, _stressDelta);
            if (_stressDelta != 0) collector?.AddFatigueStatus("stress:" + _stressDelta.ToString());
            if (_energyCost != 0) collector?.AddFatigueStatus("energy:-" + EnergyCost.ToString());
            if (_focusCost != 0) collector?.AddFatigueStatus("focus:-" + FocusCost.ToString());
            if (_status != null && _statusDelta != 0)
            {
                progress.AddStatus(_status, _statusDelta, "exploration-risk");
                collector?.AddFatigueStatus(_status.Id);
            }
        }

        public void ConfigureForTests(int stressDelta, int energyCost, int focusCost, StatusDefinition status, int? statusDelta)
        {
            _stressDelta = stressDelta;
            _energyCost = energyCost;
            _focusCost = focusCost;
            _status = status;
            _statusDelta = statusDelta.GetValueOrDefault();
        }
    }
}
