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

        public string CurrentMotionState { get; private set; } = IdleState;
        public int LastPlayedActionClipIndex { get; private set; } = -1;

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
            if (_spriteRenderers == null)
                return;

            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                var spriteRenderer = _spriteRenderers[i];
                if (spriteRenderer != null)
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
            ApplyDefaultScale();
        }

        private void ResolveReferences()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            if (_visualRoot == null)
                _visualRoot = transform;
            if (_spriteRenderers == null || _spriteRenderers.Length == 0)
                _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void ApplyDefaultScale()
        {
            Transform target = _visualRoot != null ? _visualRoot : transform;
            target.localScale = Vector3.one * DefaultVisualScale;
        }
    }
}
