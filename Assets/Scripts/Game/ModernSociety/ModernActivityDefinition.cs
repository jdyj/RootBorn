using UnityEngine;

namespace Rootborn.Game.ModernSociety
{
    [CreateAssetMenu(fileName = "ModernActivity_New", menuName = "Rootborn/Modern Society/Activity")]
    public sealed class ModernActivityDefinition : ScriptableObject
    {
        [SerializeField] private ModernActivityKind _kind;
        [SerializeField] private int _energyCost;
        [SerializeField] private int _moneyDelta;
        [SerializeField] private int _anxietyDelta;
        [SerializeField] private int _knowledgeDelta;
        [SerializeField] private int _relationshipsDelta;

        public ModernActivityKind Kind => _kind;
        public int EnergyCost => _energyCost;
        public int MoneyDelta => _moneyDelta;
        public int AnxietyDelta => _anxietyDelta;
        public int KnowledgeDelta => _knowledgeDelta;
        public int RelationshipsDelta => _relationshipsDelta;
    }
}
