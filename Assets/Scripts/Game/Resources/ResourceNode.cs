using System;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Game.Resources
{
    public sealed class ResourceNode : MonoBehaviour
    {
        [SerializeField] private ResourceNodeDefinition _definition;
        [SerializeField] private SpriteRenderer _renderer;

        private float _accumulatedPower;

        public ResourceNodeDefinition Definition => _definition;
        public bool IsBroken { get; private set; }

        public event Action<ResourceNode, ToolDefinition> OnGathered;
        public event Action<ResourceNode> OnBroken;

        private void Start()
        {
            if (_renderer != null && _definition != null && _definition.Sprite != null)
            {
                _renderer.sprite = _definition.Sprite;
            }
        }

        public void Hit(ToolDefinition tool)
        {
            if (IsBroken || _definition == null) return;

            float power = _definition.ComputeEffectivePower(tool);
            _accumulatedPower += power;

            OnGathered?.Invoke(this, tool);

            if (_accumulatedPower >= _definition.BaseHitsToBreak)
            {
                IsBroken = true;
                OnBroken?.Invoke(this);
            }
        }
    }
}
