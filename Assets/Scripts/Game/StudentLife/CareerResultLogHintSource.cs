using UnityEngine;
        
        namespace Rootborn.Game.StudentLife
        {
            [CreateAssetMenu(fileName = "CareerHintSource_ResultLog", menuName = "Rootborn/Student Life/Career Candidates/Hint Sources/Result Log Contains")]
            public sealed class CareerResultLogHintSource : CareerHintSourceBase
            {
                [SerializeField] private string _requiredText;
        
                public override bool Matches(CareerCandidateEvaluationContext context)
                {
                    if (string.IsNullOrEmpty(_requiredText)) return false;
                    for (int i = 0; i < context.ResultLogIds.Length; i++)
                    {
                        string log = context.ResultLogIds[i];
                        if (!string.IsNullOrEmpty(log) && log.Contains(_requiredText)) return true;
                    }
        
                    return false;
                }
        
                public void ConfigureForTests(string requiredText) => _requiredText = requiredText;
            }
        }
        