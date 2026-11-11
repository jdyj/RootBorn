using System;
using System.Collections.Generic;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.StudentLife;

namespace Rootborn.Data.Builders
{
    /// <summary>여러 영역의 만들기 함수가 같이 쓰는 읽기 도우미.</summary>
    internal static class BuilderHelpers
    {
        /// <summary>
        /// Unity가 에셋을 저장한 뒤 클래스에 더해진 필드들. 옛 에셋 JSON에 없을 수 있고, 없으면 Unity처럼 초기값을 쓴다.
        /// 초기값이 형의 기본값인 필드만 적는다(원본 [SerializeField] 선언으로 확인).
        /// </summary>
        public static Func<NodeReader, BuildContext, T> AddedLater<T>(string[] fields, Func<NodeReader, BuildContext, T> build)
        {
            return (node, context) => build(node.Optional(fields), context);
        }

        /// <summary>{ x, y } 정수 점.</summary>
        public static GridPoint Grid(NodeReader point) => new GridPoint(point.Int("x"), point.Int("y"));

        public static GridPoint Grid(NodeReader node, string field)
        {
            return node.TryChild(field, out var point) ? Grid(point) : default;
        }

        public static GridPoint[] Grids(NodeReader node, string field)
        {
            var points = new List<GridPoint>();
            foreach (var point in node.Items(field)) points.Add(Grid(point));
            return points.ToArray();
        }

        public static PlanePoint[] Planes(NodeReader node, string field)
        {
            var points = new List<PlanePoint>();
            foreach (var point in node.Items(field)) points.Add(new PlanePoint(point.Float("x"), point.Float("y")));
            return points.ToArray();
        }

        /// <summary>참조 목록을 DefinitionRef로. 화면 이름은 가리키는 엔티티의 displayNameKey.</summary>
        public static DefinitionRef[] DefinitionRefs(NodeReader node, string field, BuildContext context)
        {
            // 화면 이름을 위해 가리키는 엔티티를 들여다본다(만들지 않는다).
            var refs = new List<DefinitionRef>();
            foreach (var element in node.Items(field))
            {
                if (!context.TryPeekReference(element, out var target)) continue;
                refs.Add(new DefinitionRef(target.Id, target.Has("displayNameKey") ? target.Str("displayNameKey") : null));
            }

            return refs.ToArray();
        }

        /// <summary>Unity의 아이템 배열 + 개수 배열을 짝지어 id 기준 수량으로. 길이가 다르거나 빈 칸·0 이하가 있으면 오류.</summary>
        public static ItemAmount<string>[] StrictItemAmounts(NodeReader node, string itemsField, string countsField)
        {
            var items = node.RefIds(itemsField);
            var counts = node.IntArray(countsField);
            if (items.Length != counts.Length)
            {
                node.Report("wrong-type", $"'{itemsField}' {items.Length}개와 '{countsField}' {counts.Length}개의 길이가 다르다");
                return Array.Empty<ItemAmount<string>>();
            }

            var amounts = new ItemAmount<string>[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                if (counts[i] <= 0) node.Report("wrong-type", $"'{countsField}'[{i}] = {counts[i]}: 개수는 1 이상이어야 한다");
                amounts[i] = new ItemAmount<string>(items[i], counts[i]);
            }

            return amounts;
        }

    }
}
