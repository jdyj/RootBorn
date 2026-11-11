using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;

namespace Rootborn.Core.Gathering
{
    /// <summary>자원 하나의 지금 상태(원본 ResourceNode의 쌓인 힘과 부서짐). 화면이 노드마다 하나 갖는다.</summary>
    public sealed class ResourceNodeState
    {
        public float AccumulatedPower { get; internal set; }
        public bool IsBroken { get; internal set; }
    }

    public enum GatherHitResult
    {
        /// <summary>없는 노드이거나 이미 부서졌다. 아무것도 바뀌지 않는다.</summary>
        Ignored,

        /// <summary>힘이 쌓였다. 퀘스트의 채집 이벤트는 치기마다 낸다(원본과 같다).</summary>
        Hit,

        /// <summary>이번에 부서졌고 나온 물건을 받았다.</summary>
        Broken,

        /// <summary>부술 만큼 쳤지만 나온 물건이 가방에 다 들어가지 않아 부수지 않았다. 아무것도 받지 않았다.</summary>
        InventoryFull,
    }

    /// <summary>자원 치기(원본 GatherInteractor.DoInteract의 자원 부분과 ResourceNode.Hit).</summary>
    public static class ResourceGathering
    {
        /// <param name="toolId">장착한 도구. 비면 맨손.</param>
        /// <param name="findItem">나온 물건 id → 아이템. 모르는 id는 건너뛴다(원본과 같다).</param>
        /// <param name="inventory">null이면 부서지기만 하고 아무것도 받지 않는다(원본과 같다).</param>
        public static GatherHitResult Hit(ResourceNodeDefinition node, ResourceNodeState state, string toolId, float toolPowerMultiplier,
            ItemInventory inventory, Func<string, ItemDefinition> findItem, Random rng)
        {
            if (node == null || state == null || state.IsBroken) return GatherHitResult.Ignored;
            state.AccumulatedPower += node.ComputeEffectivePower(toolId, toolPowerMultiplier);
            if (state.AccumulatedPower < node.BaseHitsToBreak) return GatherHitResult.Hit;
            if (inventory == null)
            {
                state.IsBroken = true;
                return GatherHitResult.Broken;
            }

            var grants = RollDrops(node, findItem, rng);
            return inventory.TryAddAll(grants, () => state.IsBroken = true) ? GatherHitResult.Broken : GatherHitResult.InventoryFull;
        }

        private static List<InventoryGrant> RollDrops(ResourceNodeDefinition node, Func<string, ItemDefinition> findItem, Random rng)
        {
            var grants = new List<InventoryGrant>(node.Drops.Count);
            for (int i = 0; i < node.Drops.Count; i++)
            {
                var drop = node.Drops[i];
                int count = drop.Roll(rng);
                if (count <= 0 || drop.ResourceId.Length == 0) continue;
                var item = findItem?.Invoke(drop.ResourceId);
                if (item != null) grants.Add(new InventoryGrant(item, count));
            }

            return grants;
        }
    }
}
