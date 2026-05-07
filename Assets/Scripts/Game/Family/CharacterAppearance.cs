using System;
using UnityEngine;

namespace Rootborn.Game.Family
{
    [Serializable]
    public sealed class CharacterAppearance
    {
        [SerializeField] private CharacterPartSelection[] _parts = Array.Empty<CharacterPartSelection>();

        public CharacterPartSelection[] Parts => _parts;

        public string GetSelectedPartId(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId) || _parts == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < _parts.Length; i++)
            {
                var part = _parts[i];
                if (part != null && part.CategoryId == categoryId)
                {
                    return part.PartId ?? string.Empty;
                }
            }

            return string.Empty;
        }

        public void SetSelectedPart(string categoryId, string partId)
        {
            if (string.IsNullOrEmpty(categoryId))
            {
                return;
            }

            if (_parts == null)
            {
                _parts = Array.Empty<CharacterPartSelection>();
            }

            for (int i = 0; i < _parts.Length; i++)
            {
                if (_parts[i] != null && _parts[i].CategoryId == categoryId)
                {
                    _parts[i].PartId = partId ?? string.Empty;
                    return;
                }
            }

            var next = new CharacterPartSelection[_parts.Length + 1];
            Array.Copy(_parts, next, _parts.Length);
            next[next.Length - 1] = new CharacterPartSelection(categoryId, partId ?? string.Empty);
            _parts = next;
        }

        public static CharacterAppearance ResolveWithDefaults(CharacterAppearance saved, CharacterPartDefinition[] definitions)
        {
            var resolved = new CharacterAppearance();
            if (definitions == null)
            {
                return resolved;
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definition == null || string.IsNullOrEmpty(definition.CategoryId))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(resolved.GetSelectedPartId(definition.CategoryId)))
                {
                    continue;
                }

                string savedId = saved != null ? saved.GetSelectedPartId(definition.CategoryId) : string.Empty;
                var selected = FindDefinition(definitions, savedId, definition.CategoryId);
                if (selected == null)
                {
                    selected = FindDefault(definitions, definition.CategoryId);
                }
                if (selected == null)
                {
                    selected = definition;
                }

                resolved.SetSelectedPart(definition.CategoryId, selected.Id);
            }

            return resolved;
        }

        private static CharacterPartDefinition FindDefinition(CharacterPartDefinition[] definitions, string id, string categoryId)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definition != null && definition.Id == id && definition.CategoryId == categoryId)
                {
                    return definition;
                }
            }

            return null;
        }

        private static CharacterPartDefinition FindDefault(CharacterPartDefinition[] definitions, string categoryId)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definition != null && definition.CategoryId == categoryId && definition.IsDefault)
                {
                    return definition;
                }
            }

            return null;
        }
    }

    [Serializable]
    public sealed class CharacterPartSelection
    {
        [SerializeField] private string _categoryId;
        [SerializeField] private string _partId;

        public CharacterPartSelection(string categoryId, string partId)
        {
            _categoryId = categoryId;
            _partId = partId;
        }

        public string CategoryId => _categoryId;
        public string PartId
        {
            get => _partId;
            set => _partId = value;
        }
    }
}
