using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "TutorialStage", menuName = "Rootborn/Student Life/Tutorial Stage")]
    public sealed class TutorialStageDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private string _guideText;

        public string GuideText => string.IsNullOrEmpty(_guideText) ? DisplayNameKey : _guideText;

        public void ConfigureForTests(string id, string displayNameKey, string guideText)
        {
            ConfigureForTests(id, displayNameKey);
            _guideText = string.IsNullOrEmpty(guideText) ? string.Empty : guideText;
        }
    }
}
