using Rootborn.Game.Dialogue;
using Rootborn.Game.WorldState;
using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "DiscoveryClueSource_New", menuName = "Rootborn/Discovery Clues/Source")]
    public sealed class DiscoveryClueSourceDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private DiscoveryClueSourceKind _kind;
        [SerializeField] private string _displayNameKey;
        [SerializeField] private string _hintTextKey;
        [SerializeField] private NpcDefinition _relatedNpc;
        [SerializeField] private WorldStateFlagDefinition _relatedWorldState;
        [SerializeField] private int _strength;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public DiscoveryClueSourceKind Kind => _kind;
        public string DisplayNameKey => string.IsNullOrEmpty(_displayNameKey) ? Id : _displayNameKey;
        public string HintTextKey => string.IsNullOrEmpty(_hintTextKey) ? DisplayNameKey : _hintTextKey;
        public NpcDefinition RelatedNpc => _relatedNpc;
        public WorldStateFlagDefinition RelatedWorldState => _relatedWorldState;
        public int Strength => Mathf.Max(0, _strength);

        public void ConfigureForTests(string id, DiscoveryClueSourceKind kind, string displayNameKey, string hintTextKey, NpcDefinition relatedNpc, WorldStateFlagDefinition relatedWorldState, int strength)
        {
            _id = id;
            _kind = kind;
            _displayNameKey = displayNameKey;
            _hintTextKey = hintTextKey;
            _relatedNpc = relatedNpc;
            _relatedWorldState = relatedWorldState;
            _strength = Mathf.Max(0, strength);
        }
    }
}
