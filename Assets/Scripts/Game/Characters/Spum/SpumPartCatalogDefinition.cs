using System;
using Rootborn.Game.Characters;
using UnityEngine;

namespace Rootborn.Game.Characters.Spum
{
    [CreateAssetMenu(fileName = "SpumPartCatalog_New", menuName = "Rootborn/Characters/SPUM Part Catalog")]
    public sealed class SpumPartCatalogDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private SpumPartDefinition[] _parts = Array.Empty<SpumPartDefinition>();

        public string Id => string.IsNullOrWhiteSpace(_id) ? name : _id;
        public SpumPartDefinition[] Parts => _parts;

        public SpumPartDefinition ResolveSelectedPart(CharacterAppearanceSnapshot snapshot, string categoryId)
        {
            if (string.IsNullOrWhiteSpace(categoryId))
                return null;

            string selectedPartId = snapshot?.GetSelectedPartId(categoryId) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(selectedPartId))
            {
                SpumPartDefinition selected = FindPart(categoryId, selectedPartId);
                if (selected != null)
                    return selected;
            }

            return FindDefaultPart(categoryId);
        }

        public SpumPartDefinition FindDefaultPart(string categoryId)
        {
            for (int i = 0; i < _parts.Length; i++)
            {
                var part = _parts[i];
                if (part != null && part.CategoryId == categoryId && part.IsDefault)
                    return part;
            }

            return null;
        }

        public void ConfigureForTests(string id, SpumPartDefinition[] parts)
        {
            _id = id;
            _parts = parts ?? Array.Empty<SpumPartDefinition>();
        }

        private SpumPartDefinition FindPart(string categoryId, string stableId)
        {
            for (int i = 0; i < _parts.Length; i++)
            {
                var part = _parts[i];
                if (part != null && part.CategoryId == categoryId && part.StableId == stableId)
                    return part;
            }

            return null;
        }
    }
}
