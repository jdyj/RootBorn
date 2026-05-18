using UnityEngine;
        
        namespace Rootborn.Game.StudentLife
        {
            [CreateAssetMenu(fileName = "CareerInterestRule_DailyChangeLimit", menuName = "Rootborn/Student Life/Career Interests/Selection Rules/Daily Change Limit")]
            public sealed class CareerInterestDailyChangeLimitRule : CareerInterestSelectionRuleBase
            {
                [SerializeField] private int _maxChangesPerDay = 1;
                [SerializeField] private bool _allowSameInterestNoOp;
        
                public override CareerInterestSelectionEvaluation Evaluate(CareerInterestEvaluationContext context)
                {
                    if (context.InterestProgress == null) return CareerInterestSelectionEvaluation.Permit(CareerInterestSelectionState.Selected, string.Empty);
                    string nextId = context.Interest != null ? context.Interest.Id : string.Empty;
                    if (_allowSameInterestNoOp && context.InterestProgress.CurrentInterestId == nextId) return CareerInterestSelectionEvaluation.Blocked(CareerInterestSelectionState.Selected, "interest already selected");
                    if (!string.IsNullOrEmpty(context.InterestProgress.CurrentInterestId) && context.InterestProgress.LastChangedDay == context.Day && context.InterestProgress.ChangeCountForCurrentDay >= Mathf.Max(1, _maxChangesPerDay)) return CareerInterestSelectionEvaluation.Blocked(CareerInterestSelectionState.ChangedToday, "daily change limit reached");
                    return CareerInterestSelectionEvaluation.Permit(CareerInterestSelectionState.Selected, string.Empty);
                }
        
                public void ConfigureForTests(int maxChangesPerDay, bool allowSameInterestNoOp)
                {
                    _maxChangesPerDay = Mathf.Max(1, maxChangesPerDay);
                    _allowSameInterestNoOp = allowSameInterestNoOp;
                }
            }
        }
        