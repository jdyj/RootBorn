using UnityEngine;

namespace Rootborn.Game.Characters.Spum
{
    [CreateAssetMenu(fileName = "SpumPart_New", menuName = "Rootborn/Characters/SPUM Part")]
    public sealed class SpumPartDefinition : ScriptableObject
    {
        [SerializeField] private string _stableId;
        [SerializeField] private string _categoryId;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private string _addressableKey;
        [SerializeField] private Sprite _previewSprite;
        [SerializeField] private bool _isDefault;

        public string StableId => string.IsNullOrWhiteSpace(_stableId) ? name : _stableId;
        public string CategoryId => _categoryId;
        public string DisplayNameKey => _displayNameKey;
        public string AddressableKey => _addressableKey;
        public Sprite PreviewSprite => _previewSprite;
        public bool IsDefault => _isDefault;

        public void ConfigureForTests(string stableId, string categoryId, bool isDefault)
        {
            _stableId = stableId;
            _categoryId = categoryId;
            _isDefault = isDefault;
        }
    }
}
