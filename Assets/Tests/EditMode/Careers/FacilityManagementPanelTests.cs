using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Minigames;
using Rootborn.Game.Player;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.Network.Session;
using Rootborn.UI.Modern;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.UI;

namespace Rootborn.Tests.EditMode.Careers
{
    public sealed class FacilityManagementPanelTests
    {
        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private string _root; private AppConfig _config; private SaveSlotMetadata _metadata; private INetworkSession _session; private GameDataRegistry _priorRegistry;
        private int _workRequests; private FacilityWorkDefinition _requestedWork; private Component _requestedOwner;
        [SetUp] public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "rootborn-facility-ui-" + Guid.NewGuid().ToString("N"));
            _config = GameBootstrap.Config; _metadata = ActiveSaveContext.Metadata; _session = NetworkBootstrap.ActiveSession; GameDataRegistryProvider.TryGet(out _priorRegistry);
            typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, null);
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, new AppConfig { Mode = SessionMode.Single, SaveSlot = "slot" });
            SaveService.SetRootDirectoryForTests(_root); ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "slot" }); _workRequests = 0;
        }
        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) UnityEngine.Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); GameDataRegistryProvider.SetOverrideForTests(_priorRegistry); ActiveSaveContext.Set(_metadata); SaveService.SetRootDirectoryForTests(null);
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, _config); typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, _session);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test] public void FACILITY_UI_013_HudCanvasGainsPointerInputWhenHostingFacilityPanel()
        {
            var canvas = Canvas();
            Assert.That(canvas.GetComponent<GraphicRaycaster>(), Is.Null);
            Ensure(PanelType(), canvas); Ensure(PanelType(), canvas);
            Assert.That(canvas.GetComponents<GraphicRaycaster>().Length, Is.EqualTo(1));
            Assert.That(canvas.GetComponent<GraphicRaycaster>().enabled, Is.True);
        }
        [Test] public void FACILITY_UI_014_PublishedPracticeLabelIsReadableKorean()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<Rootborn.Game.Localization.GameTextCatalog>("Assets/Localization/GameTextCatalog.asset");
            Assert.That(catalog.Get("facility.practice", "ko"), Is.EqualTo("\uC9C4\uB85C \uCCB4\uD5D8\uD558\uAE30"));
        }
        [Test] public void FACILITY_UI_001_EnsureReusesHiddenPanelUnderProvidedCanvas()
        {
            var type = PanelType(); var canvas = Canvas(); var first = Ensure(type, canvas); var second = Ensure(type, canvas);
            Assert.That(first, Is.SameAs(second)); Assert.That(first.transform.IsChildOf(canvas.transform), Is.True); Assert.That(Get<bool>(first, "IsOpen"), Is.False);
            Assert.That(canvas.GetComponentsInChildren(type, true).Length, Is.EqualTo(1));
        }
        [Test] public void FACILITY_UI_002_OpenedFacilityUsesModernPanelAndBoundedWorkList()
        {
            var f = Fixture(); Show(f);
            Assert.That(Get<bool>(f.Panel, "IsOpen"), Is.True); Assert.That(f.Panel.GetComponent<ModernUiTileImage>(), Is.Not.Null);
            Assert.That(f.Panel.GetComponent<ModernUiTileImage>().Recipe, Is.EqualTo(ModernUiRecipes.CommonPanel));
            Assert.That(f.Panel.GetComponent<ScrollRect>(), Is.Not.Null); Assert.That(Get<int>(f.Panel, "WorkRowCountForTests"), Is.EqualTo(1));
            Assert.That(Get<FacilityDefinition>(f.Panel, "Facility"), Is.SameAs(f.Facility));
        }
        [Test] public void FACILITY_UI_003_WorkButtonRequestsMinigameWithoutCompletingWork()
        {
            var f = Fixture(); Show(f); var evt = f.Panel.GetType().GetEvent("WorkRequested", BindingFlags.Public | BindingFlags.Static); Assert.That(evt, Is.Not.Null);
            var callback = GetType().GetMethod("CaptureWork", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(f.Owner.GetType()).CreateDelegate(evt.EventHandlerType, this);
            evt.AddEventHandler(null, callback);
            try { WorkButton(f, "GetWorkButtonForTests").onClick.Invoke(); }
            finally { evt.RemoveEventHandler(null, callback); }
            Assert.That(_workRequests, Is.EqualTo(1)); Assert.That(_requestedWork, Is.SameAs(f.Work)); Assert.That(_requestedOwner, Is.SameAs(f.Owner));
            Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(10)); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(2));
            Assert.That(Get<AdultCareerProgress>(f.Owner, "Progress").GetMastery(f.Work), Is.Zero);
        }
        [Test] public void FACILITY_UI_004_LockedAutoSkipCannotAwardFromDirectClickInvocation()
        {
            var f = Fixture(); Show(f); var button = WorkButton(f, "GetAutoSkipButtonForTests"); Assert.That(button.interactable, Is.False); button.onClick.Invoke();
            Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(10)); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(2));
        }
        [Test] public void FACILITY_UI_005_UpgradeButtonUsesDurableOwnerCommand()
        {
            var f = Fixture(); Show(f); var button = Get<Button>(f.Panel, "UpgradeButtonForTests"); Assert.That(button.interactable, Is.True); button.onClick.Invoke();
            Assert.That(Get<AdultCareerProgress>(f.Owner, "Progress").FacilityLevel, Is.EqualTo(2), "Command result="+Get<AdultCareerResult>(f.Owner,"LastResult").Kind+" panel="+string.Join(";",System.Array.ConvertAll(f.Panel.GetComponentsInChildren<Text>(),t=>t.text))); Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(7));
            Assert.That(AdultCareerSnapshotPersistence.Load(f.Storage, PlayerIdentity.DefaultPlayerId).Adult.FacilityLevel, Is.EqualTo(2));
        }
        [Test] public void FACILITY_UI_006_AdultSelectionDisablesAnotherTransition()
        {
            var f = Fixture(); Show(f); Assert.That(Get<Button>(f.Panel, "TransitionButtonForTests").interactable, Is.False);
            Get<Button>(f.Panel, "TransitionButtonForTests").onClick.Invoke(); Assert.That(Get<AdultCareerProgress>(f.Owner, "Progress").FacilityId, Is.EqualTo(f.Facility.Id));
        }
        [Test] public void FACILITY_UI_007_FacilityInteractorOpensModalWithoutWorkReward()
        {
            var f = Fixture(); var type = typeof(CareerInterestPanel).Assembly.GetType("Rootborn.UI.Careers.CareerFacilityInteractor"); Assert.That(type, Is.Not.Null, "CareerFacilityInteractor missing");
            var marker = new GameObject("FacilityMarker"); _objects.Add(marker); var interactor = marker.AddComponent(type);
            type.GetMethod("Bind").Invoke(interactor, new object[] { f.Facility }); Assert.That(interactor, Is.InstanceOf<IPlayerInteractable>());
            Assert.That(((IPlayerInteractable)interactor).TryInteract(f.Player), Is.True); Assert.That(Get<bool>(f.Panel, "IsOpen"), Is.True);
            Assert.That(f.Inventory.Inventory.CountOf(f.Coin), Is.EqualTo(10)); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(2));
        }
        [Test] public void FACILITY_UI_008_ClosedModalCannotDispatchStaleWorkButtons()
        {
            var f = Fixture(); Show(f); var button = WorkButton(f, "GetWorkButtonForTests"); f.Panel.GetType().GetMethod("Close").Invoke(f.Panel, null);
            var evt = f.Panel.GetType().GetEvent("WorkRequested", BindingFlags.Public | BindingFlags.Static);
            var callback = GetType().GetMethod("CaptureWork", BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(f.Owner.GetType()).CreateDelegate(evt.EventHandlerType, this);
            evt.AddEventHandler(null, callback); try { button.onClick.Invoke(); } finally { evt.RemoveEventHandler(null, callback); }
            Assert.That(Get<bool>(f.Panel, "IsOpen"), Is.False); Assert.That(_workRequests, Is.Zero);
        }
        [Test] public void FACILITY_UI_009_StudentPracticeButtonRequestsAuthoredGameAndClosesModal()
        {
            var f = Fixture(false); var practice = AddPractice(f); Show(f);
            var bridge = typeof(CareerPracticeDefinition).Assembly.GetType("Rootborn.Game.StudentLife.StudentMinigameRequestBridge"); Assert.That(bridge, Is.Not.Null);
            var evt = bridge.GetEvent("PracticeRequested"); int count = 0;
            Func<GameObject, CareerPracticeDefinition, string, bool> callback = (player, selected, request) => { count++; Assert.That(player, Is.SameAs(f.Player)); Assert.That(selected, Is.SameAs(practice)); Assert.That(request, Is.Not.Empty); return true; };
            evt.AddEventHandler(null, callback);
            try { var button = Get<Button>(f.Panel, "PracticeButtonForTests"); Assert.That(button.interactable, Is.True); button.onClick.Invoke(); }
            finally { evt.RemoveEventHandler(null, callback); }
            Assert.That(count, Is.EqualTo(1)); Assert.That(Get<bool>(f.Panel, "IsOpen"), Is.False); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(2));
        }
        [Test] public void FACILITY_UI_010_AdultCannotRequestStudentPracticeFromStaleButton()
        {
            var f = Fixture(); AddPractice(f); Show(f); var button = Get<Button>(f.Panel, "PracticeButtonForTests"); Assert.That(button.interactable, Is.False); button.onClick.Invoke(); Assert.That(Get<bool>(f.Panel, "IsOpen"), Is.True);
        }
        [Test] public void FACILITY_UI_011_FacilityEntryPriorityOutranksLocationActivity()
        {
            var f = Fixture(); var marker = new GameObject("FacilityMarker"); _objects.Add(marker); var type = typeof(CareerInterestPanel).Assembly.GetType("Rootborn.UI.Careers.CareerFacilityInteractor"); var component = marker.AddComponent(type);
            type.GetMethod("Bind").Invoke(component, new object[] { f.Facility }); Assert.That(((IPrioritizedPlayerInteractable)component).InteractionPriority, Is.GreaterThan(45));
        }
        [Test] public void FACILITY_UI_012_OpeningFacilityRecordsItsLocationVisitForTourObjectives()
        {
            var f = Fixture(false); var location = New<LocationDefinition>(); location.ConfigureForTests("location.fixture", "location.fixture", Vector2.zero, null); Set(f.Facility, "_location", location);
            var marker = new GameObject("FacilityMarker"); _objects.Add(marker); var type = typeof(CareerInterestPanel).Assembly.GetType("Rootborn.UI.Careers.CareerFacilityInteractor"); var component = marker.AddComponent(type); type.GetMethod("Bind").Invoke(component, new object[] { f.Facility });
            int count = 0; Action<QuestEvent, StudentLifeProgress> listener = (evt, student) => { if (evt.Kind == QuestEventKind.LocationVisit && evt.Location == location) count++; };
            QuestGameplayEventBridge.OnRecorded += listener;
            try { Assert.That(((IPlayerInteractable)component).TryInteract(f.Player), Is.True); } finally { QuestGameplayEventBridge.OnRecorded -= listener; }
            Assert.That(count, Is.EqualTo(1)); Assert.That(f.Student.Progress.TimePointsRemaining, Is.EqualTo(2));
        }
        [Test] public void FACILITY_UI_018_SceneDuplicateResolvesCanonicalRegistryFacility()
        {
            var f=Fixture();var copy=UnityEngine.Object.Instantiate(f.Facility);_objects.Add(copy);
            var go=new GameObject("DuplicateSceneDoor");_objects.Add(go);var type=PanelType().Assembly.GetType("Rootborn.UI.Careers.CareerFacilityInteractor");var door=go.AddComponent(type);type.GetMethod("Bind").Invoke(door,new object[]{copy});
            Assert.That(((IPlayerInteractable)door).TryInteract(f.Player),Is.True);
            Assert.That(Get<FacilityDefinition>(f.Panel,"Facility"),Is.SameAs(f.Facility));
            Assert.That(WorkButton(f,"GetWorkButtonForTests").interactable,Is.True);
        }
        [Test] public void FACILITY_UI_019_UnregisteredSceneFacilityIsRejected()
        {
            var f=Fixture();var copy=UnityEngine.Object.Instantiate(f.Facility);_objects.Add(copy);Set(copy,"_id","unregistered.facility");
            var go=new GameObject("UnknownSceneDoor");_objects.Add(go);var type=PanelType().Assembly.GetType("Rootborn.UI.Careers.CareerFacilityInteractor");var door=go.AddComponent(type);type.GetMethod("Bind").Invoke(door,new object[]{copy});
            Assert.That(((IPlayerInteractable)door).TryInteract(f.Player),Is.False);
            Assert.That(Get<bool>(f.Panel,"IsOpen"),Is.False);
        }
        [TestCase(true,3,true,true)][TestCase(true,3,false,false)][TestCase(true,2,false,false)][TestCase(false,1,false,false)]
        public void FACILITY_UI_020_CompletionTrophyUsesAuthoredStageAndEarnedEnding(bool adult,int level,bool ended,bool visible)
        {
            var f=Fixture(adult,level,ended);var texture=new Texture2D(8,8);_objects.Add(texture);var sprite=Sprite.Create(texture,new Rect(0,0,8,8),Vector2.one*.5f);_objects.Add(sprite);Set(f.Facility.GetLevel(3),"_achievementSprite",sprite);Show(f);
            var image=f.Panel.transform.Find("CareerAchievement").GetComponent<Image>();Assert.That(image.gameObject.activeSelf,Is.EqualTo(visible));Assert.That(image.raycastTarget,Is.False);Assert.That(image.preserveAspect,Is.True);if(visible)Assert.That(image.sprite,Is.SameAs(sprite));Assert.That(f.Panel.transform.Find("Summary").GetComponent<RectTransform>().sizeDelta.x,Is.EqualTo(visible?700:816));
        }
        [Test] public void FACILITY_UI_021_CompletedMissingTrophyKeepsTextFallback()
        {
            var f=Fixture(true,3,true);Show(f);var image=f.Panel.transform.Find("CareerAchievement").GetComponent<Image>();Assert.That(image.gameObject.activeSelf,Is.False);Assert.That(f.Panel.transform.Find("Summary").GetComponent<RectTransform>().sizeDelta.x,Is.EqualTo(816));
        }
        private void CaptureWork<T>(FacilityWorkDefinition work, T owner) where T : Component { _workRequests++; _requestedWork = work; _requestedOwner = owner; }
        [TestCase(1)][TestCase(2)][TestCase(3)] public void FACILITY_UI_015_AdultTitleUsesCurrentStageName(int level)
        {
            var f=Fixture(true,level); Show(f);
            Assert.That(f.Panel.transform.Find("Title").GetComponent<Text>().text,Is.EqualTo("fixture.stage-name-"+level));
        }
        [Test] public void FACILITY_UI_016_StudentTitleUsesFacilityName()
        {
            var f=Fixture(false); Show(f);
            Assert.That(f.Panel.transform.Find("Title").GetComponent<Text>().text,Is.EqualTo("fixture.facility-name"));
        }
        [Test] public void FACILITY_UI_017_CommittedUpgradeRefreshesStageTitle()
        {
            var f=Fixture(); Show(f); var result=(AdultCareerResult)f.Owner.GetType().GetMethod("TryUpgrade").Invoke(f.Owner,new object[]{f.Facility,"title-upgrade"});
            Assert.That(result.Applied,Is.True,"Command result="+result.Kind); Assert.That(f.Panel.transform.Find("Title").GetComponent<Text>().text,Is.EqualTo("fixture.stage-name-2"));
        }
        private sealed class Data
        {
            public GameObject Player; public Component Owner; public Component Panel; public PlayerInventory Inventory; public StudentLifeProgressComponent Student;
            public ItemDefinition Coin; public FacilityDefinition Facility; public FacilityWorkDefinition Work; public SaveService Storage; public GameDataRegistry Registry;
        }
        private Data Fixture(bool adultPhase = true, int level = 1, bool ended = false)
        {
            var panelType = PanelType(); var f = new Data(); f.Panel = Ensure(panelType, Canvas());
            var calendar = New<LifeCalendarDefinition>(); calendar.ConfigureForTests(2, 3); f.Coin = New<ItemDefinition>(); Set(f.Coin, "_id", "item.allowance"); Set(f.Coin, "_maxStack", 9999);
            f.Facility = New<FacilityDefinition>(); Set(f.Facility, "_id", "facility.fixture"); Set(f.Facility,"_displayNameKey","fixture.facility-name"); Set(f.Facility, "_career", New<CareerDefinition>()); Set(f.Facility, "_candidate", New<CareerCandidateDefinition>());
            var levels = new FacilityLevelDefinition[3]; for (int i = 0; i < 3; i++) { levels[i] = New<FacilityLevelDefinition>(); Set(levels[i], "_id", "level-" + (i + 1)); Set(levels[i], "_level", i + 1); Set(levels[i],"_displayNameKey","fixture.stage-name-"+(i+1)); }
            Set(levels[1], "_costItems", new[] { f.Coin }); Set(levels[1], "_costCounts", new[] { 3 }); Set(levels[2], "_completesRoute", true); Set(f.Facility, "_levels", levels);
            f.Work = New<FacilityWorkDefinition>(); Set(f.Work, "_id", "work.fixture"); Set(f.Work, "_facility", f.Facility); Set(f.Work, "_rewardItems", new[] { f.Coin }); Set(f.Work, "_maximumRewardCounts", new[] { 2 }); Set(f.Facility, "_works", new[] { f.Work });
            var registry = New<GameDataRegistry>(); Set(registry, "_items", new[] { f.Coin }); Set(registry, "_studentLifeCalendar", calendar); Set(registry, "_facilityDefinitions", new[] { f.Facility }); GameDataRegistryProvider.SetOverrideForTests(registry);
            f.Registry = registry;
            f.Storage = new SaveService("slot", _root); var inventory = new Inventory(); inventory.Add(f.Coin, 10); var student = new StudentLifeProgress("slot", PlayerIdentity.DefaultPlayerId, 12, 10); student.ConfigureCalendar(calendar);
            var adult = AdultCareerProgress.FromSaveData(new AdultCareerProgressSaveData { SaveSlot = "slot", PlayerId = PlayerIdentity.DefaultPlayerId, FacilityId = adultPhase ? f.Facility.Id : string.Empty, FacilityLevel = adultPhase ? level : 0, EndingIds = ended ? new[]{f.Facility.Id} : Array.Empty<string>() });
            Assert.That(AdultCareerSnapshotPersistence.SaveSession(f.Storage, AdultCareerSnapshotPersistence.Capture(adult, student, inventory, new WorldStateProgress("slot", PlayerIdentity.DefaultPlayerId))), Is.True);
            f.Player = new GameObject("FacilityPlayer"); _objects.Add(f.Player); f.Player.AddComponent<PlayerIdentity>().Configure(PlayerIdentity.DefaultPlayerId); f.Student = f.Player.AddComponent<StudentLifeProgressComponent>(); f.Inventory = f.Player.AddComponent<PlayerInventory>();
            var ownerType = typeof(CareerInterestPanel).Assembly.GetType("Rootborn.UI.Careers.AdultCareerRuntimeComponent"); Assert.That(ownerType, Is.Not.Null);
            f.Owner = f.Player.AddComponent(ownerType); ownerType.GetMethod("Bind").Invoke(f.Owner, new object[] { registry, f.Storage }); return f;
        }
        private CareerPracticeDefinition AddPractice(Data f)
        {
            var policy = New<ActivityTimePolicyDefinition>(); policy.ConfigureForTests("practice-time", 1, 127, false);
            var activity = New<LifeActivityDefinition>(); activity.ConfigureForTests("activity.fixture", "activity.fixture", default, 0, 0, 0, 0, null, null); Set(activity, "_timePolicy", policy);
            var choice = New<ChoiceChallengeDefinition>(); Set(choice, "_symbols", new[] { "a", "b" }); Set(choice, "_answers", new[] { 0 }); Set(choice, "_prompts", new[] { "prompt" });
            var minigame = New<MinigameDefinition>(); Set(minigame, "_challenges", new ChallengeDefinitionBase[] { choice });
            var practice = New<CareerPracticeDefinition>(); practice.ConfigureForTests("practice.fixture", "practice.fixture", activity, null, Array.Empty<PracticeStepDefinition>()); Set(practice, "_minigame", minigame);
            Set(f.Facility, "_practice", practice); Set(f.Registry, "_careerPractices", new[] { practice }); return practice;
        }
        private Canvas Canvas() { var go = new GameObject("FixtureCanvas", typeof(RectTransform), typeof(Canvas)); _objects.Add(go); return go.GetComponent<Canvas>(); }
        private static Type PanelType() { var type = typeof(CareerInterestPanel).Assembly.GetType("Rootborn.UI.Careers.FacilityManagementPanel"); Assert.That(type, Is.Not.Null, "FacilityManagementPanel missing"); return type; }
        private static Component Ensure(Type type, Canvas canvas) => (Component)type.GetMethod("EnsureInScene").Invoke(null, new object[] { canvas });
        private static void Show(Data f) => f.Panel.GetType().GetMethod("Show").Invoke(f.Panel, new object[] { f.Facility, f.Owner });
        private static Button WorkButton(Data f, string method) => (Button)f.Panel.GetType().GetMethod(method).Invoke(f.Panel, new object[] { 0 });
        private T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static void Set(object target, string field, object value) { var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic); Assert.That(info, Is.Not.Null, field); info.SetValue(target, value); }
        private static T Get<T>(object target, string property) => (T)target.GetType().GetProperty(property).GetValue(target);
    }
}
