using UnityEngine;

namespace Rootborn.Game.DiscoveryClues
{
    [CreateAssetMenu(fileName = "ClueInterpretationPolicy_New", menuName = "Rootborn/Discovery Clues/Interpretations/Policy")]
    public sealed class ClueInterpretationPolicyDefinition : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private ClueInterpretationPolicyKind _kind = ClueInterpretationPolicyKind.NonExclusive;
        [SerializeField] private string _groupId;
        [SerializeField] private int _sequenceOrder;
        [SerializeField] private int _cooldownDays;
        [SerializeField] private bool _consumeItem;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public ClueInterpretationPolicyKind Kind => _kind;
        public string GroupId => string.IsNullOrEmpty(_groupId) ? Id : _groupId;
        public int SequenceOrder => Mathf.Max(0, _sequenceOrder);
        public int CooldownDays => Mathf.Max(0, _cooldownDays);
        public bool ConsumeItem => _consumeItem;

        public void ConfigureForTests(string id, ClueInterpretationPolicyKind kind, string groupId, int sequenceOrder, int cooldownDays, bool consumeItem)
        {
            _id = id;
            _kind = kind;
            _groupId = groupId;
            _sequenceOrder = Mathf.Max(0, sequenceOrder);
            _cooldownDays = Mathf.Max(0, cooldownDays);
            _consumeItem = consumeItem;
        }
    }
}
