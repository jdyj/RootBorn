using System.IO;
using NUnit.Framework;

namespace Rootborn.Tests.EditMode
{
    public sealed class DataManagerEditorFastPathSourceTests
    {
        private const string DataManagerPath = "Assets/Scripts/Game/Managers/DataManager.cs";

        [Test]
        public void InitAsync_LoadsRegistryFromAssetDatabaseBeforeAddressablesInEditor()
        {
            string source = File.ReadAllText(DataManagerPath);
            int editorLoad = source.IndexOf("LoadEditorRegistry", System.StringComparison.Ordinal);
            int addressableLoad = source.IndexOf("resource.LoadAsync<GameDataRegistry>(AddrRegistry)", System.StringComparison.Ordinal);

            Assert.GreaterOrEqual(editorLoad, 0, "Editor Play should have an AssetDatabase registry fast path.");
            Assert.GreaterOrEqual(addressableLoad, 0, "Runtime Addressables registry loading must remain available.");
            Assert.Less(editorLoad, addressableLoad, "Editor registry fast path should run before Addressables.");
        }
    }
}
