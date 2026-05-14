using Rootborn.Game.Family;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Characters
{
    public sealed class PixelwoodCharacterVisualView : MonoBehaviour, ICharacterVisualView
    {
        [SerializeField] private CharacterPartComposer _composer;
        [SerializeField] private CharacterPartAnimator _animator;

        private ToolVisualMappingDefinition _equippedToolVisualMapping;

        private void Awake()
        {
            if (_composer == null)
                _composer = GetComponent<CharacterPartComposer>();
            if (_animator == null)
                _animator = GetComponent<CharacterPartAnimator>();
        }

        public void SetMotion(Vector2 input, Vector2 facing)
        {
            if (_animator != null)
                _animator.SetMotion(input, facing);
        }

        public void SetFlipX(bool flipX)
        {
            if (_composer != null)
                _composer.SetFlipX(flipX);
        }

        public void PlayAction(CharacterVisualAction action)
        {
            if (action != CharacterVisualAction.Attack || _animator == null || _equippedToolVisualMapping == null)
                return;

            _animator.PlayClip(_equippedToolVisualMapping.PixelwoodAttackClip);
        }

        public void ApplyEquippedToolVisual(ToolVisualMappingDefinition mapping)
        {
            _equippedToolVisualMapping = mapping;
        }

        public void ConfigureForTests(CharacterPartAnimator animator, CharacterPartComposer composer)
        {
            _animator = animator;
            _composer = composer;
        }
    }
}
