using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Careers;
using Rootborn.Core.Interaction;
using Rootborn.Core.Save;
using Rootborn.Core.StudentLife;
using Rootborn.Data;
using Rootborn.Data.Builders;
using Rootborn.Data.Save;
using Rootborn.Game;
using Rootborn.Screens.AdultFacility;
using Rootborn.Ui;

namespace Rootborn.Screens.Tests.AdultFacility
{
    /// <summary>
    /// 성인 시설 화면 시험 준비: 실제 data/와 임시 폴더 세이브로 새 게임을 시작해 한 번 저장하고(하루 끝내기 → 다음 날),
    /// 저장된 스냅샷을 고쳐(요리사 후보 준비, 성인 단계·숙련·재료) 다시 읽은 뒤 그 위에 화면 쌓기를 올린다.
    /// 스냅샷 고치기는 시작 상태를 만드는 준비 단계일 뿐 확인 대상이 아니다(Rootborn.Game.Tests/SaveEdits와 같은 방식).
    /// </summary>
    internal sealed class FacilityFixture : IDisposable
    {
        public const string Slot = "slot-test";
        public const string Chef = "facility.chef";
        private static readonly Lazy<GameDefinitions> Definitions = new Lazy<GameDefinitions>(() =>
        {
            LoadResult loaded = GameDataLoader.Load(TestData.DataDirectory, RootbornBuilders.Register);
            loaded.ThrowIfFailed();
            return loaded.Definitions;
        });

        private readonly string _saves;

        /// <param name="shares">같이 하는 방의 조각 묶음(방 지갑 시험). playerId는 그 방에서의 신원.</param>
        public FacilityFixture(Action<AdultCareerSnapshotSaveData> edit = null, string language = "ko", bool candidateReady = true, bool longText = false,
            Rootborn.Core.Room.RoomShares shares = null, string playerId = null)
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-facility-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            Store = new FileSaveStore(_saves);
            GameStartSettings settings = GameStartSettings.FromDefinitions(Definitions.Value);
            GameSession first = GameSession.Start(Definitions.Value, settings, Store, Slot, playerId);
            Assert.That(first.EndDay().Ended && first.StartNextDay(), Is.True, "준비: 한 번 저장한다");
            if (candidateReady || edit != null)
            {
                string name = SaveFileNames.AdultCareerSnapshot(first.PlayerId);
                Assert.That(Store.TryRead<AdultCareerSnapshotSaveData>(Slot, name, out var snapshot), Is.True);
                if (candidateReady) MakeChefCandidateReady(snapshot);
                edit?.Invoke(snapshot);
                Store.Write(Slot, name, snapshot);
            }
            Session = GameSession.Start(Definitions.Value, settings, Store, Slot, playerId, shares: shares);
            Text = longText ? LongText.Create(language) : TestData.Text(language);
            Stack = new ScreenStack(ScreenCatalog.Create(), new ScreenServices(Text, Session));
            Stack.Layout(1920, 1080);
            Stack.OpenHud();
        }

        public FileSaveStore Store { get; }
        public GameSession Session { get; private set; }
        public GameText Text { get; }
        public ScreenStack Stack { get; }

        /// <summary>Unity AdultCareerE2EScenarioTests.MakeCandidateReady: 4일째, 진행 중, 시간 포인트 0, career.chef 힌트, candidate.chef 이해도 3.</summary>
        public static void MakeChefCandidateReady(AdultCareerSnapshotSaveData snapshot)
        {
            var student = snapshot.Student;
            student.CurrentDay = Math.Max(4, student.CurrentDay);
            student.DayState = StudentDayState.InProgress.ToString();
            student.TimePointsSpentToday = 0;
            student.CareerHintIds = student.CareerHintIds.Concat(new[] { "career.chef" }).Distinct().ToArray();
            student.CareerCandidates ??= new CareerCandidateProgressSaveData { SaveSlot = student.SaveSlot, PlayerId = student.PlayerId };
            student.CareerCandidates.Candidates = new[]
            {
                new CareerCandidateProgressSaveData.CandidateEntry
                {
                    CandidateId = "candidate.chef", UnderstandingScore = 3, HintIds = new[] { "hint.chef.practice" }, LastHintSourceId = "hint.chef.practice",
                },
            };
        }

        /// <summary>이미 요리사 시설 1단계로 성인이 된 상태로 고친다.</summary>
        public static void AlreadyAdult(AdultCareerSnapshotSaveData snapshot, int level = 1)
        {
            snapshot.Adult.FacilityId = Chef;
            snapshot.Adult.FacilityLevel = level;
        }

        public static void AlreadyAdultAtLevelOne(AdultCareerSnapshotSaveData snapshot) => AlreadyAdult(snapshot, 1);

        /// <summary>그 아이템을 정확히 count개로 둔다(있던 것은 지운다).</summary>
        public static void SetCount(AdultCareerSnapshotSaveData snapshot, string itemId, int count)
        {
            if (snapshot.Inventory?.Items != null) snapshot.Inventory.Items = snapshot.Inventory.Items.Where(i => i.ItemId != itemId).ToArray();
            if (count > 0) Fill(snapshot, itemId, count);
        }

        public static void Fill(AdultCareerSnapshotSaveData snapshot, string itemId, int count)
        {
            var items = (snapshot.Inventory?.Items ?? Array.Empty<Rootborn.Core.Inventory.PlayerInventoryItemSaveData>()).ToList();
            items.Add(new Rootborn.Core.Inventory.PlayerInventoryItemSaveData { ItemId = itemId, Count = count });
            snapshot.Inventory ??= new Rootborn.Core.Inventory.PlayerInventorySaveData();
            snapshot.Inventory.Items = items.ToArray();
        }

        public bool OpenAt(string facilityId = Chef) => Stack.Interaction(new InteractionEvent("Town", FacilityScreen.InteractionKind, facilityId));

        public FacilityScreen Screen => (FacilityScreen)Stack.Find(ScreenCatalog.Facility);

        public UiNode Node(string id) => Stack.Tree.Root.Find(id) ?? throw new InvalidOperationException("UI 요소가 없다: " + id);

        /// <summary>한 번 그린다(배치가 글자 폭에 맞춰진다).</summary>
        public DrawList Emit()
        {
            var list = new DrawList();
            Stack.Emit(list, new TestMeasurer());
            Stack.Layout(1920, 1080);
            return list;
        }

        public void Click(string id) => ClickAt(Node(id), id);

        /// <summary>일 목록의 칸 하나를 포인터로 누른다.</summary>
        public void ClickCell(int cell)
        {
            Emit();
            UiRect r = Screen.CellRect(cell);
            ClickAt(Screen.WorkGrid, "facility.works#" + cell, r.CenterX, r.CenterY);
        }

        private void ClickAt(UiNode node, string label) => ClickAt(node, label, node.Rect.CenterX, node.Rect.CenterY);

        private void ClickAt(UiNode node, string label, float x, float y)
        {
            Assert.That(node.IsActive, Is.True, "보이지 않거나 비활성: " + label);
            Assert.That(Stack.Tree.HitTest(x, y), Is.SameAs(node), "요소 " + label + " 위에 다른 요소가 있다");
            Stack.Pointer(x, y, false, 0);
            Stack.Pointer(x, y, true, 0);
            Stack.Pointer(x, y, false, 0);
        }

        /// <summary>저장했다가 읽은 새 게임 한 판(저장된 값만 보려고).</summary>
        public GameSession Reload() => GameSession.Start(Definitions.Value, GameStartSettings.FromDefinitions(Definitions.Value), Store, Slot, null);

        public void Dispose()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }
    }
}
