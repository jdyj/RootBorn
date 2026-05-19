using System.Collections.Generic;
using Rootborn.Game.Interiors;
using Rootborn.Game.Placement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Rootborn.UI.Interiors
{
    public sealed class InteriorPlacementPreviewOverlay : MonoBehaviour
    {
        private static readonly Color ValidColor = new Color(0.15f, 0.95f, 0.35f, 0.38f);
        private static readonly Color InvalidColor = new Color(0.95f, 0.18f, 0.12f, 0.24f);
        private static readonly Color ValidGhostColor = new Color(1f, 1f, 1f, 0.62f);
        private static readonly Color InvalidGhostColor = new Color(1f, 0.25f, 0.2f, 0.48f);
        private static readonly Color ValidFootprintColor = new Color(0.15f, 0.95f, 0.95f, 0.44f);
        private static readonly Color InvalidFootprintColor = new Color(1f, 0.2f, 0.12f, 0.42f);
        private static readonly List<FurniturePlacementSaveData> SavedFurnitureLayout = new List<FurniturePlacementSaveData>();

        private readonly FurniturePlacementRegistry _registry = new FurniturePlacementRegistry();
        private Tilemap _overlay;
        private Tilemap _furnitureTilemap;
        private Tilemap _occupancyTilemap;
        private Tilemap _ghostTilemap;
        private Tilemap _footprintPreviewTilemap;
        private TilePlacementSurface _surface;
        private Tile _validTile;
        private Tile _invalidTile;
        private Tile _validFootprintTile;
        private Tile _invalidFootprintTile;
        private InteriorGeneratedMap _map;
        private InteriorFurniturePlacementRequest _request;
        private InteriorTilemapApplier _applier;
        private InteriorFurnitureDefinition _activeFurniture;
        private FurniturePlacementDirection _activeDirection = FurniturePlacementDirection.North;
        private FurniturePlacementInstance _selectedInstance;
        private bool _hasSelectedInstance;
        private bool _wasMousePressed;
        private bool _hasPreviewMapCell;
        private Vector2Int _lastPreviewMapCell;
        private Vector3Int _offset;

        public int ValidCellCount { get; private set; }
        public int InvalidCellCount { get; private set; }
        public string LastMessage { get; private set; }
        public FurniturePlacementDirection ActiveDirection => _activeDirection;
        public int ActiveSurfaceCellCountForTests => _surface != null ? _surface.Bounds.size.x * _surface.Bounds.size.y : 0;

        public static InteriorPlacementPreviewOverlay Ensure()
        {
            var scene = SceneManager.GetActiveScene();
            var existing = FindFirstObjectByType<InteriorPlacementPreviewOverlay>();
            if (existing != null && existing.gameObject.scene == scene)
            {
                return existing;
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            var go = new GameObject("[HousePlacementAvailabilityOverlay]", typeof(InteriorPlacementPreviewOverlay));
            try
            {
                SceneManager.MoveGameObjectToScene(go, scene);
            }
            catch (System.ArgumentException)
            {
                Destroy(go);
                return null;
            }

            return go.GetComponent<InteriorPlacementPreviewOverlay>();
        }

        public void Refresh(InteriorGeneratedMap map, InteriorFurniturePlacementRequest request, InteriorTilemapApplier applier)
        {
            _activeFurniture = null;
            _activeDirection = FurniturePlacementDirection.North;
            ClearGhost();
            RefreshInternal(map, request, applier);
        }

        public void RefreshFurniture(InteriorGeneratedMap map, InteriorFurniturePlacementRequest request, InteriorTilemapApplier applier, InteriorFurnitureDefinition furniture)
        {
            _surface = null;
            _furnitureTilemap = null;
            _occupancyTilemap = null;
            _overlay = null;
            _activeFurniture = furniture;
            _activeDirection = FurniturePlacementDirection.North;
            _hasSelectedInstance = false;
            ClearGhost();
            EnsureFurnitureTilemap();
            RefreshInternal(map, request, applier);
        }

        public void BindGeneratedMapForPersistence(InteriorGeneratedMap map, InteriorTilemapApplier applier)
        {
            _map = map;
            _applier = applier;
            _offset = map != null ? new Vector3Int(-map.Width / 2, -map.Height / 2, 0) : Vector3Int.zero;
            EnsureFurnitureTilemap();
            ConfigureSurface();
        }

        public bool HasSavedFurnitureLayout()
        {
            return SavedFurnitureLayout.Count > 0 || FurniturePlacementLayoutPersistence.HasHouseLayout();
        }

        public FurniturePlacementDirection RotateActiveFurniture()
        {
            _activeDirection = NextDirection(_activeDirection);
            LastMessage = "Direction " + _activeDirection;
            Draw();
            if (_hasPreviewMapCell && !_hasSelectedInstance)
            {
                DrawGhost(_lastPreviewMapCell);
            }

            return _activeDirection;
        }

        public bool TryManualPlaceFirstValidForTests(out string message)
        {
            if (_activeFurniture != null && _map != null)
            {
                for (int y = 0; y < _map.Height; y++)
                {
                    for (int x = 0; x < _map.Width; x++)
                    {
                        if (TryManualPlace(new Vector2Int(x, y), out message))
                        {
                            return true;
                        }
                    }
                }

                message = LastMessage ?? "No valid cells";
                return false;
            }

            var candidates = InteriorPlacementAvailability.CollectCandidates(_map, _request);
            if (candidates.Valid.Count == 0)
            {
                message = "No valid cells";
                return false;
            }

            for (int i = 0; i < candidates.Valid.Count; i++)
            {
                if (TryManualPlace(candidates.Valid[i], out message))
                {
                    return true;
                }
            }

            message = LastMessage ?? "No valid cells";
            return false;
        }

        public bool MoveSelectedFurniture()
        {
            if (!_hasSelectedInstance || _surface == null || _map == null || _activeFurniture == null)
            {
                return false;
            }

            var originalInstance = _selectedInstance;
            var direction = originalInstance.Direction;
            ClearMapCells(originalInstance);
            var originalMapAnchor = new Vector2Int(originalInstance.AnchorCell.x - _offset.x, originalInstance.AnchorCell.y - _offset.y);
            for (int y = 0; y < _map.Height; y++)
            {
                for (int x = 0; x < _map.Width; x++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (candidate == originalMapAnchor || MoveTargetOverlaps(originalInstance, candidate, direction) || !CanPlaceFurnitureAt(candidate))
                    {
                        continue;
                    }

                    var newAnchor = new Vector3Int(candidate.x, candidate.y, 0) + _offset;
                    if (_registry.TryMove(_surface, originalInstance.InstanceId, newAnchor, direction, out var moved, out _))
                    {
                        PlaceMapFootprint(candidate, direction);
                        ApplyGeneratedMap();
                        ClearDefaultDecorationTiles(candidate);
                        _selectedInstance = moved;
                        _hasSelectedInstance = true;
                        _activeDirection = direction;
                        LastMessage = "Moved " + moved.FurnitureId;
                        ClearOverlay();
                        return true;
                    }
                }
            }

            PlaceMapFootprint(originalMapAnchor, direction);
            LastMessage = "No valid move target";
            Draw();
            return false;
        }

        public bool SaveFurnitureLayout()
        {
            SavedFurnitureLayout.Clear();
            foreach (var instance in _registry.Instances)
            {
                var saveData = FurniturePlacementSaveData.FromInstance(instance, string.Empty);
                saveData.TileName = ResolveTileName(instance);
                SavedFurnitureLayout.Add(saveData);
            }

            FurniturePlacementLayoutPersistence.SaveHouseLayout(SavedFurnitureLayout);
            LastMessage = "Saved " + SavedFurnitureLayout.Count + " furniture";
            return SavedFurnitureLayout.Count > 0;
        }

        public bool ClearPlacedFurniture()
        {
            return ClearPlacedFurnitureInternal(true);
        }

        public bool LoadFurnitureLayout()
        {
            FurniturePlacementLayoutPersistence.TryLoadHouseLayout(SavedFurnitureLayout);
            if (SavedFurnitureLayout.Count == 0 || _map == null)
            {
                LastMessage = "No saved furniture";
                return false;
            }

            EnsureFurnitureTilemap();
            ConfigureSurface();
            if (_surface == null)
            {
                return false;
            }

            ClearPlacedFurnitureInternal(false);
            var catalog = InteriorFurnitureCatalog.LoadFurniture();
            var placementDefinitions = new List<FurnitureDefinition>();
            for (int i = 0; i < catalog.Count; i++)
            {
                if (catalog[i].PlacementDefinition != null)
                {
                    placementDefinitions.Add(catalog[i].PlacementDefinition);
                }
            }

            var restored = FurniturePlacementSaveUtility.Restore(_surface, _registry, SavedFurnitureLayout, placementDefinitions);
            int mapRestored = 0;
            var restoredAnchors = new List<Vector2Int>();
            var restoredFurniture = new List<InteriorFurnitureDefinition>();
            var restoredDirections = new List<FurniturePlacementDirection>();
            for (int i = 0; i < SavedFurnitureLayout.Count; i++)
            {
                var save = SavedFurnitureLayout[i];
                var furniture = FindFurniture(catalog, save.FurnitureId);
                if (furniture == null)
                {
                    continue;
                }

                _activeFurniture = furniture;
                _activeDirection = save.Direction;
                var mapAnchor = new Vector2Int(save.AnchorX - _offset.x, save.AnchorY - _offset.y);
                if (PlaceMapFootprint(mapAnchor, save.Direction))
                {
                    restoredAnchors.Add(mapAnchor);
                    restoredFurniture.Add(furniture);
                    restoredDirections.Add(save.Direction);
                    mapRestored++;
                }
            }

            _hasSelectedInstance = false;
            ApplyGeneratedMap();
            for (int i = 0; i < restoredAnchors.Count; i++)
            {
                _activeFurniture = restoredFurniture[i];
                _activeDirection = restoredDirections[i];
                ClearDefaultDecorationTiles(restoredAnchors[i]);
            }

            ClearOverlay();
            LastMessage = "Loaded " + restored.RestoredCount + " furniture";
            return restored.RestoredCount > 0 && mapRestored == restored.RestoredCount;
        }

        public bool DeleteSelectedFurniture()
        {
            if (!_hasSelectedInstance || _surface == null)
            {
                return false;
            }

            ClearMapCells(_selectedInstance);
            bool deleted = _registry.TryDelete(_surface, _selectedInstance.InstanceId);
            if (deleted)
            {
                _hasSelectedInstance = false;
                ApplyGeneratedMap();
                LastMessage = "Deleted " + _selectedInstance.FurnitureId;
                ClearGhost();
            }

            return deleted;
        }

        private void RefreshInternal(InteriorGeneratedMap map, InteriorFurniturePlacementRequest request, InteriorTilemapApplier applier)
        {
            _map = map;
            _request = request;
            _applier = applier;
            EnsureTilemap();
            Draw();
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                _wasMousePressed = false;
                ClearGhost();
                return;
            }

            if (_map == null || _request == null || _overlay == null)
            {
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            var screenPosition = mouse.position.ReadValue();
            if (IsPointerOverUi(screenPosition))
            {
                ClearGhost();
                return;
            }

            if (!TryScreenToWorldOnTilePlane(camera, screenPosition, out var world))
            {
                ClearGhost();
                return;
            }

            var tileCell = _overlay.WorldToCell(world);
            var mapCell = new Vector2Int(tileCell.x - _offset.x, tileCell.y - _offset.y);
            if (_activeFurniture != null && !_hasSelectedInstance)
            {
                DrawGhost(mapCell);
            }
            else
            {
                ClearGhost();
            }

            bool isMousePressed = mouse.leftButton.isPressed;
            if (!isMousePressed)
            {
                _wasMousePressed = false;
                return;
            }

            if (_wasMousePressed)
            {
                return;
            }

            _wasMousePressed = true;
            if (_registry.TryFindAt(_surface != null ? _surface.SurfaceId : "house", tileCell, out var instance))
            {
                _selectedInstance = instance;
                _hasSelectedInstance = true;
                LastMessage = "Selected " + instance.FurnitureId;
                ClearGhost();
                return;
            }

            TryManualPlace(mapCell, out _);
        }

        private static bool TryScreenToWorldOnTilePlane(Camera camera, Vector2 screenPosition, out Vector3 world)
        {
            world = default;
            if (camera == null)
            {
                return false;
            }

            var ray = camera.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out var distance))
            {
                return false;
            }

            world = ray.GetPoint(distance);
            world.z = 0f;
            return true;
        }

        private static bool IsPointerOverUi(Vector2 screenPosition)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            return results.Count > 0;
        }

        private bool TryManualPlace(Vector2Int mapCell, out string message)
        {
            if (_activeFurniture != null)
            {
                return TryManualPlaceFurniture(mapCell, out message);
            }

            if (!InteriorPlacementAvailability.TryPlaceManual(_map, mapCell, _request, out message))
            {
                LastMessage = message;
                Draw();
                return false;
            }

            LastMessage = message;
            ApplyGeneratedMap();
            Draw();
            return true;
        }

        private bool TryManualPlaceFurniture(Vector2Int mapCell, out string message)
        {
            if (!CanPlaceFurnitureAt(mapCell))
            {
                message = "Cell is not valid for " + _activeFurniture.Id;
                LastMessage = message;
                Draw();
                return false;
            }

            EnsureFurnitureTilemap();
            ConfigureSurface();
            var anchorCell = new Vector3Int(mapCell.x, mapCell.y, 0) + _offset;
            if (_surface == null || _activeFurniture.PlacementDefinition == null || !_registry.TryPlace(_surface, _activeFurniture.PlacementDefinition, anchorCell, _activeDirection, out var instance, out var placement))
            {
                message = "Could not place " + _activeFurniture.Id;
                LastMessage = message;
                Draw();
                return false;
            }

            if (!PlaceMapFootprint(mapCell, _activeDirection))
            {
                _registry.TryDelete(_surface, instance.InstanceId);
                message = "Cell is already occupied";
                LastMessage = message;
                Draw();
                return false;
            }

            ApplyGeneratedMap();
            ClearDefaultDecorationTilesForPlacedFurniture();
            _selectedInstance = instance;
            _hasSelectedInstance = true;
            message = "Placed " + placement.FurnitureId;
            LastMessage = message;
            ClearOverlay();
            return true;
        }

        private bool CanPlaceFurnitureAt(Vector2Int anchorCell)
        {
            if (_map == null || _activeFurniture == null)
            {
                return false;
            }

            var footprint = ResolveActiveFootprintCells();
            for (int i = 0; i < footprint.Count; i++)
            {
                var cell = anchorCell + footprint[i];
                if (!_map.InBounds(cell) || !_map.IsWalkableBase(cell) || _map.GetObject(cell) != InteriorObjectKind.None)
                {
                    return false;
                }
            }

            EnsureFurnitureTilemap();
            ConfigureSurface();
            if (_surface != null && _activeFurniture.PlacementDefinition != null)
            {
                var surfaceFootprint = _activeFurniture.PlacementDefinition.GetFootprintCells(_activeDirection);
                var surfaceAnchor = new Vector3Int(anchorCell.x, anchorCell.y, 0) + _offset;
                for (int i = 0; i < surfaceFootprint.Count; i++)
                {
                    var local = surfaceFootprint[i];
                    var surfaceCell = surfaceAnchor + new Vector3Int(local.x, local.y, 0);
                    if (_surface.PlacementRules.HasFlag(TilePlacementRuleFlags.StayInsideBounds) && !_surface.Bounds.Contains(surfaceCell))
                    {
                        return false;
                    }

                    if (_surface.PlacementRules.HasFlag(TilePlacementRuleFlags.RequireGround) && (_surface.GroundTilemap == null || _surface.GroundTilemap.GetTile(surfaceCell) == null))
                    {
                        return false;
                    }

                    if (_surface.PlacementRules.HasFlag(TilePlacementRuleFlags.RejectOccupiedCells) && _surface.OccupancyTilemap != null && _surface.OccupancyTilemap.GetTile(surfaceCell) != null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool ClearPlacedFurnitureInternal(bool updateMessage)
        {
            if (_surface == null)
            {
                EnsureFurnitureTilemap();
                ConfigureSurface();
            }

            if (_surface == null)
            {
                return false;
            }

            var instances = new List<FurniturePlacementInstance>(_registry.Instances);
            for (int i = 0; i < instances.Count; i++)
            {
                ClearMapCells(instances[i]);
            }

            int cleared = _registry.ClearSurface(_surface);
            if (_furnitureTilemap != null)
            {
                _furnitureTilemap.ClearAllTiles();
            }

            if (_occupancyTilemap != null)
            {
                _occupancyTilemap.ClearAllTiles();
            }

            _hasSelectedInstance = false;
            ApplyGeneratedMap();
            ClearOverlay();
            if (updateMessage)
            {
                LastMessage = "Cleared " + cleared + " furniture";
            }

            return cleared > 0 || instances.Count > 0;
        }

        private string ResolveTileName(FurniturePlacementInstance instance)
        {
            if (_furnitureTilemap == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < instance.TileCells.Count; i++)
            {
                var tile = _furnitureTilemap.GetTile(instance.TileCells[i]);
                if (tile != null)
                {
                    return tile.name;
                }
            }

            return string.Empty;
        }
        private static InteriorFurnitureDefinition FindFurniture(IReadOnlyList<InteriorFurnitureDefinition> catalog, string furnitureId)
        {
            if (catalog == null || string.IsNullOrEmpty(furnitureId))
            {
                return null;
            }

            for (int i = 0; i < catalog.Count; i++)
            {
                var furniture = catalog[i];
                if (furniture != null && furniture.PlacementDefinition != null && furniture.PlacementDefinition.StableId == furnitureId)
                {
                    return furniture;
                }
            }

            return null;
        }

        private bool MoveTargetOverlaps(FurniturePlacementInstance originalInstance, Vector2Int candidateAnchor, FurniturePlacementDirection direction)
        {
            var footprint = ResolveFootprintCells(direction);
            for (int i = 0; i < footprint.Count; i++)
            {
                var candidateCell = new Vector3Int(candidateAnchor.x + footprint[i].x, candidateAnchor.y + footprint[i].y, 0) + _offset;
                for (int j = 0; j < originalInstance.OccupiedCells.Count; j++)
                {
                    if (candidateCell == originalInstance.OccupiedCells[j])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void ClearMapCells(FurniturePlacementInstance instance)
        {
            for (int i = 0; i < instance.OccupiedCells.Count; i++)
            {
                var cell = instance.OccupiedCells[i];
                _map.ClearObject(new Vector2Int(cell.x - _offset.x, cell.y - _offset.y));
            }
        }

        private bool PlaceMapFootprint(Vector2Int anchorCell, FurniturePlacementDirection direction)
        {
            var footprint = ResolveFootprintCells(direction);
            for (int i = 0; i < footprint.Count; i++)
            {
                var cell = anchorCell + footprint[i];
                if (!_map.TryPlaceObject(cell, _activeFurniture.ObjectKind, _activeFurniture.BlocksMovement, ToInteriorDirection(direction)))
                {
                    return false;
                }
            }

            return true;
        }

        private IReadOnlyList<Vector2Int> ResolveFootprintCells(FurniturePlacementDirection direction)
        {
            if (_activeFurniture != null && _activeFurniture.PlacementDefinition != null)
            {
                return _activeFurniture.PlacementDefinition.GetFootprintCells(direction);
            }

            return _activeFurniture != null ? _activeFurniture.Footprint : System.Array.Empty<Vector2Int>();
        }

        private IReadOnlyList<Vector2Int> ResolveActiveFootprintCells()
        {
            return ResolveFootprintCells(_activeDirection);
        }

        private void ClearDefaultDecorationTiles(Vector2Int anchorCell)
        {
            var decorations = FindTilemapByName("HouseDecorationTilemap");
            if (decorations == null || _activeFurniture == null)
            {
                return;
            }

            var footprint = ResolveActiveFootprintCells();
            for (int i = 0; i < footprint.Count; i++)
            {
                var localCell = footprint[i];
                var tileCell = new Vector3Int(anchorCell.x + localCell.x, anchorCell.y + localCell.y, 0) + _offset;
                decorations.SetTile(tileCell, null);
                decorations.RefreshTile(tileCell);
            }
        }

        private void ClearDefaultDecorationTilesForPlacedFurniture()
        {
            var decorations = FindTilemapByName("HouseDecorationTilemap");
            if (decorations == null)
            {
                return;
            }

            foreach (var instance in _registry.Instances)
            {
                for (int i = 0; i < instance.OccupiedCells.Count; i++)
                {
                    var tileCell = instance.OccupiedCells[i];
                    decorations.SetTile(tileCell, null);
                    decorations.RefreshTile(tileCell);
                }
            }
        }

        private void ApplyGeneratedMap()
        {
            if (_applier == null)
            {
                _applier = FindFirstObjectByType<InteriorTilemapApplier>();
            }

            if (_applier != null)
            {
                _applier.Apply(_map);
            }
        }

        private void EnsureTilemap()
        {
            if (_overlay != null)
            {
                return;
            }

            if (TryBindExistingPlacementSurface() && _overlay != null)
            {
                return;
            }

            var existingOverlay = FindTilemapByName("HousePlacementAvailabilityTilemap");
            if (existingOverlay != null)
            {
                _overlay = existingOverlay;
                var existingRenderer = _overlay.GetComponent<TilemapRenderer>();
                if (existingRenderer != null)
                {
                    existingRenderer.enabled = true;
                    existingRenderer.sortingOrder = 200;
                }

                EnsurePreviewTiles();
                ConfigureSurface();
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            var floor = FindTilemapByName("HouseGroundTilemap");
            var parent = floor != null ? floor.transform.parent : null;
            var go = new GameObject("HousePlacementAvailabilityTilemap", typeof(Tilemap), typeof(TilemapRenderer));
            try
            {
                SceneManager.MoveGameObjectToScene(go, scene);
            }
            catch (System.ArgumentException)
            {
                Destroy(go);
                return;
            }

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            _overlay = go.GetComponent<Tilemap>();
            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = 200;
            EnsurePreviewTiles();
            ConfigureSurface();
        }

        private void EnsureFurnitureTilemap()
        {
            if (TryBindExistingPlacementSurface() && _overlay != null)
            {
                return;
            }

            if (_furnitureTilemap == null)
            {
                var existing = FindTilemapByName("HouseFurnitureObjectTilemap") ?? FindTilemapByName("HouseSampleFurnitureTilemap");
                if (existing != null)
                {
                    existing.gameObject.name = "HouseFurnitureObjectTilemap";
                    _furnitureTilemap = existing;
                }
                else
                {
                    _furnitureTilemap = CreateSceneTilemap("HouseFurnitureObjectTilemap", 210, true);
                }
            }

            if (_occupancyTilemap == null)
            {
                _occupancyTilemap = CreateSceneTilemap("HouseFurnitureOccupancyTilemap", 0, false);
            }

            ConfigureSurface();
        }

        private void EnsureGhostTilemap()
        {
            if (_ghostTilemap != null)
            {
                return;
            }

            var existing = FindTilemapByName("HouseFurnitureGhostPreviewTilemap");
            if (existing != null)
            {
                _ghostTilemap = existing;
                var existingRenderer = _ghostTilemap.GetComponent<TilemapRenderer>();
                if (existingRenderer != null)
                {
                    existingRenderer.enabled = true;
                    existingRenderer.sortingOrder = 205;
                }

                return;
            }

            _ghostTilemap = CreateSceneTilemap("HouseFurnitureGhostPreviewTilemap", 205, true);
        }

        private void EnsureFootprintPreviewTilemap()
        {
            if (_footprintPreviewTilemap != null)
            {
                return;
            }

            var existing = FindTilemapByName("HouseFurnitureFootprintPreviewTilemap");
            if (existing != null)
            {
                _footprintPreviewTilemap = existing;
                var existingRenderer = _footprintPreviewTilemap.GetComponent<TilemapRenderer>();
                if (existingRenderer != null)
                {
                    existingRenderer.enabled = true;
                    existingRenderer.sortingOrder = 204;
                }

                return;
            }

            _footprintPreviewTilemap = CreateSceneTilemap("HouseFurnitureFootprintPreviewTilemap", 204, true);
        }

        private void ConfigureSurface()
        {
            if (_surface != null && _surface.ObjectTilemap == _furnitureTilemap && _surface.OccupancyTilemap == _occupancyTilemap && !string.IsNullOrEmpty(_surface.SurfaceId))
            {
                return;
            }

            if (_furnitureTilemap == null || _occupancyTilemap == null)
            {
                return;
            }

            if (_surface == null)
            {
                _surface = _furnitureTilemap.GetComponent<TilePlacementSurface>();
                if (_surface == null)
                {
                    _surface = _furnitureTilemap.gameObject.AddComponent<TilePlacementSurface>();
                }
            }

            var width = _map != null ? _map.Width : 32;
            var height = _map != null ? _map.Height : 32;
            var bounds = new BoundsInt(-width / 2, -height / 2, 0, width, height, 1);
            _surface.ConfigureForTests(
                "house",
                FindTilemapByName("HouseGroundTilemap"),
                _furnitureTilemap,
                _occupancyTilemap,
                _overlay,
                bounds,
                TilePlacementRuleFlags.RejectOccupiedCells | TilePlacementRuleFlags.StayInsideBounds);
        }

        private bool TryBindExistingPlacementSurface()
        {
            if (_surface != null && _surface.ObjectTilemap != null && _surface.OccupancyTilemap != null)
            {
                _furnitureTilemap = _surface.ObjectTilemap;
                _occupancyTilemap = _surface.OccupancyTilemap;
                if (_surface.PreviewTilemap != null)
                {
                    _overlay = _surface.PreviewTilemap;
                }

                EnsurePreviewTiles();
                return true;
            }

            var surfaces = FindObjectsByType<TilePlacementSurface>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            TilePlacementSurface fallback = null;
            for (int i = 0; i < surfaces.Length; i++)
            {
                var candidate = surfaces[i];
                if (candidate == null || candidate.ObjectTilemap == null || candidate.OccupancyTilemap == null)
                {
                    continue;
                }

                if (!string.Equals(candidate.SurfaceId, "house", System.StringComparison.Ordinal))
                {
                    _surface = candidate;
                    break;
                }

                fallback = candidate;
            }

            if (_surface == null)
            {
                _surface = fallback;
            }

            if (_surface == null)
            {
                return false;
            }

            _furnitureTilemap = _surface.ObjectTilemap;
            _occupancyTilemap = _surface.OccupancyTilemap;
            if (_surface.PreviewTilemap != null)
            {
                _overlay = _surface.PreviewTilemap;
                var renderer = _overlay.GetComponent<TilemapRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = true;
                    renderer.sortingOrder = 200;
                }
            }

            EnsurePreviewTiles();
            return true;
        }

        private void EnsurePreviewTiles()
        {
            if (_validTile == null)
            {
                _validTile = CreateTile(ValidColor);
            }

            if (_invalidTile == null)
            {
                _invalidTile = CreateTile(InvalidColor);
            }

            if (_validFootprintTile == null)
            {
                _validFootprintTile = CreateTile(ValidFootprintColor);
            }

            if (_invalidFootprintTile == null)
            {
                _invalidFootprintTile = CreateTile(InvalidFootprintColor);
            }
        }

        private static Tilemap CreateSceneTilemap(string objectName, int sortingOrder, bool render)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            var floor = FindTilemapByName("HouseGroundTilemap");
            var parent = floor != null ? floor.transform.parent : null;
            var go = new GameObject(objectName, typeof(Tilemap), typeof(TilemapRenderer));
            try
            {
                SceneManager.MoveGameObjectToScene(go, scene);
            }
            catch (System.ArgumentException)
            {
                Destroy(go);
                return null;
            }

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            var renderer = go.GetComponent<TilemapRenderer>();
            renderer.enabled = render;
            renderer.sortingOrder = sortingOrder;
            return go.GetComponent<Tilemap>();
        }

        private void Draw()
        {
            if (_overlay == null)
            {
                return;
            }

            _overlay.ClearAllTiles();
            ValidCellCount = 0;
            InvalidCellCount = 0;
            if (_map == null || _request == null)
            {
                return;
            }

            _offset = new Vector3Int(-_map.Width / 2, -_map.Height / 2, 0);
            var candidates = InteriorPlacementAvailability.CollectCandidates(_map, _request);
            for (int i = 0; i < candidates.Invalid.Count; i++)
            {
                var cell = candidates.Invalid[i];
                _overlay.SetTile(new Vector3Int(cell.x, cell.y, 0) + _offset, _invalidTile);
                InvalidCellCount++;
            }

            for (int i = 0; i < candidates.Valid.Count; i++)
            {
                var cell = candidates.Valid[i];
                _overlay.SetTile(new Vector3Int(cell.x, cell.y, 0) + _offset, _validTile);
                ValidCellCount++;
            }
        }

        private void DrawGhost(Vector2Int mapCell)
        {
            if (_activeFurniture == null || _activeFurniture.PlacementDefinition == null)
            {
                ClearGhost();
                return;
            }

            EnsureFurnitureTilemap();
            EnsureGhostTilemap();
            EnsureFootprintPreviewTilemap();
            EnsurePreviewTiles();
            if (_ghostTilemap == null || _footprintPreviewTilemap == null)
            {
                return;
            }

            _ghostTilemap.ClearAllTiles();
            _footprintPreviewTilemap.ClearAllTiles();
            _lastPreviewMapCell = mapCell;
            _hasPreviewMapCell = true;
            bool canPlace = CanPlaceFurnitureAt(mapCell);
            _ghostTilemap.color = canPlace ? ValidGhostColor : InvalidGhostColor;
            _footprintPreviewTilemap.color = Color.white;

            var anchorCell = new Vector3Int(mapCell.x, mapCell.y, 0) + _offset;
            var footprintTile = canPlace ? _validFootprintTile : _invalidFootprintTile;
            var footprint = _activeFurniture.PlacementDefinition.GetFootprintCells(_activeDirection);
            for (int i = 0; i < footprint.Count; i++)
            {
                var local = footprint[i];
                var cell = anchorCell + new Vector3Int(local.x, local.y, 0);
                _footprintPreviewTilemap.SetTile(cell, footprintTile);
            }

            var parts = _activeFurniture.PlacementDefinition.GetTileParts(_activeDirection);
            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i];
                var cell = anchorCell + new Vector3Int(part.LocalCell.x, part.LocalCell.y, 0);
                _ghostTilemap.SetTile(cell, part.Tile);
            }

            _footprintPreviewTilemap.CompressBounds();
            _ghostTilemap.CompressBounds();
        }

        private void ClearGhost()
        {
            _hasPreviewMapCell = false;
            if (_ghostTilemap != null)
            {
                _ghostTilemap.ClearAllTiles();
            }

            if (_footprintPreviewTilemap != null)
            {
                _footprintPreviewTilemap.ClearAllTiles();
            }
        }

        private void ClearOverlay()
        {
            if (_overlay != null)
            {
                _overlay.ClearAllTiles();
            }

            ClearGhost();
            var tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                var name = tilemaps[i].gameObject.name;
                if (name.Contains("PlacementAvailability") || name.Contains("FurniturePreview") || name.Contains("GhostPreview") || name.Contains("FootprintPreview"))
                {
                    tilemaps[i].ClearAllTiles();
                }
            }

            ValidCellCount = 0;
            InvalidCellCount = 0;
        }

        private static Tilemap FindTilemapByName(string objectName)
        {
            var tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < tilemaps.Length; i++)
            {
                if (tilemaps[i] != null && tilemaps[i].gameObject.name == objectName)
                {
                    return tilemaps[i];
                }
            }

            return null;
        }

        private static FurniturePlacementDirection NextDirection(FurniturePlacementDirection direction)
        {
            switch (direction)
            {
                case FurniturePlacementDirection.North: return FurniturePlacementDirection.East;
                case FurniturePlacementDirection.East: return FurniturePlacementDirection.South;
                case FurniturePlacementDirection.South: return FurniturePlacementDirection.West;
                default: return FurniturePlacementDirection.North;
            }
        }

        private static InteriorFacingDirection ToInteriorDirection(FurniturePlacementDirection direction)
        {
            switch (direction)
            {
                case FurniturePlacementDirection.East: return InteriorFacingDirection.East;
                case FurniturePlacementDirection.South: return InteriorFacingDirection.South;
                case FurniturePlacementDirection.West: return InteriorFacingDirection.West;
                default: return InteriorFacingDirection.North;
            }
        }

        private static Tile CreateTile(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = color;
            tile.flags = TileFlags.None;
            return tile;
        }
    }
}
