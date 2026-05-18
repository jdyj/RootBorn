using System;
        using UnityEngine;
        
        namespace Rootborn.Game.StudentLife
        {
            [CreateAssetMenu(fileName = "CareerInterestRec_Static", menuName = "Rootborn/Student Life/Career Interests/Recommendations/Static")]
            public sealed class CareerInterestStaticRecommendation : CareerInterestRecommendationBase
            {
                [SerializeField] private string[] _recommendationKeys = Array.Empty<string>();
        
                public override string[] GetRecommendationKeys(CareerInterestEvaluationContext context) => _recommendationKeys ?? Array.Empty<string>();
                public void ConfigureForTests(string[] recommendationKeys) => _recommendationKeys = recommendationKeys ?? Array.Empty<string>();
            }
        }
        