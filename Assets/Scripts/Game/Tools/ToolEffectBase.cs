using UnityEngine;
using UnityEngine.Tilemaps;

namespace Rootborn.Game.Tools
{
    public abstract class ToolEffectBase : ScriptableObject
    {
        public abstract void Apply(in ToolUseContext ctx);
    }

    /// <summary>
    /// 도구 효과 적용 컨텍스트. 기존 3-arg 생성자(Tool/Target/Surface)는 ResourceNode 경로 호환 유지.
    /// 7-arg 확장 생성자는 농사 시스템 (FarmGrid, Inventory, Clock, Tilemap) 경로용.
    /// 확장 필드는 Object 참조이므로 미사용 시 null — 각 effect 가 자체 precondition check 후 no-op.
    /// </summary>
    public readonly struct ToolUseContext
    {
        public readonly ToolDefinition Tool;
        public readonly GameObject Target;
        public readonly string Surface;
        public readonly Vector3Int TargetCell;
        public readonly Tilemap GroundTilemap;
        public readonly Object FarmGrid;
        public readonly Object Inventory;
        public readonly Object Clock;

        public ToolUseContext(ToolDefinition tool, GameObject target, string surface)
        {
            Tool = tool;
            Target = target;
            Surface = surface;
            TargetCell = default;
            GroundTilemap = null;
            FarmGrid = null;
            Inventory = null;
            Clock = null;
        }

        public ToolUseContext(ToolDefinition tool, GameObject target, string surface,
            Vector3Int targetCell, Tilemap groundTilemap, Object farmGrid, Object inventory, Object clock)
        {
            Tool = tool;
            Target = target;
            Surface = surface;
            TargetCell = targetCell;
            GroundTilemap = groundTilemap;
            FarmGrid = farmGrid;
            Inventory = inventory;
            Clock = clock;
        }
    }
}
