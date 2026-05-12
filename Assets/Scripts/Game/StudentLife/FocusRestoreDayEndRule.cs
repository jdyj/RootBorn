using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DayEndRule_FocusRestore", menuName = "Rootborn/Student Life/Day End Rules/Focus Restore")]
    public sealed class FocusRestoreDayEndRule : DayEndRuleBase
    {
        [SerializeField] private int _restoredFocus = 10;

        public override void Apply(StudentLifeProgress progress)
        {
            progress?.RestoreFocusForDay(_restoredFocus);
        }

        public void ConfigureForTests(int restoredFocus)
        {
            _restoredFocus = Mathf.Max(0, restoredFocus);
        }
    }
}
