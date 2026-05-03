using System.Collections.Generic;
using UnityEngine;

namespace Rootborn.Game.Status
{
    public sealed class PlayerStatus : MonoBehaviour
    {
        [SerializeField] private List<StatusEffectDefinition> _trackedStatuses = new List<StatusEffectDefinition>();

        private readonly Dictionary<StatusEffectDefinition, StatusValue> _values = new Dictionary<StatusEffectDefinition, StatusValue>();

        public IReadOnlyDictionary<StatusEffectDefinition, StatusValue> Values => _values;

        private void Awake()
        {
            for (int i = 0; i < _trackedStatuses.Count; i++)
            {
                var def = _trackedStatuses[i];
                if (def != null && !_values.ContainsKey(def))
                {
                    _values.Add(def, new StatusValue(def));
                }
            }
        }

        private void Update()
        {
            float dt = UnityEngine.Time.deltaTime;
            foreach (var pair in _values)
            {
                pair.Value.Tick(dt);
            }
        }

        public StatusValue Get(StatusEffectDefinition def)
        {
            return _values.TryGetValue(def, out var v) ? v : null;
        }

        public float WorstPenalty01()
        {
            float worst = 0f;
            foreach (var pair in _values)
            {
                float p = pair.Value.Penalty01();
                if (p > worst) worst = p;
            }
            return worst;
        }
    }
}
