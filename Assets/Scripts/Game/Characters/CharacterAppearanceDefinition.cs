using UnityEngine;

namespace Rootborn.Game.Characters
{
    public abstract class CharacterAppearanceDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _visualKind;

        public string Id => string.IsNullOrWhiteSpace(_id) ? name : _id;
        public string VisualKind => string.IsNullOrWhiteSpace(_visualKind) ? "pixelwood" : _visualKind;

        protected void ConfigureBaseForTests(string id, string visualKind)
        {
            _id = id;
            _visualKind = visualKind;
        }
    }
}
