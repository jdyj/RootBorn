using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
using Rootborn.UI.Modern;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Careers
{
    public sealed class FacilityUpgradeBlueprintIconTests
    {
        private const string SpritePath = "Assets/Art/UI/UrbanMolding/facility_upgrade_blueprint.png";
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly List<Object> _owned = new List<Object>();
        private readonly Dictionary<FieldInfo, object> _priorStatics = new Dictionary<FieldInfo, object>();
        private readonly List<KeyValuePair<IDictionary, DictionaryEntry[]>> _priorDictionaries = new List<KeyValuePair<IDictionary, DictionaryEntry[]>>();
        private AppConfig _config;
        private SaveSlotMetadata _metadata;
        private INetworkSession _network;
        private string _tempRoot, _slot;
        private FacilityManagementPanel _panel;
        private AdultCareerRuntimeComponent _owner;
        private FacilityDefinition _facility;

        [SetUp]
        public void SetUp()
        {
            _config = GameBootstrap.Config; _metadata = ActiveSaveContext.Metadata; _network = NetworkBootstrap.ActiveSession;
            SnapshotStatic(typeof(SaveService), "s_rootDirectoryOverride");
            foreach (var name in new[] { "s_provider", "s_overrideRegistry", "s_cachedRegistry" }) SnapshotStatic(typeof(GameDataRegistryProvider), name);
            foreach (var name in new[] { "s_inventoriesByPlayer", "s_questLogsByPlayer" })
            {
                var field = typeof(PlayerGlobalState).GetField(name, StaticPrivate);
                Assert.That(field, Is.Not.Null, "Fixture global-state isolation seam changed: " + name);
                var dictionary = (IDictionary)field.GetValue(null);
                var entries = new List<DictionaryEntry>();
                foreach (DictionaryEntry entry in dictionary) entries.Add(entry);
                _priorDictionaries.Add(new KeyValuePair<IDictionary, DictionaryEntry[]>(dictionary, entries.ToArray()));
            }
            _slot = "blueprint-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _tempRoot = Path.Combine(Path.GetTempPath(), "rootborn-blueprint-" + Guid.NewGuid().ToString("N"));
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, new AppConfig { Mode = SessionMode.Single, SaveSlot = _slot });
            typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, null);
            SaveService.SetRootDirectoryForTests(_tempRoot);
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = _slot });
        }

        [TearDown]
        public void TearDown()
        {
            if (_panel != null) _panel.Close();
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            ActiveSaveContext.Set(_metadata);
            foreach (var pair in _priorDictionaries)
            {
                pair.Key.Clear();
                foreach (var entry in pair.Value) pair.Key.Add(entry.Key, entry.Value);
            }
            _priorDictionaries.Clear();
            foreach (var pair in _priorStatics) pair.Key.SetValue(null, pair.Value);
            _priorStatics.Clear();
            typeof(GameBootstrap).GetProperty("Config").SetValue(null, _config);
            typeof(NetworkBootstrap).GetProperty("ActiveSession").SetValue(null, _network);
            if (!string.IsNullOrEmpty(_tempRoot) && Directory.Exists(_tempRoot))
            {
                string resolved = Path.GetFullPath(_tempRoot);
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Assert.That(resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(resolved).StartsWith("rootborn-blueprint-", StringComparison.Ordinal), Is.True);
                Directory.Delete(resolved, true);
            }
        }

        [Test]
        public void ReviewedBlueprintUsesSavedSpriteGeometryImportAddressAndOneSharedThemeKey()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            Assert.That(sprite, Is.Not.Null, "Import the reviewed expansion blueprint before exposing it on the existing button.");
            Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, 64, 64)));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(32, 32)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16));
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(SpritePath));
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.address, Is.EqualTo("sprites/ui/urban-life/facility-upgrade"));
            Assert.That(entry.parentGroup.Name, Is.EqualTo("Sprites"));
            var theme = AssetDatabase.LoadAssetAtPath<UrbanUiThemeDefinition>("Assets/Data/UI/UrbanMoldingTheme.asset");
            Assert.That(theme.GetIcon("facility-upgrade"), Is.SameAs(sprite));
            var icons = new SerializedObject(theme).FindProperty("_icons");
            Assert.That(icons.arraySize, Is.EqualTo(26));
            int matchingKeys = 0;
            for (int i = 0; i < icons.arraySize; i++)
                if (icons.GetArrayElementAtIndex(i).FindPropertyRelative("_key").stringValue == "facility-upgrade") matchingKeys++;
            Assert.That(matchingKeys, Is.EqualTo(1), "One shared theme icon entry owns this asset.");
        }

        [Test]
        public void BlueprintSourceUsesBinaryAlphaAtMost31ColorsAndExactTwoPixelClusters()
        {
            Assert.That(File.Exists(SpritePath), Is.True, "Reviewed blueprint PNG is missing.");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(SpritePath)), Is.True);
                Assert.That(new Vector2Int(texture.width, texture.height), Is.EqualTo(new Vector2Int(64, 64)));
                var pixels = texture.GetPixels32();
                Assert.That(pixels.All(p => p.a == 0 || p.a == 255), Is.True);
                var colors = new HashSet<uint>(pixels.Where(p => p.a > 0).Select(ColorKey));
                Assert.That(colors.Count, Is.InRange(1, 31));
                for (int y = 0; y < 64; y += 2)
                    for (int x = 0; x < 64; x += 2)
                    {
                        uint color = ColorKey(pixels[y * 64 + x]);
                        Assert.That(new[] { ColorKey(pixels[y * 64 + x + 1]), ColorKey(pixels[(y + 1) * 64 + x]), ColorKey(pixels[(y + 1) * 64 + x + 1]) },
                            Is.All.EqualTo(color), "Nonuniform2x2 cluster at source pixel " + x + "," + y);
                    }
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void UpgradeIconIsDecorativeInsideTheExistingButtonAndLeavesItsLabelClear()
        {
            BuildFixture(10);
            var before = OwnedDtos();
            _panel.Show(_facility, _owner);
            var button = _panel.UpgradeButtonForTests;
            var icon = UpgradeIcon();
            var rect = (RectTransform)icon.transform;
            var label = button.transform.Find("Label").GetComponent<Text>();
            Assert.That(((RectTransform)button.transform).sizeDelta, Is.EqualTo(new Vector2(256, 48)));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(40, 40)));
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(10, -4)));
            Assert.That(new SerializedObject(icon).FindProperty("_key").stringValue, Is.EqualTo("facility-upgrade"));
            var image = icon.GetComponent<Image>();
            Assert.That(image.preserveAspect, Is.True);
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(icon.GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(icon.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(icon.GetComponentsInChildren<Button>(true), Is.Empty, "The existing UpgradeFacility button must remain the only click owner.");
            Assert.That(label.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(58, -4)));
            Assert.That(label.rectTransform.sizeDelta, Is.EqualTo(new Vector2(186, 40)));
            Assert.That(label.raycastTarget, Is.False);
            Assert.That(label.text, Is.Not.Empty);
            AssertInside(rect, (RectTransform)button.transform);
            AssertInside(label.rectTransform, (RectTransform)button.transform);
            var a = new Vector3[4]; var b = new Vector3[4];
            rect.GetWorldCorners(a); label.rectTransform.GetWorldCorners(b);
            Assert.That(a.Max(p => button.transform.InverseTransformPoint(p).x) + 8f,
                Is.LessThanOrEqualTo(b.Min(p => button.transform.InverseTransformPoint(p).x) + .01f));
            Assert.That(OwnedDtos(), Is.EqualTo(before), "Showing decorative art cannot mutate fixture progression or items.");
        }

        [TestCase(10, true, 1f)]
        [TestCase(2, false, .35f)]
        public void BlueprintOpacityReflectsExistingUpgradeEligibility(int coins, bool eligible, float opacity)
        {
            BuildFixture(coins);
            var before = OwnedDtos();
            Assert.That(_owner.CanUpgrade(_facility), Is.EqualTo(eligible), "Fixture must establish real eligibility from the authored cost.");
            _panel.Show(_facility, _owner);
            Assert.That(_panel.UpgradeButtonForTests.interactable, Is.EqualTo(eligible));
            Assert.That(UpgradeIcon().GetComponent<CanvasGroup>().alpha, Is.EqualTo(opacity).Within(.001f));
            Assert.That(OwnedDtos(), Is.EqualTo(before));
        }

        [Test]
        public void ShowCloseReusesOneIconWithoutDuplicateThemeOrOwnerSubscriptions()
        {
            BuildFixture(10);
            _panel.Show(_facility, _owner);
            var icon = UpgradeIcon();
            int childCount = _panel.UpgradeButtonForTests.transform.childCount;
            var before = OwnedDtos();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(HandlerCount(typeof(UrbanUiThemeRuntime), null, "Changed", icon), Is.EqualTo(1));
                Assert.That(HandlerCount(typeof(AdultCareerRuntimeComponent), _owner, "Committed", _panel), Is.EqualTo(1));
                _panel.Close();
                Assert.That(HandlerCount(typeof(UrbanUiThemeRuntime), null, "Changed", icon), Is.Zero);
                Assert.That(HandlerCount(typeof(AdultCareerRuntimeComponent), _owner, "Committed", _panel), Is.Zero);
                _panel.Show(_facility, _owner);
                Assert.That(UpgradeIcon(), Is.SameAs(icon));
                Assert.That(_panel.UpgradeButtonForTests.transform.childCount, Is.EqualTo(childCount));
                Assert.That(_panel.UpgradeButtonForTests.GetComponentsInChildren<UrbanUiIcon>(true).Count(x => x.name == "UpgradeIcon"), Is.EqualTo(1));
            }
            Assert.That(OwnedDtos(), Is.EqualTo(before));
        }

        private void BuildFixture(int coins)
        {
            var calendar = Own(ScriptableObject.CreateInstance<LifeCalendarDefinition>()); calendar.ConfigureForTests(2, 3);
            var coin = Own(ScriptableObject.CreateInstance<ItemDefinition>()); Set(coin, "_id", "fixture.blueprint.coin"); Set(coin, "_maxStack", 9999);
            _facility = Own(ScriptableObject.CreateInstance<FacilityDefinition>());
            Set(_facility, "_id", "fixture.blueprint.facility"); Set(_facility, "_displayNameKey", "Blueprint fixture");
            Set(_facility, "_career", Own(ScriptableObject.CreateInstance<CareerDefinition>()));
            Set(_facility, "_candidate", Own(ScriptableObject.CreateInstance<CareerCandidateDefinition>()));
            var levels = new FacilityLevelDefinition[3];
            for (int i = 0; i < levels.Length; i++)
            {
                levels[i] = Own(ScriptableObject.CreateInstance<FacilityLevelDefinition>());
                Set(levels[i], "_id", "fixture.blueprint.level" + (i + 1)); Set(levels[i], "_level", i + 1);
            }
            Set(levels[1], "_costItems", new[] { coin }); Set(levels[1], "_costCounts", new[] { 3 });
            Set(levels[2], "_completesRoute", true); Set(_facility, "_levels", levels);
            var registry = Own(ScriptableObject.CreateInstance<GameDataRegistry>());
            Set(registry, "_items", new[] { coin }); Set(registry, "_studentLifeCalendar", calendar); Set(registry, "_facilityDefinitions", new[] { _facility });
            GameDataRegistryProvider.SetOverrideForTests(registry);
            var inventory = new Inventory(); inventory.Add(coin, coins);
            const string playerId = "blueprint-unit";
            var student = new StudentLifeProgress(_slot, playerId, 12, 10); student.ConfigureCalendar(calendar);
            var adult = AdultCareerProgress.FromSaveData(new AdultCareerProgressSaveData { SaveSlot = _slot, PlayerId = playerId, FacilityId = _facility.Id, FacilityLevel = 1 });
            var storage = new SaveService(_slot, _tempRoot);
            Assert.That(AdultCareerSnapshotPersistence.SaveSession(storage, AdultCareerSnapshotPersistence.Capture(adult, student, inventory, new WorldStateProgress(_slot, playerId))), Is.True);
            var actor = Own(new GameObject("BlueprintUnitOwner"));
            actor.AddComponent<PlayerIdentity>().Configure(playerId); actor.AddComponent<StudentLifeProgressComponent>(); actor.AddComponent<PlayerInventory>();
            _owner = actor.AddComponent<AdultCareerRuntimeComponent>(); _owner.Bind(registry, storage);
            Assert.That(_owner.IsBound && _owner.CanMutate, Is.True, "Use the existing durable owner fixture, never live progression.");
            var canvas = Own(new GameObject("BlueprintUnitCanvas", typeof(RectTransform), typeof(Canvas)));
            _panel = FacilityManagementPanel.EnsureInScene(canvas.GetComponent<Canvas>());
        }

        private UrbanUiIcon UpgradeIcon()
        {
            var child = _panel.UpgradeButtonForTests.transform.Find("UpgradeIcon");
            Assert.That(child, Is.Not.Null, "The existing expansion button needs one shared-owner blueprint icon.");
            var icon = child.GetComponent<UrbanUiIcon>();
            Assert.That(icon, Is.Not.Null);
            return icon;
        }
        private string[] OwnedDtos() => new[] {
            JsonUtility.ToJson(_owner.StudentComponent.Progress.ToSaveData()), JsonUtility.ToJson(_owner.PlayerInventory.ToSaveData()),
            JsonUtility.ToJson(_owner.Progress.ToSaveData()), JsonUtility.ToJson(_owner.World.ToSaveData()), JsonUtility.ToJson(_owner.FurnitureUnlocks.ToSaveData()) };
        private static int HandlerCount(Type type, object instance, string name, object target)
        {
            var field = type.GetField(name, instance == null ? StaticPrivate : InstancePrivate);
            Assert.That(field, Is.Not.Null, "Event fixture observation seam changed: " + name);
            var handlers = field.GetValue(instance) as Delegate;
            return handlers == null ? 0 : handlers.GetInvocationList().Count(h => object.ReferenceEquals(h.Target, target));
        }
        private static uint ColorKey(Color32 p) => p.a == 0 ? 0u : ((uint)p.a << 24) | ((uint)p.r << 16) | ((uint)p.g << 8) | p.b;
        private void SnapshotStatic(Type type, string name)
        {
            var field = type.GetField(name, StaticPrivate); Assert.That(field, Is.Not.Null, name); _priorStatics.Add(field, field.GetValue(null));
        }
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, InstancePrivate); Assert.That(field, Is.Not.Null, name); field.SetValue(target, value);
        }
        private static void AssertInside(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            foreach (var point in corners)
            {
                var local = parent.InverseTransformPoint(point);
                Assert.That(local.x, Is.InRange(parent.rect.xMin + 4f - .01f, parent.rect.xMax - 4f + .01f));
                Assert.That(local.y, Is.InRange(parent.rect.yMin + 4f - .01f, parent.rect.yMax - 4f + .01f));
            }
        }
    }
}
