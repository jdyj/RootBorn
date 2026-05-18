using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    public abstract class CampaignObjectiveBase : StudentLifeDefinitionBase
    {
        [SerializeField] private bool _requiresSchoolClass;
        public bool RequiresSchoolClass => _requiresSchoolClass;
        public abstract CampaignObjectiveEvaluation Evaluate(in CampaignEvaluationContext context);

        protected void ConfigureObjectiveForTests(string id, bool requiresSchoolClass)
        {
            ConfigureForTests(id, id);
            _requiresSchoolClass = requiresSchoolClass;
        }
    }
}
