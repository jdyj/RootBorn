using System;
using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "CampaignRoute_New", menuName = "Rootborn/Student Life/Campaigns/Route")]
    public sealed class CampaignRouteDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private string _descriptionKey;
        [SerializeField] private CampaignObjectiveBase[] _objectives = Array.Empty<CampaignObjectiveBase>();

        public string DescriptionKey => string.IsNullOrEmpty(_descriptionKey) ? DisplayNameKey : _descriptionKey;
        public CampaignObjectiveBase[] Objectives => _objectives;

        public bool CanCompleteWithoutSchoolClass
        {
            get
            {
                if (_objectives == null || _objectives.Length == 0) return false;
                for (int i = 0; i < _objectives.Length; i++) if (_objectives[i] != null && _objectives[i].RequiresSchoolClass) return false;
                return true;
            }
        }

        public void ConfigureForTests(string id, string displayNameKey, string descriptionKey, CampaignObjectiveBase[] objectives)
        {
            ConfigureForTests(id, displayNameKey);
            _descriptionKey = string.IsNullOrEmpty(descriptionKey) ? displayNameKey : descriptionKey;
            _objectives = objectives ?? Array.Empty<CampaignObjectiveBase>();
        }
    }
}
