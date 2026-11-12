using System;
using System.Collections.Generic;
using Rootborn.Core.Careers;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;
using Rootborn.Core.Workspaces;

namespace Rootborn.Game
{
    /// <summary>시설 하나의 일터 상태(화면·일 명령이 읽는다). 점수는 그 일터의 놓인 가구에서 낸다.</summary>
    public sealed class WorkspaceStatus
    {
        public WorkspaceStatus(Workspace workspace, int placed, int capacity, int scorePercent, SpaceScores scores, bool owned)
        {
            Workspace = workspace;
            Placed = placed;
            Capacity = capacity;
            ScorePercent = scorePercent;
            Scores = scores ?? SpaceScores.Empty;
            Owned = owned;
        }

        public Workspace Workspace { get; }

        /// <summary>놓인 가구 수(열려 있지 않으면 저장된 기록 수, 기록이 없으면 기본 템플릿의 가구 수).</summary>
        public int Placed { get; }

        /// <summary>놓을 수 있는 가구 수. 자기 시설이 아니면 0이다.</summary>
        public int Capacity { get; }

        public int ScorePercent { get; }
        public SpaceScores Scores { get; }

        /// <summary>지금 일하는 시설의 일터다.</summary>
        public bool Owned { get; }
    }

    /// <summary>
    /// 일터: 시설 안에서 가구를 놓는 면. 가구를 놓는 맵에 들어오면(<see cref="EnterPlacementSurface"/>) 그 면의 일터가 데이터에 있으면 일터로,
    /// 없으면 집으로 연다. <see cref="House"/>는 "지금 열린 배치"다(집이든 일터든).
    /// - 일터 배치는 플레이어마다 면마다 파일 하나(workspace-layout-*.json), 진행(적용한 템플릿 요청, 깐 일터, 오늘 꾸미며 배운 횟수)은
    ///   workspace-progress-*.json이다. 일터는 같이 하는 방에서도 각자 것이다(같이 쓰는 집의 길을 타지 않는다).
    /// - 처음 들어온 자기 일터(배치 파일이 없고 깐 적이 없다)에는 기본 템플릿을 값 없이 깐다. 맞지 않으면 빈 채로 두고 깐 것으로 적지 않는다.
    /// - 놓기는 자기 시설이고 한도 안일 때만 된다. 옮기기·치우기는 자기 시설이면 한도와 무관하다. 남의 일터에서는 모두 안 된다.
    /// - 템플릿으로 놓은 가구와 일터에 놓은 가구는 가구 퀘스트 이벤트를 내지 않는다(집만 낸다).
    /// - 공간 점수는 면마다 기억하고 Furniture·Workspace 알림 때와 다른 면에 들어갈 때 버린다(일 명령마다 가구를 훑지 않는다).
    /// - 일터가 없는 시설의 일도 기술 성장은 받는다(공간 이득만 0).
    /// 규칙 판단(한도, 템플릿 적용, 꾸미며 배우기, 점수)은 Core가 한다.
    /// </summary>
    public sealed partial class GameSession
    {
        private const string TemplateCheckRequestId = "workspace-template-check";
        private static readonly string[] WorkspaceTopics = { GameChangeTopics.Furniture, GameChangeTopics.Workspace };

        private readonly WorkspaceService _workspaceService = new WorkspaceService();
        private WorkspaceProgress _workspaceProgress;
        private Dictionary<string, Workspace> _workspaceBySurface;
        private Dictionary<string, Workspace> _workspaceByFacility;
        private Dictionary<string, string> _facilityByWork;
        private Dictionary<string, PlacementSkillGrowth> _growthBySurface;
        private Dictionary<string, FurnitureDefinition> _furnitureById;
        private readonly Dictionary<string, IReadOnlyList<WorkspaceTemplate>> _templatesByWorkspace = new Dictionary<string, IReadOnlyList<WorkspaceTemplate>>(StringComparer.Ordinal);
        private readonly Dictionary<string, ScoreEntry> _workspaceScores = new Dictionary<string, ScoreEntry>(StringComparer.Ordinal);

        private sealed class ScoreEntry
        {
            public int Percent;
            public int Placed;
            public SpaceScores Scores;
        }

        /// <summary>지금 열린 배치가 일터면 그 일터, 집이면(또는 아직 열지 않았으면) null.</summary>
        public Workspace ActiveWorkspace { get; private set; }

        /// <summary>지금 열린 일터가 지금 일하는 시설의 것이다.</summary>
        public bool OwnsActiveWorkspace => ActiveWorkspace != null && Owns(ActiveWorkspace);

        /// <summary>
        /// 가구를 놓는 맵에 들어온다. 그 면의 일터가 데이터에 있으면 일터로, 없으면 집으로 연다. Host는 이것만 부른다.
        /// 들어올 때마다 기록을 새로 읽는다.
        /// </summary>
        public void EnterPlacementSurface(IPlacementGrid grid, string surfaceId)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (surfaceId == null || !WorkspaceBySurface.TryGetValue(surfaceId, out var workspace))
            {
                ActiveWorkspace = null;
                EnterHouseLayout(grid, surfaceId);
                return;
            }

            _houseGrid = grid;
            var layout = new HouseFurnitureLayout(new HousePlacementModel(grid), surfaceId, CommitWorkspaceLayout, workspace.AcceptsSurfaceIds);
            var records = WorkspaceSaveFiles.LoadLayout(_store, SaveSlot, surfaceId, PlayerId);
            if (records != null) layout.Restore(records, _definitions.All<FurnitureDefinition>());
            if (House != null) House.PlacementCommitted -= AnnouncePlacement; // 일터는 가구 퀘스트 이벤트를 내지 않는다.
            House = layout;
            ActiveWorkspace = workspace;
            _workspaceScores.Clear();

            if (records == null && !_workspaceProgress.IsSeeded(workspace.Id) && Owns(workspace) && SeedDefaultTemplate(workspace, layout))
                SaveAndNotify(null, WorkspaceTopics);
        }

        /// <summary>그 시설의 일터 상태. 일터가 없으면 null. 배치가 열려 있지 않아도 저장된 기록(없으면 기본 템플릿의 가구)으로 점수를 낸다.</summary>
        public WorkspaceStatus WorkspaceOf(string facilityId)
        {
            if (facilityId == null || !WorkspaceByFacility.TryGetValue(facilityId, out var workspace)) return null;
            return StatusOf(workspace);
        }

        /// <summary>지금 열린 일터의 상태(화면의 점수 줄). 집이거나 아직 열지 않았으면 null.</summary>
        public WorkspaceStatus ActiveWorkspaceStatus => ActiveWorkspace == null ? null : StatusOf(ActiveWorkspace);

        /// <summary>
        /// 지금 열린 일터에 가구를 하나 더 놓을 수 있는가(Applied면 된다). 못 하면 이유: 일터가 아님 InvalidRequest, 남의 일터 NotOwner,
        /// 한도에 닿음 OverCapacity. 칸이 맞는지는 보지 않는다(놓을 칸마다 House.CanPlace). 아무것도 바꾸지 않는다.
        /// </summary>
        public WorkspaceResultKind CanAddToActiveWorkspace()
        {
            if (ActiveWorkspace == null || House == null) return WorkspaceResultKind.InvalidRequest;
            if (!OwnsActiveWorkspace) return WorkspaceResultKind.NotOwner;
            return ActiveWorkspace.CanAdd(House.Placed.Count, Adult.FacilityLevel, Student) ? WorkspaceResultKind.Applied : WorkspaceResultKind.OverCapacity;
        }

        /// <summary>지금 열린 배치가 같이 쓰는 집이다(방의 허락을 기다리는 가구 명령). 일터는 방에서도 각자 것이라 false다.</summary>
        public bool SharesOpenLayout => SharedHouseOpen;

        private WorkspaceStatus StatusOf(Workspace workspace)
        {
            bool owned = Owns(workspace);
            var entry = Scores(workspace);
            int capacity = owned ? workspace.Capacity(Adult.FacilityLevel, Student) : 0;
            return new WorkspaceStatus(workspace, entry.Placed, capacity, entry.Percent, entry.Scores, owned);
        }

        /// <summary>
        /// 그 일이 지금 일하는 시설의 일이면 공간 이득과 기술 성장, 아니면 default.
        /// 그 시설에 일터가 없으면 공간 이득은 0이고 기술 성장만 남는다(일을 하면 기술이 자라는 것은 일터와 상관없다).
        /// </summary>
        public WorkExtras WorkExtrasFor(string workId)
        {
            if (workId == null || !FacilityByWork.TryGetValue(workId, out var facilityId) || facilityId != Adult.FacilityId) return default;
            var scores = WorkspaceByFacility.TryGetValue(facilityId, out var workspace) ? Scores(workspace).Scores : SpaceScores.Empty;
            return _definitions.WorkLinks.ExtrasFor(workId, scores);
        }

        /// <summary>지금 열린 일터의 템플릿(정의 순서). 일터가 아니면 빈 목록.</summary>
        public IReadOnlyList<WorkspaceTemplate> TemplatesForActiveWorkspace()
        {
            if (ActiveWorkspace == null) return Array.Empty<WorkspaceTemplate>();
            if (_templatesByWorkspace.TryGetValue(ActiveWorkspace.Id, out var cached)) return cached;
            var list = new List<WorkspaceTemplate>();
            foreach (var template in _definitions.All<WorkspaceTemplate>())
                if (template.WorkspaceId == ActiveWorkspace.Id) list.Add(template);
            _templatesByWorkspace[ActiveWorkspace.Id] = list;
            return list;
        }

        /// <summary>지금 열린 일터에 그 템플릿을 적용할 수 있는지(Applied면 된다). 아무것도 바꾸지 않는다.</summary>
        public WorkspaceResultKind CanApplyTemplate(string templateId)
        {
            if (ActiveWorkspace == null || House == null) return WorkspaceResultKind.InvalidRequest;
            var template = FindTemplate(templateId);
            WorkspaceResultKind Check() => _workspaceService.CanApplyTemplate(template, ActiveWorkspace, House, FurnitureById, CareerInventory, _workspaceProgress,
                OwnsActiveWorkspace, ActiveCapacity(), TemplateCheckRequestId, false);
            // 방 지갑은 범위 밖의 돈 쓰기를 늘 거절한다. 확인용 범위(허락받거나 물은 적이 없는 고정 id) 안에서 보면 잔액으로 판단하고, 확인은 요청을 적지 않는다.
            // 이미 다른 명령의 범위 안이면(그 명령의 알림 중) 그 범위로 본다 — 명령이 끝난 뒤의 알림에서 다시 물으면 바른 값이 된다.
            return _roomWallet == null || _walletScopeOpen ? Check() : InScope(TemplateCheckRequestId, Check);
        }

        /// <summary>
        /// 지금 열린 일터에 템플릿을 돈 내고 깐다. 놓인 가구를 모두 바꾸고(물건이 든 상자는 남는다) 같은 요청은 한 번만 된다.
        /// 적용되면 배치·진행·가방을 한 저장 묶음으로 쓴다. 돈을 같이 쓰는 방이면 다른 돈 쓰는 명령처럼 방의 허락을 기다린다.
        /// </summary>
        public WorkspaceResultKind ApplyTemplate(string templateId, string requestId)
        {
            if (ActiveWorkspace == null || House == null) return WorkspaceResultKind.InvalidRequest;
            var template = FindTemplate(templateId);
            // 돈을 같이 쓰는 방이면 이 람다는 허락이 온 뒤 다시 돈다. 그 사이 일터를 떠났으면(다른 면·다시 들어와 새 배치) 아무것도 바꾸지 않는다.
            var workspace = ActiveWorkspace;
            var layout = House;
            return Shared(() =>
            {
                if (ActiveWorkspace != workspace || House != layout) return WorkspaceResultKind.InvalidRequest;
                var result = _workspaceService.TryApplyTemplate(template, workspace, layout, FurnitureById, CareerInventory, _workspaceProgress,
                    Owns(workspace), workspace.Capacity(Adult.FacilityLevel, Student), requestId, false);
                return Saved(result, result == WorkspaceResultKind.Applied, WorkspaceTopics, GameChangeTopics.Inventory);
            }, r => r == WorkspaceResultKind.Applied);
        }

        // ---------- GameSession.Furniture가 부른다 ----------

        // 일터에 놓기: 자기 시설이고 한도 안이어야 한다. 놓으면 꾸미며 배운다.
        private bool PlaceInWorkspace(FurnitureDefinition furniture, Rootborn.Core.Minigames.GridPoint anchor, FurniturePlacementDirection direction)
        {
            if (!OwnsActiveWorkspace || !ActiveWorkspace.CanAdd(House.Placed.Count, Adult.FacilityLevel, Student)) return false;
            if (!House.TryPlace(furniture, anchor, direction)) return false;
            bool grown = GrowByPlacing(House.SurfaceId);
            SaveAndNotify(grown ? new[] { GameChangeTopics.Student } : null, WorkspaceTopics);
            return true;
        }

        /// <summary>그 면의 꾸미며 배우기 규칙이 있으면 오늘 한도 안에서 기술을 올린다(집도 같다).</summary>
        private bool GrowByPlacing(string surfaceId)
            => surfaceId != null && GrowthBySurface.TryGetValue(surfaceId, out var rule) && _workspaceService.TryGrowByPlacing(rule, Student, _workspaceProgress);

        private void LoadWorkspaces(ISaveStore store)
        {
            // 복사한 슬롯도 지금 슬롯·플레이어로 읽는다(다른 영역의 Rebind와 같다).
            store.TryRead<WorkspaceProgressSaveData>(SaveSlot, SaveFileNames.WorkspaceProgress(PlayerId), out var data);
            if (data != null)
            {
                data.SaveSlot = SaveSlot;
                data.PlayerId = PlayerId;
            }

            _workspaceProgress = WorkspaceProgress.FromSaveData(data, SaveSlot, PlayerId);
        }

        private void SaveWorkspaces(ISaveStore store) => WorkspaceSaveFiles.Save(store, _workspaceProgress);

        /// <summary>Furniture·Workspace를 알릴 때 기억한 점수를 버린다.</summary>
        private void ForgetWorkspaceScores(string topic)
        {
            if (topic == GameChangeTopics.Furniture || topic == GameChangeTopics.Workspace) _workspaceScores.Clear();
        }

        // 일터 배치의 commit은 파일을 바로 쓰지 않고 표시만 세운다. 바뀐 배치는 명령 끝의 Save() 묶음이 가방·일터 진행과 함께 쓴다
        // (템플릿의 돈·적용 기록과 배치가 따로 저장되지 않게). 그래서 일터를 바꾸는 명령은 모두 끝에 Save()를 부른다.
        // 묶음 쓰기가 실패해도 돌던 세션의 메모리는 되돌리지 않는다(다른 명령의 저장 실패와 같다). 디스크는 셋이 함께 바뀌거나 그대로다.
        private bool CommitWorkspaceLayout(FurniturePlacementSaveData[] records)
        {
            if (ActiveWorkspace == null) return false;
            _houseLayoutDirty = true;
            return true;
        }

        // 저장 묶음 안에서: 바뀐 일터 배치(가구 놓기·옮기기·치우기·템플릿, 상자 내용물)를 같은 묶음으로 쓴다.
        private void SaveWorkspaceLayout(ISaveStore batch)
            => WorkspaceSaveFiles.SaveLayout(batch, SaveSlot, ActiveWorkspace.SurfaceId, PlayerId, House.BuildSaveRecords());

        // ---------- 안 ----------

        private bool Owns(Workspace workspace) => !string.IsNullOrEmpty(Adult.FacilityId) && Adult.FacilityId == workspace.FacilityId;

        private int ActiveCapacity() => ActiveWorkspace.Capacity(Adult.FacilityLevel, Student);

        private bool SeedDefaultTemplate(Workspace workspace, HouseFurnitureLayout layout)
        {
            var template = FindTemplate(workspace.DefaultTemplateId);
            var result = _workspaceService.TryApplyTemplate(template, workspace, layout, FurnitureById, CareerInventory, _workspaceProgress,
                true, workspace.Capacity(Adult.FacilityLevel, Student), workspace.Id, true);
            return result == WorkspaceResultKind.Applied;
        }

        private WorkspaceTemplate FindTemplate(string templateId)
            => !string.IsNullOrEmpty(templateId) && _definitions.TryGet<WorkspaceTemplate>(templateId, out var template) ? template : null;

        // 점수 배율이 바뀌었으면(기술이 단계에 닿음) 다시 낸다. 그 밖에는 Furniture·Workspace 알림 전까지 기억한 값을 쓴다.
        private ScoreEntry Scores(Workspace workspace)
        {
            int percent = workspace.ScorePercent(Student);
            if (_workspaceScores.TryGetValue(workspace.SurfaceId, out var cached) && cached.Percent == percent) return cached;

            var ids = PlacedFurnitureIds(workspace);
            var source = new FurnitureQualitySource(ids, _definitions.FurnitureQuality, workspace.RepeatPercent);
            var entry = new ScoreEntry { Percent = percent, Placed = ids.Count, Scores = SpaceScoreCalculator.Calculate(new ISpaceQualitySource[] { source }, percent) };
            _workspaceScores[workspace.SurfaceId] = entry;
            return entry;
        }

        // 열린 일터면 놓인 가구, 아니면 저장된 기록, 기록이 없으면 기본 템플릿의 가구.
        private List<string> PlacedFurnitureIds(Workspace workspace)
        {
            if (ActiveWorkspace == workspace && House != null)
            {
                var open = new List<string>(House.Placed.Count);
                foreach (var placed in House.Placed) open.Add(placed.Definition.StableId);
                return open;
            }

            var records = WorkspaceSaveFiles.LoadLayout(_store, SaveSlot, workspace.SurfaceId, PlayerId);
            if (records != null)
            {
                var saved = new List<string>(records.Length);
                foreach (var record in records) saved.Add(record.FurnitureId);
                return saved;
            }

            var template = FindTemplate(workspace.DefaultTemplateId);
            var seeded = new List<string>(template?.Placements.Count ?? 0);
            if (template != null) foreach (var placement in template.Placements) seeded.Add(placement.FurnitureId);
            return seeded;
        }

        private Dictionary<string, Workspace> WorkspaceBySurface => _workspaceBySurface ??= BuildWorkspaces(w => w.SurfaceId);
        private Dictionary<string, Workspace> WorkspaceByFacility => _workspaceByFacility ??= BuildWorkspaces(w => w.FacilityId);

        private Dictionary<string, Workspace> BuildWorkspaces(Func<Workspace, string> key)
        {
            var map = new Dictionary<string, Workspace>(StringComparer.Ordinal);
            foreach (var workspace in _definitions.All<Workspace>())
                if (workspace != null && !map.ContainsKey(key(workspace))) map[key(workspace)] = workspace;
            return map;
        }

        private Dictionary<string, string> FacilityByWork
        {
            get
            {
                if (_facilityByWork != null) return _facilityByWork;
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var facility in _definitions.All<Facility>())
                    foreach (var work in facility.Works)
                        if (work != null && !map.ContainsKey(work.Id)) map[work.Id] = facility.Id;
                return _facilityByWork = map;
            }
        }

        private Dictionary<string, PlacementSkillGrowth> GrowthBySurface
        {
            get
            {
                if (_growthBySurface != null) return _growthBySurface;
                var map = new Dictionary<string, PlacementSkillGrowth>(StringComparer.Ordinal);
                foreach (var rule in _definitions.All<PlacementSkillGrowth>())
                    if (rule != null && !map.ContainsKey(rule.SurfaceId)) map[rule.SurfaceId] = rule;
                return _growthBySurface = map;
            }
        }

        private IReadOnlyDictionary<string, FurnitureDefinition> FurnitureById
        {
            get
            {
                if (_furnitureById != null) return _furnitureById;
                var map = new Dictionary<string, FurnitureDefinition>(StringComparer.Ordinal);
                foreach (var furniture in _definitions.All<FurnitureDefinition>())
                    if (furniture != null && !map.ContainsKey(furniture.StableId)) map[furniture.StableId] = furniture;
                return _furnitureById = map;
            }
        }
    }
}
