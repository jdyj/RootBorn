using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using Rootborn.Game.Placement;

namespace Rootborn.Editor.Tools
{
    public static class SchoolPixelAddressablesSetup
    {
        public const string Label = "SchoolPixels";
        private const string ArtRoot = "Assets/Art/SchoolPixels";
        private const string DataRoot = "Assets/Data/Interiors/Furniture/SchoolPixels";

        public static IReadOnlyList<(string assetPath, string address, string group)> GetEntries()
        {
            var entries = new List<(string, string, string)>();
            foreach (string root in new[] { ArtRoot, DataRoot })
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (string path in AssetDatabase.FindAssets("", new[] { root }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal))
                {
                    string extension = Path.GetExtension(path);
                    if (extension == ".png")
                    {
                        string relative = path.Substring(ArtRoot.Length + 1);
                        relative = relative.Substring(0, relative.Length - extension.Length);
                        entries.Add((path, "sprites/school/" + Normalize(relative), "Sprites"));
                    }
                    else if (extension == ".asset")
                    {
                        var definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(path);
                        if (definition != null)
                            entries.Add((path, "data/furniture/" + definition.StableId.Replace('.', '-'), "Data"));
                        else if (AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>(path) != null)
                            entries.Add((path, "tiles/school/" + Normalize(Path.GetFileNameWithoutExtension(path)), "Tiles"));
                    }
                }
            }
            return entries;
        }

        [MenuItem("Rootborn/School Pixels/Wire Addressables")]
        public static void WireAssets()
        {
            var settings = AddressablesSetup.EnsureSettings();
            settings.AddLabel(Label);
            foreach (var item in GetEntries())
            {
                var group = settings.FindGroup(item.group);
                if (group == null) throw new InvalidOperationException("Run Addressables/Wire All to create core group: " + item.group);
                string guid = AssetDatabase.AssetPathToGUID(item.assetPath);
                var conflicting = settings.groups.Where(g => g != null).SelectMany(g => g.entries)
                    .FirstOrDefault(e => e.address == item.address && e.guid != guid);
                if (conflicting != null) throw new InvalidOperationException("Address already belongs to another asset: " + item.address);
                var entry = settings.CreateOrMoveEntry(guid, group);
                entry.SetAddress(item.address);
                entry.SetLabel(Label, true);
            }
            AssetDatabase.SaveAssets();
        }

        private static string Normalize(string value) => value.ToLowerInvariant().Replace('_', '-').Replace(' ', '-');
    }
}
