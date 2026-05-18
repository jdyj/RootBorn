using UnityEngine;

namespace Rootborn.Game.Player
{
    /// <summary>
    /// Follows the configured target and optionally clamps the camera viewport inside a 2D bounds collider.
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private BoxCollider2D _bounds;
        [SerializeField] private float _smoothTime = 0.15f;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, -10f);

        private Camera _camera;
        private Vector3 _velocity;

        public void SetTarget(Transform target) => _target = target;

        public void SetBounds(BoxCollider2D bounds) => _bounds = bounds;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 desired = _target.position + _offset;
            desired = ClampToBounds(desired);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, _smoothTime);
            transform.position = ClampToBounds(transform.position);
        }

        private Vector3 ClampToBounds(Vector3 position)
        {
            if (_bounds == null || _camera == null || !_camera.orthographic)
            {
                return position;
            }

            Bounds bounds = _bounds.bounds;
            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;

            float minX = bounds.min.x + halfWidth;
            float maxX = bounds.max.x - halfWidth;
            float minY = bounds.min.y + halfHeight;
            float maxY = bounds.max.y - halfHeight;

            position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : bounds.center.x;
            position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : bounds.center.y;
            return position;
        }
    }
}
