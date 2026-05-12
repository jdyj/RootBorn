using System;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.World
{
    [Serializable]
    public sealed class WorldProgressSaveData
    {
        public string CurrentScene;
        public string CurrentEntryPointId;
        public string LastPortalDisplayName;
    }

    [DisallowMultipleComponent]
    public sealed class WorldPortal : MonoBehaviour, IPlayerInteractable, IPrioritizedPlayerInteractable
    {
        private const int PortalInteractionPriority = 100;
        private const string WorldProgressFileName = "world-progress.json";

        [SerializeField] private string _displayName;
        [SerializeField] private string _destinationScene;
        [SerializeField] private string _destinationSpawnId;

        private GameObject _travellingPlayer;
        private bool _isTravelling;

        public string DisplayName => _displayName;
        public string DestinationScene => _destinationScene;
        public string DestinationSpawnId => _destinationSpawnId;
        public int InteractionPriority => PortalInteractionPriority;
        public string InteractionPrompt => string.IsNullOrEmpty(_displayName) ? "[E] Enter" : "[E] Enter " + _displayName;
        public Vector3 InteractionPromptOffset => new Vector3(0f, 1.25f, 0f);
        public Transform InteractionTransform => transform;

        public void Bind(string displayName, string destinationScene, string destinationSpawnId)
        {
            _displayName = displayName;
            _destinationScene = destinationScene;
            _destinationSpawnId = destinationSpawnId;
        }

        public bool CanTravel => !string.IsNullOrEmpty(_destinationScene) && !string.IsNullOrEmpty(_destinationSpawnId);

        public bool CanInteract(GameObject player)
        {
            return CanTravel && !_isTravelling;
        }

        public bool TryInteract(GameObject player)
        {
            if (!CanInteract(player))
            {
                return false;
            }

            Travel();
            return true;
        }

        public void Travel()
        {
            if (!CanTravel || _isTravelling)
            {
                return;
            }

            _isTravelling = true;
            _travellingPlayer = FindByName(SceneManager.GetActiveScene(), "Player");
            if (_travellingPlayer != null)
            {
                UnityEngine.Object.DontDestroyOnLoad(_travellingPlayer);
            }

            SceneManager.sceneLoaded -= HandleDestinationLoaded;
            SceneManager.sceneLoaded += HandleDestinationLoaded;
            SceneManager.LoadScene(_destinationScene);
        }

        private void HandleDestinationLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != _destinationScene)
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleDestinationLoaded;
            var player = _travellingPlayer != null ? _travellingPlayer : FindByName(scene, "Player");
            if (_travellingPlayer != null)
            {
                RemoveDestinationPlayerDuplicate(scene, _travellingPlayer);
            }

            _travellingPlayer = null;
            _isTravelling = false;
            if (player == null)
            {
                return;
            }

            SceneManager.MoveGameObjectToScene(player, scene);
            var spawn = FindSpawnPoint(scene, _destinationSpawnId);
            if (spawn != null)
            {
                player.transform.position = spawn.transform.position;
            }

            SaveWorldProgress(scene.name, _destinationSpawnId);

            var camera = Camera.main;
            if (camera != null)
            {
                var follow = camera.GetComponent<Player.CameraFollow>();
                if (follow == null)
                {
                    follow = camera.gameObject.AddComponent<Player.CameraFollow>();
                }
                follow.SetTarget(player.transform);
            }
        }

        private void SaveWorldProgress(string currentScene, string entryPointId)
        {
            var metadata = ActiveSaveContext.Metadata;
            if (metadata == null || string.IsNullOrEmpty(metadata.SlotId))
            {
                return;
            }

            var data = new WorldProgressSaveData
            {
                CurrentScene = string.IsNullOrEmpty(currentScene) ? string.Empty : currentScene,
                CurrentEntryPointId = string.IsNullOrEmpty(entryPointId) ? string.Empty : entryPointId,
                LastPortalDisplayName = string.IsNullOrEmpty(_displayName) ? string.Empty : _displayName,
            };
            new SaveService(metadata.SlotId).WriteJson(WorldProgressFileName, JsonUtility.ToJson(data, true));
        }

        private static void RemoveDestinationPlayerDuplicate(Scene scene, GameObject travellingPlayer)
        {
            var duplicate = FindByName(scene, "Player");
            if (duplicate == null || duplicate == travellingPlayer)
            {
                return;
            }

            duplicate.name = "Player_Removed";
            duplicate.SetActive(false);
            UnityEngine.Object.Destroy(duplicate);
        }

        private static WorldSpawnPoint FindSpawnPoint(Scene scene, string spawnId)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var spawn = FindComponentInChildren<WorldSpawnPoint>(roots[i].transform);
                if (spawn != null && spawn.SpawnId == spawnId)
                {
                    return spawn;
                }
            }

            return null;
        }

        private static GameObject FindByName(Scene scene, string objectName)
        {
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var match = FindByName(roots[i].transform, objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static GameObject FindByName(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root.gameObject;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindByName(root.GetChild(i), objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static T FindComponentInChildren<T>(Transform root) where T : Component
        {
            if (root.TryGetComponent<T>(out var component))
            {
                return component;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var match = FindComponentInChildren<T>(root.GetChild(i));
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
