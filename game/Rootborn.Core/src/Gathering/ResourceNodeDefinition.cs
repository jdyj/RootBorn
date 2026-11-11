using System;
using System.Collections.Generic;

namespace Rootborn.Core.Gathering
{
    /// <summary>자원을 부쉈을 때 나오는 물건 하나(원본 Assets/Scripts/Game/Resources/ResourceDrop.cs).</summary>
    public readonly struct ResourceDrop
    {
        public ResourceDrop(string resourceId, int minCount, int maxCount)
        {
            ResourceId = resourceId ?? string.Empty;
            MinCount = minCount;
            MaxCount = maxCount;
        }

        /// <summary>나오는 아이템 id.</summary>
        public string ResourceId { get; }

        public int MinCount { get; }
        public int MaxCount { get; }

        /// <summary>최소(0 이상)~최대(최소 이상) 사이. 난수가 없으면 최대.</summary>
        public int Roll(Random rng)
        {
            int min = Math.Max(0, MinCount);
            int max = Math.Max(min, MaxCount);
            return rng == null ? max : rng.Next(min, max + 1);
        }
    }

    /// <summary>
    /// 마을의 나무 같은 채집 자원(원본 Assets/Scripts/Game/Resources/ResourceNodeDefinition.cs).
    /// 도구는 id와 힘으로만 받는다. 지금 게임은 도구를 장착할 방법이 없어 늘 맨손이다.
    /// </summary>
    public sealed class ResourceNodeDefinition
    {
        private readonly ResourceDrop[] _drops;

        public ResourceNodeDefinition(string id, string displayKey, float baseHitsToBreak, string preferredToolId, float bareHandPenaltyMul,
            IReadOnlyList<ResourceDrop> drops, string surfaceTag, bool isWalkable, float colliderWidth, float colliderHeight, string sprite = null)
        {
            Id = id ?? string.Empty;
            DisplayKey = displayKey ?? string.Empty;
            BaseHitsToBreak = baseHitsToBreak;
            PreferredToolId = preferredToolId ?? string.Empty;
            BareHandPenaltyMul = bareHandPenaltyMul;
            _drops = drops == null ? Array.Empty<ResourceDrop>() : new List<ResourceDrop>(drops).ToArray();
            SurfaceTag = surfaceTag ?? string.Empty;
            IsWalkable = isWalkable;
            ColliderWidth = colliderWidth;
            ColliderHeight = colliderHeight;
            Sprite = sprite ?? string.Empty;
        }

        public string Id { get; }
        public string DisplayKey { get; }

        /// <summary>쌓인 힘이 이 값에 닿으면 부서진다.</summary>
        public float BaseHitsToBreak { get; }

        public string PreferredToolId { get; }
        public float BareHandPenaltyMul { get; }
        public IReadOnlyList<ResourceDrop> Drops => _drops;
        public string SurfaceTag { get; }

        /// <summary>false면 화면이 ColliderWidth×ColliderHeight(월드 칸) 충돌로 막는다.</summary>
        public bool IsWalkable { get; }

        public float ColliderWidth { get; }
        public float ColliderHeight { get; }

        /// <summary>그림 주소(화면 몫).</summary>
        public string Sprite { get; }

        /// <summary>맨손(빈 도구 id)은 맨손 배율. 도구는 그 힘, 선호 도구가 있는데 다른 도구면 절반.</summary>
        public float ComputeEffectivePower(string toolId, float toolPowerMultiplier)
        {
            if (string.IsNullOrEmpty(toolId)) return BareHandPenaltyMul;
            float power = toolPowerMultiplier;
            if (PreferredToolId.Length > 0 && !string.Equals(toolId, PreferredToolId, StringComparison.Ordinal)) power *= 0.5f;
            return power;
        }
    }
}
