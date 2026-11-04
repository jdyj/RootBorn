using Rootborn.Game.Family;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Characters.Spum
{
    public sealed class SpumCharacterVisualView : MonoBehaviour, ICharacterVisualView
    {
        public const float DefaultVisualScale = 32f / 49f;
        private const string IdleState = "IDLE";
        private const string MoveState = "MOVE";

        [SerializeField] private Animator _animator;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private SpriteRenderer[] _spriteRenderers = System.Array.Empty<SpriteRenderer>();

        private ToolVisualMappingDefinition _equippedToolVisualMapping;
        private bool _ownsVisualRoot;

        public string CurrentMotionState { get; private set; } = IdleState;
        public int LastPlayedActionClipIndex { get; private set; } = -1;

        // Queried during backend selection, not from the frame-by-frame motion path.
        public bool HasRenderableVisual
        {
            get
            {
                ResolveReferences();
                if (!isActiveAndEnabled || !_ownsVisualRoot || _spriteRenderers == null)
                    return false;

                for (int i = 0; i < _spriteRenderers.Length; i++)
                {
                    var renderer = _spriteRenderers[i];
                    if (OwnsRenderer(renderer) && renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sprite != null)
                        return true;
                }

                return false;
            }
        }

        private void Reset()
        {
            ApplyDefaultScale();
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyDefaultScale();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyDefaultScale();
        }

        public void SetMotion(Vector2 input, Vector2 facing)
        {
            CurrentMotionState = input.sqrMagnitude > 0.01f ? MoveState : IdleState;
            if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y))
                SetFlipX(facing.x > 0f);
        }

        public void SetFlipX(bool flipX)
        {
            if (!_ownsVisualRoot || _spriteRenderers == null)
                return;

            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                var spriteRenderer = _spriteRenderers[i];
                if (OwnsRenderer(spriteRenderer))
                    spriteRenderer.flipX = flipX;
            }
        }

        public void PlayAction(CharacterVisualAction action)
        {
            if (action != CharacterVisualAction.Attack || _equippedToolVisualMapping == null)
                return;

            LastPlayedActionClipIndex = _equippedToolVisualMapping.SpumAttackClipIndex;
        }

        public void ApplyEquippedToolVisual(ToolVisualMappingDefinition mapping)
        {
            _equippedToolVisualMapping = mapping;
        }

        public void ConfigureForTests(Animator animator, Transform visualRoot, SpriteRenderer[] spriteRenderers)
        {
            _animator = animator;
            _visualRoot = visualRoot != null ? visualRoot : transform;
            _spriteRenderers = spriteRenderers ?? System.Array.Empty<SpriteRenderer>();
            ResolveReferences();
            ApplyDefaultScale();
        }

        private void ResolveReferences()
        {
            if (_visualRoot == null)
                _visualRoot = transform;

            _ownsVisualRoot = CanOwnVisualRoot(_visualRoot);
            if (!_ownsVisualRoot)
                return;

            if (_animator == null)
                _animator = _visualRoot.GetComponentInChildren<Animator>();
            if (_spriteRenderers == null || _spriteRenderers.Length == 0)
                _spriteRenderers = _visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void ApplyDefaultScale()
        {
            Transform target = _visualRoot != null ? _visualRoot : transform;
            if (CanOwnVisualRoot(target))
                target.localScale = Vector3.one * DefaultVisualScale;
        }

        private bool OwnsRenderer(SpriteRenderer renderer)
        {
            return _ownsVisualRoot && renderer != null && _visualRoot != null
                && (renderer.transform == _visualRoot || renderer.transform.IsChildOf(_visualRoot));
        }

        private static bool CanOwnVisualRoot(Transform candidate)
        {
            if (candidate == null || candidate.GetComponent<CharacterPartComposer>() != null)
                return false;

            var composer = candidate.GetComponentInParent<CharacterPartComposer>();
            if (composer != null)
            {
                var layers = composer.Layers;
                for (int i = 0; i < layers.Length; i++)
                {
                    var layer = layers[i] != null ? layers[i].Renderer : null;
                    if (layer != null && (candidate == layer.transform || candidate.IsChildOf(layer.transform)))
                        return false;
                }
            }

            return true;
        }
    }
}
