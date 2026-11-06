using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.World
{
    public sealed class InteriorDoorPortalRuntimeInstaller : MonoBehaviour
    {
        private const string RootName = "[InteriorDoorPortals]";
        private const string TownSceneName = "Town";
        private const string TownSpawnId = "town-entry";
        // Leaving a building puts the player back in front of that building's own door in Town.
        private static readonly Dictionary<string, string> TownReturnSpawnBySelfDoor = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "house", "town-home-entry" },
            { "hospital", "town-hospital-entry" },
            { "shop", "town-shop-entry" },
            { "police_1", "town-police-entry" },
            { "police_2", "town-police-entry" },
            { "kitchen1", "town-kitchen-entry" },
            { "kitchen2", "town-kitchen-entry" },
        };
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
            EnsureForSceneInternal(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForSceneInternal(scene);
        }

        public static readonly string[] InteriorSceneNames =
        {
            "HouseInterior", "HospitalInterior", "ShopInterior", "PoliceInterior", "PoliceShootingInterior", "KitchenInterior", "Kitchen2Interior"
        };

        // The LDtk level files exist only in the editor; players rely on the portals saved into each interior scene.
        // Returns the number of portals present after the pass so the editor bake can refuse to save an empty result.
        public static int EnsureForScene(Scene scene)
        {
            EnsureForSceneInternal(scene);
            var root = FindInScene(scene, RootName);
            return root != null ? root.transform.childCount : 0;
        }

        private static void EnsureForSceneInternal(Scene scene)
        {
            if (!TryGetInteriorLevel(scene.name, out string levelPath, out string selfDoorAlias))
            {
                return;
            }

            var root = FindInScene(scene, RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            IReadOnlyList<InteriorDoorDefinition> definitions = LoadInteriorDoorPortals(levelPath, selfDoorAlias);
            for (int i = 0; i < definitions.Count; i++)
            {
                EnsureDoorPortal(scene, root.transform, definitions[i]);
            }
        }

        public static bool TryGetInteriorLevel(string sceneName, out string levelPath, out string selfDoorAlias)
        {
            if (string.Equals(sceneName, "HouseInterior", StringComparison.OrdinalIgnoreCase))
            {
                levelPath = "Assets/LDtk/Interiors/interiors/House_1.ldtkl";
                selfDoorAlias = "house";
                return true;
            }
            if (string.Equals(sceneName, "HospitalInterior", StringComparison.OrdinalIgnoreCase))
            {
                levelPath = "Assets/LDtk/Interiors/interiors/Hospital_1.ldtkl";
                selfDoorAlias = "hospital";
                return true;
            }
            if (string.Equals(sceneName, "ShopInterior", StringComparison.OrdinalIgnoreCase))
            {
                levelPath = "Assets/LDtk/Interiors/interiors/Shop.ldtkl";
                selfDoorAlias = "shop";
                return true;
            }
            if (string.Equals(sceneName, "PoliceInterior", StringComparison.OrdinalIgnoreCase))
            {
                levelPath = "Assets/LDtk/Interiors/interiors/Police_1.ldtkl";
                selfDoorAlias = "police_1";
                return true;
            }
            if (string.Equals(sceneName, "PoliceShootingInterior", StringComparison.OrdinalIgnoreCase))
            {
                levelPath = "Assets/LDtk/Interiors/interiors/Police_2.ldtkl";
                selfDoorAlias = "police_2";
                return true;
            }
            if (string.Equals(sceneName, "KitchenInterior", StringComparison.OrdinalIgnoreCase))
            {
                levelPath = "Assets/LDtk/Interiors/interiors/Level_14.ldtkl";
                selfDoorAlias = "kitchen1";
                return true;
            }
            if (string.Equals(sceneName, "Kitchen2Interior", StringComparison.OrdinalIgnoreCase))
            {
                levelPath = "Assets/LDtk/Interiors/interiors/Kitchen2.ldtkl";
                selfDoorAlias = "kitchen2";
                return true;
            }
            levelPath = string.Empty;
            selfDoorAlias = string.Empty;
            return false;
        }

        private static IReadOnlyList<InteriorDoorDefinition> LoadInteriorDoorPortals(string levelPath, string selfDoorAlias)
        {
            var result = new List<InteriorDoorDefinition>();
            string resolvedLevelPath = ResolveProjectPath(levelPath);
            if (!File.Exists(resolvedLevelPath))
            {
                return result;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(resolvedLevelPath));
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
                    if (TryCreateDefinition(entity, selfDoorAlias, out InteriorDoorDefinition definition))
                    {
                        result.Add(definition);
                    }
                }
            }

            return result;
        }

        private static bool TryCreateDefinition(JsonElement entity, string selfDoorAlias, out InteriorDoorDefinition definition)
        {
            definition = default;
            string where = Normalize(ReadField(entity, "Where"));
            if (string.IsNullOrEmpty(where))
            {
                return false;
            }

            if (!TryResolveDestination(where, selfDoorAlias, out string destinationScene, out string defaultSpawnId))
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
            if (entity.TryGetProperty("__grid", out JsonElement grid) && grid.ValueKind == JsonValueKind.Array && grid.GetArrayLength() >= 2)
            {
                cellX = grid[0].GetInt32();
                cellY = grid[1].GetInt32();
            }
            string positionField = ReadField(entity, "Position").Trim();
            string spawnId = string.IsNullOrEmpty(positionField) ? defaultSpawnId : positionField;
            string objectName = "InteriorLdtkDoor_" + where.Replace(' ', '_') + "_" + cellX + "_" + cellY;
            Vector3 position = new Vector3((pixelX + width * 0.5f) / PixelsPerUnit, -(pixelY + height * 0.5f) / PixelsPerUnit, 0f);
            Vector2 colliderSize = new Vector2(Mathf.Max(0.75f, width / PixelsPerUnit), Mathf.Max(1.0f, height / PixelsPerUnit));
            definition = new InteriorDoorDefinition(objectName, destinationScene, position, colliderSize, destinationScene, spawnId);
            return true;
        }

        private static bool TryResolveDestination(string where, string selfDoorAlias, out string destinationScene, out string spawnId)
        {
            if (where == "town" || where == "outside" || where == Normalize(selfDoorAlias))
            {
                destinationScene = TownSceneName;
                spawnId = TownReturnSpawnId(selfDoorAlias);
                return true;
            }
            if (where == "police_2")
            {
                destinationScene = "PoliceShootingInterior";
                spawnId = "police-2-entry";
                return true;
            }
            if (where == "police_1")
            {
                destinationScene = "PoliceInterior";
                spawnId = "police-entry";
                return true;
            }
            if (where == "kitchen2")
            {
                destinationScene = "Kitchen2Interior";
                spawnId = "kitchen-2-entry";
                return true;
            }
            if (where == "kitchen1")
            {
                destinationScene = "KitchenInterior";
                spawnId = "kitchen-entry";
                return true;
            }
            destinationScene = string.Empty;
            spawnId = string.Empty;
            return false;
        }

        public static string TownReturnSpawnId(string selfDoorAlias)
        {
            return selfDoorAlias != null && TownReturnSpawnBySelfDoor.TryGetValue(selfDoorAlias, out string spawnId) ? spawnId : TownSpawnId;
        }

        private static string ResolveProjectPath(string assetPath)
        {
            if (Path.IsPathRooted(assetPath))
            {
                return assetPath;
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
        }

        private static string ReadField(JsonElement entity, string fieldName)
        {
            if (!entity.TryGetProperty("fieldInstances", out JsonElement fields) || fields.ValueKind != JsonValueKind.Array)
            {
                return string.Empty;
            }

            foreach (JsonElement field in fields.EnumerateArray())
            {
                string identifier = field.TryGetProperty("__identifier", out JsonElement id) ? id.GetString() ?? string.Empty : string.Empty;
                if (!string.Equals(identifier, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!field.TryGetProperty("__value", out JsonElement value) || value.ValueKind == JsonValueKind.Null)
                {
                    return string.Empty;
                }
                return value.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        private static string Normalize(string value)
        {
            return value.Trim().ToLowerInvariant().Replace('-', ' ');
        }

        private static void EnsureDoorPortal(Scene scene, Transform root, InteriorDoorDefinition definition)
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
            autoPortal.Configure(0.35f);
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

        private readonly struct InteriorDoorDefinition
        {
            public readonly string ObjectName;
            public readonly string DisplayName;
            public readonly Vector3 Position;
            public readonly Vector2 ColliderSize;
            public readonly string DestinationScene;
            public readonly string DestinationSpawnId;

            public InteriorDoorDefinition(string objectName, string displayName, Vector3 position, Vector2 colliderSize, string destinationScene, string destinationSpawnId)
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
