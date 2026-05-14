using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.Characters
{
    [Serializable]
    public sealed class CharacterAppearanceSnapshot
    {
        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private string visualKind;
        [SerializeField] private string appearanceDefinitionId;
        [SerializeField] private string catalogId;
        [SerializeField] private CharacterAppearancePartSelection[] selectedParts = Array.Empty<CharacterAppearancePartSelection>();

        public CharacterAppearanceSnapshot()
        {
        }

        public CharacterAppearanceSnapshot(
            int schemaVersion,
            string visualKind,
            string appearanceDefinitionId,
            string catalogId,
            CharacterAppearancePartSelection[] selectedParts)
        {
            this.schemaVersion = Math.Max(1, schemaVersion);
            this.visualKind = visualKind ?? string.Empty;
            this.appearanceDefinitionId = appearanceDefinitionId ?? string.Empty;
            this.catalogId = catalogId ?? string.Empty;
            this.selectedParts = selectedParts ?? Array.Empty<CharacterAppearancePartSelection>();
        }

        public int SchemaVersion => schemaVersion;
        public string VisualKind => visualKind;
        public string AppearanceDefinitionId => appearanceDefinitionId;
        public string CatalogId => catalogId;
        public IReadOnlyList<CharacterAppearancePartSelection> SelectedParts => selectedParts;

        public string GetSelectedPartId(string categoryId)
        {
            if (string.IsNullOrWhiteSpace(categoryId))
                return string.Empty;

            for (int i = 0; i < selectedParts.Length; i++)
            {
                var selection = selectedParts[i];
                if (selection != null && selection.CategoryId == categoryId)
                    return selection.PartId;
            }

            return string.Empty;
        }
    }

    [Serializable]
    public sealed class CharacterAppearancePartSelection
    {
        [SerializeField] private string categoryId;
        [SerializeField] private string partId;

        public CharacterAppearancePartSelection()
        {
        }

        public CharacterAppearancePartSelection(string categoryId, string partId)
        {
            this.categoryId = categoryId ?? string.Empty;
            this.partId = partId ?? string.Empty;
        }

        public string CategoryId => categoryId;
        public string PartId => partId;
    }
}
