using Rootborn.Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rootborn.UI.Interiors
{
    public sealed class HousePlacementCameraController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _minOrthographicSize = 4f;
        [SerializeField] private float _maxOrthographicSize = 14f;
        [SerializeField] private float _zoomStep = 1f;
        [SerializeField] private float _keyboardZoomUnitsPerSecond = 8f;
        [SerializeField] private float _panUnitsPerSecond = 10f;
        [SerializeField] private float _mouseDragPanScale = 1f;
        [SerializeField] private float _fullViewPadding = 1.08f;

        private CameraFollow _follow;
        private bool _restoreFollowEnabled;
        private bool _dragging;
        private Vector2 _lastMousePosition;
        private Bounds _bounds = new Bounds(Vector3.zero, new Vector3(20f, 12f, 1f));

        public void Configure(Camera camera, Bounds bounds)
        {
            _camera = camera;
            _bounds = bounds;
            _follow = _camera != null ? _camera.GetComponent<CameraFollow>() : null;
        }

        public void BeginPlacementControl()
        {
            if (_follow == null)
            {
                return;
            }

            _restoreFollowEnabled = _follow.enabled;
            _follow.enabled = false;
        }

        public void EndPlacementControl()
        {
            if (_follow != null)
            {
                _follow.enabled = _restoreFollowEnabled;
            }
        }

        public void FullView()
        {
            if (_camera == null || !_camera.orthographic)
            {
                return;
            }

            _camera.orthographicSize = ClampOrthographicSize(CalculateFullViewOrthographicSize(_bounds, _camera.aspect, _fullViewPadding), _minOrthographicSize, _maxOrthographicSize);
            _camera.transform.position = ClampPosition(new Vector3(_bounds.center.x, _bounds.center.y, _camera.transform.position.z), _bounds, _camera.orthographicSize, _camera.aspect);
        }

        private void Update()
        {
            if (_camera == null || !_camera.orthographic)
            {
                return;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            var pan = Vector2.zero;
            float keyboardZoom = 0f;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) pan.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) pan.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) pan.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) pan.y += 1f;
                if (keyboard.equalsKey.isPressed || keyboard.numpadPlusKey.isPressed) keyboardZoom -= 1f;
                if (keyboard.minusKey.isPressed || keyboard.numpadMinusKey.isPressed) keyboardZoom += 1f;
                if (keyboard.fKey.wasPressedThisFrame) FullView();
            }

            if (Mathf.Abs(keyboardZoom) > 0f)
            {
                Zoom(keyboardZoom * _zoomStep * _keyboardZoomUnitsPerSecond * Time.unscaledDeltaTime);
            }

            if (mouse != null)
            {
                var scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    Zoom(scroll > 0f ? -_zoomStep : _zoomStep);
                }

                ApplyMouseDragPan(mouse);
            }
            else
            {
                _dragging = false;
            }

            if (pan.sqrMagnitude > 0f)
            {
                var delta = new Vector3(pan.normalized.x, pan.normalized.y, 0f) * (_panUnitsPerSecond * Time.unscaledDeltaTime);
                _camera.transform.position = ClampPosition(_camera.transform.position + delta, _bounds, _camera.orthographicSize, _camera.aspect);
            }
        }

        private void ApplyMouseDragPan(Mouse mouse)
        {
            bool dragPressed = mouse.rightButton.isPressed || mouse.middleButton.isPressed;
            var currentMousePosition = mouse.position.ReadValue();
            if (dragPressed && !_dragging)
            {
                _dragging = true;
                _lastMousePosition = currentMousePosition;
                return;
            }

            if (dragPressed && _dragging)
            {
                var pixelDelta = currentMousePosition - _lastMousePosition;
                _lastMousePosition = currentMousePosition;
                if (pixelDelta.sqrMagnitude <= 0f)
                {
                    return;
                }

                var worldUnitsPerPixel = (_camera.orthographicSize * 2f) / Mathf.Max(1f, _camera.pixelHeight);
                var dragDelta = new Vector3(-pixelDelta.x, -pixelDelta.y, 0f) * (worldUnitsPerPixel * _mouseDragPanScale);
                _camera.transform.position = ClampPosition(_camera.transform.position + dragDelta, _bounds, _camera.orthographicSize, _camera.aspect);
                return;
            }

            _dragging = false;
        }

        private void Zoom(float delta)
        {
            _camera.orthographicSize = ClampOrthographicSize(_camera.orthographicSize + delta, _minOrthographicSize, _maxOrthographicSize);
            _camera.transform.position = ClampPosition(_camera.transform.position, _bounds, _camera.orthographicSize, _camera.aspect);
        }

        public static float ClampOrthographicSize(float size, float min, float max)
        {
            return Mathf.Clamp(size, Mathf.Min(min, max), Mathf.Max(min, max));
        }

        public static float CalculateFullViewOrthographicSize(Bounds bounds, float aspect, float padding)
        {
            var safeAspect = Mathf.Max(0.01f, aspect);
            var halfHeight = bounds.size.y * 0.5f;
            var halfWidthAsHeight = bounds.size.x / safeAspect * 0.5f;
            return Mathf.Max(halfHeight, halfWidthAsHeight) * Mathf.Max(1f, padding);
        }

        public static Vector3 ClampPosition(Vector3 position, Bounds bounds, float orthographicSize, float aspect)
        {
            var halfHeight = Mathf.Max(0f, orthographicSize);
            var halfWidth = halfHeight * Mathf.Max(0.01f, aspect);
            var minX = bounds.min.x + halfWidth;
            var maxX = bounds.max.x - halfWidth;
            var minY = bounds.min.y + halfHeight;
            var maxY = bounds.max.y - halfHeight;
            position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : bounds.center.x;
            position.y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : bounds.center.y;
            return position;
        }
    }
}