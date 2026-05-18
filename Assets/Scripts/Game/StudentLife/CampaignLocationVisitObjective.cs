using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "CampaignObjective_LocationVisit", menuName = "Rootborn/Student Life/Campaigns/Objectives/Location Visit")]
    public sealed class CampaignLocationVisitObjective : CampaignObjectiveBase
    {
        [SerializeField] private LocationDefinition _location;
        [SerializeField] private int _requiredCount = 1;

        public LocationDefinition Location => _location;

        public void ConfigureForTests(string id, LocationDefinition location, int requiredCount)
        {
            ConfigureObjectiveForTests(id, false);
            _location = location;
            _requiredCount = Mathf.Max(1, requiredCount);
        }

        public override CampaignObjectiveEvaluation Evaluate(in CampaignEvaluationContext context)
        {
            int count = 0;
            string locationId = _location != null ? _location.Id : string.Empty;
            for (int i = 0; i < context.VisitedLocations.Count; i++)
            {
                var visited = context.VisitedLocations[i];
                if (visited != null && !string.IsNullOrEmpty(locationId) && visited.Id == locationId) count++;
            }
            return new CampaignObjectiveEvaluation(count, _requiredCount);
        }
    }
}
