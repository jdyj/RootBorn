using System;
using System.Collections.Generic;
using Rootborn.Game.StudentLife;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "ClueInterpretationOutcome_CareerHint", menuName = "Rootborn/Discovery Clues/Interpretations/Outcomes/Career Hint")]
    public sealed class ClueInterpretationCareerHintOutcome : ClueInterpretationOutcomeBase
    {
        [SerializeField] private CareerDefinition[] _careers = Array.Empty<CareerDefinition>();
        public override string OutcomeId => ClueInterpretationOutcomeIds.CareerHint;
        public override bool Apply(in ClueInterpretationContext context, ClueInterpretationDefinition interpretation, List<string> encyclopediaEntryIds, List<string> careerHintIds, List<string> followUpQuestIds, List<string> worldStateIds, List<string> relationshipIds, List<string> statusIds)
        {
            bool applied = false;
            for (int i = 0; i < _careers.Length; i++)
            {
                var career = _careers[i];
                if (career == null || string.IsNullOrEmpty(career.Id)) continue;
                if (context.StudentLifeProgress == null || !context.StudentLifeProgress.IsCareerHintUnlocked(career)) applied = true;
                context.StudentLifeProgress?.UnlockCareerHint(career);
                ClueInterpretationOutcomeIds.AddUnique(careerHintIds, career.Id);
            }
            return applied;
        }
        public void ConfigureForTests(CareerDefinition[] careers) => _careers = careers ?? Array.Empty<CareerDefinition>();
    }
}
