using Rootborn.Game.Knowledge;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Generation
{
    [CreateAssetMenu(fileName = "Gen_New", menuName = "Rootborn/Generation/Generation Profile")]
    public sealed class GenerationProfile : ScriptableObject
    {
        [SerializeField] private int _generationIndex = 1;
        [SerializeField] private string _displayKey;
        [SerializeField] private float _lifetimeSec = 1800f;
        [SerializeField] private KnowledgeNode[] _startingKnowledge = System.Array.Empty<KnowledgeNode>();
        [SerializeField] private ToolDefinition[] _startingInventory = System.Array.Empty<ToolDefinition>();
        [SerializeField] private GenerationProfile _nextGeneration;

        public int GenerationIndex => _generationIndex;
        public string DisplayKey => _displayKey;
        public float LifetimeSec => _lifetimeSec;
        public KnowledgeNode[] StartingKnowledge => _startingKnowledge;
        public ToolDefinition[] StartingInventory => _startingInventory;
        public GenerationProfile NextGeneration => _nextGeneration;
    }
}
