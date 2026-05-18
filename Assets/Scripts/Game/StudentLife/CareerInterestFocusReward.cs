using UnityEngine;
        
        namespace Rootborn.Game.StudentLife
        {
            [CreateAssetMenu(fileName = "CareerInterestReward_Focus", menuName = "Rootborn/Student Life/Career Interests/Rewards/Focus")]
            public sealed class CareerInterestFocusReward : CareerInterestRewardBase
            {
                [SerializeField] private string _rewardId;
                [SerializeField] private int _focusDelta = 1;
        
                public override string RewardId => string.IsNullOrEmpty(_rewardId) ? name : _rewardId;
        
                public override void Apply(CareerInterestEvaluationContext context)
                {
                    if (context.StudentProgress != null) context.StudentProgress.RestoreFocusForDay(context.StudentProgress.Focus + Mathf.Max(0, _focusDelta));
                }
        
                public void ConfigureForTests(string rewardId, int focusDelta)
                {
                    _rewardId = rewardId;
                    _focusDelta = Mathf.Max(0, focusDelta);
                }
            }
        }
        