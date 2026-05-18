using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Rootborn.Editor.Interiors
{
    public sealed class ModernOfficeFurniturePainterWindow : EditorWindow
    {
        private const string SampleTileFolder = "Assets/Data/Interiors/TileSets/ModernOfficeChairSamples";
        private const string DefaultTargetTilemapName = "ModernOfficeFurniturePainterTilemap";

        private readonly List<SampleTileEntry> _tiles = new List<SampleTileEntry>();
        private Vector2 _scroll;
        private Tilemap _targetTilemap;
        private int _selectedIndex;
        private bool _eraseMode;
        private bool _paintEnabled = true;

        public readonly struct SampleTileEntry
        {
            public SampleTileEntry(string name, Tile tile)
            {
                Name = name;
                Tile = tile;
            }

            public string Name { get; }
            public Tile Tile { get; }
        }

        [MenuItem("Rootborn/Interiors/Modern Office Furniture Painter")]
        public static void Open()
        {
            var window = GetWindow<ModernOfficeFurniturePainterWindow>("Office Furniture");
            window.minSize = new Vector2(320f, 420f);
            window.RefreshTiles();
            window.Show();
        }

        public static List<SampleTileEntry> LoadSampleTilesForTests()
        {
            return LoadSampleTiles();
        }

        public static void PaintTileForTests(Tilemap tilemap, Tile tile, Vector3Int cell, bool erase)
        {
            PaintTile(tilemap, tile, cell, erase);
        }

        private void OnEnable()
        {
            RefreshTiles();
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Modern Office Furniture Painter", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            _targetTilemap = (Tilemap)EditorGUILayout.ObjectField("Target Tilemap", _targetTilemap, typeof(Tilemap), true);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use/Create Target"))
                {
                    _targetTilemap = ResolveOrCreateTargetTilemap();
                    Selection.activeGameObject = _targetTilemap != null ? _targetTilemap.gameObject : null;
                }

                if (GUILayout.Button("Refresh"))
                {
                    RefreshTiles();
                }
            }

            _paintEnabled = EditorGUILayout.Toggle("Scene Paint Enabled", _paintEnabled);
            _eraseMode = EditorGUILayout.Toggle("Erase Mode", _eraseMode);
            EditorGUILayout.HelpBox("Scene View에서 타일을 선택한 뒤 좌클릭하면 배치됩니다. Erase Mode에서는 클릭한 칸을 지웁니다.", MessageType.Info);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
            if (_tiles.Count == 0)
            {
                EditorGUILayout.HelpBox($"No Tile assets found under {SampleTileFolder}.", MessageType.Warning);
                return;
            }

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _tiles.Count - 1);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _tiles.Count; i++)
            {
                DrawTileButton(i);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawTileButton(int index)
        {
            var entry = _tiles[index];
            using (new EditorGUILayout.HorizontalScope())
            {
                var selected = index == _selectedIndex;
                var style = selected ? EditorStyles.toolbarButton : GUI.skin.button;
                var preview = entry.Tile != null && entry.Tile.sprite != null ? AssetPreview.GetAssetPreview(entry.Tile.sprite) : null;
                if (GUILayout.Button(preview, GUILayout.Width(48f), GUILayout.Height(48f)))
                {
                    _selectedIndex = index;
                    _eraseMode = false;
                    Repaint();
                }

                if (GUILayout.Button(entry.Name, style, GUILayout.Height(48f)))
                {
                    _selectedIndex = index;
                    _eraseMode = false;
                    Repaint();
                }
            }
        }

        private void OnSceneGui(SceneView sceneView)
        {
            if (!_paintEnabled)
            {
                return;
            }

            var current = Event.current;
            if (current == null || current.type != EventType.MouseDown || current.button != 0 || current.alt)
            {
                return;
            }

            if (_targetTilemap == null)
            {
                _targetTilemap = ResolveOrCreateTargetTilemap();
            }

            if (_targetTilemap == null)
            {
                return;
            }

            var worldRay = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(worldRay, out var distance))
            {
                return;
            }

            var world = worldRay.GetPoint(distance);
            var cell = _targetTilemap.WorldToCell(world);
            var selectedTile = !_eraseMode && _tiles.Count > 0 ? _tiles[Mathf.Clamp(_selectedIndex, 0, _tiles.Count - 1)].Tile : null;
            PaintTile(_targetTilemap, selectedTile, cell, _eraseMode);
            current.Use();
            sceneView.Repaint();
        }

        private void RefreshTiles()
        {
            _tiles.Clear();
            _tiles.AddRange(LoadSampleTiles());
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _tiles.Count - 1));
        }

        private static List<SampleTileEntry> LoadSampleTiles()
        {
            var guids = AssetDatabase.FindAssets("t:Tile", new[] { SampleTileFolder });
            return guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<Tile>(path))
                .Where(tile => tile != null)
                .OrderBy(tile => tile.name)
                .Select(tile => new SampleTileEntry(tile.name, tile))
                .ToList();
        }

        private static void PaintTile(Tilemap tilemap, Tile tile, Vector3Int cell, bool erase)
        {
            if (tilemap == null)
            {
                return;
            }

            Undo.RecordObject(tilemap, erase ? "Erase Office Furniture Tile" : "Paint Office Furniture Tile");
            tilemap.SetTile(cell, erase ? null : tile);
            tilemap.RefreshTile(cell);
            EditorUtility.SetDirty(tilemap);
            var scene = tilemap.gameObject.scene;
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static Tilemap ResolveOrCreateTargetTilemap()
        {
            var existing = GameObject.Find(DefaultTargetTilemapName)?.GetComponent<Tilemap>();
            if (existing != null)
            {
                return existing;
            }

            var grid = Object.FindFirstObjectByType<Grid>();
            if (grid == null)
            {
                var gridObject = new GameObject("Grid", typeof(Grid));
                SceneManager.MoveGameObjectToScene(gridObject, SceneManager.GetActiveScene());
                grid = gridObject.GetComponent<Grid>();
                grid.cellSize = Vector3.one;
            }

            var tilemapObject = new GameObject(DefaultTargetTilemapName, typeof(Tilemap), typeof(TilemapRenderer));
            SceneManager.MoveGameObjectToScene(tilemapObject, SceneManager.GetActiveScene());
            tilemapObject.transform.SetParent(grid.transform, false);
            var renderer = tilemapObject.GetComponent<TilemapRenderer>();
            renderer.sortingOrder = 260;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return tilemapObject.GetComponent<Tilemap>();
        }
    }
}
