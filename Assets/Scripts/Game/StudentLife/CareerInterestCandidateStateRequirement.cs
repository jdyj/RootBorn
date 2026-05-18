using UnityEngine;
        
        namespace Rootborn.Game.StudentLife
        {
            [CreateAssetMenu(fileName = "CareerInterestReq_CandidateState", menuName = "Rootborn/Student Life/Career Interests/Requirements/Candidate State")]
            public sealed class CareerInterestCandidateStateRequirement : CareerInterestUnlockRequirementBase
            {
                [SerializeField] private CareerCandidateState _minimumState = CareerCandidateState.Revealed;
        
                public override CareerInterestSelectionEvaluation Evaluate(CareerInterestEvaluationContext context)
                {
                    var candidate = context.Interest != null ? context.Interest.Candidate : null;
                    var state = candidate != null ? candidate.GetState(context.CandidateProgress) : CareerCandidateState.Locked;
                    return state < _minimumState
                        ? CareerInterestSelectionEvaluation.Blocked(CareerInterestSelectionState.LockedByCondition, "candidate state " + state + " below " + _minimumState)
                        : CareerInterestSelectionEvaluation.Permit(CareerInterestSelectionState.Selected, string.Empty);
                }
        
                public void ConfigureForTests(CareerCandidateState minimumState) => _minimumState = minimumState;
            }
        }
        