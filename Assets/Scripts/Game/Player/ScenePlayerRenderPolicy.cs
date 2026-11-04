using System.Collections.Generic;
using Rootborn.Game.Interiors;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Rootborn.Game.Player
{
    // Scene-authored policy; an absent policy preserves legacy scene behavior.
    [DisallowMultipleComponent]
    public sealed class ScenePlayerRenderPolicy : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D _cameraBounds;
        [SerializeField] private int _actorBaseSortingOrder = 10000;
        [SerializeField, Min(1)] private int _unitsPerWorld = 48;
        [SerializeField] private Vector2 _footPointOffset = Vector2.zero;
        private static readonly Dictionary<int, ScenePlayerRenderPolicy> Policies = new Dictionary<int, ScenePlayerRenderPolicy>();

        public BoxCollider2D CameraBounds => _cameraBounds;
        public int ActorBaseSortingOrder => _actorBaseSortingOrder;
        public int UnitsPerWorld => Mathf.Max(1, _unitsPerWorld);

        public void Configure(BoxCollider2D cameraBounds, int actorBaseSortingOrder, int unitsPerWorld)
        {
            _cameraBounds = cameraBounds;
            _actorBaseSortingOrder = actorBaseSortingOrder;
            _unitsPerWorld = Mathf.Max(1, unitsPerWorld);
        }

        public bool Apply(GameObject player, Camera camera)
        {
            if (!isActiveAndEnabled || player == null || camera == null || _cameraBounds == null
                || !_cameraBounds.enabled || !_cameraBounds.gameObject.activeInHierarchy
                || _cameraBounds.size.x <= 0 || _cameraBounds.size.y <= 0) return false;
            var sorter = player.GetComponent<GroundDepthSort>();
            if (sorter == null) sorter = player.AddComponent<GroundDepthSort>();
            sorter.ConfigureScenePolicy(_footPointOffset, UnitsPerWorld, _actorBaseSortingOrder);

            var follow = camera.GetComponent<CameraFollow>();
            if (follow == null) follow = camera.gameObject.AddComponent<CameraFollow>();
            follow.SetTarget(player.transform);
            follow.SetBounds(_cameraBounds);
            follow.SnapToTarget();
            return true;
        }

        public static bool TryGetForScene(Scene scene, out ScenePlayerRenderPolicy policy)
        {
            policy = null;
            if (!scene.IsValid() || !Policies.TryGetValue(scene.handle, out var found) || found == null
                || !found.isActiveAndEnabled || found.gameObject.scene != scene) return false;
            policy = found; return true;
        }
        private void OnEnable()
        {
            if (gameObject.scene.IsValid()) Policies[gameObject.scene.handle] = this;
        }
        private void OnDisable() => Unregister();
        private void OnDestroy() => Unregister();
        private void Unregister()
        {
            if (Policies.TryGetValue(gameObject.scene.handle, out var current) && ReferenceEquals(current, this))
                Policies.Remove(gameObject.scene.handle);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => Policies.Clear();
    }
}
