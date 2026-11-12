using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Inventory;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Core.Workspaces;

namespace Rootborn.Core.Tests
{
    public sealed class WorkspaceTemplateTests
    {
        private const string Coin = "currency.fixture";
        private const string Surface = "workplace.test";
        private const string Slot = "slot";
        private const string Player = "player";

        private static GridPoint P(int x, int y) => new GridPoint(x, y);

        private static IEnumerable<GridPoint> Rect(int xMin, int xMax, int yMin, int yMax)
        {
            for (int x = xMin; x <= xMax; x++)
                for (int y = yMin; y <= yMax; y++)
                    yield return P(x, y);
        }

        private static FurnitureDefinition Furniture(string id, bool blocks)
            => new FurnitureDefinition(id, id, null, "Seating", new[] { "house" }, new[] { new FurnitureTilePart(P(0, 0), "sprite.png", "tile") }, null, blocks, FurniturePlacementPreference.Any);

        private static readonly FurnitureDefinition Chair = Furniture("chair", true);
        private static readonly FurnitureDefinition Rug = Furniture("rug", false);

        /// <summary>바닥 (0..4, 0..2), 고정물 (2,1), 시작 (0,0), 문 (4,0).</summary>
        private static HousePlacementModel Room()
            => new HousePlacementModel(new PlacementGrid(Rect(0, 4, 0, 2), new[] { P(2, 1) }, P(0, 0), new[] { P(4, 0) }));

        private sealed class Fixture
        {
            public readonly ItemSlots<string, ItemSlot<string>> Slots = new ItemSlots<string, ItemSlot<string>>(8, id => 999);
            public readonly ICareerInventory Inventory;
            public readonly Workspace Workspace = new Workspace("workspace.test", "facility.test", Surface, new[] { "house" }, new[] { 2, 3, 4 }, 0, null, "template.test");
            public readonly Dictionary<string, FurnitureDefinition> Catalog = new Dictionary<string, FurnitureDefinition> { { "chair", Chair }, { "rug", Rug } };
            public readonly WorkspaceService Service = new WorkspaceService();
            public WorkspaceProgress Progress = new WorkspaceProgress(Slot, Player);
            public HouseFurnitureLayout Layout;
            public int Commits;
            public bool SaveOk = true;
            public FurniturePlacementSaveData[] Written;

            public Fixture(int coins)
            {
                Inventory = new ItemSlotsCareerInventory<ItemSlot<string>>(Slots);
                if (coins > 0) Slots.Add(Coin, coins);
                Layout = NewLayout();
            }

            public HouseFurnitureLayout NewLayout()
                => new HouseFurnitureLayout(Room(), Surface, records => { Commits++; if (!SaveOk) return false; Written = records; return true; }, new[] { "house" });

            public int Money => Inventory.CountOf(Coin);

            public WorkspaceResultKind Apply(WorkspaceTemplate template, string requestId = "req-1", bool owns = true, int capacity = 2, bool free = false)
                => Service.TryApplyTemplate(template, Workspace, Layout, Catalog, Inventory, Progress, owns, capacity, requestId, free);

            public WorkspaceResultKind Check(WorkspaceTemplate template, string requestId = "req-1", bool owns = true, int capacity = 2, bool free = false)
                => Service.CanApplyTemplate(template, Workspace, Layout, Catalog, Inventory, Progress, owns, capacity, requestId, free);

            public string[] Placed() => Layout.Placed.Select(p => p.Definition.StableId + "@" + p.Anchor.X + "," + p.Anchor.Y).ToArray();
        }

        private static WorkspaceTemplate Template(int cost = 10, params TemplatePlacement[] placements)
            => new WorkspaceTemplate("template.test", "workspace.test", "workspace.template.test", new[] { new ItemAmount<string>(Coin, cost) },
                placements.Length > 0 ? placements : new[]
                {
                    new TemplatePlacement("rug", P(1, 1), FurniturePlacementDirection.North),
                    new TemplatePlacement("chair", P(3, 1), FurniturePlacementDirection.East),
                });

        // --- 보상 원칙 다섯 경우 ---

        [Test]
        public void Apply_PaysAndPlaces()
        {
            var f = new Fixture(10);
            var template = Template();
            Assert.That(f.Check(template), Is.EqualTo(WorkspaceResultKind.Applied));
            Assert.That(f.Commits, Is.Zero, "확인은 아무것도 바꾸지 않는다");
            Assert.That(f.Money, Is.EqualTo(10));

            Assert.That(f.Apply(template), Is.EqualTo(WorkspaceResultKind.Applied));
            Assert.That(f.Money, Is.Zero);
            Assert.That(f.Layout.Placed.Count, Is.EqualTo(template.Placements.Count));
            Assert.That(f.Placed(), Is.EqualTo(new[] { "rug@1,1", "chair@3,1" }));
            Assert.That(f.Commits, Is.EqualTo(1), "배치는 한 번만 저장한다");
            Assert.That(f.Written.All(r => r.SurfaceId == Surface), Is.True);
            Assert.That(f.Progress.HasApplied("req-1"), Is.True);
            Assert.That(f.Progress.IsSeeded(f.Workspace.Id), Is.False, "돈을 낸 적용은 기본 깔기가 아니다");
        }

        [Test]
        public void Apply_NotEnoughMoney_NothingChanges()
        {
            var f = new Fixture(5);
            Assert.That(f.Layout.TryPlace(Chair, P(1, 2), FurniturePlacementDirection.North), Is.True);
            int commits = f.Commits;

            Assert.That(f.Apply(Template()), Is.EqualTo(WorkspaceResultKind.InsufficientResources));
            Assert.That(f.Money, Is.EqualTo(5));
            Assert.That(f.Placed(), Is.EqualTo(new[] { "chair@1,2" }));
            Assert.That(f.Commits, Is.EqualTo(commits));
            Assert.That(f.Progress.HasApplied("req-1"), Is.False);
        }

        [Test]
        public void Apply_OnePlacementDoesNotFit_NothingChanges()
        {
            var f = new Fixture(10);
            var template = Template(10,
                new TemplatePlacement("rug", P(1, 1), FurniturePlacementDirection.North),
                new TemplatePlacement("chair", P(2, 1), FurniturePlacementDirection.North));

            Assert.That(f.Apply(template), Is.EqualTo(WorkspaceResultKind.DoesNotFit), "둘째가 고정물 칸");
            Assert.That(f.Money, Is.EqualTo(10));
            Assert.That(f.Layout.Placed, Is.Empty);
            Assert.That(f.Commits, Is.Zero);
            Assert.That(f.Progress.HasApplied("req-1"), Is.False);
        }

        [Test]
        public void Apply_SameRequestTwice_PaysOnce()
        {
            var f = new Fixture(20);
            Assert.That(f.Apply(Template()), Is.EqualTo(WorkspaceResultKind.Applied));
            Assert.That(f.Apply(Template()), Is.EqualTo(WorkspaceResultKind.DuplicateRequest));
            Assert.That(f.Check(Template()), Is.EqualTo(WorkspaceResultKind.DuplicateRequest));
            Assert.That(f.Money, Is.EqualTo(10));
            Assert.That(f.Commits, Is.EqualTo(1));
        }

        [Test]
        public void Apply_AfterSaveAndReload_StillOnce()
        {
            var f = new Fixture(20);
            var store = new InMemorySaveStore();
            Assert.That(f.Apply(Template()), Is.EqualTo(WorkspaceResultKind.Applied));
            WorkspaceSaveFiles.Save(store, f.Progress);
            WorkspaceSaveFiles.SaveLayout(store, Slot, Surface, Player, f.Layout.BuildSaveRecords());

            // 메모리 왕복
            f.Progress = WorkspaceProgress.FromSaveData(f.Progress.ToSaveData(), Slot, Player);
            Assert.That(f.Apply(Template()), Is.EqualTo(WorkspaceResultKind.DuplicateRequest));

            // 저장소 왕복(새 판): 진행과 배치를 다시 읽는다
            f.Progress = WorkspaceSaveFiles.LoadOrCreate(store, Slot, Player);
            f.Layout = f.NewLayout();
            f.Layout.Restore(WorkspaceSaveFiles.LoadLayout(store, Slot, Surface, Player), f.Catalog.Values);
            Assert.That(f.Placed(), Is.EqualTo(new[] { "rug@1,1", "chair@3,1" }));
            Assert.That(f.Apply(Template()), Is.EqualTo(WorkspaceResultKind.DuplicateRequest));
            Assert.That(f.Money, Is.EqualTo(10));
        }

        // --- 나머지 판정 ---

        [Test]
        public void Apply_NotOwner_Refused()
        {
            var f = new Fixture(10);
            Assert.That(f.Apply(Template(), owns: false), Is.EqualTo(WorkspaceResultKind.NotOwner));
            Assert.That(f.Money, Is.EqualTo(10));
            Assert.That(f.Layout.Placed, Is.Empty);
            Assert.That(f.Progress.HasApplied("req-1"), Is.False);
        }

        [Test]
        public void Apply_OverCapacity_Refused()
        {
            var f = new Fixture(10);
            Assert.That(f.Apply(Template(), capacity: 1), Is.EqualTo(WorkspaceResultKind.OverCapacity));
            Assert.That(f.Money, Is.EqualTo(10));
            Assert.That(f.Layout.Placed, Is.Empty);
            Assert.That(f.Progress.HasApplied("req-1"), Is.False);
        }

        [Test]
        public void Apply_KeptChestCountsTowardCapacity()
        {
            var f = new Fixture(10);
            Assert.That(f.Layout.TryPlace(Chair, P(1, 2), FurniturePlacementDirection.North), Is.True);
            var chest = f.Layout.Placed.Single().InstanceId;
            f.Layout.SetStorage(chest, new FurnitureStorageSaveData { Items = new[] { new PlayerInventoryItemSaveData { ItemId = "item.wood", Count = 2 } } });

            Assert.That(f.Apply(Template(), capacity: 2), Is.EqualTo(WorkspaceResultKind.OverCapacity), "남는 상자 1 + 템플릿 2 > 2");
            Assert.That(f.Money, Is.EqualTo(10));

            Assert.That(f.Apply(Template(), capacity: 3), Is.EqualTo(WorkspaceResultKind.Applied));
            Assert.That(f.Placed(), Is.EqualTo(new[] { "chair@1,2", "rug@1,1", "chair@3,1" }), "물건이 든 상자는 남는다");
            Assert.That(f.Layout.Placed[0].HoldsItems, Is.True);
        }

        [Test]
        public void Apply_UnknownFurniture_InvalidDefinition()
        {
            var f = new Fixture(10);
            var unknown = Template(10, new TemplatePlacement("missing", P(1, 1), FurniturePlacementDirection.North));
            Assert.That(f.Apply(unknown), Is.EqualTo(WorkspaceResultKind.InvalidDefinition));

            var otherWorkspace = new WorkspaceTemplate("template.other", "workspace.other", null, null, new[] { new TemplatePlacement("rug", P(1, 1), FurniturePlacementDirection.North) });
            Assert.That(f.Apply(otherWorkspace), Is.EqualTo(WorkspaceResultKind.InvalidDefinition), "다른 일터의 템플릿");
            Assert.That(f.Apply(null), Is.EqualTo(WorkspaceResultKind.InvalidDefinition));

            Assert.That(f.Money, Is.EqualTo(10));
            Assert.That(f.Layout.Placed, Is.Empty);
            Assert.That(f.Progress.HasApplied("req-1"), Is.False);
        }

        [Test]
        public void Apply_EmptyRequest_InvalidRequest()
        {
            var f = new Fixture(10);
            Assert.That(f.Apply(Template(), requestId: ""), Is.EqualTo(WorkspaceResultKind.InvalidRequest));
            Assert.That(f.Apply(Template(), requestId: null), Is.EqualTo(WorkspaceResultKind.InvalidRequest));
            Assert.That(f.Money, Is.EqualTo(10));
        }

        [Test]
        public void Apply_LayoutSaveFails_MoneyUnchanged()
        {
            var f = new Fixture(10);
            f.SaveOk = false;
            Assert.That(f.Apply(Template()), Is.EqualTo(WorkspaceResultKind.DoesNotFit));
            Assert.That(f.Commits, Is.EqualTo(1), "저장을 시도했다");
            Assert.That(f.Money, Is.EqualTo(10), "교환을 되돌렸다");
            Assert.That(f.Layout.Placed, Is.Empty);
            Assert.That(f.Progress.HasApplied("req-1"), Is.False);

            f.SaveOk = true;
            Assert.That(f.Apply(Template()), Is.EqualTo(WorkspaceResultKind.Applied), "실패한 요청은 쓴 것으로 남지 않는다");
            Assert.That(f.Money, Is.Zero);
        }

        [Test]
        public void FreeSeed_MarksSeeded_AndCostsNothing()
        {
            var f = new Fixture(3);
            var template = Template();
            Assert.That(f.Apply(template, requestId: "req-free", free: true), Is.EqualTo(WorkspaceResultKind.InvalidRequest), "기본 깔기의 요청 id는 일터 id다");

            Assert.That(f.Apply(template, requestId: f.Workspace.Id, free: true), Is.EqualTo(WorkspaceResultKind.Applied));
            Assert.That(f.Money, Is.EqualTo(3));
            Assert.That(f.Layout.Placed.Count, Is.EqualTo(2));
            Assert.That(f.Progress.IsSeeded(f.Workspace.Id), Is.True);
            Assert.That(f.Progress.HasApplied(f.Workspace.Id), Is.True);

            Assert.That(f.Apply(template, requestId: f.Workspace.Id, free: true), Is.EqualTo(WorkspaceResultKind.DuplicateRequest));
            var reloaded = WorkspaceProgress.FromSaveData(f.Progress.ToSaveData(), Slot, Player);
            Assert.That(reloaded.IsSeeded(f.Workspace.Id), Is.True);

            var notDefault = new WorkspaceTemplate("template.fancy", "workspace.test", null, null, new[] { new TemplatePlacement("rug", P(1, 1), FurniturePlacementDirection.North) });
            var g = new Fixture(0);
            Assert.That(g.Apply(notDefault, requestId: g.Workspace.Id, free: true), Is.EqualTo(WorkspaceResultKind.InvalidRequest), "공짜는 기본 템플릿만");
            Assert.That(g.Layout.Placed, Is.Empty);
        }

        [Test]
        public void FreeSeed_AfterPaidApply_Refused_NothingChanges()
        {
            var f = new Fixture(10);
            var paid = Template(10, new TemplatePlacement("chair", P(1, 2), FurniturePlacementDirection.North));
            Assert.That(f.Apply(paid), Is.EqualTo(WorkspaceResultKind.Applied));
            int commits = f.Commits;

            Assert.That(f.Apply(Template(), requestId: f.Workspace.Id, free: true), Is.EqualTo(WorkspaceResultKind.InvalidRequest), "돈을 내고 깐 배치를 지우지 않는다");
            Assert.That(f.Placed(), Is.EqualTo(new[] { "chair@1,2" }));
            Assert.That(f.Money, Is.Zero);
            Assert.That(f.Commits, Is.EqualTo(commits));
            Assert.That(f.Progress.IsSeeded(f.Workspace.Id), Is.False);
            Assert.That(f.Progress.HasApplied(f.Workspace.Id), Is.False);
        }

        [Test]
        public void FreeSeed_AfterPlayerPlaced_Refused_NothingChanges()
        {
            var f = new Fixture(0);
            Assert.That(f.Layout.TryPlace(Chair, P(1, 2), FurniturePlacementDirection.North), Is.True);
            int commits = f.Commits;

            Assert.That(f.Check(Template(), requestId: f.Workspace.Id, free: true), Is.EqualTo(WorkspaceResultKind.InvalidRequest));
            Assert.That(f.Apply(Template(), requestId: f.Workspace.Id, free: true), Is.EqualTo(WorkspaceResultKind.InvalidRequest));
            Assert.That(f.Placed(), Is.EqualTo(new[] { "chair@1,2" }));
            Assert.That(f.Commits, Is.EqualTo(commits));
            Assert.That(f.Progress.IsSeeded(f.Workspace.Id), Is.False);
        }

        [Test]
        public void FreeSeed_WithUnresolvedRecords_Refused()
        {
            var f = new Fixture(0);
            f.Layout.Restore(new[] { new FurniturePlacementSaveData { FurnitureId = "missing", SurfaceId = Surface, AnchorX = 1, AnchorY = 2 } }, f.Catalog.Values);
            Assert.That(f.Layout.UnresolvedCount, Is.EqualTo(1));
            Assert.That(f.Apply(Template(), requestId: f.Workspace.Id, free: true), Is.EqualTo(WorkspaceResultKind.InvalidRequest), "되살리지 못한 기록이 있는 일터는 빈 일터가 아니다");
            Assert.That(f.Layout.Placed, Is.Empty);
            Assert.That(f.Progress.IsSeeded(f.Workspace.Id), Is.False);
        }

        // --- 꾸미며 배우기 ---

        [Test]
        public void GrowByPlacing_StopsAtDailyLimit_AndResetsNextDay()
        {
            var service = new WorkspaceService();
            var rule = new PlacementSkillGrowth("growth.interior", Surface, "skill.interior", 3, 2);
            var student = new StudentLifeProgress(Slot, Player, 10, 10);
            var progress = new WorkspaceProgress(Slot, Player);

            Assert.That(progress.GrowthLeftToday(student.CurrentDay, rule.DailyLimit), Is.EqualTo(2));
            Assert.That(service.TryGrowByPlacing(rule, student, progress), Is.True);
            Assert.That(service.TryGrowByPlacing(rule, student, progress), Is.True);
            Assert.That(service.TryGrowByPlacing(rule, student, progress), Is.False, "오늘 한도");
            Assert.That(student.GetSkillValue("skill.interior"), Is.EqualTo(6));
            Assert.That(progress.GrowthLeftToday(student.CurrentDay, rule.DailyLimit), Is.Zero);

            // 저장·다시 읽기 뒤에도 오늘 센 것은 남는다
            progress = WorkspaceProgress.FromSaveData(progress.ToSaveData(), Slot, Player);
            Assert.That(service.TryGrowByPlacing(rule, student, progress), Is.False);

            var saved = student.ToSaveData();
            saved.CurrentDay = student.CurrentDay + 1;
            var nextDay = StudentLifeProgress.FromSaveData(saved);
            Assert.That(progress.GrowthLeftToday(nextDay.CurrentDay, rule.DailyLimit), Is.EqualTo(2));
            Assert.That(service.TryGrowByPlacing(rule, nextDay, progress), Is.True);
            Assert.That(nextDay.GetSkillValue("skill.interior"), Is.EqualTo(9));
        }

        [Test]
        public void GrowByPlacing_OtherPlayer_OrInvalidRule_Refused()
        {
            var service = new WorkspaceService();
            var student = new StudentLifeProgress(Slot, Player, 10, 10);
            var progress = new WorkspaceProgress(Slot, "someone-else");
            var rule = new PlacementSkillGrowth("growth.interior", Surface, "skill.interior", 3, 2);
            Assert.That(service.TryGrowByPlacing(rule, student, progress), Is.False);
            Assert.That(service.TryGrowByPlacing(new PlacementSkillGrowth("", Surface, "skill.interior", 3, 2), student, new WorkspaceProgress(Slot, Player)), Is.False);
            Assert.That(service.TryGrowByPlacing(null, student, new WorkspaceProgress(Slot, Player)), Is.False);
            Assert.That(student.GetSkillValue("skill.interior"), Is.Zero);
        }

        // --- 잘못된 데이터는 던지지 않고 알린다 ---

        [Test]
        public void InvalidData_IsReported_NotThrown()
        {
            Assert.That(new TemplatePlacement(null, P(0, 0), FurniturePlacementDirection.North).IsValid, Is.False);
            Assert.That(new TemplatePlacement("rug", P(0, 0), (FurniturePlacementDirection)99).IsValid, Is.False);
            Assert.That(new TemplatePlacement("rug", P(0, 0), FurniturePlacementDirection.North).IsValid, Is.True);

            var empty = new WorkspaceTemplate(null, null, null, null, null);
            Assert.That(empty.IsValid, Is.False);
            Assert.That(empty.Placements, Is.Empty);
            Assert.That(empty.FurnitureIds, Is.Empty);
            Assert.That(new WorkspaceTemplate("t", "w", null, new[] { new ItemAmount<string>(Coin, -1) }, null).IsValid, Is.False, "음수 비용");
            Assert.That(new WorkspaceTemplate("t", "w", null, null, new[] { new TemplatePlacement("", P(0, 0), FurniturePlacementDirection.North) }).IsValid, Is.False);

            var template = Template(10,
                new TemplatePlacement("rug", P(1, 1), FurniturePlacementDirection.North),
                new TemplatePlacement("chair", P(3, 1), FurniturePlacementDirection.North),
                new TemplatePlacement("rug", P(0, 2), FurniturePlacementDirection.North));
            Assert.That(template.IsValid, Is.True);
            Assert.That(template.FurnitureIds, Is.EqualTo(new[] { "rug", "chair" }));

            var growth = new PlacementSkillGrowth(null, null, null, -3, -1);
            Assert.That(growth.IsValid, Is.False);
            Assert.That(growth.Amount, Is.Zero);
            Assert.That(growth.DailyLimit, Is.Zero);
        }

        // --- 세이브 파일 ---

        [Test]
        public void SaveFileNames_ArePerPlayer_AndPerSurface()
        {
            Assert.That(SaveFileNames.WorkspaceProgress("p/1"), Is.EqualTo("workspace-progress-p_1.json"));
            Assert.That(SaveFileNames.WorkspaceProgress(null), Is.EqualTo("workspace-progress-player.json"));
            Assert.That(SaveFileNames.WorkspaceLayout("workplace.restaurant", "p1"), Is.EqualTo("workspace-layout-workplace_restaurant-p1.json"));
        }

        [Test]
        public void LayoutFile_MissingIsNull_AndRoundTrips()
        {
            var store = new InMemorySaveStore();
            Assert.That(WorkspaceSaveFiles.LoadLayout(store, Slot, Surface, Player), Is.Null);

            var records = new[] { new FurniturePlacementSaveData { FurnitureId = "rug", SurfaceId = Surface, AnchorX = 1, AnchorY = 1 } };
            WorkspaceSaveFiles.SaveLayout(store, Slot, Surface, Player, records);
            Assert.That(store.Contains(Slot, SaveFileNames.WorkspaceLayout(Surface, Player)), Is.True);
            var loaded = WorkspaceSaveFiles.LoadLayout(store, Slot, Surface, Player);
            Assert.That(loaded.Single().FurnitureId, Is.EqualTo("rug"));
            Assert.That(WorkspaceSaveFiles.LoadLayout(store, Slot, Surface, "someone-else"), Is.Null);

            store.Put(Slot, SaveFileNames.WorkspaceLayout(Surface, "broken"), new FurniturePlacementLayoutSaveData { Version = 2, Items = null });
            Assert.Throws<InvalidOperationException>(() => WorkspaceSaveFiles.LoadLayout(store, Slot, Surface, "broken"), "Items가 없는 파일은 빈 일터로 읽지 않는다");
        }

        [Test]
        public void SaveData_RoundTripsAndCleans()
        {
            var data = new WorkspaceProgressSaveData
            {
                SaveSlot = "", PlayerId = null, AppliedRequestIds = new[] { "b", "", null, "a" }, SeededWorkspaceIds = null, GrowthDay = -3, GrowthCountToday = -1,
            };
            var progress = WorkspaceProgress.FromSaveData(data, Slot, Player);
            Assert.That(progress.SaveSlot, Is.EqualTo(Slot));
            Assert.That(progress.PlayerId, Is.EqualTo(Player));
            var saved = progress.ToSaveData();
            Assert.That(saved.Version, Is.EqualTo(WorkspaceProgressSaveData.KnownVersion));
            Assert.That(saved.AppliedRequestIds, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(saved.SeededWorkspaceIds, Is.Empty);
            Assert.That(saved.GrowthDay, Is.Zero);
            Assert.That(saved.GrowthCountToday, Is.Zero);
            Assert.That(WorkspaceProgress.FromSaveData(null, Slot, Player).HasApplied("a"), Is.False);
        }

        /// <summary>파일 저장소(FileSaveStore)처럼 아는 버전보다 높은 파일은 읽지 않는 저장소.</summary>
        private sealed class VersionCheckingStore : ISaveStore
        {
            public readonly InMemorySaveStore Inner = new InMemorySaveStore();

            public bool TryRead<T>(string saveSlot, string fileName, out T data) where T : class, new()
            {
                if (!Inner.TryRead(saveSlot, fileName, out data)) return false;
                if (data is IVersionedSave versioned && versioned.Version > versioned.KnownVersion) throw new InvalidOperationException("too new");
                return true;
            }

            public void Write<T>(string saveSlot, string fileName, T data) where T : class => Inner.Write(saveSlot, fileName, data);
            public void Delete(string saveSlot, string fileName) => Inner.Delete(saveSlot, fileName);
        }

        [Test]
        public void SaveData_HigherVersion_IsNotRead()
        {
            IVersionedSave fresh = new WorkspaceProgressSaveData();
            Assert.That(fresh.KnownVersion, Is.EqualTo(1));
            Assert.That(fresh.Version, Is.EqualTo(1), "새 파일은 버전 1로 쓴다");

            var store = new VersionCheckingStore();
            store.Inner.Put(Slot, SaveFileNames.WorkspaceProgress(Player), new WorkspaceProgressSaveData { Version = 2, AppliedRequestIds = new[] { "req-1" } });
            Assert.Throws<InvalidOperationException>(() => WorkspaceSaveFiles.LoadOrCreate(store, Slot, Player), "높은 버전을 새 진행으로 읽지 않는다");
            Assert.That(store.Inner.Writes, Is.Zero, "읽다가 덮어쓰지 않는다");
        }
    }
}
