using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.World
{
    public sealed class TownDoorPortalRuntimeInstaller : MonoBehaviour
    {
        private const string TownSceneName = "Town";
        private const string RootName = "[TownDoorPortals]";
        private const string LdtkLevelPath = "Assets/LDtk/Town/town/Level_0.ldtkl";
        private const float PixelsPerUnit = 32f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureActiveScene()
        {
            EnsureForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene(scene);
        }

        private static void EnsureForScene(Scene scene)
        {
            if (AuthoredWorldScene.Find(scene) != null) return;
            if (scene.name != TownSceneName)
            {
                return;
            }

            var root = FindInScene(scene, RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            DisableLegacyPortals(scene, root.transform);

            IReadOnlyList<LdtkDoorPortalDefinition> definitions = LoadLdtkDoorPortals();
            for (int i = 0; i < definitions.Count; i++)
            {
                EnsureDoorPortal(scene, root.transform, definitions[i]);
            }
        }

        private static void DisableLegacyPortals(Scene scene, Transform root)
        {
            string[] legacyNames =
            {
                "TownDoorPortal_Bakery",
                "TownDoorPortal_Apartment",
                "TownDoorPortal_Office",
                "TownDoorPortal_Cafe",
                "TownDoorPortal_Market",
                "TownDoorPortal_Police",
                "TownDoorPortal_Hospital",
                "TownDoorPortal_BusinessCenter"
            };

            for (int i = 0; i < legacyNames.Length; i++)
            {
                var legacy = FindInScene(scene, legacyNames[i]);
                if (legacy == null)
                {
                    continue;
                }

                legacy.transform.SetParent(root, true);
                var collider = legacy.GetComponent<Collider2D>();
                if (collider != null)
                {
                    collider.enabled = false;
                }

                var portal = legacy.GetComponent<WorldPortal>();
                if (portal != null)
                {
                    portal.Bind(string.Empty, string.Empty, string.Empty);
                }
            }
        }

        private static IReadOnlyList<LdtkDoorPortalDefinition> LoadLdtkDoorPortals()
        {
            var result = new List<LdtkDoorPortalDefinition>();
            if (!File.Exists(LdtkLevelPath))
            {
                return result;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(LdtkLevelPath));
            if (!document.RootElement.TryGetProperty("layerInstances", out JsonElement layers) || layers.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (JsonElement layer in layers.EnumerateArray())
            {
                string layerName = layer.TryGetProperty("__identifier", out JsonElement layerIdentifier) ? layerIdentifier.GetString() ?? string.Empty : string.Empty;
                if (!string.Equals(layerName, "Door", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!layer.TryGetProperty("entityInstances", out JsonElement entities) || entities.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement entity in entities.EnumerateArray())
                {
                    if (!TryCreateDefinition(entity, out LdtkDoorPortalDefinition definition))
                    {
                        continue;
                    }

                    result.Add(definition);
                }
            }

            return result;
        }

        private static bool TryCreateDefinition(JsonElement entity, out LdtkDoorPortalDefinition definition)
        {
            definition = default;
            string doorField = ReadDoorField(entity).Trim();
            if (string.IsNullOrEmpty(doorField))
            {
                return false;
            }

            string normalized = NormalizeDoorField(doorField);
            string destinationScene;
            string spawnId;
            string displayName;
            if (normalized == "house")
            {
                destinationScene = "HouseInterior";
                spawnId = "house-entry";
                displayName = "House";
            }
            else if (normalized == "hospital")
            {
                destinationScene = "HospitalInterior";
                spawnId = "hospital-entry";
                displayName = "Hospital";
            }
            else if (normalized == "shopping center" || normalized == "shop")
            {
                destinationScene = "ShopInterior";
                spawnId = "shop-entry";
                displayName = "Shop";
            }
            else if (normalized == "police")
            {
                destinationScene = "PoliceInterior";
                spawnId = "police-entry";
                displayName = "Police";
            }
            else if (normalized == "kitchen")
            {
                destinationScene = "KitchenInterior";
                spawnId = "kitchen-entry";
                displayName = "Kitchen";
            }
            else
            {
                return false;
            }

            if (!entity.TryGetProperty("px", out JsonElement px) || px.ValueKind != JsonValueKind.Array || px.GetArrayLength() < 2)
            {
                return false;
            }

            int width = entity.TryGetProperty("width", out JsonElement widthProperty) ? widthProperty.GetInt32() : 32;
            int height = entity.TryGetProperty("height", out JsonElement heightProperty) ? heightProperty.GetInt32() : 32;
            int pixelX = px[0].GetInt32();
            int pixelY = px[1].GetInt32();
            int cellX = Mathf.FloorToInt(pixelX / PixelsPerUnit);
            int cellY = Mathf.FloorToInt(pixelY / PixelsPerUnit);
            string objectName = "TownLdtkDoor_" + normalized.Replace(' ', '_') + "_" + cellX + "_" + cellY;
            Vector3 position = new Vector3((pixelX + width * 0.5f) / PixelsPerUnit, -(pixelY + height * 0.5f) / PixelsPerUnit, 0f);
            Vector2 colliderSize = new Vector2(Mathf.Max(0.75f, width / PixelsPerUnit), Mathf.Max(1.0f, height / PixelsPerUnit));
            definition = new LdtkDoorPortalDefinition(objectName, displayName, position, colliderSize, destinationScene, spawnId);
            return true;
        }

        private static string ReadDoorField(JsonElement entity)
        {
            if (!entity.TryGetProperty("fieldInstances", out JsonElement fields) || fields.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            foreach (JsonElement field in fields.EnumerateArray())
            {
                string identifier = field.TryGetProperty("__identifier", out JsonElement id) ? id.GetString() ?? string.Empty : string.Empty;
                if (!string.Equals(identifier, "Door", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return field.TryGetProperty("__value", out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;
            }

            return string.Empty;
        }

        private static string NormalizeDoorField(string doorField)
        {
            return doorField.Trim().ToLowerInvariant().Replace('-', ' ');
        }

        private static void EnsureDoorPortal(Scene scene, Transform root, LdtkDoorPortalDefinition definition)
        {
            var portal = FindInScene(scene, definition.ObjectName);
            if (portal == null)
            {
                portal = new GameObject(definition.ObjectName);
                SceneManager.MoveGameObjectToScene(portal, scene);
            }

            portal.transform.SetParent(root, false);
            portal.transform.position = definition.Position;
            portal.SetActive(true);

            var collider = portal.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = portal.AddComponent<BoxCollider2D>();
            }
            collider.enabled = true;
            collider.isTrigger = true;
            collider.size = definition.ColliderSize;
            collider.offset = Vector2.zero;

            var worldPortal = portal.GetComponent<WorldPortal>();
            if (worldPortal == null)
            {
                worldPortal = portal.AddComponent<WorldPortal>();
            }
            worldPortal.Bind(definition.DisplayName, definition.DestinationScene, definition.DestinationSpawnId);

            var autoPortal = portal.GetComponent<TownAutoDoorPortal>();
            if (autoPortal == null)
            {
                autoPortal = portal.AddComponent<TownAutoDoorPortal>();
            }
            autoPortal.Configure(0.8f);
        }

        private static GameObject FindInScene(Scene scene, string objectName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindInChildren(roots[i].transform, objectName);
                if (match != null)
                {
                    return match.gameObject;
                }
            }

            return null;
        }

        private static Transform FindInChildren(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindInChildren(root.GetChild(i), objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private readonly struct LdtkDoorPortalDefinition
        {
            public readonly string ObjectName;
            public readonly string DisplayName;
            public readonly Vector3 Position;
            public readonly Vector2 ColliderSize;
            public readonly string DestinationScene;
            public readonly string DestinationSpawnId;

            public LdtkDoorPortalDefinition(string objectName, string displayName, Vector3 position, Vector2 colliderSize, string destinationScene, string destinationSpawnId)
            {
                ObjectName = objectName;
                DisplayName = displayName;
                Position = position;
                ColliderSize = colliderSize;
                DestinationScene = destinationScene;
                DestinationSpawnId = destinationSpawnId;
            }
        }
    }
}