using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Careers;
using Rootborn.Game.Common;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.Game.WorldState;
using Rootborn.Network.Session;
using Rootborn.UI.Careers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Careers
{
    public sealed class FacilityWorldPresenterTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private readonly List<string> _loads = new List<string>(), _releases = new List<string>();
        private Type _type;
        private string _root, _slot, _player;
        private AppConfig _config;
        private SaveSlotMetadata _metadata;
        private INetworkSession _network;
        private GameDataRegistry _priorRegistry, _registry;
        private GameObject _site;
        private Component _presenter;
        private SpriteRenderer _renderer;
        private CareerFacilityInteractor _door;
        private Transform _body;
        private FacilityDefinition _facility;
        private FacilityVisualDefinition[] _visuals;
        private Sprite[] _sprites;

        [SetUp] public void Setup()
        {
            _loads.Clear(); _releases.Clear();
            _type = typeof(AdultCareerRuntimeComponent).Assembly.GetType("Rootborn.UI.Careers.FacilityWorldPresenter");
            Assert.That(_type, Is.Not.Null, "FacilityWorldPresenter is required for authored facility geometry and owner-driven visual levels.");
            _root = Path.Combine(Path.GetTempPath(), "rootborn-facility-world-" + Guid.NewGuid().ToString("N"));
            // Keep the fixture below Unity's Windows path limit after identity encoding and the atomic temp suffix.
            _slot = "site-slot"; _player = "site-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _config = GameBootstrap.Config; _metadata = ActiveSaveContext.Metadata; _network = NetworkBootstrap.ActiveSession;
            GameDataRegistryProvider.TryGet(out _priorRegistry);
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, new AppConfig { Mode = SessionMode.Single });
            typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, null);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = _slot }); SaveService.SetRootDirectoryForTests(_root);
            _registry = Make<GameDataRegistry>(); var calendar = Make<LifeCalendarDefinition>(); calendar.ConfigureForTests(2, 3);
            Set(_registry, "_studentLifeCalendar", calendar); GameDataRegistryProvider.SetOverrideForTests(_registry);
            _facility = Make<FacilityDefinition>(); Set(_facility, "_id", "facility.site"); Set(_facility, "_career", Make<CareerDefinition>()); Set(_facility, "_candidate", Make<CareerCandidateDefinition>());
            var levels = new FacilityLevelDefinition[3]; _visuals = new FacilityVisualDefinition[3]; _sprites = new Sprite[3];
            for (int i = 0; i < 3; i++)
            {
                levels[i] = Make<FacilityLevelDefinition>(); Set(levels[i], "_id", "level-" + (i + 1)); Set(levels[i], "_level", i + 1);
                _visuals[i] = Make<FacilityVisualDefinition>(); Set(_visuals[i], "_facility", _facility); Set(_visuals[i], "_level", i + 1);
                Set(_visuals[i], "_spriteAddress", "fixture/facility/" + (i + 1)); Set(_visuals[i], "_siteSize", new Vector2Int(4, 4));
                Set(_visuals[i], "_spriteOffset", new Vector2(1.5f, 2f + i)); Set(_visuals[i], "_entranceOffset", new Vector2(0.5f, -2f - i));
                Set(_visuals[i], "_collisionCells", new[] { new Vector2Int(0, 1), new Vector2Int(1, 1) });
                Set(_visuals[i], "_entranceCells", new[] { new Vector2Int(2, 0) }); Set(_visuals[i], "_approachCells", new[] { new Vector2Int(2, -1) });
                var texture = new Texture2D(4, 4); _objects.Add(texture); _sprites[i] = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f, 4); _objects.Add(_sprites[i]);
            }
            Set(levels[2], "_completesRoute", true); Set(_facility, "_levels", levels); Set(_registry, "_facilityDefinitions", new[] { _facility });
            _site = Go("FacilitySite"); _site.transform.position = new Vector3(30, 10, 0);
            _renderer = Child("Visual").AddComponent<SpriteRenderer>(); _door = Child("Door").AddComponent<CareerFacilityInteractor>(); _body = Child("CollisionCells").transform;
            _presenter = _site.AddComponent(_type); Set(_presenter, "_facility", _facility); Set(_presenter, "_visuals", _visuals); Set(_presenter, "_spriteRenderer", _renderer); Set(_presenter, "_door", _door); Set(_presenter, "_collisionRoot", _body);
            LoadWith(address => { _loads.Add(address); return Task.FromResult(_sprites[Array.FindIndex(_visuals, v => v.SpriteAddress == address)]); });
        }
        [TearDown] public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            if (_root == null) return;
            GameDataRegistryProvider.SetOverrideForTests(_priorRegistry); ActiveSaveContext.Set(_metadata); SaveService.SetRootDirectoryForTests(null);
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, _config); typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, _network);
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test] public async Task FACILITY_WORLD_001_StudentDefaultsToLevelOneAndUsesMeasuredOffsets()
        {
            Assert.That(await Bind(Owner(0)), Is.True); Assert.That(Get<int>("SelectedLevel"), Is.EqualTo(1)); Assert.That(Get<FacilityVisualDefinition>("CurrentVisual"), Is.SameAs(_visuals[0]));
            Assert.That(_renderer.sprite, Is.SameAs(_sprites[0])); Assert.That((Vector2)_renderer.transform.localPosition, Is.EqualTo(_visuals[0].SpriteOffset));
            Assert.That((Vector2)_door.transform.localPosition, Is.EqualTo(_visuals[0].EntranceLocalPosition)); Assert.That(_door.Facility, Is.SameAs(_facility));
        }
        [Test] public async Task FACILITY_WORLD_002_OtherCareerAndUnboundSiteUseLevelOne()
        {
            Assert.That(await Bind(Owner(3, "facility.other")), Is.True); Assert.That(Get<int>("SelectedLevel"), Is.EqualTo(1));
            Assert.That(await Bind(null), Is.True); Assert.That(Get<int>("SelectedLevel"), Is.EqualTo(1));
        }
        [Test] public async Task FACILITY_WORLD_003_SelectedCareerUsesItsSavedFacilityLevel()
        {
            Assert.That(await Bind(Owner(2)), Is.True); Assert.That(Get<int>("SelectedLevel"), Is.EqualTo(2)); Assert.That(_renderer.sprite, Is.SameAs(_sprites[1]));
        }
        [Test] public async Task FACILITY_WORLD_004_CollisionUsesOnlyIntegerCenteredAuthoredCellsAndDoorIsATrigger()
        {
            await Bind(null); var colliders = _body.GetComponentsInChildren<BoxCollider2D>(true); Assert.That(colliders.Length, Is.EqualTo(2));
            var centers = new List<Vector2>(); foreach (var collider in colliders) { Assert.That(collider.isTrigger, Is.False); Assert.That(collider.size, Is.EqualTo(Vector2.one)); centers.Add((Vector2)_site.transform.InverseTransformPoint(collider.transform.TransformPoint(collider.offset))); }
            CollectionAssert.AreEquivalent(new[] { new Vector2(0, 1), new Vector2(1, 1) }, centers);
            var doorCollider = _door.GetComponent<Collider2D>(); Assert.That(doorCollider, Is.Not.Null); Assert.That(doorCollider.isTrigger, Is.True);
            CollectionAssert.DoesNotContain(centers, new Vector2(2, 0)); CollectionAssert.DoesNotContain(centers, new Vector2(2, -1));
        }
        [Test] public async Task FACILITY_WORLD_005_RepeatedRefreshDoesNotDuplicateGeometryDoorOrAssetLoad()
        {
            await Bind(null); var before = _body.GetComponentsInChildren<BoxCollider2D>(true); var doorId = _door.GetInstanceID();
            await Refresh(); await Refresh(); Assert.That(_loads.Count, Is.EqualTo(1)); CollectionAssert.AreEqual(before, _body.GetComponentsInChildren<BoxCollider2D>(true));
            Assert.That(_site.GetComponentsInChildren<CareerFacilityInteractor>(true).Length, Is.EqualTo(1)); Assert.That(_door.GetInstanceID(), Is.EqualTo(doorId));
        }
        [Test] public async Task FACILITY_WORLD_006_CommittedUpgradeRefreshesLevelWithoutPolling()
        {
            var owner = Owner(1); await Bind(owner); Assert.That(owner.TryUpgrade(_facility, "upgrade-for-view").Applied, Is.True);
            Assert.That(Get<int>("SelectedLevel"), Is.EqualTo(2)); Assert.That(_renderer.sprite, Is.SameAs(_sprites[1])); Assert.That(_loads.Count, Is.EqualTo(2));
            Assert.That(_type.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public), Is.Null);
        }
        [Test] public async Task FACILITY_WORLD_007_DisableLifecycleHandlerUnsubscribesAndReleasesOnlyItsOwnedKey()
        {
            var owner = Owner(1); await Bind(owner); _site.SetActive(false);
            // Ordinary MonoBehaviours do not receive their runtime lifecycle in this EditMode fixture.
            // Native SetActive/Destroy delivery is verified by FacilityWorldPresenterLifecycleTests.
            _type.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_presenter, null);
            Assert.That(owner.TryUpgrade(_facility, "upgrade-while-hidden").Applied, Is.True);
            Assert.That(_loads.Count, Is.EqualTo(1)); Assert.That(Get<int>("SelectedLevel"), Is.EqualTo(1)); CollectionAssert.AreEqual(new[] { _visuals[0].SpriteAddress }, _releases);
            _type.GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_presenter, null);
            Object.DestroyImmediate(_site); Assert.That(_releases.Count, Is.EqualTo(1));
        }
        [Test] public async Task FACILITY_WORLD_008_LatePriorLevelLoadCannotReplaceNewerVisual()
        {
            var first = new TaskCompletionSource<Sprite>(); var second = new TaskCompletionSource<Sprite>();
            LoadWith(address => address == _visuals[0].SpriteAddress ? first.Task : second.Task);
            var firstBind = Bind(null); var secondBind = Bind(Owner(2)); second.SetResult(_sprites[1]); Assert.That(await secondBind, Is.True);
            first.SetResult(_sprites[0]); await firstBind; Assert.That(Get<int>("SelectedLevel"), Is.EqualTo(2)); Assert.That(_renderer.sprite, Is.SameAs(_sprites[1]));
            CollectionAssert.Contains(_releases, _visuals[0].SpriteAddress); Assert.That(_releases, Does.Not.Contain(_visuals[1].SpriteAddress));
        }
        [Test] public async Task FACILITY_WORLD_009_FailedReplacementKeepsLastCompleteSpriteAndGeometry()
        {
            await Bind(null); LoadWith(address => Task.FromResult<Sprite>(null)); var before = _body.GetComponentsInChildren<BoxCollider2D>(true);
            Assert.That(await Bind(Owner(2)), Is.False); Assert.That(Get<int>("SelectedLevel"), Is.EqualTo(1)); Assert.That(_renderer.sprite, Is.SameAs(_sprites[0]));
            Assert.That((Vector2)_renderer.transform.localPosition, Is.EqualTo(_visuals[0].SpriteOffset)); CollectionAssert.AreEqual(before, _body.GetComponentsInChildren<BoxCollider2D>(true));
        }
        [Test] public async Task FACILITY_WORLD_010_InvalidOrDuplicateLevelSetCannotLoadOrBuildGeometry()
        {
            Set(_presenter, "_visuals", new[] { _visuals[0], _visuals[0], _visuals[2] }); Assert.That(await Bind(null), Is.False);
            Assert.That(_loads, Is.Empty); Assert.That(_body.GetComponentsInChildren<Collider2D>(true), Is.Empty); Assert.That(_renderer.sprite, Is.Null);
        }
        private Task<bool> Bind(AdultCareerRuntimeComponent owner) => (Task<bool>)_type.GetMethod("BindOwner").Invoke(_presenter, new object[] { owner });
        private Task<bool> Refresh() => (Task<bool>)_type.GetMethod("RefreshAsync").Invoke(_presenter, null);
        private T Get<T>(string property) => (T)_type.GetProperty(property).GetValue(_presenter);
        private void LoadWith(Func<string, Task<Sprite>> load) => _type.GetMethod("ConfigureSpriteLoading").Invoke(_presenter, new object[] { load, new Action<string>(key => _releases.Add(key)) });
        [Test] public async Task FACILITY_WORLD_010_SceneBinderUsesExistingAuthoritativeOwner()
        {
            var type = typeof(FacilityWorldPresenter).Assembly.GetType("Rootborn.UI.World.AuthoredWorldOwnerBinder");
            Assert.That(type,Is.Not.Null,"Scene handoff must explicitly bind facility presenters.");
            var owner = Owner(3);
            var result = (Task<bool>)type.GetMethod("Bind").Invoke(null,new object[] { _site.scene,owner });
            Assert.That(await result,Is.True);
            Assert.That(Get<int>("SelectedLevel"),Is.EqualTo(3));
            Assert.That(_site.GetComponent<AdultCareerRuntimeComponent>(),Is.Null);
        }
        [Test] public async Task FACILITY_WORLD_011_SceneBinderRejectsMissingOwner()
        {
            var type = typeof(FacilityWorldPresenter).Assembly.GetType("Rootborn.UI.World.AuthoredWorldOwnerBinder");
            Assert.That(type,Is.Not.Null);
            Assert.That(await (Task<bool>)type.GetMethod("Bind").Invoke(null,new object[] { _site.scene,null }),Is.False);
            Assert.That(_loads,Is.Empty);
        }
        [Test] public async Task FACILITY_WORLD_012_SceneBinderRejectsReplacedOwner()
        {
            var type = typeof(FacilityWorldPresenter).Assembly.GetType("Rootborn.UI.World.AuthoredWorldOwnerBinder");
            Assert.That(type,Is.Not.Null);
            var stale = Owner(1); var current = Owner(3);
            Assert.That(stale.CanMutate,Is.False);
            Assert.That(await (Task<bool>)type.GetMethod("Bind").Invoke(null,new object[] { _site.scene,stale }),Is.False);
            Assert.That(_loads,Is.Empty);
            Assert.That(await (Task<bool>)type.GetMethod("Bind").Invoke(null,new object[] { _site.scene,current }),Is.True);
            Assert.That(Get<int>("SelectedLevel"),Is.EqualTo(3));
        }
        private AdultCareerRuntimeComponent Owner(int level, string facilityId = null)
        {
            var student = new StudentLifeProgress(_slot, _player, 12, 10); student.ConfigureCalendar(_registry.StudentLifeCalendar);
            var adult = AdultCareerProgress.FromSaveData(new AdultCareerProgressSaveData { SaveSlot = _slot, PlayerId = _player, FacilityId = level > 0 ? facilityId ?? _facility.Id : string.Empty, FacilityLevel = level });
            var storage = new SaveService(_slot); Assert.That(AdultCareerSnapshotPersistence.SaveSession(storage, AdultCareerSnapshotPersistence.Capture(adult, student, new Inventory(), new WorldStateProgress(_slot, _player))), Is.True);
            var go = Go("SitePlayer"); go.AddComponent<PlayerIdentity>().Configure(_player); go.AddComponent<StudentLifeProgressComponent>(); go.AddComponent<PlayerInventory>();
            var owner = go.AddComponent<AdultCareerRuntimeComponent>(); owner.Bind(_registry, storage); Assert.That(owner.CanMutate, Is.True); return owner;
        }
        private GameObject Go(string name) { var go = new GameObject(name); _objects.Add(go); return go; }
        private GameObject Child(string name) { var go = new GameObject(name); go.transform.SetParent(_site.transform, false); return go; }
        private T Make<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value; }
        private static void Set(object target, string name, object value) { var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); Assert.That(field, Is.Not.Null, name); field.SetValue(target, value); }
    }
}
