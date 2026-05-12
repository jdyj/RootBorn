using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "CareerPractice_New", menuName = "Rootborn/Student Life/Career Practice")]
    public sealed class CareerPracticeDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private LifeActivityDefinition _activity;
        [SerializeField] private CareerDefinition _careerHint;
        [SerializeField] private PracticeStepDefinition[] _steps = Array.Empty<PracticeStepDefinition>();

        public LifeActivityDefinition Activity => _activity;
        public CareerDefinition CareerHint => _careerHint;
        public IReadOnlyList<PracticeStepDefinition> Steps => _steps;

        public void ConfigureForTests(
            string id,
            string displayNameKey,
            LifeActivityDefinition activity,
            CareerDefinition careerHint,
            PracticeStepDefinition[] steps)
        {
            ConfigureForTests(id, displayNameKey);
            _activity = activity;
            _careerHint = careerHint;
            _steps = steps ?? Array.Empty<PracticeStepDefinition>();
        }
    }
}
