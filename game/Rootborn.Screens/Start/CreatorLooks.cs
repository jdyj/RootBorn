using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Rootborn.Core.Save;

namespace Rootborn.Screens.Start
{
    /// <summary>
    /// 만들기 화면이 고르는 겉모습 하나(Unity GrowingCharacterAppearanceDefinition): 몸·머리·옷 그림 시트 id, 이름 키, 그 겉모습을 쓰는 시기.
    /// 그림은 시트 id로만 가리킨다(어느 PNG인지는 Host가 안다).
    /// </summary>
    public sealed record CreatorLook(string Id, string BodyId, string HeadId, string OutfitId, string NameKey, int MinimumStudentDay, bool RequiresAdult, string FacilityId)
    {
        /// <summary>그리는 순서(Unity AddPreviewLayer: 몸 → 옷 → 머리).</summary>
        public IEnumerable<string> SheetIds()
        {
            yield return BodyId;
            yield return OutfitId;
            yield return HeadId;
        }
    }

    /// <summary>
    /// 만들기 화면의 단계: 몸 하나에 겉모습 여러 개. 단계 묶기와 순서, 고른 결과의 저장 모양은 Unity GrowingCharacterStageSelector와
    /// SpumCharacterCreatorPanel.CreateSnapshot과 같다. 겉모습 데이터는 data/characters의 GrowingCharacterAppearanceDefinition.json
    /// (Core 정의가 아닌 그림 데이터라 로더가 만들지 않는다)에서 읽는다. 겉모습을 더하는 일은 데이터만 바꾸면 된다.
    /// </summary>
    public sealed class CreatorLooks
    {
        public const string RelativePath = "characters/GrowingCharacterAppearanceDefinition.json";
        /// <summary>Unity GrowingCharacterStageSelector.OutfitCategoryPrefix.</summary>
        public const string OutfitCategoryPrefix = "outfit:";
        /// <summary>Unity GrowingCharacterStageSelector.CreatorCatalogId.</summary>
        public const string CreatorCatalogId = "growing.creator";
        /// <summary>Unity GrowingCharacterAppearanceDefinition.VisualKindName.</summary>
        public const string VisualKind = "growing";

        private readonly Dictionary<string, CreatorLook> _byId = new Dictionary<string, CreatorLook>(StringComparer.Ordinal);

        public CreatorLooks(IEnumerable<CreatorLook> looks)
        {
            var all = new List<CreatorLook>();
            if (looks != null)
                foreach (CreatorLook look in looks)
                    if (look != null && !string.IsNullOrEmpty(look.Id) && !_byId.ContainsKey(look.Id)) { _byId[look.Id] = look; all.Add(look); }
            Stages = BuildStages(all);
        }

        /// <summary>단계(어린 몸 먼저, 어른 마지막). 단계마다 겉모습이 하나 이상.</summary>
        public IReadOnlyList<IReadOnlyList<CreatorLook>> Stages { get; }

        public CreatorLook Find(string id) => id != null && _byId.TryGetValue(id, out CreatorLook look) ? look : null;

        /// <summary>data 폴더의 겉모습 파일을 읽는다. 파일이 없으면 빈 목록(만들기 화면은 이름만 고른다).</summary>
        public static CreatorLooks Load(string dataDirectory)
        {
            string path = Path.Combine(dataDirectory, RelativePath);
            if (!File.Exists(path)) return new CreatorLooks(Array.Empty<CreatorLook>());
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            var looks = new List<CreatorLook>();
            foreach (JsonElement a in doc.RootElement.EnumerateArray())
            {
                if (Text(a, "visualKind") != VisualKind) continue;
                looks.Add(new CreatorLook(Text(a, "id"), Ref(a, "body"), Ref(a, "head"), Ref(a, "outfit"), Text(a, "displayNameKey"),
                    Number(a, "minimumStudentDay"), Number(a, "requiresAdult") != 0, Text(a, "facilityId")));
            }
            return new CreatorLooks(looks);
        }

        /// <summary>
        /// Unity CreatorStages: 몸이 있고 시설 옷이 아닌 겉모습을 몸별로 묶고(데이터 순서 유지), 어른 단계는 뒤로, 나머지는 시작 일차 순.
        /// 시설 옷은 일하면서 얻으므로 여기서 고르지 않는다.
        /// </summary>
        private static IReadOnlyList<IReadOnlyList<CreatorLook>> BuildStages(List<CreatorLook> looks)
        {
            var stages = new List<List<CreatorLook>>();
            foreach (CreatorLook look in looks)
            {
                if (string.IsNullOrEmpty(look.BodyId) || !string.IsNullOrEmpty(look.FacilityId)) continue;
                List<CreatorLook> stage = stages.Find(s => s[0].BodyId == look.BodyId);
                if (stage == null) stages.Add(stage = new List<CreatorLook>());
                stage.Add(look);
            }
            // List.Sort는 안정 정렬이 아니어서 같은 값의 순서를 지키도록 처음 자리로 비긴다.
            var order = new List<List<CreatorLook>>(stages);
            stages.Sort((a, b) =>
            {
                if (a[0].RequiresAdult != b[0].RequiresAdult) return a[0].RequiresAdult ? 1 : -1;
                int byDay = a[0].MinimumStudentDay.CompareTo(b[0].MinimumStudentDay);
                return byDay != 0 ? byDay : order.IndexOf(a).CompareTo(order.IndexOf(b));
            });
            return stages.ConvertAll(s => (IReadOnlyList<CreatorLook>)s.AsReadOnly()).AsReadOnly();
        }

        /// <summary>단계 이름 키(Unity StageLabel): 어른 / 첫날부터(어릴 때) / 학교 다닐 때.</summary>
        public static string StageLabelKey(CreatorLook look)
        {
            if (look == null) return string.Empty;
            if (look.RequiresAdult) return "ui.creator.stage.adult";
            return look.MinimumStudentDay <= 1 ? "ui.creator.stage.child" : "ui.creator.stage.student";
        }

        /// <summary>
        /// 고른 결과의 저장 모양(Unity CreateSnapshot): 단계마다 ("outfit:" + 몸 id, 겉모습 id), 대표 겉모습은 첫 단계의 것.
        /// choices[i]는 단계 i에서 고른 번호.
        /// </summary>
        public CharacterAppearanceSnapshotSaveData Snapshot(IReadOnlyList<int> choices)
        {
            var parts = new List<CharacterAppearancePartSaveData>(Stages.Count);
            CreatorLook first = null;
            for (int i = 0; i < Stages.Count; i++)
            {
                CreatorLook look = Choice(i, choices);
                if (i == 0) first = look;
                if (look == null || string.IsNullOrEmpty(look.BodyId)) continue;
                parts.Add(new CharacterAppearancePartSaveData { categoryId = OutfitCategoryPrefix + look.BodyId, partId = look.Id });
            }
            return new CharacterAppearanceSnapshotSaveData
            {
                schemaVersion = 1,
                visualKind = VisualKind,
                appearanceDefinitionId = first?.Id ?? string.Empty,
                catalogId = CreatorCatalogId,
                selectedParts = parts.ToArray(),
            };
        }

        public CreatorLook Choice(int stage, IReadOnlyList<int> choices)
        {
            if (stage < 0 || stage >= Stages.Count || Stages[stage].Count == 0) return null;
            int index = choices != null && stage < choices.Count ? choices[stage] : 0;
            return Stages[stage][Math.Clamp(index, 0, Stages[stage].Count - 1)];
        }

        /// <summary>
        /// 슬롯 칸에 그릴 겉모습: 슬롯 정보의 만들기 결과에서 첫 단계 몸의 선택, 없으면 첫 단계의 첫 겉모습.
        /// </summary>
        public CreatorLook SlotLook(SaveSlotMetadata metadata)
        {
            if (Stages.Count == 0) return null;
            IReadOnlyList<CreatorLook> first = Stages[0];
            CharacterAppearanceSnapshotSaveData snapshot = metadata?.CharacterAppearanceSnapshot;
            if (snapshot?.selectedParts != null && snapshot.visualKind == VisualKind)
                foreach (CharacterAppearancePartSaveData part in snapshot.selectedParts)
                    if (part != null && part.categoryId == OutfitCategoryPrefix + first[0].BodyId)
                        foreach (CreatorLook look in first)
                            if (look.Id == part.partId) return look;
            return first[0];
        }

        private static string Text(JsonElement e, string name) =>
            e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

        private static int Number(JsonElement e, string name) =>
            e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : 0;

        private static string Ref(JsonElement e, string name) =>
            e.TryGetProperty(name, out JsonElement r) && r.ValueKind == JsonValueKind.Object && r.TryGetProperty("ref", out JsonElement id) ? id.GetString() ?? string.Empty : string.Empty;
    }
}
