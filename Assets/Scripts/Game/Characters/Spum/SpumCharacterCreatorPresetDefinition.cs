using System;
using System.Collections.Generic;
using Rootborn.Game.Characters;
using UnityEngine;

namespace Rootborn.Game.Characters.Spum
{
    [CreateAssetMenu(fileName = "SpumCharacterCreatorPreset_New", menuName = "Rootborn/Characters/SPUM Character Creator Preset")]
    public sealed class SpumCharacterCreatorPresetDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private SpumAppearanceDefinition _appearance;
        [SerializeField] private string[] _defaultCategoryIds = { "body", "skin", "eyes", "hair", "outfit" };

        public string Id => string.IsNullOrWhiteSpace(_id) ? name : _id;
        public SpumAppearanceDefinition Appearance => _appearance;
        public IReadOnlyList<string> DefaultCategoryIds => _defaultCategoryIds;

        public CharacterAppearanceSnapshot CreateDefaultSnapshot()
        {
            var catalog = _appearance != null ? _appearance.Catalog : null;
            var selections = new List<CharacterAppearancePartSelection>(_defaultCategoryIds.Length);
            for (int i = 0; i < _defaultCategoryIds.Length; i++)
            {
                string categoryId = _defaultCategoryIds[i];
                SpumPartDefinition defaultPart = catalog != null ? catalog.FindDefaultPart(categoryId) : null;
                if (defaultPart != null)
                    selections.Add(new CharacterAppearancePartSelection(categoryId, defaultPart.StableId));
            }

            return new CharacterAppearanceSnapshot(
                schemaVersion: 1,
                visualKind: _appearance != null ? _appearance.VisualKind : string.Empty,
                appearanceDefinitionId: _appearance != null ? _appearance.Id : string.Empty,
                catalogId: catalog != null ? catalog.Id : string.Empty,
                selectedParts: selections.ToArray());
        }

        public void ConfigureForTests(string id, SpumAppearanceDefinition appearance, string[] defaultCategoryIds)
        {
            _id = id;
            _appearance = appearance;
            _defaultCategoryIds = defaultCategoryIds ?? Array.Empty<string>();
        }
    }
}
