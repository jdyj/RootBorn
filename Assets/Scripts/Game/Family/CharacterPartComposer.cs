using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Rootborn.Game.Family
{
    public sealed class CharacterPartComposer : MonoBehaviour
    {
        private static readonly Regex GridSuffix = new Regex(@"_r\d+_c\d+$", RegexOptions.Compiled);

        [SerializeField] private CharacterPartLayer[] _layers = Array.Empty<CharacterPartLayer>();

        public CharacterPartLayer[] Layers => _layers;

        public void EnsureLayers(CharacterPartDefinition[] definitions)
        {
            if (definitions == null)
            {
                _layers = Array.Empty<CharacterPartLayer>();
                return;
            }

            var byCategory = new Dictionary<string, CharacterPartDefinition>();
            for (int i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definition == null || string.IsNullOrEmpty(definition.CategoryId))
                {
                    continue;
                }

                if (!byCategory.TryGetValue(definition.CategoryId, out var existing) || definition.LayerOrder < existing.LayerOrder)
                {
                    byCategory[definition.CategoryId] = definition;
                }
            }

            var layers = new List<CharacterPartLayer>(byCategory.Count);
            foreach (var pair in byCategory)
            {
                var renderer = EnsureRenderer(pair.Key);
                renderer.enabled = true;
                renderer.sortingOrder = pair.Value.LayerOrder;
                layers.Add(new CharacterPartLayer(pair.Key, renderer));
            }

            layers.Sort((a, b) => string.CompareOrdinal(a.CategoryId, b.CategoryId));
            _layers = layers.ToArray();
        }

        public void ApplySprites(CharacterAppearance appearance, CharacterPartDefinition[] definitions, Func<CharacterPartDefinition, Sprite> resolveSprite)
        {
            if (resolveSprite == null)
            {
                return;
            }

            ApplyDefinitions(appearance, definitions, definition => resolveSprite(definition));
        }

        public void ApplyAnimationFrame(
            CharacterAppearance appearance,
            CharacterPartDefinition[] definitions,
            int row,
            int column,
            Func<CharacterPartDefinition, string, Sprite> resolveSprite)
        {
            if (resolveSprite == null)
            {
                return;
            }

            ApplyDefinitions(appearance, definitions, definition =>
            {
                string requestedName = BuildFrameSubSpriteName(definition, row, column);
                var sprite = resolveSprite(definition, requestedName);
                if (sprite == null && requestedName != definition.SubSpriteName)
                {
                    sprite = resolveSprite(definition, definition.SubSpriteName);
                }

                return sprite;
            });
        }

        public static string BuildFrameSubSpriteName(CharacterPartDefinition definition, int row, int column)
        {
            if (definition == null || string.IsNullOrEmpty(definition.SubSpriteName))
            {
                return string.Empty;
            }

            string baseName = GridSuffix.Replace(definition.SubSpriteName, string.Empty);
            int safeRow = Mathf.Max(0, row);
            int safeColumn = Mathf.Max(0, column);
            return $"{baseName}_r{safeRow}_c{safeColumn}";
        }

        public void SetFlipX(bool flipX)
        {
            if (_layers == null)
            {
                return;
            }

            for (int i = 0; i < _layers.Length; i++)
            {
                var renderer = _layers[i] != null ? _layers[i].Renderer : null;
                if (renderer != null)
                {
                    renderer.flipX = flipX;
                }
            }
        }

        private void ApplyDefinitions(CharacterAppearance appearance, CharacterPartDefinition[] definitions, Func<CharacterPartDefinition, Sprite> resolveSprite)
        {
            if (appearance == null || definitions == null || resolveSprite == null)
            {
                return;
            }

            for (int i = 0; i < _layers.Length; i++)
            {
                var layer = _layers[i];
                if (layer == null || layer.Renderer == null)
                {
                    continue;
                }

                string selectedId = appearance.GetSelectedPartId(layer.CategoryId);
                var definition = FindDefinition(definitions, selectedId, layer.CategoryId);
                if (definition == null)
                {
                    definition = FindDefault(definitions, layer.CategoryId);
                }
                if (definition == null)
                {
                    layer.Renderer.sprite = null;
                    continue;
                }

                layer.Renderer.sprite = resolveSprite(definition);
                layer.Renderer.sortingOrder = definition.LayerOrder;
            }
        }

        private SpriteRenderer EnsureRenderer(string categoryId)
        {
            string childName = "Part_" + categoryId;
            var child = transform.Find(childName);
            if (child == null)
            {
                var go = new GameObject(childName);
                go.transform.SetParent(transform, false);
                child = go.transform;
            }

            var renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = child.gameObject.AddComponent<SpriteRenderer>();
            }

            return renderer;
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
}
