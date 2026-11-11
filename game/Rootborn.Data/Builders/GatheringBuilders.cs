using System.Collections.Generic;
using Rootborn.Core.Gathering;

namespace Rootborn.Data.Builders
{
    /// <summary>채집 자원과 레시피. 레시피의 만들기 단계와 만드는 도구는 게임이 쓰지 않아 읽지 않는다(NotUsedByGame).</summary>
    internal static class GatheringBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            // 선호 도구는 도구 정의(게임이 쓰지 않음)를 id로만 가리킨다. sprite는 그림 주소다.
            b.Add("ResourceNodeDefinition", (n, c) => new ResourceNodeDefinition(
                n.Id,
                n.Str("displayKey"),
                n.Float("baseHitsToBreak"),
                n.RefId("preferredTool"),
                n.Float("bareHandPenaltyMul"),
                Drops(n),
                n.Str("surfaceTag"),
                n.Bool("isWalkable"),
                ColliderSize(n, "x"),
                ColliderSize(n, "y"),
                n.TryChild("sprite", out var sprite) ? sprite.Str("sprite") : null));

            b.Add("RecipeDefinition", (n, c) => new RecipeDefinition(n.Id, n.Str("displayNameKey"), n.Str("nextHintKey")));
        }

        private static ResourceDrop[] Drops(NodeReader n)
        {
            var drops = new List<ResourceDrop>();
            foreach (var drop in n.Items("drops")) drops.Add(new ResourceDrop(drop.Str("resourceId"), drop.Int("minCount"), drop.Int("maxCount")));
            return drops.ToArray();
        }

        private static float ColliderSize(NodeReader n, string axis)
        {
            return n.TryChild("colliderSize", out var size) ? size.Float(axis) : 1f;
        }
    }
}
