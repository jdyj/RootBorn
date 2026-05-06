using Rootborn.Game.Quests;
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
    /// 확장 생성자는 농사 시스템(FarmGrid, Inventory, Clock, Tilemap) 경로에 사용된다.
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
        public readonly IQuestEventSink QuestEvents;

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
            QuestEvents = null;
        }

        public ToolUseContext(ToolDefinition tool, GameObject target, string surface,
            Vector3Int targetCell, Tilemap groundTilemap, Object farmGrid, Object inventory, Object clock)
            : this(tool, target, surface, targetCell, groundTilemap, farmGrid, inventory, clock, null)
        {
        }

        public ToolUseContext(ToolDefinition tool, GameObject target, string surface,
            Vector3Int targetCell, Tilemap groundTilemap, Object farmGrid, Object inventory, Object clock,
            IQuestEventSink questEvents)
        {
            Tool = tool;
            Target = target;
            Surface = surface;
            TargetCell = targetCell;
            GroundTilemap = groundTilemap;
            FarmGrid = farmGrid;
            Inventory = inventory;
            Clock = clock;
            QuestEvents = questEvents;
        }
    }
}
