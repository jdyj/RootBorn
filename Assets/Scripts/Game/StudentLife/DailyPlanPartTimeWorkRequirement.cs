using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DailyPlanRequirement_PartTimeWork", menuName = "Rootborn/Student Life/Daily Plan/Requirements/Part-Time Work")]
    public sealed class DailyPlanPartTimeWorkRequirement : DailyPlanRouteRequirementBase
    {
        [SerializeField] private PartTimeWorkDefinition _work;
        [SerializeField] private bool _expectedCompleted = true;

        public override bool IsSatisfied(StudentLifeProgress progress, DailyPlanRouteDefinition route)
        {
            bool completed = HasCompletedWork(progress);
            return completed == _expectedCompleted;
        }

        public override bool IsSatisfied(in DailyPlanRouteContext context, DailyPlanRouteDefinition route)
        {
            return IsSatisfied(context.StudentProgress, route);
        }

        public void ConfigureForTests(PartTimeWorkDefinition work, bool expectedCompleted)
        {
            _work = work;
            _expectedCompleted = expectedCompleted;
        }

        private bool HasCompletedWork(StudentLifeProgress progress)
        {
            if (progress == null || _work == null || string.IsNullOrEmpty(_work.Id))
            {
                return false;
            }

            if (Contains(progress.GetTodayActivityIds(), _work.Id))
            {
                return true;
            }

            if (Contains(PartTimeWorkDayLog.FromResultLogs(progress.GetTodayResultLogIds()).WorkIds, _work.Id))
            {
                return true;
            }

            return Contains(PartTimeWorkDayLog.FromResultLogs(progress.GetPreviousDayResultLogIds()).WorkIds, _work.Id);
        }

        private static bool Contains(string[] values, string expected)
        {
            if (values == null || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == expected)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
