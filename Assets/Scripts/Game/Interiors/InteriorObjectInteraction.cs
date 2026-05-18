using System.Collections.Generic;
using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Interiors
{
    [CreateAssetMenu(fileName = "InteriorInteraction_New", menuName = "Rootborn/Interiors/Interaction Definition")]
    public sealed class InteriorInteractionDefinition : ScriptableObject
    {
        [SerializeField] private InteriorObjectKind _objectKind = InteriorObjectKind.Computer;
        [SerializeField] private string _prompt = "[E] Use";
        [SerializeField] private Vector2 _colliderSize = new Vector2(0.8f, 0.8f);
        [SerializeField] private Vector3 _promptOffset = new Vector3(0f, 0.85f, 0f);
        [SerializeField] private int _priority = 10;

        public InteriorObjectKind ObjectKind => _objectKind;
        public string Prompt => string.IsNullOrWhiteSpace(_prompt) ? "[E] Interact" : _prompt;
        public Vector2 ColliderSize => new Vector2(Mathf.Max(0.1f, _colliderSize.x), Mathf.Max(0.1f, _colliderSize.y));
        public Vector3 PromptOffset => _promptOffset;
        public int Priority => _priority;

        public bool Matches(InteriorObjectKind objectKind)
        {
            return _objectKind == objectKind;
        }

        public void ConfigureForTests(InteriorObjectKind objectKind, string prompt)
        {
            _objectKind = objectKind;
            _prompt = prompt;
            _colliderSize = new Vector2(0.8f, 0.8f);
            _promptOffset = new Vector3(0f, 0.85f, 0f);
            _priority = 10;
        }

        public static InteriorInteractionDefinition CreateForTests(InteriorObjectKind objectKind, string prompt)
        {
            var definition = CreateInstance<InteriorInteractionDefinition>();
            definition.ConfigureForTests(objectKind, prompt);
            return definition;
        }
    }

    public static class InteriorInteractorSpawner
    {
        public static IReadOnlyList<GameObject> SpawnInteractables(
            InteriorGeneratedMap map,
            Tilemap anchorTilemap,
            Vector3Int tileOffset,
            Transform parent,
            IReadOnlyList<InteriorInteractionDefinition> definitions)
        {
            var spawned = new List<GameObject>();
            if (map == null || anchorTilemap == null || definitions == null || definitions.Count == 0)
            {
                ClearChildren(parent);
                return spawned;
            }

            ClearChildren(parent);
            for (int i = 0; i < map.PlacedObjects.Count; i++)
            {
                var placed = map.PlacedObjects[i];
                var definition = ResolveDefinition(placed.ObjectKind, definitions);
                if (definition == null)
                {
                    continue;
                }

                var go = new GameObject("InteriorInteractor_" + placed.ObjectKind + "_" + placed.Cell.x + "_" + placed.Cell.y);
                if (parent != null)
                {
                    go.transform.SetParent(parent, false);
                }

                var world = anchorTilemap.GetCellCenterWorld(new Vector3Int(placed.Cell.x, placed.Cell.y, 0) + tileOffset);
                world.z = 0f;
                go.transform.position = world;

                var collider = go.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = definition.ColliderSize;

                var interactor = go.AddComponent<InteriorObjectInteractor>();
                interactor.Bind(definition, placed);
                spawned.Add(go);
            }

            return spawned;
        }

        private static InteriorInteractionDefinition ResolveDefinition(InteriorObjectKind objectKind, IReadOnlyList<InteriorInteractionDefinition> definitions)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition != null && definition.Matches(objectKind))
                {
                    return definition;
                }
            }

            return null;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Object.Destroy(child);
                }
                else
                {
                    Object.DestroyImmediate(child);
                }
            }
        }
    }
}
