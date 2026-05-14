using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Characters
{
    public interface ICharacterVisualView
    {
        void SetMotion(Vector2 input, Vector2 facing);
        void SetFlipX(bool flipX);
        void PlayAction(CharacterVisualAction action);
        void ApplyEquippedToolVisual(ToolVisualMappingDefinition mapping);
    }
}
