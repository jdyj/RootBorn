using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Rootborn.Game.Localization;
using UnityEditor;
using UnityEngine;
namespace Rootborn.Editor.StudentLife
{
    public static class DayResultLocalizationInstaller
    {
        [Serializable] private sealed class Entry { public string key; public string ko; public string en; }
        [Serializable] private sealed class Document { public Entry[] entries; }
        [Serializable] private sealed class Value { public string value; }
        private const string Folder = "output/full-design-20261027/day-result-polish";
        [MenuItem("Rootborn/Student Life/Install Day Result Localization")]
        public static void Install()
        {
            const string assetPath = "Assets/Localization/GameTextCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<GameTextCatalog>(assetPath);
            if (catalog == null) throw new InvalidOperationException("Existing GameTextCatalog is required.");
            var patch = JsonUtility.FromJson<Document>(File.ReadAllText(Folder + "/localization-patch.json", Encoding.UTF8));
            var entries = new SortedDictionary<string, Entry>(StringComparer.Ordinal);
            var serialized = new SerializedObject(catalog); var property = serialized.FindProperty("_entries");
            for (int i = 0; i < property.arraySize; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                var entry = new Entry { key = element.FindPropertyRelative("key").stringValue, ko = element.FindPropertyRelative("ko").stringValue, en = element.FindPropertyRelative("en").stringValue };
                entries[entry.key] = entry;
            }
            foreach (var entry in patch.entries) entries[entry.key] = entry;
            var merged = new Entry[entries.Count]; entries.Values.CopyTo(merged, 0);
            string json = JsonUtility.ToJson(new Document { entries = merged });
            var validation = ScriptableObject.CreateInstance<GameTextCatalog>();
            try { validation.ConfigureJson(json); } finally { UnityEngine.Object.DestroyImmediate(validation); }
            var languages = new Dictionary<string, string>();
            foreach (string language in new[] { "ko", "en" })
            {
                string path = "Assets/Localization/" + language + ".json";
                var values = ReadLanguage(File.ReadAllText(path, Encoding.UTF8));
                foreach (var entry in patch.entries) values[entry.key] = language == "ko" ? entry.ko : entry.en;
                var output = new StringBuilder("{\n"); bool first = true;
                foreach (var pair in values) { if (!first) output.Append(",\n"); first = false; output.Append("  ").Append(Quote(pair.Key)).Append(": ").Append(Quote(pair.Value)); }
                languages[path] = output.Append("\n}\n").ToString();
            }
            string backup = Folder + "/backups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"); Directory.CreateDirectory(backup);
            foreach (string path in new[] { assetPath, "Assets/Localization/ko.json", "Assets/Localization/en.json" }) File.Copy(path, backup + "/" + Path.GetFileName(path));
            try
            {
                Undo.RecordObject(catalog, "Install day result localization"); catalog.ConfigureJson(json); EditorUtility.SetDirty(catalog);
                foreach (var pair in languages) File.WriteAllText(pair.Key, pair.Value, new UTF8Encoding(false));
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                Debug.Log("Day result localization installed. Backup: " + backup);
            }
            catch
            {
                foreach (string path in new[] { assetPath, "Assets/Localization/ko.json", "Assets/Localization/en.json" }) { File.Copy(backup + "/" + Path.GetFileName(path), path, true); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
                throw;
            }
        }
        private static string Quote(string value) { string wrapper = JsonUtility.ToJson(new Value { value = value }); return wrapper.Substring(wrapper.IndexOf(':') + 1).TrimEnd('}'); }
        private static SortedDictionary<string,string> ReadLanguage(string json)
        {
            var result = new SortedDictionary<string,string>(StringComparer.Ordinal);
            const string token = "\"(?:\\\\.|[^\"\\\\])*\"";
            foreach (Match match in Regex.Matches(json, "(?<key>" + token + ")\\s*:\\s*(?<value>" + token + ")"))
            {
                string key = JsonUtility.FromJson<Value>("{\"value\":" + match.Groups["key"].Value + "}").value;
                string value = JsonUtility.FromJson<Value>("{\"value\":" + match.Groups["value"].Value + "}").value;
                result.Add(key, value);
            }
            if (result.Count == 0) throw new InvalidOperationException("Localization source must be a nonempty flat string map.");
            return result;
        }
    }
}
