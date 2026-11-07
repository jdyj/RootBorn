using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Rootborn.Game.Interiors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Rootborn.Editor.Interiors
{
    // One-time editor migration. This does not call Apply or rebuild the house.
    public static class HousePersistentTileRepair
    {
        private const string Staging = "output/student-life-integration-20261027/house-repair";

        [Serializable] private sealed class Plan
        {
            public string scenePath, sceneSha256, tileSetPath, tileSetSha256, assetRoot;
            public TileSpec[] tiles;
            public LayerGroup[] layers;
            public FieldBinding[] runtimeFields, styleFields;
        }
        [Serializable] private sealed class TileSpec { public string key, spritePath; public bool gridCollider, fitWidth; }
        [Serializable] private sealed class Cell { public int x, y, z; public Vector3Int Position => new Vector3Int(x, y, z); }
        [Serializable] private sealed class LayerGroup { public string layerName, tileKey, sourceTileFileId; public Cell[] cells; }
        [Serializable] private sealed class FieldBinding { public string field, tileKey; }

        [MenuItem("Rootborn/Interiors/Repair Saved House Missing Tile Art")]
        public static void RepairFromStagedPlan()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop PlayMode before repairing the saved House.");
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(Staging + "/repair-plan.json"));
            RequireHash(plan.scenePath, plan.sceneSha256);
            RequireHash(plan.tileSetPath, plan.tileSetSha256);
            var scene = SceneManager.GetSceneByPath(plan.scenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (!openedHere && scene.isDirty)
                throw new InvalidOperationException("House contains unsaved edits. Save them through Unity and refresh the staged audit/plan before applying this targeted repair.");

            string backup = Staging + "/backups/" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", CultureInfo.InvariantCulture);
            Directory.CreateDirectory(backup);
            File.Copy(plan.scenePath, backup + "/House.before.unity", false);
            File.Copy(plan.tileSetPath, backup + "/TileSet.before.asset", false);
            File.Copy(plan.tileSetPath + ".meta", backup + "/TileSet.before.asset.meta", false);
            if (openedHere) scene = EditorSceneManager.OpenScene(plan.scenePath, OpenSceneMode.Additive);
            try
            {
                var applier = Find<InteriorTilemapApplier>(scene, "[HouseInteriorGenerator]");
                if (applier == null) throw new InvalidOperationException("Saved House applier is missing.");
                var applierData = new SerializedObject(applier);
                var tileSet = applierData.FindProperty("_tileSet").objectReferenceValue as InteriorTileSetDefinition;
                if (tileSet == null || AssetDatabase.GetAssetPath(tileSet) != plan.tileSetPath)
                    throw new InvalidOperationException("The House runtime tileset differs from the reviewed plan.");
                if (EditorUtility.IsDirty(tileSet))
                    throw new InvalidOperationException("The runtime tileset has unsaved edits. Save those through Unity and refresh the staged plan before repair.");

                // Preflight all references and all source sprites before creating or changing assets.
                foreach (var spec in plan.tiles) RequireSprite(spec.spritePath);
                foreach (var group in plan.layers)
                {
                    var layer = Find<Tilemap>(scene, group.layerName);
                    if (layer == null) throw new InvalidOperationException("Missing layer: " + group.layerName);
                    TileBase expected = null;
                    foreach (var cell in group.cells)
                    {
                        var tile = layer.GetTile(cell.Position);
                        if (tile == null) throw new InvalidOperationException("An expected occupied cell is empty: " + group.layerName + " " + cell.Position);
                        if (expected == null) expected = tile;
                        if (tile != expected) throw new InvalidOperationException("The source palette has changed inside " + group.layerName);
                        if (HasPersistentArt(tile)) throw new InvalidOperationException("Plan would replace healthy art at " + group.layerName + " " + cell.Position);
                    }
                }

                string beforeLayout = LayoutFingerprint(scene);
                File.WriteAllText(backup + "/layout.before.txt", beforeLayout);
                var replacements = new Dictionary<string, Tile>(StringComparer.Ordinal);
                EnsureFolder(plan.assetRoot);
                foreach (var spec in plan.tiles) replacements.Add(spec.key, EnsureTile(plan.assetRoot, spec));

                int changedCells = 0;
                EditorSceneManager.MarkSceneDirty(scene);
                foreach (var group in plan.layers)
                {
                    var layer = Find<Tilemap>(scene, group.layerName);
                    Undo.RecordObject(layer, "Repair missing House tile art");
                    foreach (var cell in group.cells)
                    {
                        var position = cell.Position;
                        // Preserve per-cell tint/rotation/scale relative to the old Tile defaults.
                        var oldTile = layer.GetTile(position) as Tile;
                        var oldMatrix = layer.GetTransformMatrix(position);
                        var oldColor = layer.GetColor(position);
                        var replacement = replacements[group.tileKey];
                        var relativeMatrix = oldTile != null ? oldMatrix * oldTile.transform.inverse : oldMatrix;
                        var relativeColor = oldTile != null ? RelativeColor(oldColor, oldTile.color) : oldColor;
                        var oldFlags = layer.GetTileFlags(position);
                        layer.SetTile(position, replacement);
                        layer.SetTileFlags(position, TileFlags.None);
                        layer.SetTransformMatrix(position, relativeMatrix * replacement.transform);
                        layer.SetColor(position, relativeColor * replacement.color);
                        layer.SetTileFlags(position, oldFlags);
                        changedCells++;
                    }
                    layer.RefreshAllTiles();
                    EditorUtility.SetDirty(layer);
                }

                Undo.RecordObject(tileSet, "Repair House runtime tileset art");
                var data = new SerializedObject(tileSet);
                int fields = 0;
                foreach (var binding in plan.runtimeFields)
                    fields += RepairField(data.FindProperty(binding.field), replacements[binding.tileKey]);
                var styles = data.FindProperty("_styleOptions");
                for (int i = 0; i < styles.arraySize; i++)
                    foreach (var binding in plan.styleFields)
                        fields += RepairField(styles.GetArrayElementAtIndex(i).FindPropertyRelative(binding.field), replacements[binding.tileKey]);
                data.ApplyModifiedProperties();

                string afterLayout = LayoutFingerprint(scene);
                File.WriteAllText(backup + "/layout.after.txt", afterLayout);
                if (!string.Equals(beforeLayout, afterLayout, StringComparison.Ordinal))
                    throw new InvalidOperationException("Layout changed unexpectedly. Do not save; inspect the in-memory Undo group and backup.");
                AssetDatabase.SaveAssetIfDirty(tileSet);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, plan.scenePath))
                    throw new IOException("Unity did not save the repaired House scene. Backup: " + backup);
                File.Copy(plan.scenePath, backup + "/House.after.unity", false);
                File.Copy(plan.tileSetPath, backup + "/TileSet.after.asset", false);
                File.WriteAllText(backup + "/result.txt", "Changed cells=" + changedCells + "; runtime fields=" + fields
                    + "\nLayout fingerprint unchanged.\nScene SHA256=" + Hash(plan.scenePath));
                Debug.Log("[ROOTBORN/House repair] Saved " + changedCells + " cell references and " + fields
                    + " runtime/style references. Backups and before/after evidence: " + backup
                    + ". Run HousePersistentTileArtTests, then actual player input/save/reload verification.");
            }
            finally
            {
                // Never close a dirty scene after a failed repair: preserve it for inspection/Undo.
                if (openedHere && scene.IsValid() && scene.isLoaded && !scene.isDirty)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static int RepairField(SerializedProperty property, Tile replacement)
        {
            if (property == null) throw new InvalidOperationException("Reviewed serialized field is absent.");
            if (HasPersistentArt(property.objectReferenceValue as TileBase)) return 0;
            property.objectReferenceValue = replacement;
            return 1;
        }

        private static Tile EnsureTile(string root, TileSpec spec)
        {
            string path = root + "/HouseRepair_" + spec.key + ".asset";
            var sprite = RequireSprite(spec.spritePath);
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (existing != null)
            {
                if (!HasPersistentArt(existing) || existing.sprite != sprite)
                    throw new InvalidOperationException("Unexpected existing repair tile; preserve and inspect: " + path);
                return existing;
            }
            if (File.Exists(path)) throw new InvalidOperationException("Refusing to overwrite: " + path);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = "HouseRepair_" + spec.key;
            tile.sprite = sprite;
            tile.color = Color.white;
            // The invisible collision layer must keep a 1x1 Grid collider, independent of art PPU.
            float uniformScale = spec.fitWidth ? sprite.pixelsPerUnit / sprite.rect.width : 1f;
            tile.transform = Matrix4x4.Scale(new Vector3(uniformScale, uniformScale, 1f));
            tile.flags = TileFlags.LockColor | TileFlags.LockTransform;
            tile.colliderType = spec.gridCollider ? Tile.ColliderType.Grid : Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, path);
            AssetDatabase.SaveAssetIfDirty(tile);
            return tile;
        }

        private static Sprite RequireSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null || sprite.texture == null || !AssetDatabase.Contains(sprite) || !AssetDatabase.Contains(sprite.texture))
                throw new InvalidOperationException("Persistent sprite/texture is required: " + path);
            if (sprite.rect.width <= 0 || sprite.pixelsPerUnit <= 0) throw new InvalidOperationException("Invalid sprite dimensions: " + path);
            return sprite;
        }

        private static bool HasPersistentArt(TileBase value)
        {
            var tile = value as Tile;
            return tile != null && AssetDatabase.Contains(tile) && AssetDatabase.GetAssetPath(tile).EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
                && tile.sprite != null && tile.sprite.texture != null
                && AssetDatabase.Contains(tile.sprite) && AssetDatabase.Contains(tile.sprite.texture);
        }

        private static Color RelativeColor(Color current, Color source)
        {
            return new Color(source.r != 0 ? current.r / source.r : current.r, source.g != 0 ? current.g / source.g : current.g,
                source.b != 0 ? current.b / source.b : current.b, source.a != 0 ? current.a / source.a : current.a);
        }

        private static T Find<T>(Scene scene, string name) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<T>(true))
                    if (component.gameObject.name == name) return component;
            return null;
        }

        private static string LayoutFingerprint(Scene scene)
        {
            var lines = new List<string>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    string path = t.name;
                    for (var parent = t.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
                    lines.Add(path + "|" + t.localPosition.ToString("R") + "|" + t.localRotation.ToString("R") + "|" + t.localScale.ToString("R")
                        + "|" + t.gameObject.activeSelf + "|" + t.gameObject.layer + "|" + t.gameObject.tag);
                    foreach (var component in t.GetComponents<Component>())
                        lines.Add(path + "|component|" + (component != null ? component.GetType().FullName : "MISSING"));
                    var map = t.GetComponent<Tilemap>();
                    if (map == null) continue;
                    foreach (var position in map.cellBounds.allPositionsWithin)
                        if (map.HasTile(position)) lines.Add(path + "|cell|" + position);
                }
            lines.Sort(StringComparer.Ordinal);
            return string.Join("\n", lines);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        private static void RequireHash(string path, string expected)
        {
            if (!string.Equals(Hash(path), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The source changed since review: " + path + ". Refresh and inspect the staged audit/plan.");
        }
    }
}
