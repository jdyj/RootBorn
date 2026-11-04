using UnityEngine;

namespace Rootborn.Game.Characters.Growing
{
    /// <summary>A growth stage, hair style and outfit combination. Each combination is data, never a code branch.</summary>
    [CreateAssetMenu(fileName = "GrowingAppearance", menuName = "Rootborn/Characters/Growing Appearance")]
    public sealed class GrowingCharacterAppearanceDefinition : CharacterAppearanceDefinition
    {
        public const string VisualKindName = "growing";

        [SerializeField] private GrowingCharacterSheetDefinition _body;
        [SerializeField] private GrowingCharacterSheetDefinition _head;
        [SerializeField] private GrowingCharacterSheetDefinition _outfit;

        [SerializeField] private string _displayNameKey;
        [SerializeField] private GrowingCharacterVisualView _visualPrefab;

        [Header("When this look applies")]
        [SerializeField] private int _minimumStudentDay = 1;
        [SerializeField] private bool _requiresAdult;
        [Tooltip("Empty means any adult facility without its own look.")]
        [SerializeField] private string _facilityId;

        public GrowingCharacterSheetDefinition Body => _body;
        public GrowingCharacterSheetDefinition Head => _head;
        public GrowingCharacterSheetDefinition Outfit => _outfit;
        public string DisplayNameKey => _displayNameKey;
        public GrowingCharacterVisualView VisualPrefab => _visualPrefab;
        public int MinimumStudentDay => _minimumStudentDay;
        public bool RequiresAdult => _requiresAdult;
        public string FacilityId => _facilityId;

        public void ConfigureForTests(string id, GrowingCharacterSheetDefinition body, GrowingCharacterSheetDefinition head, GrowingCharacterSheetDefinition outfit)
        {
            ConfigureBaseForTests(id, VisualKindName);
            _body = body;
            _head = head;
            _outfit = outfit;
        }

        public void ConfigureRulesForTests(int minimumStudentDay, bool requiresAdult, string facilityId)
        {
            _minimumStudentDay = minimumStudentDay;
            _requiresAdult = requiresAdult;
            _facilityId = facilityId;
        }
    }
}
