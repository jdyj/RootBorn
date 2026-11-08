using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    /// <summary>
    /// When ROOTBORN_GAMEVIEW_SIZE is set (e.g. "1920x1080"), every Game View renders at that fixed resolution, scaled to fit
    /// its window. FEVM test editors run in a small window, so without this some UI sits outside the screen and a mouse
    /// device cannot reach it. Unset (developer editors) changes nothing.
    /// </summary>
    [InitializeOnLoad]
    public static class TestGameViewSize
    {
        public const string EnvironmentVariable = "ROOTBORN_GAMEVIEW_SIZE";
        private const string Label = "ROOTBORN test";

        static TestGameViewSize()
        {
            EditorApplication.delayCall += ApplyFromEnvironment;
            EditorApplication.playModeStateChanged += _ => ApplyFromEnvironment();
        }

        public static bool TryParse(string value, out int width, out int height)
        {
            width = height = 0;
            if (string.IsNullOrEmpty(value)) return false;
            var parts = value.ToLowerInvariant().Split('x');
            return parts.Length == 2 && int.TryParse(parts[0], out width) && int.TryParse(parts[1], out height) && width > 0 && height > 0;
        }

        private static void ApplyFromEnvironment()
        {
            if (!TryParse(Environment.GetEnvironmentVariable(EnvironmentVariable), out int width, out int height)) return;
            try
            {
                Apply(width, height);
            }
            catch (Exception error)
            {
                Debug.LogWarning("[ROOTBORN] Test Game View size could not be applied: " + error.Message);
            }
        }

        private static void Apply(int width, int height)
        {
            var editorAssembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
            var sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
            var sizeKindType = editorAssembly.GetType("UnityEditor.GameViewSizeType");
            var gameViewType = editorAssembly.GetType("UnityEditor.GameView");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var groupKind = sizesType.GetProperty("currentGroupType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).GetValue(sizes);
            var group = sizesType.GetMethod("GetGroup", BindingFlags.Public | BindingFlags.Instance).Invoke(sizes, new[] { groupKind });
            var groupType = group.GetType();

            int index = FindSize(group, groupType, width, height);
            if (index < 0)
            {
                var fixedKind = Enum.Parse(sizeKindType, "FixedResolution");
                var size = Activator.CreateInstance(sizeType, fixedKind, width, height, Label);
                groupType.GetMethod("AddCustomSize", BindingFlags.Public | BindingFlags.Instance).Invoke(group, new[] { size });
                index = FindSize(group, groupType, width, height);
            }

            var selected = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            foreach (var view in Resources.FindObjectsOfTypeAll(gameViewType))
            {
                selected?.SetValue(view, index);
                ((EditorWindow)view).Repaint();
            }

            Debug.Log("[ROOTBORN] Test Game View size " + width + "x" + height + " (index " + index + ")");
        }

        private static int FindSize(object group, Type groupType, int width, int height)
        {
            int total = (int)groupType.GetMethod("GetTotalCount", BindingFlags.Public | BindingFlags.Instance).Invoke(group, null);
            var getSize = groupType.GetMethod("GetGameViewSize", BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < total; i++)
            {
                var size = getSize.Invoke(group, new object[] { i });
                var type = size.GetType();
                int w = (int)type.GetProperty("width").GetValue(size);
                int h = (int)type.GetProperty("height").GetValue(size);
                string kind = type.GetProperty("sizeType").GetValue(size).ToString();
                if (w == width && h == height && kind == "FixedResolution") return i;
            }

            return -1;
        }
    }
}
