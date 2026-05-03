# Full Sample

```csharp
#nullable enable
using System;
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.Unity.MCP.Editor.Utils;
using com.IvanMurzak.Unity.MCP.Runtime.Data;
using UnityEditor;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    [McpPluginToolType]
    public partial class Tool_Sample
    {
        [McpPluginTool("sample-get", Title = "Sample / Get")]
        [Description("Finds a GameObject and returns its ref data.")]
        public GameObjectRef Get
        (
            [Description("Name of the GameObject to find.")]
            string name
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var go = GameObject.Find(name)
                    ?? throw new ArgumentException($"GameObject '{name}' not found.", nameof(name));

                return new GameObjectRef(go);
            });
        }

        [McpPluginTool("sample-rename", Title = "Sample / Rename")]
        [Description("Renames a GameObject.")]
        public void Rename
        (
            [Description("Current name of the GameObject.")]
            string name,
            [Description("New name to assign.")]
            string newName
        )
        {
            MainThread.Instance.Run(() =>
            {
                var go = GameObject.Find(name)
                    ?? throw new ArgumentException($"GameObject '{name}' not found.", nameof(name));

                go.name = newName;
                EditorUtility.SetDirty(go);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorUtils.RepaintAllEditorWindows();
            });
        }
    }
}
```
