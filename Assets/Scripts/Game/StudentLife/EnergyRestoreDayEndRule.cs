using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DayEndRule_EnergyRestore", menuName = "Rootborn/Student Life/Day End Rules/Energy Restore")]
    public sealed class EnergyRestoreDayEndRule : DayEndRuleBase
    {
        [SerializeField] private int _restoredEnergy = 12;

        public override void Apply(StudentLifeProgress progress)
        {
            progress?.RestoreEnergyForDay(_restoredEnergy);
        }

        public void ConfigureForTests(int restoredEnergy)
        {
            _restoredEnergy = Mathf.Max(0, restoredEnergy);
        }
    }
}
