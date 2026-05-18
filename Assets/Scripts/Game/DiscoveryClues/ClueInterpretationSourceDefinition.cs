using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "ClueInterpretationSource_New", menuName = "Rootborn/Discovery Clues/Interpretations/Source")]
    public sealed class ClueInterpretationSourceDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private ClueInterpretationSourceKind _kind;
        [SerializeField] private string _targetId;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private string _hintTextKey;
        [SerializeField] private int _strength;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public ClueInterpretationSourceKind Kind => _kind;
        public string TargetId => string.IsNullOrEmpty(_targetId) ? Id : _targetId;
        public string DisplayNameKey => string.IsNullOrEmpty(_displayNameKey) ? Id : _displayNameKey;
        public string HintTextKey => string.IsNullOrEmpty(_hintTextKey) ? DisplayNameKey : _hintTextKey;
        public int Strength => Mathf.Max(0, _strength);

        public void ConfigureForTests(string id, ClueInterpretationSourceKind kind, string targetId, string displayNameKey, string hintTextKey, int strength)
        {
            _id = id;
            _kind = kind;
            _targetId = targetId;
            _displayNameKey = displayNameKey;
            _hintTextKey = hintTextKey;
            _strength = Mathf.Max(0, strength);
        }
    }
}
