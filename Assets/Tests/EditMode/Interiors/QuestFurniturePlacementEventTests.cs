using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Interiors;
using Rootborn.Game.Placement;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using Rootborn.UI.Interiors;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.Interiors
{
    public sealed class QuestFurniturePlacementEventTests
    {
        private string _tempRoot;
        private SaveSlotMetadata _oldSave;
        private GameObject _root;
        private Tile _art;
        private FurnitureDefinition _definition;
        private InteriorGeneratedMap _map;
        private InteriorPlacementPreviewOverlay _overlay;
        private TilePlacementSurface _surface;
        private StudentLifeProgress _student;
        private EventInfo _recorded;
        private Action<QuestEvent, StudentLifeProgress> _handler;
        private readonly List<QuestEvent> _events = new List<QuestEvent>();
        private bool _layoutSavedWhenObserved;
        private static readonly Vector3Int Offset = new Vector3Int(-4, -4, 0);

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "rootborn-quest-placement", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
            SaveService.SetRootDirectoryForTests(_tempRoot);
            _oldSave = ActiveSaveContext.Metadata;
            ActiveSaveContext.Set(new SaveSlotMetadata { SlotId = "placement-slot" });
            _student = new StudentLifeProgress("placement-slot", "placement-player", 20, 20);
            _root = new GameObject("QuestPlacementFixture", typeof(Grid));
            var ground = Map("FixtureGround");
            var objects = Map("FixtureObjects");
            var occupancy = Map("FixtureOccupancy");
            var preview = Map("FixturePreview");
            _art = ScriptableObject.CreateInstance<Tile>();
            _definition = FurnitureDefinition.CreateForTests("fixture.quest-desk", "Desk", "Desk", new[] { "house" },
                new[] { new FurnitureTilePart(Vector2Int.zero, _art) }, new[] { Vector2Int.zero }, true, FurniturePlacementPreference.Any);
            var furniture = new InteriorFurnitureDefinition(_definition.StableId, _definition.DisplayName,
                InteriorObjectKind.Desk, true, InteriorPlacementPreference.Any,
                new[] { new InteriorFurnitureTile(Vector2Int.zero, _art) }, _definition.FootprintCells, _definition);
            _map = new InteriorGeneratedMap(8, 8);
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            {
                _map.SetKind(new Vector2Int(x, y), InteriorCellKind.Floor);
                ground.SetTile(new Vector3Int(x, y, 0) + Offset, _art);
            }
            _surface = objects.gameObject.AddComponent<TilePlacementSurface>();
            _surface.ConfigureForTests("house", ground, objects, occupancy, preview, new BoundsInt(-4, -4, 0, 8, 8, 1),
                TilePlacementRuleFlags.RequireGround | TilePlacementRuleFlags.RejectOccupiedCells | TilePlacementRuleFlags.StayInsideBounds);
            _overlay = _root.AddComponent<InteriorPlacementPreviewOverlay>();
            Set("_surface", _surface); Set("_furnitureTilemap", objects); Set("_occupancyTilemap", occupancy);
            Set("_overlay", preview); Set("_map", _map); Set("_activeFurniture", furniture); Set("_offset", Offset);
            Set("_request", InteriorFurniturePlacementRequest.CreateForTests(InteriorObjectKind.Desk, 1,
                InteriorPlacementPreference.Any, InteriorFacingDirection.None, false));
            _events.Clear();
            _layoutSavedWhenObserved = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (_recorded != null && _handler != null) _recorded.RemoveEventHandler(null, _handler);
            _recorded = null; _handler = null;
            var cached = typeof(InteriorPlacementPreviewOverlay).GetField("SavedFurnitureLayout", BindingFlags.Static | BindingFlags.NonPublic);
            if (cached?.GetValue(null) is List<FurniturePlacementSaveData> list) list.Clear();
            Object.DestroyImmediate(_root); Object.DestroyImmediate(_definition); Object.DestroyImmediate(_art);
            ActiveSaveContext.Set(_oldSave);
            SaveService.SetRootDirectoryForTests(null);
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true);
        }

        [Test]
        public void SuccessfulManualPlacement_PublishesExactFurnitureAndActor_AfterLayoutIsSaved()
        {
            SubscribeAndBind();
            Assert.That(_overlay.TryManualPlaceFirstValidForTests(out var message), Is.True, message);
            Assert.That(_overlay.PlacedFurnitureCountForTests, Is.EqualTo(1));
            Assert.That(_events.Count, Is.EqualTo(1));
            Assert.That(_events[0].Kind.ToString(), Is.EqualTo("FurniturePlaced"));
            Assert.That(Field(_events[0], "Furniture"), Is.SameAs(_definition));
            Assert.That(Field(_events[0], "SurfaceId"), Is.EqualTo("house"));
            Assert.That(_events[0].EventKey, Is.Not.Empty);
            Assert.That(_layoutSavedWhenObserved, Is.True, "Story progress must not precede saved visible furniture.");
        }

        [Test]
        public void RejectedManualPlacement_DoesNotPublishOrSaveFurniture()
        {
            SubscribeAndBind();
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) _map.SetKind(new Vector2Int(x, y), InteriorCellKind.Wall);
            Assert.That(_overlay.TryManualPlaceFirstValidForTests(out _), Is.False);
            Assert.That(_events, Is.Empty);
            Assert.That(_overlay.PlacedFurnitureCountForTests, Is.EqualTo(0));
            Assert.That(FurniturePlacementLayoutPersistence.HasHouseLayoutForStage(0), Is.False);
        }

        [Test]
        public void SaveRestoreAndMove_DoNotPublishNewPlacementQuestEvents()
        {
            SubscribeAndBind();
            Assert.That(_overlay.TryManualPlaceFirstValidForTests(out var message), Is.True, message);
            Assert.That(_events.Count, Is.EqualTo(1));
            Assert.That(_overlay.MoveSelectedFurniture(), Is.True);
            Assert.That(_events.Count, Is.EqualTo(1), "Moving an existing object is not a purchase/place completion.");
            _overlay.SaveFurnitureLayout();
            var saved = new List<FurniturePlacementSaveData>();
            Assert.That(FurniturePlacementLayoutPersistence.TryLoadHouseLayoutForStage(0, saved), Is.True);
            var registry = (FurniturePlacementRegistry)typeof(InteriorPlacementPreviewOverlay)
                .GetField("_registry", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_overlay);
            var restored = FurniturePlacementSaveUtility.Restore(_surface, registry, saved, new[] { _definition });
            Assert.That(restored.RestoredCount, Is.EqualTo(1));
            Assert.That(_events.Count, Is.EqualTo(1), "The shared placement service/registry must not emit player-action events during restore.");
        }

        private void SubscribeAndBind()
        {
            var bridge = typeof(QuestEvent).Assembly.GetType("Rootborn.Game.Quests.QuestGameplayEventBridge");
            Assert.That(bridge, Is.Not.Null, "Missing shared gameplay event bridge.");
            _recorded = bridge.GetEvent("OnRecorded", BindingFlags.Public | BindingFlags.Static);
            Assert.That(_recorded, Is.Not.Null);
            _handler = (e, p) =>
            {
                Assert.That(p, Is.SameAs(_student));
                _events.Add(e);
                var saved = new List<FurniturePlacementSaveData>();
                _layoutSavedWhenObserved = FurniturePlacementLayoutPersistence.TryLoadHouseLayoutForStage(0, saved)
                    && saved.Count == 1 && saved[0].FurnitureId == _definition.StableId;
            };
            _recorded.AddEventHandler(null, _handler);
            var bind = typeof(InteriorPlacementPreviewOverlay).GetMethod("BindQuestPlayer", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(bind, Is.Not.Null, "Placement must retain the acting player rather than a cross-slot static identity.");
            bind.Invoke(_overlay, new object[] { _student });
        }
        private Tilemap Map(string name)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(_root.transform, false);
            return go.GetComponent<Tilemap>();
        }
        private void Set(string name, object value) => typeof(InteriorPlacementPreviewOverlay)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_overlay, value);
        private static object Field(QuestEvent value, string name)
        {
            var field = typeof(QuestEvent).GetField(name);
            Assert.That(field, Is.Not.Null, name);
            return field.GetValue(value);
        }
    }
}
