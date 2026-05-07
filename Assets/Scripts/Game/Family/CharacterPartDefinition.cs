using UnityEngine;

namespace Rootborn.Game.Family
{
    [CreateAssetMenu(fileName = "CharacterPartDefinition", menuName = "Rootborn/Family/Character Part Definition")]
    public sealed class CharacterPartDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _categoryId;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private string _sheetAddress;
        [SerializeField] private string _subSpriteName;
        [SerializeField] private string _editorAssetPath;
        [SerializeField] private Sprite _previewSprite;
        [SerializeField] private int _layerOrder;
        [SerializeField] private bool _isDefault;

        public string Id => _id;
        public string CategoryId => _categoryId;
        public string DisplayNameKey => _displayNameKey;
        public string SheetAddress => _sheetAddress;
        public string SubSpriteName => _subSpriteName;
        public string EditorAssetPath => _editorAssetPath;
        public Sprite PreviewSprite => _previewSprite;
        public int LayerOrder => _layerOrder;
        public bool IsDefault => _isDefault;

        public bool IsValid(out string error)
        {
            error = string.Empty;
            AppendMissing(ref error, _id, "id");
            AppendMissing(ref error, _categoryId, "category");
            AppendMissing(ref error, _sheetAddress, "sheet");
            AppendMissing(ref error, _subSpriteName, "sub-sprite");
            return string.IsNullOrEmpty(error);
        }

        private static void AppendMissing(ref string error, string value, string label)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!string.IsNullOrEmpty(error))
            {
                error += ", ";
            }

            error += label;
        }
    }
}
