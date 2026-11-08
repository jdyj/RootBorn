using Rootborn.Core.Characters;
using Rootborn.Game.Characters.Growing;
using UnityEngine;

namespace Rootborn.Game.Characters.Wearables
{
    /// <summary>A growth stage: when it applies and the body art drawn under every wearable.</summary>
    [CreateAssetMenu(fileName = "Stage", menuName = "Rootborn/Characters/Growth Stage")]
    public sealed class CharacterGrowthStageDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayNameKey;
        [Tooltip("Students reach this stage on or after this student day.")]
        [SerializeField] private int _minimumStudentDay = 1;
        [SerializeField] private bool _requiresAdult;
        [SerializeField] private GrowingCharacterSheetDefinition _body;

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public int MinimumStudentDay => _minimumStudentDay;
        public bool RequiresAdult => _requiresAdult;
        public GrowingCharacterSheetDefinition Body => _body;

        public GrowthStageRule ToRule() => new GrowthStageRule(_id, _minimumStudentDay, _requiresAdult);
    }
}
