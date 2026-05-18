using UnityEngine;

namespace Rootborn.Game.StudentLife
{
    [CreateAssetMenu(fileName = "DailyEventKind_New", menuName = "Rootborn/Student Life/Daily Events/Kind")]
    public sealed class DailyEventKindDefinition : StudentLifeDefinitionBase
    {
        [SerializeField] private bool _isOptional = true;
        [SerializeField] private bool _isDeferrable;
        [SerializeField] private bool _isTimed;
        [SerializeField] private bool _isRequired;
        [SerializeField] private bool _isRepeatable;
        [SerializeField] private bool _isConditional;
        [SerializeField] private int _deferDays = 1;
        [SerializeField] private int _dueTimeMinutes;

        public bool IsOptional => _isOptional;
        public bool IsDeferrable => _isDeferrable;
        public bool IsTimed => _isTimed;
        public bool IsRequired => _isRequired;
        public bool IsRepeatable => _isRepeatable;
        public bool IsConditional => _isConditional;
        public int DeferDays => Mathf.Max(0, _deferDays);
        public int DueTimeMinutes => Mathf.Max(0, _dueTimeMinutes);

        public void ConfigureForTests(string id, string displayNameKey, bool isOptional, bool isDeferrable, bool isTimed, bool isRequired, bool isRepeatable, bool isConditional, int deferDays, int dueTimeMinutes)
        {
            ConfigureForTests(id, displayNameKey);
            _isOptional = isOptional;
            _isDeferrable = isDeferrable;
            _isTimed = isTimed;
            _isRequired = isRequired;
            _isRepeatable = isRepeatable;
            _isConditional = isConditional;
            _deferDays = deferDays;
            _dueTimeMinutes = dueTimeMinutes;
        }
    }
}
