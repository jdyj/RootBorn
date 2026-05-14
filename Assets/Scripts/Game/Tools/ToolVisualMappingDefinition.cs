using Rootborn.Game.Family;
using UnityEngine;

namespace Rootborn.Game.Tools
{
    [CreateAssetMenu(fileName = "ToolVisualMapping_New", menuName = "Rootborn/Tools/Tool Visual Mapping")]
    public sealed class ToolVisualMappingDefinition : ScriptableObject
    {
        [SerializeField] private ToolDefinition _tool;
        [SerializeField] private CharacterPartAnimationClipDefinition _pixelwoodAttackClip;
        [SerializeField] private int _spumAttackClipIndex = -1;

        public ToolDefinition Tool => _tool;
        public CharacterPartAnimationClipDefinition PixelwoodAttackClip => _pixelwoodAttackClip;
        public int SpumAttackClipIndex => _spumAttackClipIndex;

        public void ConfigureForTests(CharacterPartAnimationClipDefinition pixelwoodAttackClip)
        {
            _pixelwoodAttackClip = pixelwoodAttackClip;
        }

        public void ConfigureForTests(ToolDefinition tool, CharacterPartAnimationClipDefinition pixelwoodAttackClip)
        {
            _tool = tool;
            _pixelwoodAttackClip = pixelwoodAttackClip;
        }

        public void ConfigureSpumForTests(int attackClipIndex)
        {
            _spumAttackClipIndex = attackClipIndex;
        }
    }
}
