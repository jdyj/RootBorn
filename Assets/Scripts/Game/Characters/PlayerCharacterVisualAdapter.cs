using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Characters
{
    public sealed class PlayerCharacterVisualAdapter : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour _visualViewComponent;

        private ICharacterVisualView _visualView;

        private void Awake()
        {
            ResolveVisualView();
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

        public void ConfigureForTests(ICharacterVisualView visualView)
        {
            _visualView = visualView;
            _visualViewComponent = visualView as MonoBehaviour;
        }

        private ICharacterVisualView ResolveVisualView()
        {
            if (_visualView != null)
                return _visualView;

            if (_visualViewComponent != null)
                _visualView = _visualViewComponent as ICharacterVisualView;

            if (_visualView == null)
                _visualView = GetComponent<ICharacterVisualView>();

            return _visualView;
        }
    }
}
