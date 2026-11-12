using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rootborn.Core.Minigames;
using Rootborn.Core.Placement;
using Rootborn.Core.Quests;
using Rootborn.Core.World;
using Rootborn.Game;
using Rootborn.Host.World;
using Rootborn.Screens;
using Rootborn.Screens.Career;
using Rootborn.Screens.Day;
using Rootborn.Screens.Dialogue;
using Rootborn.Screens.Furniture;
using Rootborn.Screens.Hud;
using Rootborn.Screens.Minigames;
using Rootborn.Screens.Objectives;
using Rootborn.Screens.Start;
using Rootborn.Ui;
using Rootborn.World;

namespace Rootborn.Host.Tests
{
    /// <summary>
    /// 메인 체인(Unity MainChainOpeningE2EScenarioTests)을 실제 창과 게임 루프로 끝까지. 조작은 방향 키·E·Tab·Esc·글자·포인터뿐이다
    /// (PlayerDriver의 동사, 장치 이벤트 주입, OS 입력 계층 미확인). 순간이동·내부 명령 호출·상태 강제 설정 없음.
    /// 어디로 걸어갈지와 어느 칸·줄을 누를지 정할 때만 맵 데이터(상호작용 지점, NPC 자리)와 화면 모델을 읽는다.
    ///
    /// MAIN_CHAIN_000은 실행 → 슬롯 선택 → 새 게임 → 캐릭터 만들기부터 체인 완료까지 한 번에 가고, 2일째 시작에서 끄고 같은 슬롯을 이어 한다.
    /// MAIN_CHAIN_001~011은 원본의 단계 테스트에 대응한다. 원본은 그 단계 앞까지를 퀘스트 세이브를 넣어 만들었지만(준비 단계), 여기서는
    /// 그 단계 앞까지도 실행부터 실제 입력으로 진행한다(PlayTo). 그래서 준비 단계가 없고, 각 테스트가 따로 돈다.
    /// 스크린샷: output/host-screens-evidence/main-chain-*.png(000), main-chain-step-*.png(단계 테스트의 그 단계).
    /// </summary>
    [TestFixture, SingleThreaded, NonParallelizable]
    public sealed class MainChainHostTests
    {
        public const string ChainId = "main-story.school-opening";
        public const string FreeDesk = "growth31.assembly.desk";
        public const string Shop = "location.corner-store";
        public const string ShopWork = "work.shift.corner-store";
        public const string Library = "location.library";
        public const string LibraryBooks = "opening.clue.archive-door";
        public const string Restaurant = "location.restaurant", Clinic = "location.medical-clinic";
        public static readonly string[] TourLocations = { Restaurant, Clinic, "location.design-studio", "location.training-ground" };

        private string _saves;
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _saves = Path.Combine(Path.GetTempPath(), "rootborn-main-chain-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            _root = HostOptions.Resolve(new[] { "--sample" }).RepoRoot;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_saves, true); } catch (IOException) { }
        }

        /// <summary>인자 없이(세이브 폴더만) 띄운다: 슬롯 선택부터.</summary>
        private MainChainPlayer Launch()
        {
            HostOptions options = HostOptions.Resolve(new[] { "--save-root", _saves, "--size", "1920x1080", "--evidence", Path.Combine(_root, "output", "host-screens-evidence") });
            options.TestMode = true;
            var player = new PlayerDriver(options);
            player.Frames(3);
            return new MainChainPlayer(player);
        }

        // ---------- 단계 앞까지(실제 입력) ----------

        /// <summary>메인 체인이 거치는 곳. PlayTo가 앞에서부터 이 순서로 진행한다.</summary>
        public enum Stage
        {
            /// <summary>001 끝: 엄마 → 수첩에서 추적 → 일과 → 학교 선생님(1일째, BackToSchool 목표 둘 다).</summary>
            TeacherTalked = 1,
            /// <summary>002 끝: 집에서 하루 마감(2일째, AfterSchoolTour).</summary>
            DayTwo,
            /// <summary>003 끝: 일터 네 곳(FirstPartTimeJob).</summary>
            Toured,
            /// <summary>004 끝: 가게 알바 한 번.</summary>
            ShiftWorked,
            /// <summary>005 끝: 하루 마감(3일째, MyDream).</summary>
            DayThree,
            /// <summary>006 끝: 도서관 직업 책(MyOwnSpace).</summary>
            BooksRead,
        }

        /// <summary>실행부터 그 곳까지 실제 입력으로 진행한다. shots가 참이면 단계마다 찍는다(000).</summary>
        private static void PlayTo(MainChainPlayer p, Stage stage, bool shots = false)
        {
            string S(string name) => shots ? "main-chain-" + name : null;
            p.NewStory("하늘", shots);
            p.Shot(S("00-house"));
            p.AssertChain(null, 1);

            // 001: 집에서 엄마 → 수첩에서 메인 체인 추적 → 일과 → 학교 선생님.
            p.TalkTo("npc.first-guide", S("01-mom"));
            p.AssertChain("mainquest.BackToSchool", 1);
            p.TrackMainChain(S("02-journal-tracked"));
            p.AssertTrackedHud("mainquest.BackToSchool");
            p.ChooseFirstPlan(S("03-plan"));
            p.GoTo("School");
            p.TalkTo("npc.teacher", S("04-teacher"));
            p.AssertObjectives(1, 1);
            p.AssertChain("mainquest.BackToSchool", 1);
            if (stage == Stage.TeacherTalked) return;

            // 002: 집에 가서 하루 마감 → 2일째.
            p.GoTo("House");
            p.EndDay(S("05-day-1-result"));
            p.AssertChain("mainquest.AfterSchoolTour", 2);
            p.AssertTrackedHud("mainquest.AfterSchoolTour");
            if (stage == Stage.DayTwo) return;
            ContinueFromDayTwo(p, stage, shots);
        }

        /// <summary>2일째 집에서부터(이어 하기 뒤에도 같은 길).</summary>
        private static void ContinueFromDayTwo(MainChainPlayer p, Stage stage, bool shots)
        {
            string S(string name) => shots ? "main-chain-" + name : null;

            // 003: 마을의 일터 네 곳.
            p.GoTo("Town");
            foreach (string place in TourLocations) p.VisitPlace(place, null, S("07-tour-" + place.Substring("location.".Length)));
            p.AssertChain("mainquest.FirstPartTimeJob", 2);
            if (stage == Stage.Toured) return;

            // 004: 가게 알바를 미니게임으로.
            p.WorkShift(S("08-shift"));
            p.AssertObjectives(1);
            p.AssertChain("mainquest.FirstPartTimeJob", 2);
            if (stage == Stage.ShiftWorked) return;

            // 005: 집에서 하루 마감 → 3일째.
            p.GoTo("House");
            p.EndDay(S("09-day-2-result"));
            p.AssertChain("mainquest.MyDream", 3);
            if (stage == Stage.DayThree) return;

            // 006: 도서관에서 직업 책 읽기.
            p.GoTo("Town");
            p.VisitPlace(Library, LibraryBooks, S("10-library"));
            p.AssertChain("mainquest.MyOwnSpace", 3);
        }

        // ---------- 처음부터 끝까지 ----------

        [Test]
        public void MAIN_CHAIN_000_FromLaunchToMyOwnRoom_WithARestartAtDayTwo()
        {
            var clock = Stopwatch.StartNew();
            var marks = new List<string>();
            using (MainChainPlayer p = Launch())
            {
                PlayTo(p, Stage.DayTwo, shots: true);
                marks.Add("2일째 시작 " + clock.Elapsed.TotalSeconds.ToString("0") + "s");
            }

            // 끄고 같은 슬롯을 이어서 시작한다(2일째 시작).
            using (MainChainPlayer p = Launch())
            {
                p.Continue(0);
                Assert.That(p.Player.World.Map.Id, Is.EqualTo("House"), "하루를 마감한 집에서 이어 한다");
                p.AssertChain("mainquest.AfterSchoolTour", 2);
                p.AssertTrackedHud("mainquest.AfterSchoolTour");
                p.Shot("main-chain-06-continue-day-2");

                ContinueFromDayTwo(p, Stage.BooksRead, shots: true);
                marks.Add("도서관 " + clock.Elapsed.TotalSeconds.ToString("0") + "s");

                // 009: 식당 소개를 듣고 장래희망을 고른다.
                p.VisitPlace(Restaurant, p.FirstActivityAt(Restaurant), "main-chain-11-restaurant-intro");
                string dream = p.ChooseFirstAllowedDream("main-chain-12-dream");
                Assert.That(p.Session.CareerInterests.CurrentInterestId, Is.EqualTo(dream));

                // 007: 집에 가서 받은 가구(책상)를 놓으면 체인 완료. 놓은 책상 옆까지 걸어가 본다.
                p.GoTo("House");
                p.PlaceFurniture(MainChainHostTests.FreeDesk, "main-chain-13-desk", firstSlot: false);
                Assert.That(p.ChainState, Is.EqualTo(QuestChainState.Completed), "집에 가구를 놓으면 체인이 끝난다");
                p.WalkNextToPlacedFurniture(MainChainHostTests.FreeDesk, "main-chain-14-complete-next-to-desk");
                p.AssertClean();
            }

            using (MainChainPlayer p = Launch())
            {
                p.Continue(0);
                Assert.That(p.ChainState, Is.EqualTo(QuestChainState.Completed), "다시 띄워도 완료");
                Assert.That(p.Session.House.Placed.Select(f => f.Definition.StableId), Does.Contain(FreeDesk));
                Assert.That(p.Player.Game.World.FurnitureDrawCount, Is.EqualTo(1), "놓은 책상을 그린다");
                p.Shot("main-chain-15-complete-after-restart");
            }

            marks.Add("전체 " + clock.Elapsed.TotalSeconds.ToString("0") + "s");
            TestContext.Progress.WriteLine("[MAIN_CHAIN_000] " + string.Join(", ", marks));
        }

        // ---------- 단계 테스트(원본 001~011) ----------

        [Test]
        public void MAIN_CHAIN_001_BackToSchool_MomStartsTheChain_PlanAndTeacherCount()
        {
            using MainChainPlayer p = Launch();
            PlayTo(p, Stage.TeacherTalked);
            p.Shot("main-chain-step-001-teacher");
            Assert.That(p.Session.TrackedObjective().Objectives.Select(o => o.Current), Is.EqualTo(new[] { 1, 1 }));
            p.AssertClean();
        }

        [Test]
        public void MAIN_CHAIN_002_BackToSchool_SleepingIntoDayTwoOpensTheTour()
        {
            using MainChainPlayer p = Launch();
            PlayTo(p, Stage.DayTwo);
            p.Shot("main-chain-step-002-day-two");
            p.AssertClean();
        }

        [Test]
        public void MAIN_CHAIN_003_AfterSchoolTour_VisitingTheFourCareerPlaces()
        {
            using MainChainPlayer p = Launch();
            PlayTo(p, Stage.Toured);
            foreach (string place in TourLocations)
                Assert.That(new Rootborn.Core.StudentLife.LocationVisitProgress(p.Session.Student).HasVisited(place), Is.True, place);
            p.Shot("main-chain-step-003-toured");
            p.AssertClean();
        }

        [Test]
        public void MAIN_CHAIN_004_FirstPartTimeJob_AShiftCounts()
        {
            using MainChainPlayer p = Launch();
            PlayTo(p, Stage.Toured);
            int money = p.Allowance;
            PlayTo_Shift(p);
            Assert.That(p.Allowance, Is.GreaterThan(money), "알바가 용돈을 준다");
            p.Shot("main-chain-step-004-shift");
            p.AssertClean();
        }

        private static void PlayTo_Shift(MainChainPlayer p)
        {
            p.WorkShift("main-chain-step-004-minigame");
            p.AssertObjectives(1);
            p.AssertChain("mainquest.FirstPartTimeJob", 2);
        }

        [Test]
        public void MAIN_CHAIN_005_FirstPartTimeJob_DayThreeOpensMyDream()
        {
            using MainChainPlayer p = Launch();
            PlayTo(p, Stage.DayThree);
            p.Shot("main-chain-step-005-day-three");
            p.AssertClean();
        }

        [Test]
        public void MAIN_CHAIN_006_MyDream_ReadingJobBooksAtTheLibrary()
        {
            using MainChainPlayer p = Launch();
            PlayTo(p, Stage.BooksRead);
            p.Shot("main-chain-step-006-books");
            p.AssertClean();
        }

        [Test]
        public void MAIN_CHAIN_007_MyOwnSpace_PlacingRoomFurnitureCompletesTheChain()
        {
            using MainChainPlayer p = Launch();
            PlayTo(p, Stage.BooksRead);
            p.GoTo("House");
            Assert.That(p.ChainState, Is.EqualTo(QuestChainState.Tracked), "들어오기만 해서는 끝나지 않는다");
            p.PlaceFurniture(FreeDesk, "main-chain-step-007-desk", firstSlot: false);
            Assert.That(p.ChainState, Is.EqualTo(QuestChainState.Completed));
            p.AssertClean();
        }

        /// <summary>3일째 도서관 뒤: 알바 한 번 더(남은 TP 1)로 값을 모아 가게에서 산다. 값은 한 번만, 산 물건은 다시 눌리지 않는다.</summary>
        private static FurnitureDefinition EarnAndBuy(MainChainPlayer p, string shot)
        {
            FurnitureDefinition offer = p.Session.FurnitureForSaleAt(Shop)[0];
            for (int shift = 0; p.Allowance < offer.Price; shift++)
            {
                Assert.That(shift, Is.LessThan(3), "알바로 값을 모으지 못했다");
                int before = p.Allowance;
                p.WorkShift(null);
                Assert.That(p.Allowance, Is.GreaterThan(before), "알바가 용돈을 주지 않았다");
            }
            int money = p.Allowance;
            p.BuyAt(Shop, offer.StableId, shot);
            Assert.That(p.Allowance, Is.EqualTo(money - offer.Price), "값은 한 번만 낸다");
            Assert.That(p.Session.FurnitureUnlocks.IsUnlocked(offer.UnlockToken), Is.True, "산 가구가 열렸다");
            return offer;
        }

        [Test]
        public void MAIN_CHAIN_008_MyOwnSpace_BuyingFurnitureWithShiftAllowance()
        {
            using MainChainPlayer p = Launch();
            PlayTo(p, Stage.BooksRead);
            EarnAndBuy(p, "main-chain-step-008-bought");
            p.AssertClean();
        }

        [Test]
        public void MAIN_CHAIN_009_MyDream_ChoosingADreamShowsItInTheJournal()
        {
            using MainChainPlayer p = Launch();
            PlayTo(p, Stage.DayThree);
            p.GoTo("Town");
            p.VisitPlace(Restaurant, p.FirstActivityAt(Restaurant), null);
            string dream = p.ChooseFirstAllowedDream("main-chain-step-009-dream");
            Assert.That(p.Step, Is.EqualTo("mainquest.MyOwnSpace"), "장래희망을 고르면 MyDream이 끝난다");
            string shown = p.JournalHintDetail(dream, "main-chain-step-009-journal");
            string name = p.Player.Game.Text.Get(p.Session.Definitions.Get<Rootborn.Core.StudentLife.CareerInterest>(dream).DisplayNameKey);
            Assert.That(shown, Does.Contain(p.Player.Game.Text.Format("dream.journal.current", name)), "수첩이 고른 장래희망을 말한다");
            p.AssertClean();
        }

        [Test]
        public void MAIN_CHAIN_010_MyOwnSpace_PlacingBoughtFurnitureCompletesTheChain()
        {
            using MainChainPlayer p = Launch();
            PlayTo(p, Stage.BooksRead);
            FurnitureDefinition offer = p.Session.FurnitureForSaleAt(Shop)[0];
            p.GoTo("House");
            Assert.That(p.PaletteHas(offer.StableId), Is.False, "사기 전에는 집 팔레트에 없다");
            p.GoTo("Town");
            EarnAndBuy(p, null);
            p.GoTo("House");
            p.PlaceFurniture(offer.StableId, "main-chain-step-010-bought-placed", firstSlot: true);
            Assert.That(p.ChainState, Is.EqualTo(QuestChainState.Completed), "산 가구를 놓아도 체인이 끝난다");
            p.AssertClean();
        }

        [Test]
        public void MAIN_CHAIN_011_ASecondDreamChangeTheSameDayIsRefused_AndStaysRefusedAfterContinue()
        {
            string first, second;
            using (MainChainPlayer p = Launch())
            {
                PlayTo(p, Stage.DayThree);
                p.GoTo("Town");
                p.VisitPlace(Restaurant, p.FirstActivityAt(Restaurant), null);
                p.VisitPlace(Clinic, p.FirstActivityAt(Clinic), null);
                string[] allowed = p.AllowedDreams();
                Assert.That(allowed.Length, Is.GreaterThanOrEqualTo(2), "두 소개 뒤에는 두 장래희망을 고를 수 있다");
                (first, second) = (allowed[0], allowed[1]);
                p.OpenDreams();
                p.ClickDream(first);
                Assert.That(p.Session.CareerInterests.CurrentInterestId, Is.EqualTo(first));
                p.ClickDream(second);
                Assert.That(p.Session.CareerInterests.CurrentInterestId, Is.EqualTo(first), "같은 날 두 번째 바꾸기는 거절");
                Assert.That(p.DreamCardState(second), Is.EqualTo(Rootborn.Core.StudentLife.CareerInterestSelectionState.ChangedToday), "카드가 오늘 바꿨다고 말한다");
                p.ShotDreams("main-chain-step-011-refused");
                p.CloseDreams();
                string firstName = p.Player.Game.Text.Get(p.Session.Definitions.Get<Rootborn.Core.StudentLife.CareerInterest>(first).DisplayNameKey);
                Assert.That(p.JournalHintDetail(first, null), Does.Contain(p.Player.Game.Text.Format("dream.journal.current", firstName)));
            }

            using (MainChainPlayer p = Launch())
            {
                p.Continue(0);
                Assert.That(p.Session.CareerInterests.CurrentInterestId, Is.EqualTo(first), "이어 하기가 고른 장래희망을 지킨다");
                p.OpenDreams();
                p.ClickDream(second);
                Assert.That(p.Session.CareerInterests.CurrentInterestId, Is.EqualTo(first), "이어 하기가 같은 날 제한을 풀지 않는다");
                p.ShotDreams("main-chain-step-011-refused-after-continue");
                p.CloseDreams();
                p.AssertClean();
            }
        }
    }

    /// <summary>메인 체인을 걷는 플레이어. 동작마다 PlayerDriver의 동사만 쓴다.</summary>
    internal sealed class MainChainPlayer : IDisposable
    {
        public readonly PlayerDriver Player;

        public MainChainPlayer(PlayerDriver player)
        {
            Player = player;
        }

        public GameSession Session => Player.Game.Session ?? throw new InvalidOperationException("게임 한 판이 없다");
        public QuestChainDefinition Chain => Session.Chain(MainChainHostTests.ChainId);
        public string Step => Session.QuestChains.GetCurrentStep(Chain)?.Id;
        public QuestChainState ChainState => Session.QuestChains.GetState(Chain);
        public int Allowance => Session.Inventory.CountOf(Session.Item("item.allowance"));

        public void Dispose() => Player.Dispose();

        private T Screen<T>(string id) where T : GameScreen => (T)Player.Screens.Find(id);

        /// <summary>전체 화면을 찍는다. name이 null이면 찍지 않는다(단계 앞까지 진행할 때).</summary>
        public void Shot(string name)
        {
            if (name == null) return;
            Player.Frames(2);
            Player.Screenshot(name);
        }

        private void ShotScreen(string id, string name)
        {
            if (name == null) return;
            Player.MovePointer(-50, -50);
            Player.Frames(2);
            Player.ScreenshotScreen(id, name);
        }

        // ---------- 확인 ----------

        public void AssertChain(string step, int day)
        {
            if (step == null) Assert.That(ChainState, Is.EqualTo(QuestChainState.Available), "아직 체인 전");
            else Assert.That(Step, Is.EqualTo(step), "체인 단계");
            Assert.That(Session.StudentDay, Is.EqualTo(day), "날");
            Assert.That(Player.Screens.MovementBlocked, Is.False, "막는 화면이 남아 있지 않다");
        }

        public void AssertObjectives(params int[] progress)
        {
            for (int i = 0; i < progress.Length; i++)
                Assert.That(Session.QuestChains.GetObjectiveProgress(Chain, i), Is.EqualTo(progress[i]), "목표 " + i);
        }

        public void AssertTrackedHud(string step)
        {
            var hud = Screen<TrackedObjectiveHudScreen>(ScreenCatalog.HudTracked);
            Assert.That(hud.IsShown, Is.True, "추적 목표 HUD가 보인다");
            Assert.That(Session.TrackedObjective().StepId, Is.EqualTo(step));
            Assert.That(hud.VisibleText, Is.Not.Empty);
        }

        /// <summary>남은 것: 빠진 현지화 키, 못 찾은 그림, 벽을 뚫은 걸음.</summary>
        public void AssertClean()
        {
            Assert.That(Player.CollisionViolations, Is.EqualTo(0), "벽을 뚫고 걷지 않았다");
            Rootborn.Screens.Tests.KnownMissingKeys.AssertOnlyKnownMissing(Player.Game.Text, Array.Empty<string>());
            Assert.That(Player.Game.Renderer.MissingImages, Is.Empty);
        }

        // ---------- 시작 화면 ----------

        public void NewStory(string name, bool shots)
        {
            Assert.That(Player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True, "실행하면 슬롯 선택부터");
            ShotScreen(ScreenCatalog.SaveSlotSelect, shots ? "main-chain-00-slots" : null);
            Player.PressButton(SaveSlotScreen.NewGameId(0));
            Player.WaitForScreen(ScreenCatalog.CharacterCreator);
            Player.PressButton(CharacterCreatorScreen.NameFieldId);
            Player.TypeText(name);
            Player.PressKey(TestKey.Escape);
            ShotScreen(ScreenCatalog.CharacterCreator, shots ? "main-chain-00-creator" : null);
            Player.PressButton(CharacterCreatorScreen.ConfirmId);
            Player.Frames(3);
            Assert.That(Player.Game.World, Is.Not.Null, "시작하기로 월드에 들어간다");
            Assert.That(Player.World.Map.Id, Is.EqualTo("House"));
        }

        public void Continue(int slot)
        {
            Assert.That(Player.Screens.IsOpen(ScreenCatalog.SaveSlotSelect), Is.True);
            Player.PressButton(SaveSlotScreen.ContinueId(slot));
            Player.Frames(3);
            Assert.That(Player.Game.World, Is.Not.Null, "이어서 시작으로 월드에 들어간다");
        }

        // ---------- 걷기 ----------

        /// <summary>맵 사이 이동: 문 목적지 값으로 걷는다(지금 맵 기준 문 이름 — 맵이 바뀌면 이 표만 고친다).</summary>
        private static readonly Dictionary<(string From, string To), string> Doors = new Dictionary<(string, string), string>
        {
            { ("House", "Town"), "town" }, { ("Town", "House"), "house" }, { ("Town", "School"), "north" }, { ("School", "Town"), "town" },
        };

        private static readonly Dictionary<(string From, string To), string> Via = new Dictionary<(string, string), string>
        {
            { ("House", "School"), "Town" }, { ("School", "House"), "Town" },
        };

        public void GoTo(string map)
        {
            for (int hop = 0; hop < 4 && Player.World.Map.Id != map; hop++)
            {
                string from = Player.World.Map.Id;
                string next = Via.TryGetValue((from, map), out string via) ? via : map;
                Assert.That(Doors.TryGetValue((from, next), out string door), Is.True, from + "에서 " + next + "로 가는 문을 모른다");
                Assert.That(Player.WalkThroughDoor(door), Is.EqualTo(next));
                DismissPopups();
            }
            Assert.That(Player.World.Map.Id, Is.EqualTo(map));
        }

        /// <summary>저절로 뜬 하루 이벤트가 있으면 닫기로 닫는다(Unity DismissDailyEvent).</summary>
        public void DismissPopups()
        {
            Player.Frames(2);
            if (Player.Screens.IsOpen(ScreenCatalog.DailyEvent))
            {
                Player.PressButton(DailyEventScreen.CloseId);
                Player.WaitForScreenClosed(ScreenCatalog.DailyEvent);
            }
        }

        private InteractionThing Thing(string kind, string id)
        {
            InteractionThing thing = Player.Game.World.HereData.Things.FirstOrDefault(t => t.Kind == kind && (id == null || t.Id == id));
            Assert.That(thing, Is.Not.Null, Player.World.Map.Id + "에 " + kind + "/" + id + " 지점이 없다");
            return thing;
        }

        /// <summary>지점까지 걸어가 그것이 E의 대상인지 본다.</summary>
        private void WalkToThing(InteractionThing thing)
        {
            float within = Player.World.Map.Grid.CellSize * 1.25f;
            Player.WalkNear(thing.X, thing.Y, within);
            var target = Player.Game.World.Interactions.Target;
            Assert.That((target?.Kind, target?.Id), Is.EqualTo((thing.Kind, thing.Id)), "걸어간 자리에서 E의 대상");
        }

        // ---------- 대화 ----------

        public void TalkTo(string npcId, string shot)
        {
            NpcOnMap npc = Player.Game.World.Npcs.FirstOrDefault(n => n.NpcId == npcId);
            Assert.That(npc, Is.Not.Null, Player.World.Map.Id + "에 " + npcId + "가 서 있지 않다 (" + Session.StudentHour + "시)");
            Player.WalkNear(npc.Pose.X, npc.Pose.Y + Player.World.Map.Grid.CellSize, Player.World.Map.Grid.CellSize * 0.75f);
            Assert.That(Player.Game.World.Interactions.Target?.Id, Is.EqualTo(npcId), "NPC 앞에서 E의 대상");
            Player.PressKey(TestKey.E);
            Player.WaitForScreen(ScreenCatalog.Dialogue);
            Assert.That(Session.CurrentDialogueNpcId, Is.EqualTo(npcId));
            ShotScreen(ScreenCatalog.Dialogue, shot);
            Player.PressButton(DialogueScreen.CloseId);
            Player.WaitForScreenClosed(ScreenCatalog.Dialogue);
        }

        // ---------- 수첩 ----------

        public void TrackMainChain(string shot)
        {
            Player.OpenScreenWithKey(TestKey.Tab, ScreenCatalog.ObjectiveJournal);
            var journal = Screen<ObjectiveJournalScreen>(ScreenCatalog.ObjectiveJournal);
            Player.PressButton(ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Quests));
            int row = journal.Items.ToList().FindIndex(i => i.StableKey.Contains(MainChainHostTests.ChainId));
            Assert.That(row, Is.GreaterThanOrEqualTo(0), "수첩에 메인 체인 줄이 있다. 줄: " + string.Join(", ", journal.Items.Select(i => i.StableKey)));
            UiRect rect = journal.List.RowRect(row);
            Assert.That(Player.Game.Ui.HitTest(rect.CenterX, rect.CenterY), Is.SameAs(journal.List));
            Player.ClickAt(rect.CenterX, rect.CenterY);
            Player.PressButton(ObjectiveJournalScreen.TrackId);
            ShotScreen(ScreenCatalog.ObjectiveJournal, shot);
            Player.CloseScreenWithKey(TestKey.Tab, ScreenCatalog.ObjectiveJournal);
        }

        // ---------- 일과 ----------

        public void ChooseFirstPlan(string shot)
        {
            WalkToThing(Thing(DailyPlanScreen.InteractionKind, null));
            Player.PressKey(TestKey.E);
            Player.WaitForScreen(ScreenCatalog.DailyPlan);
            var plan = Screen<DailyPlanScreen>(ScreenCatalog.DailyPlan);
            Assert.That(plan.Model.Routes.Count, Is.GreaterThan(0), "고를 일과가 있다");
            UiRect card = plan.RouteCardRect(0);
            Assert.That(Player.Game.Ui.HitTest(card.CenterX, card.CenterY)?.Id, Is.EqualTo(DailyPlanScreen.RouteGridId));
            Player.ClickAt(card.CenterX, card.CenterY);
            Assert.That(Session.DailyPlan.Slots[0].RouteId, Is.EqualTo(plan.Model.Routes[0].RouteId), "포인터로 고른 일과");
            ShotScreen(ScreenCatalog.DailyPlan, shot);
            Player.PressButton(DailyPlanScreen.CloseTopId);
            Player.WaitForScreenClosed(ScreenCatalog.DailyPlan);
        }

        // ---------- 하루 마감 ----------

        public void EndDay(string shot)
        {
            int day = Session.StudentDay;
            WalkToThing(Thing(DayResultScreen.InteractionKind, null));
            Player.PressKey(TestKey.E);
            Player.WaitForScreen(ScreenCatalog.DayResult);
            ShotScreen(ScreenCatalog.DayResult, shot);
            Player.PressButton(DayResultScreen.NextDayId);
            Player.WaitForScreenClosed(ScreenCatalog.DayResult);
            DismissPopups();
            Assert.That(Session.StudentDay, Is.EqualTo(day + 1), "다음 날");
        }

        // ---------- 장소 ----------

        /// <summary>그 장소의 첫 활동 id(화면이 보이는 첫 줄).</summary>
        public string FirstActivityAt(string locationId) => LocationModel.From(Session, locationId).Rows.First(r => r.Kind == LocationRowKind.Activity).Id;

        /// <summary>장소 지점까지 걸어가 E → 장소 화면(방문). activity가 있으면 그 줄을 포인터로 누른다. 닫고, 뒤에 뜬 하루 이벤트도 닫는다.</summary>
        public void VisitPlace(string locationId, string activity, string shot)
        {
            WalkToThing(Thing(LocationScreen.InteractionKind, locationId));
            Player.PressKey(TestKey.E);
            Player.WaitForScreen(ScreenCatalog.Location);
            var screen = Screen<LocationScreen>(ScreenCatalog.Location);
            Assert.That(screen.Model.LocationId, Is.EqualTo(locationId));
            if (activity != null)
            {
                int row = screen.Model.Rows.ToList().FindIndex(r => r.Id == activity);
                Assert.That(row, Is.GreaterThanOrEqualTo(0), locationId + " 화면에 " + activity + " 줄이 없다");
                Assert.That(screen.Model.Rows[row].Enabled, Is.True, activity + " 줄을 누를 수 없다: " + screen.Model.Rows[row].Refusal);
                int logs = Session.Student.GetActivityLogIds().Length;
                UiRect rect = screen.RowRect(row);
                Assert.That(Player.Game.Ui.HitTest(rect.CenterX, rect.CenterY)?.Id, Is.EqualTo(LocationScreen.RowsId), "줄이 보이고 가려지지 않았다");
                Player.ClickAt(rect.CenterX, rect.CenterY);
                Assert.That(Session.Student.GetActivityLogIds().Length, Is.EqualTo(logs + 1), activity + "를 했다");
            }
            ShotScreen(ScreenCatalog.Location, shot);
            Player.PressButton(LocationScreen.CloseId);
            Player.WaitForScreenClosed(ScreenCatalog.Location);
            DismissPopups();
        }

        // ---------- 알바 ----------

        /// <summary>가게 알바 자리까지 걸어가 E → 미니게임(순서 외우기)을 포인터로 → 정산 → Esc.</summary>
        public void WorkShift(string shot)
        {
            WalkToThing(Thing("part-time-work", MainChainHostTests.ShopWork));
            Player.PressKey(TestKey.E);
            Player.WaitForScreen(ScreenCatalog.Minigame);
            var screen = Screen<MinigameScreen>(ScreenCatalog.Minigame);
            for (int guard = 0; guard < 8 && !screen.Session.IsComplete; guard++)
            {
                ChallengeSession challenge = screen.Session.Current;
                Assert.That(challenge.Mechanic, Is.EqualTo(MinigameMechanic.Sequence), "가게 알바는 순서 외우기다(다른 종류는 이 도우미가 아직 못 한다)");
                int[] order = challenge.PreviewSequence;
                for (int i = 0; i < 600 && challenge.PreviewVisible; i++) Player.Frames(1);
                foreach (int answer in order) Player.PressButton(SymbolChoiceView.ChoiceId(answer));
                for (int i = 0; i < 120 && screen.Session.Current == challenge && !screen.Session.IsComplete; i++) Player.Frames(1);
            }
            Assert.That(screen.Session.IsComplete && screen.Session.Succeeded, Is.True, "알바 미니게임 성공");
            Assert.That(Session.Student.HasPendingMinigame, Is.False, "정산됐다");
            ShotScreen(ScreenCatalog.Minigame, shot);
            Player.PressKey(TestKey.Escape);
            Player.WaitForScreenClosed(ScreenCatalog.Minigame);
            DismissPopups();
        }

        // ---------- 장래희망 ----------

        /// <summary>HUD의 장래희망 버튼 → 고를 수 있는 첫 카드 → 닫기. 고른 장래희망 id.</summary>
        public string ChooseFirstAllowedDream(string shot)
        {
            Player.PressButton("hud.button.dream");
            Player.WaitForScreen(ScreenCatalog.CareerInterest);
            var screen = Screen<CareerInterestScreen>(ScreenCatalog.CareerInterest);
            var allowed = new HashSet<string>(Session.CareerInterestChoices().Where(c => c.Evaluation.Allowed).Select(c => c.Interest.Id));
            int index = Array.FindIndex(screen.Model.Cards, c => allowed.Contains(c.InterestId));
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "고를 수 있는 장래희망 카드가 있다");
            string id = screen.Model.Cards[index].InterestId;
            ClickCard(index);
            Assert.That(Session.CareerInterests.CurrentInterestId, Is.EqualTo(id), "카드를 눌러 골랐다");
            ShotScreen(ScreenCatalog.CareerInterest, shot);
            Player.PressButton(CareerInterestScreen.CloseId);
            Player.WaitForScreenClosed(ScreenCatalog.CareerInterest);
            return id;
        }

        public void ClickCard(int index)
        {
            var cards = (UiCardRow)Player.Node(CareerInterestScreen.CardsId);
            UiRect r = cards.CardRect(index);
            Assert.That(Player.Game.Ui.HitTest(r.CenterX, r.CenterY), Is.SameAs(cards), "카드 자리 맨 위 요소");
            Player.ClickAt(r.CenterX, r.CenterY);
        }

        // ---------- 가구 ----------

        private HostPlacementView View() => new HostPlacementView(Player.Game.World, () => Player.Game.Fit.Scale);

        /// <summary>집 HUD 버튼으로 배치 켬 → 검색 칸을 비움.</summary>
        private FurniturePlacementScreen OpenPalette()
        {
            Player.PressButton("hud.button.placement");
            Player.WaitForScreen(ScreenCatalog.FurniturePlacement);
            var screen = Screen<FurniturePlacementScreen>(ScreenCatalog.FurniturePlacement);
            if (screen.Palette.Query.Length > 0)
            {
                Player.PressButton(FurniturePlacementScreen.SearchId);
                Player.PressKey(TestKey.Backspace, screen.Palette.Query.Length + 2);
                Player.PressKey(TestKey.Escape);
            }
            Assert.That(screen.Palette.Query, Is.Empty, "검색 칸을 비웠다");
            return screen;
        }

        private void ClosePalette()
        {
            Player.PressButton("hud.button.placement");
            Player.WaitForScreenClosed(ScreenCatalog.FurniturePlacement);
        }

        /// <summary>집 팔레트에 그 가구가 있나: 배치를 켜고 검색 칸에 id를 친 뒤 칸을 본다. 닫고 돌아온다.</summary>
        public bool PaletteHas(string furnitureId)
        {
            FurniturePlacementScreen screen = OpenPalette();
            Player.PressButton(FurniturePlacementScreen.SearchId);
            Player.TypeText(furnitureId);
            Player.PressKey(TestKey.Escape);
            bool found = Enumerable.Range(0, 20).Any(i => screen.Palette.At(i)?.StableId == furnitureId);
            ClosePalette();
            return found;
        }

        /// <summary>
        /// 배치 켬 → 가구 고르기 → 놓을 수 있는 칸을 포인터로 눌러 놓음 → 배치 끔. firstSlot이면 검색 없이 첫 쪽 첫 칸이 그 가구여야 한다
        /// (산 가구는 목록 맨 앞, Unity MAIN_E2E_010). 아니면 검색 칸에 id를 친다.
        /// </summary>
        public void PlaceFurniture(string furnitureId, string shot, bool firstSlot)
        {
            FurniturePlacementScreen screen = OpenPalette();
            int slot;
            if (firstSlot)
            {
                Assert.That(screen.Palette.Page, Is.EqualTo(0));
                Assert.That(screen.Palette.At(0)?.StableId, Is.EqualTo(furnitureId), "산 가구가 팔레트 첫 칸");
                slot = 0;
            }
            else
            {
                Player.PressButton(FurniturePlacementScreen.SearchId);
                Player.TypeText(furnitureId);
                slot = Enumerable.Range(0, 20).FirstOrDefault(i => screen.Palette.At(i)?.StableId == furnitureId, -1);
                Assert.That(slot, Is.GreaterThanOrEqualTo(0), "팔레트에서 " + furnitureId + "를 찾았다");
            }
            Player.PressButton(FurniturePlacementScreen.SlotId(slot));
            FurnitureDefinition furniture = screen.Active;
            Assert.That(furniture?.StableId, Is.EqualTo(furnitureId));

            var house = Session.House;
            FurnitureRoom room = Player.Game.World.Room;
            HostPlacementView view = new HostPlacementView(Player.Game.World, () => Player.Game.Fit.Scale);
            GridPoint target = default;
            bool found = false;
            foreach (GridPoint cell in room.FloorCells)
            {
                UiRect r = view.CellRect(cell);
                if (r.X < 0 || r.Y < 0 || r.Right > 1920 || r.Bottom > 1080) continue;
                if (Player.Game.Ui.HitTest(r.CenterX, r.CenterY) != null) continue;
                if (Math.Abs(cell.X - room.Grid.Spawn.X) <= 2 && Math.Abs(cell.Y - room.Grid.Spawn.Y) <= 2) continue;
                if (!house.CanPlace(furniture, cell, FurniturePlacementDirection.North)) continue;
                target = cell; found = true; break;
            }
            Assert.That(found, Is.True, furnitureId + "를 놓을 칸이 화면에 있다");
            UiRect at = view.CellRect(target);
            int placed = house.Placed.Count;
            Player.MovePointer(at.CenterX, at.CenterY);
            Assert.That(screen.HoverValid, Is.True, "놓을 수 있는 칸 미리보기");
            Player.ClickAt(at.CenterX, at.CenterY);
            Assert.That(screen.LastAction, Is.EqualTo("placed"));
            Assert.That(house.Placed.Count, Is.EqualTo(placed + 1));
            Player.MovePointer(at.CenterX, at.CenterY + 200);
            Player.Frames(2);
            Shot(shot);
            ClosePalette();
            Assert.That(Player.Game.World.FurnitureDrawCount, Is.EqualTo(house.Placed.Count), "월드가 놓인 가구를 그린다");
        }

        /// <summary>놓은 가구 옆(1.6칸 안의 걸을 수 있는 칸)까지 걸어가 찍는다 — 일반 카메라에서 보이는지.</summary>
        public void WalkNextToPlacedFurniture(string furnitureId, string shot)
        {
            PlacedFurniture placed = Session.House.Placed.First(f => f.Definition.StableId == furnitureId);
            CellPos cell = Player.Game.World.Room.ToMap(placed.Anchor);
            int size = Player.World.Map.Grid.CellSize;
            var center = CellPos.Center(cell, size);
            Player.WalkNear(center.X, center.Y, size * 1.6f);
            Shot(shot);
        }

        // ---------- 가게 ----------

        /// <summary>가게 지점까지 걸어가 E → 장소 화면에서 물건 줄을 포인터로 누른다 → 다시 눌러도 바뀌지 않는다 → 닫기.</summary>
        public void BuyAt(string locationId, string furnitureId, string shot)
        {
            WalkToThing(Thing(LocationScreen.InteractionKind, locationId));
            Player.PressKey(TestKey.E);
            Player.WaitForScreen(ScreenCatalog.Location);
            var screen = Screen<LocationScreen>(ScreenCatalog.Location);
            int row = screen.Model.Rows.ToList().FindIndex(r => r.Kind == LocationRowKind.Offer && r.Id == furnitureId);
            Assert.That(row, Is.GreaterThanOrEqualTo(0), locationId + " 화면에 " + furnitureId + " 물건 줄이 없다");
            Assert.That(screen.Model.Rows[row].Enabled, Is.True, "살 수 있는 줄(용돈 " + Allowance + ")");
            UiRect rect = ScrollRowIntoView(screen, row);
            Player.ClickAt(rect.CenterX, rect.CenterY);
            Assert.That(screen.Model.Rows[row].Owned, Is.True, "샀다");
            Assert.That(screen.Model.Rows[row].Enabled, Is.False, "가진 물건은 다시 눌리지 않는다");
            int money = Allowance;
            Player.ClickAt(rect.CenterX, rect.CenterY);
            Assert.That(Allowance, Is.EqualTo(money), "다시 눌러도 값을 내지 않는다");
            ShotScreen(ScreenCatalog.Location, shot);
            Player.PressButton(LocationScreen.CloseId);
            Player.WaitForScreenClosed(ScreenCatalog.Location);
            DismissPopups();
        }

        /// <summary>장소 목록 줄이 보이도록 휠로 내린 뒤 그 줄 사각형(맨 위 요소가 목록인지 확인).</summary>
        private UiRect ScrollRowIntoView(LocationScreen screen, int row)
        {
            UiNode list = Player.Node(LocationScreen.RowsId);
            for (int guard = 0; guard < 40; guard++)
            {
                UiRect r = screen.RowRect(row);
                if (r.Y >= list.Rect.Y && r.Bottom <= list.Rect.Bottom) break;
                Player.MovePointer(list.Rect.CenterX, list.Rect.CenterY);
                Player.Wheel(r.Y < list.Rect.Y ? 1 : -1);
            }
            UiRect rect = screen.RowRect(row);
            Assert.That(Player.Game.Ui.HitTest(rect.CenterX, rect.CenterY)?.Id, Is.EqualTo(LocationScreen.RowsId), "줄이 보이고 가려지지 않았다");
            return rect;
        }

        // ---------- 장래희망(여러 번) ----------

        /// <summary>지금 고를 수 있는 장래희망(정의 순서).</summary>
        public string[] AllowedDreams() =>
            Session.CareerInterestChoices().Where(c => c.Evaluation.Allowed).Select(c => c.Interest.Id).ToArray();

        public void OpenDreams()
        {
            Player.PressButton("hud.button.dream");
            Player.WaitForScreen(ScreenCatalog.CareerInterest);
        }

        public void ClickDream(string interestId)
        {
            var screen = Screen<CareerInterestScreen>(ScreenCatalog.CareerInterest);
            int index = Array.FindIndex(screen.Model.Cards, c => c.InterestId == interestId);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "카드: " + interestId);
            ClickCard(index);
        }

        public Rootborn.Core.StudentLife.CareerInterestSelectionState DreamCardState(string interestId) =>
            Screen<CareerInterestScreen>(ScreenCatalog.CareerInterest).Model.Cards.First(c => c.InterestId == interestId).State;

        public void ShotDreams(string shot) => ShotScreen(ScreenCatalog.CareerInterest, shot);

        public void CloseDreams()
        {
            Player.PressButton(CareerInterestScreen.CloseId);
            Player.WaitForScreenClosed(ScreenCatalog.CareerInterest);
        }

        // ---------- 수첩의 장래희망 줄 ----------

        /// <summary>Tab → 발견 분류 → 그 장래희망 줄(휠로 넘겨 보이게)을 포인터로 → 상세의 보이는 글자. Tab으로 닫는다.</summary>
        public string JournalHintDetail(string interestId, string shot)
        {
            Player.OpenScreenWithKey(TestKey.Tab, ScreenCatalog.ObjectiveJournal);
            var journal = Screen<ObjectiveJournalScreen>(ScreenCatalog.ObjectiveJournal);
            Player.PressButton(ObjectiveJournalScreen.CategoryButtonId(JournalCategories.Hints));
            int row = journal.Items.ToList().FindIndex(i => i.StableKey == "career-interest." + interestId);
            Assert.That(row, Is.GreaterThanOrEqualTo(0), "수첩 발견 분류에 장래희망 줄");
            UiRect view = journal.List.View;
            for (int guard = 0; guard < 100; guard++)
            {
                UiRect r = journal.List.RowRect(row);
                if (r.Y >= view.Y && r.Bottom <= view.Bottom) break;
                Player.MovePointer(view.CenterX, view.CenterY);
                Player.Wheel(r.Y < view.Y ? 1 : -1);
            }
            UiRect rect = journal.List.RowRect(row);
            Assert.That(Player.Game.Ui.HitTest(rect.CenterX, rect.CenterY), Is.SameAs(journal.List));
            Player.ClickAt(rect.CenterX, rect.CenterY);
            Player.Frames(2);
            string text = journal.Detail.VisibleText;
            ShotScreen(ScreenCatalog.ObjectiveJournal, shot);
            Player.CloseScreenWithKey(TestKey.Tab, ScreenCatalog.ObjectiveJournal);
            return text;
        }
    }
}
