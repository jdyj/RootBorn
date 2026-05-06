using Rootborn.Game.Common;
using Rootborn.Game.Resources;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.WorldGeneration
{
    public sealed class SeededFarmWorldApplier : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureOnFarmSceneLoad()
        {
            if (SceneManager.GetActiveScene().name != "Farm")
            {
                return;
            }

            if (Object.FindFirstObjectByType<SeededFarmWorldApplier>() != null)
            {
                return;
            }

            var go = new GameObject("[SeededFarmWorldApplier]");
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            go.AddComponent<SeededFarmWorldApplier>();
        }

        private void Start()
        {
            ApplyFromActiveContext();
        }

        public void ApplyFromActiveContext()
        {
            var metadata = Rootborn.Game.Save.ActiveSaveContext.Metadata;
            if (metadata == null)
            {
                return;
            }

            var registry = Rootborn.Game.Managers.Managers.Data != null ? Rootborn.Game.Managers.Managers.Data.Registry : null;
            if (registry == null)
            {
                registry = global::UnityEngine.Resources.Load<GameDataRegistry>("GameDataRegistry");
            }

            if (registry == null || registry.DefaultFarmTerrainGeneration == null)
            {
                return;
            }

            var generated = SeededWorldGenerator.Generate(registry.DefaultFarmTerrainGeneration, metadata.WorldSeed, metadata.TileSeed);
            ApplyTiles(generated);
            ApplyProps(generated);
        }

        private static void ApplyTiles(SeededWorldGenerator.GeneratedWorld generated)
        {
            var tilemap = Object.FindFirstObjectByType<Tilemap>();
            if (tilemap == null || generated == null)
            {
                return;
            }

            for (int x = 0; x < generated.Width; x++)
            {
                for (int y = 0; y < generated.Height; y++)
                {
                    var tile = generated.GetTile(x, y);
                    if (tile != null)
                    {
                        tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                    }
                }
            }
        }

        private static void ApplyProps(SeededWorldGenerator.GeneratedWorld generated)
        {
            if (generated == null)
            {
                return;
            }

            var root = GameObject.Find("[Resources]");
            if (root == null)
            {
                root = new GameObject("[Resources]");
            }
            else
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Destroy(root.transform.GetChild(i).gameObject);
                }
            }

            for (int i = 0; i < generated.Props.Length; i++)
            {
                var prop = generated.Props[i];
                if (prop.Resource == null)
                {
                    continue;
                }

                SpawnNode(root.transform, prop.Resource, new Vector3(prop.Cell.x + 0.5f, prop.Cell.y + 0.5f, 0f), $"{prop.Resource.name}_{i:00}");
            }
        }

        private static void SpawnNode(Transform parent, ResourceNodeDefinition definition, Vector3 position, string objectName)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = definition.Sprite;
            renderer.sortingOrder = 1;

            if (!definition.IsWalkable && ResourceCollisionToggle.Enabled)
            {
                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = definition.ColliderSize;
                collider.isTrigger = false;
            }

            var node = go.AddComponent<ResourceNode>();
            node.BindForRuntime(definition, renderer);
        }
    }
}
