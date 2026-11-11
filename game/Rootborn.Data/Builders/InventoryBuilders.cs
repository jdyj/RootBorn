using System;
using Rootborn.Core.Inventory;

namespace Rootborn.Data.Builders
{
    internal static class InventoryBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            // icon은 그림. seedFor는 레거시 작물 연결로 지금 데이터에서 모두 null이다(읽지 않음).
            b.Add("ItemDefinition", BuilderHelpers.AddedLater(new[] { "toolSpritePrefix" }, (n, c) => new ItemDefinition(
                n.Id,
                Math.Max(1, n.Int("maxStack")), // Unity ItemDefinition.MaxStack과 같게
                n.Enum<ItemCategory>("category"),
                n.Str("displayKey"),
                n.Str("description"),
                n.Str("toolSpritePrefix"))));
        }
    }
}
