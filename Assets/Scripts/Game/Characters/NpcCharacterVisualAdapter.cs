using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Characters
{
    public sealed class NpcCharacterVisualAdapter : MonoBehaviour
    {
        [SerializeField] private CharacterAppearanceDefinition _appearanceDefinition;
        [SerializeField] private MonoBehaviour _visualViewComponent;

        private ICharacterVisualView _visualView;

        public CharacterAppearanceDefinition AppearanceDefinition => _appearanceDefinition;

        private void Awake()
        {
            ResolveVisualView();
        }

        public void Bind(CharacterAppearanceDefinition appearanceDefinition, ICharacterVisualView visualView)
        {
            _appearanceDefinition = appearanceDefinition;
            _visualView = visualView;
            _visualViewComponent = visualView as MonoBehaviour;
        }

        public void SetMotion(Vector2 input, Vector2 facing)
        {
            ResolveVisualView()?.SetMotion(input, facing);
        }

        public void SetFlipX(bool flipX)
        {
            ResolveVisualView()?.SetFlipX(flipX);
        }

        public void PlayAction(CharacterVisualAction action)
        {
            ResolveVisualView()?.PlayAction(action);
        }

        public void ApplyEquippedToolVisual(ToolVisualMappingDefinition mapping)
        {
            ResolveVisualView()?.ApplyEquippedToolVisual(mapping);
        }

        private ICharacterVisualView ResolveVisualView()
        {
            if (_visualView != null)
            {
                return _visualView;
            }

            if (_visualViewComponent != null)
            {
                _visualView = _visualViewComponent as ICharacterVisualView;
            }

            if (_visualView == null)
            {
                _visualView = GetComponent<ICharacterVisualView>();
            }

            return _visualView;
        }
    }
}
