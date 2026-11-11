using System.Collections.Generic;
using Rootborn.Core.Characters;

namespace Rootborn.Data.Builders
{
    /// <summary>옷 칸, 옷, 성장 단계의 규칙 부분. 그림 시트는 화면 몫이라 만들지 않는다.</summary>
    internal static class CharacterBuilders
    {
        public static void Register(DefinitionBuilders b)
        {
            b.Add("WearableSlotDefinition", (n, c) => new WearableSlotRule(
                n.Id, n.Int("drawOrder"), n.RefIds("defaultWearables"), n.RefIds("hidesSlots")));

            // Unity WearableDefinition.ToRule과 같게: 단계와 그림 시트가 모두 있는 단계만 "그림 있음"이다.
            b.Add("WearableDefinition", (n, c) =>
            {
                var stages = new List<string>();
                foreach (var art in n.Items("stageArt"))
                {
                    var stage = art.RefId("Stage");
                    if (stage.Length > 0 && art.RefId("Sheet").Length > 0) stages.Add(stage);
                }

                return new WearableRule(n.Id, n.RefId("slot"), stages, n.Str("autoEquipFacilityId"));
            });

            // body는 그림 시트다.
            b.Add("CharacterGrowthStageDefinition", (n, c) => new GrowthStageRule(
                n.Id, n.Int("minimumStudentDay"), n.Bool("requiresAdult")));
        }
    }
}
