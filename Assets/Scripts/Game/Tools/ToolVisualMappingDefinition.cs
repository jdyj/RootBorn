using Rootborn.Game.Family;
using UnityEngine;

namespace Rootborn.Game.Tools
{
    [CreateAssetMenu(fileName = "ToolVisualMapping_New", menuName = "Rootborn/Tools/Tool Visual Mapping")]
    public sealed class ToolVisualMappingDefinition : ScriptableObject
    {
        [SerializeField] private CharacterPartAnimationClipDefinition _pixelwoodAttackClip;

        public CharacterPartAnimationClipDefinition PixelwoodAttackClip => _pixelwoodAttackClip;

        public void ConfigureForTests(CharacterPartAnimationClipDefinition pixelwoodAttackClip)
        {
            _pixelwoodAttackClip = pixelwoodAttackClip;
        }
    }
}
