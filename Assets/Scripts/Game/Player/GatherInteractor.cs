using Rootborn.Game.Knowledge;
using Rootborn.Game.Resources;
using Rootborn.Game.Tools;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rootborn.Game.Player
{
    /// <summary>
    /// 플레이어 근처 가장 가까운 ResourceNode를 E 키로 타격.
    /// KnowledgeProgress에 액션을 기록하여 도구 해금 트리거를 평가.
    /// </summary>
    public sealed class GatherInteractor : MonoBehaviour
    {
        [SerializeField] private float _interactRadius = 1.5f;
        [SerializeField] private ToolDefinition _equippedTool;

        private InputAction _interactAction;
        private KnowledgeProgress _knowledgeProgress;

        public KnowledgeProgress KnowledgeProgress => _knowledgeProgress;
        public ToolDefinition EquippedTool { get => _equippedTool; set => _equippedTool = value; }

        private void OnEnable()
        {
            _interactAction = new InputAction(type: InputActionType.Button);
            _interactAction.AddBinding("<Keyboard>/e");
            _interactAction.AddBinding("<Keyboard>/space");
            _interactAction.performed += OnInteract;
            _interactAction.Enable();
        }

        private void OnDisable()
        {
            if (_interactAction != null)
            {
                _interactAction.performed -= OnInteract;
                _interactAction.Disable();
                _interactAction.Dispose();
                _interactAction = null;
            }
        }

        public void Bind(KnowledgeProgress progress)
        {
            _knowledgeProgress = progress;
        }

        private void OnInteract(InputAction.CallbackContext ctx)
        {
            var node = FindNearestNode();
            if (node == null) return;
            if (node.IsBroken) return;

            node.Hit(_equippedTool);

            if (_knowledgeProgress != null && node.Definition != null)
            {
                _knowledgeProgress.RecordAction(
                    KnowledgeAction.HitGround,
                    _equippedTool,
                    node.Definition.Id,
                    node.Definition.SurfaceTag);
            }
        }

        private ResourceNode FindNearestNode()
        {
            var all = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            ResourceNode nearest = null;
            float bestSqr = _interactRadius * _interactRadius;
            for (int i = 0; i < all.Length; i++)
            {
                var n = all[i];
                if (n == null || n.IsBroken) continue;
                float sqr = (n.transform.position - transform.position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    nearest = n;
                }
            }
            return nearest;
        }
    }
}
