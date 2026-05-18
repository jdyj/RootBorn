using UnityEngine;

namespace Rootborn.Game.WorldState
{
    [CreateAssetMenu(fileName = "WorldStateScope_New", menuName = "Rootborn/World State/Scope")]
    public sealed class WorldStateScopeDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private WorldStateScopeKind _kind;
        [SerializeField] private string _displayNameKey;

        public string Id => _id;
        public WorldStateScopeKind Kind => _kind;
        public string DisplayNameKey => string.IsNullOrEmpty(_displayNameKey) ? _id : _displayNameKey;

        public void ConfigureForTests(string id, WorldStateScopeKind kind, string displayNameKey)
        {
            _id = id;
            _kind = kind;
            _displayNameKey = string.IsNullOrEmpty(displayNameKey) ? id : displayNameKey;
        }
    }
}
