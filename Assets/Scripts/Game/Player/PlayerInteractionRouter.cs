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
        private IPlayerInteractable _currentInteractable;

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
            return interactable.TryInteract(gameObject);
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

            _promptText.text = interactable.InteractionPrompt;
            var promptPosition = anchor.position + ResolvePromptOffset(interactable);
            promptPosition.z = transform.position.z;
            _promptRoot.transform.position = promptPosition;
            _promptRoot.SetActive(true);
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

                ConsiderCandidate(ResolveInteractable(collider.transform), ref best, ref bestSqr, ref bestPriority);
            }

            var behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerInteractable interactable)
                {
                    ConsiderCandidate(interactable, ref best, ref bestSqr, ref bestPriority);
                }
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

        private static IPlayerInteractable ResolveInteractable(Transform source)
        {
            var current = source;
            while (current != null)
            {
                var components = current.GetComponents<MonoBehaviour>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] is IPlayerInteractable interactable)
                    {
                        return interactable;
                    }
                }

                current = current.parent;
            }

            return null;
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
            if (renderer != null)
            {
                renderer.sortingOrder = short.MaxValue;
            }
        }
    }
}
