using UnityEngine;

namespace Rootborn.Game.Story
{
    [CreateAssetMenu(fileName = "StoryFlag_New", menuName = "Rootborn/Story/Story Flag")]
    public sealed class StoryFlagDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayKey;

        public string Id => _id;
        public string DisplayKey => _displayKey;
    }
}
