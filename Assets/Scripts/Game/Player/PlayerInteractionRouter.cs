using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.Player
{
    public interface IPlayerInteractable
    {
        string InteractionPrompt { get; }
        Vector3 InteractionPromptOffset { get; }
        Transform InteractionTransform { get; }
        bool CanInteract(GameObject player);
        bool TryInteract(GameObject player);
    }

    // Tells the player why pressing the key did nothing, when the reason is worth knowing.
    public interface IPlayerInteractionFailureExplainer
    {
        string ExplainFailure(GameObject player);
    }

    public interface IPlayerInteractionPromptAppearance
    {
        bool TryGetInteractionPromptColor(out Color color);
    }

    public interface IPrioritizedPlayerInteractable
    {
        int InteractionPriority { get; }
    }

    [DisallowMultipleComponent]
    public sealed class PlayerInteractionRouter : MonoBehaviour
    {
        [SerializeField] private float _interactionRadius = 1.75f;
        [SerializeField] private Vector3 _defaultPromptOffset = new Vector3(0f, 1.1f, 0f);

        private TextMesh _promptText;
        private GameObject _promptRoot;
        private MeshRenderer _promptRenderer;
        private Camera _promptCamera;
        private IPlayerInteractable _currentInteractable;
        private const float PromptScreenPadding = 8f;
        private readonly List<Rect> _promptReservations = new List<Rect>(4);
        private readonly List<Vector2> _promptFreeIntervals = new List<Vector2>(8);
        private readonly Vector3[] _promptScreenCorners = new Vector3[8];

        private const float FailureMessageSeconds = 2.5f;
        private string _failureMessage;
        private float _failureMessageUntil;
        private IPlayerInteractable _failureMessageTarget;

        public IPlayerInteractable CurrentInteractable => _currentInteractable;
        public bool PromptVisible => _promptRoot != null && _promptRoot.activeSelf;
        public string PromptText => _promptText != null ? _promptText.text : string.Empty;

        private void Awake()
        {
            EnsurePrompt();
            HidePrompt();
        }

        private void Update()
        {
            RefreshPromptNow();
        }

        public void ConfigureForTests(float interactionRadius)
        {
            _interactionRadius = interactionRadius;
        }

        public bool HasAvailableTarget()
        {
            return FindNearestInteractable() != null;
        }

        public bool TryInteractWithNearest()
        {
            var interactable = FindNearestInteractable();
            _currentInteractable = interactable;
            if (interactable == null)
            {
                RefreshPrompt(null);
                return false;
            }

            RefreshPrompt(interactable);
            bool interacted = interactable.TryInteract(gameObject);
            if (!interacted && interactable is IPlayerInteractionFailureExplainer explainer)
            {
                _failureMessage = explainer.ExplainFailure(gameObject);
                _failureMessageUntil = UnityEngine.Time.realtimeSinceStartup + FailureMessageSeconds;
                _failureMessageTarget = interactable;
                RefreshPrompt(interactable);
            }
            return interacted;
        }

        public void RefreshPromptNow()
        {
            var interactable = FindNearestInteractable();
            _currentInteractable = interactable;
            RefreshPrompt(interactable);
        }

        private void RefreshPrompt(IPlayerInteractable interactable)
        {
            if (interactable == null || string.IsNullOrEmpty(interactable.InteractionPrompt))
            {
                HidePrompt();
                return;
            }

            EnsurePrompt();
            var anchor = interactable.InteractionTransform;
            if (anchor == null)
            {
                HidePrompt();
                return;
            }

            bool explaining = ReferenceEquals(interactable, _failureMessageTarget) && !string.IsNullOrEmpty(_failureMessage) && UnityEngine.Time.realtimeSinceStartup < _failureMessageUntil;
            _promptText.text = explaining ? _failureMessage : interactable.InteractionPrompt;
            _promptText.color = interactable is IPlayerInteractionPromptAppearance appearance &&
                appearance.TryGetInteractionPromptColor(out var color)
                ? color
                : new Color(1f, 0.96f, 0.68f, 1f);
            var promptPosition = anchor.position + ResolvePromptOffset(interactable);
            promptPosition.z = transform.position.z;
            _promptRoot.transform.position = promptPosition;
            _promptRoot.SetActive(true);
            FitPromptToCamera();
        }

        // World anchors keep their authored pivot; lettering stays readable at both indoor and outdoor zoom.
        private void FitPromptToCamera()
        {
            if (_promptCamera == null || !_promptCamera.isActiveAndEnabled)
                _promptCamera = Camera.main;
            if (_promptCamera == null || _promptRenderer == null || _promptCamera.pixelHeight <= 0) return;

            var worldSize = _promptRenderer.bounds.size;
            if (worldSize.y <= 0.0001f || worldSize.x <= 0.0001f) return;
            float worldHeight = _promptCamera.orthographic
                ? 2f * _promptCamera.orthographicSize
                : 2f * Mathf.Abs(Vector3.Dot(_promptRoot.transform.position - _promptCamera.transform.position,
                    _promptCamera.transform.forward)) * Mathf.Tan(_promptCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float worldPerPixel = worldHeight / _promptCamera.pixelHeight;
            float widthLimit = Mathf.Min(360f, _promptCamera.pixelWidth * 0.85f);
            float ratio = Mathf.Min(24f * worldPerPixel / worldSize.y, widthLimit * worldPerPixel / worldSize.x);
            if (!float.IsNaN(ratio) && !float.IsInfinity(ratio) && ratio > 0f)
                _promptRoot.transform.localScale *= ratio;
            FitPromptAroundScreenReservations();
        }

        private void FitPromptAroundScreenReservations()
        {
            InteractionPromptScreenReservations.CollectRects(_promptReservations);
            var promptTransform = _promptRoot.transform;
            var authoredPosition = promptTransform.position;
            var cameraFitScale = promptTransform.localScale;
            float authoredDepth = _promptCamera.WorldToScreenPoint(authoredPosition).z;

            // Reproject after scaling: font bearings can put mesh center away from the anchor.
            // Bounded retries handle that measurement without changing the authored Y or Z.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (!TryGetPromptScreenRect(out var actual) ||
                    !InteractionPromptScreenReservations.TryFitHorizontal(actual, _promptCamera.pixelRect,
                        _promptReservations, PromptScreenPadding, _promptFreeIntervals, out var fitted, out float scale))
                    break;

                // A collapsed or non-finite measurement would poison the persistent prompt for every later scene.
                if (!float.IsFinite(scale) || scale <= 0f || !float.IsFinite(fitted.center.x) || !float.IsFinite(actual.center.x)) break;
                if (scale < 1f && actual.width - fitted.width > 0.01f)
                {
                    promptTransform.localScale *= scale;
                    continue;
                }

                float horizontalDelta = fitted.center.x - actual.center.x;
                if (Mathf.Abs(horizontalDelta) > 0.01f)
                {
                    var anchorOnScreen = _promptCamera.WorldToScreenPoint(promptTransform.position);
                    anchorOnScreen.x += horizontalDelta;
                    if (!float.IsFinite(anchorOnScreen.x) || !float.IsFinite(anchorOnScreen.y) || !float.IsFinite(anchorOnScreen.z)) break;
                    var shifted = _promptCamera.ScreenToWorldPoint(anchorOnScreen);
                    if (float.IsNaN(shifted.x) || float.IsInfinity(shifted.x)) break;
                    promptTransform.position = new Vector3(shifted.x, authoredPosition.y, authoredPosition.z);
                }

                if (!TryGetPromptScreenRect(out var projected)) break;
                float depth = _promptCamera.WorldToScreenPoint(promptTransform.position).z;
                if (Mathf.Abs(projected.center.y - actual.center.y) > 0.01f || Mathf.Abs(depth - authoredDepth) > 0.0001f)
                    break;
                if (InteractionPromptScreenReservations.TryFitHorizontal(projected, _promptCamera.pixelRect,
                        _promptReservations, PromptScreenPadding, _promptFreeIntervals, out var verified, out float verifiedScale) &&
                    verifiedScale >= 0.9999f && Mathf.Abs(verified.center.x - projected.center.x) <= 0.05f)
                    return;
            }

            // An unsupported camera mapping or fully covered row retains the prior presentation.
            promptTransform.position = authoredPosition;
            promptTransform.localScale = cameraFitScale;
        }

        private bool TryGetPromptScreenRect(out Rect rectangle)
        {
            rectangle = default;
            var bounds = _promptRenderer.bounds;
            var min = bounds.min;
            var max = bounds.max;
            float left = float.PositiveInfinity, bottom = float.PositiveInfinity;
            float right = float.NegativeInfinity, top = float.NegativeInfinity;
            for (int i = 0; i < _promptScreenCorners.Length; i++)
            {
                var world = new Vector3((i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                var point = _promptCamera.WorldToScreenPoint(world);
                _promptScreenCorners[i] = point;
                if (point.z <= 0f || float.IsNaN(point.x) || float.IsInfinity(point.x) ||
                    float.IsNaN(point.y) || float.IsInfinity(point.y)) return false;
                left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x);
                bottom = Mathf.Min(bottom, point.y); top = Mathf.Max(top, point.y);
            }
            rectangle = Rect.MinMaxRect(left, bottom, right, top);
            return rectangle.width > 0f && rectangle.height > 0f;
        }

        private Vector3 ResolvePromptOffset(IPlayerInteractable interactable)
        {
            var offset = interactable.InteractionPromptOffset;
            return offset.sqrMagnitude > 0.0001f ? offset : _defaultPromptOffset;
        }

        private void HidePrompt()
        {
            _currentInteractable = null;
            if (_promptRoot != null)
            {
                _promptRoot.SetActive(false);
            }
        }

        private IPlayerInteractable FindNearestInteractable()
        {
            if (Rootborn.Game.Common.UiGameplayInputLock.IsBlocked) return null;
            IPlayerInteractable best = null;
            float bestSqr = _interactionRadius * _interactionRadius;
            int bestPriority = int.MinValue;

            var colliders = Physics2D.OverlapCircleAll(transform.position, _interactionRadius);
            for (int i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null || collider.gameObject == gameObject)
                {
                    continue;
                }

                ConsiderCandidatesFrom(collider.transform, ref best, ref bestSqr, ref bestPriority);
            }
            return best;
        }

        private void ConsiderCandidate(IPlayerInteractable interactable, ref IPlayerInteractable best, ref float bestSqr, ref int bestPriority)
        {
            if (interactable == null || !interactable.CanInteract(gameObject))
            {
                return;
            }

            var anchor = interactable.InteractionTransform;
            if (anchor == null || anchor.gameObject == gameObject)
            {
                return;
            }

            float sqr = (anchor.position - transform.position).sqrMagnitude;
            if (sqr > _interactionRadius * _interactionRadius)
            {
                return;
            }

            int priority = ResolvePriority(interactable);
            if (priority > bestPriority || (priority == bestPriority && sqr <= bestSqr))
            {
                bestPriority = priority;
                bestSqr = sqr;
                best = interactable;
            }
        }

        private static int ResolvePriority(IPlayerInteractable interactable)
        {
            return interactable is IPrioritizedPlayerInteractable prioritized ? prioritized.InteractionPriority : 0;
        }

        private void ConsiderCandidatesFrom(Transform source, ref IPlayerInteractable best, ref float bestSqr, ref int bestPriority)
        {
            for (var current = source; current != null; current = current.parent)
            {
                var components = current.GetComponents<MonoBehaviour>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] != null && components[i].isActiveAndEnabled && components[i] is IPlayerInteractable interactable)
                        ConsiderCandidate(interactable, ref best, ref bestSqr, ref bestPriority);
                }
            }
        }

        private void EnsurePrompt()
        {
            if (_promptRoot != null && _promptText != null)
            {
                return;
            }

            _promptRoot = new GameObject("[InteractionPrompt]");
            _promptRoot.transform.SetParent(transform, false);
            _promptText = _promptRoot.AddComponent<TextMesh>();
            _promptText.anchor = TextAnchor.MiddleCenter;
            _promptText.alignment = TextAlignment.Center;
            _promptText.characterSize = 0.18f;
            _promptText.fontSize = 48;
            _promptText.color = new Color(1f, 0.96f, 0.68f, 1f);

            var renderer = _promptRoot.GetComponent<MeshRenderer>();
            _promptRenderer = renderer;
            if (renderer != null)
            {
                renderer.sortingOrder = short.MaxValue;
            }
        }
    }
}
