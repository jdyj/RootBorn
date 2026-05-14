using Rootborn.Game.Characters;
using UnityEngine;

namespace Rootborn.Game.Characters.Spum
{
    [CreateAssetMenu(fileName = "SpumAppearance_New", menuName = "Rootborn/Characters/SPUM Appearance")]
    public sealed class SpumAppearanceDefinition : CharacterAppearanceDefinition
    {
        [SerializeField] private SpumPartCatalogDefinition _catalog;

        public SpumPartCatalogDefinition Catalog => _catalog;

        public void ConfigureForTests(string id, string visualKind, SpumPartCatalogDefinition catalog)
        {
            ConfigureBaseForTests(id, visualKind);
            _catalog = catalog;
        }
    }
}
