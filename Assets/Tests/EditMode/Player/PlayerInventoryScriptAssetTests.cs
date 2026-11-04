using System.IO;
        using NUnit.Framework;
        
        namespace Rootborn.Tests.EditMode.Player
        {
            public sealed class PlayerInventoryScriptAssetTests
            {
                [Test]
                public void PLAYER_SCRIPT_EDIT_001_PlayerInventoryMonoBehaviourHasMatchingScriptAssetFile()
                {
                    string inventoryPath = "Assets/Scripts/Game/Player/PlayerInventory.cs";
                    string gatherPath = "Assets/Scripts/Game/Player/GatherInteractor.cs";
        
                    Assert.IsTrue(File.Exists(inventoryPath), "PlayerInventory MonoBehaviour must live in PlayerInventory.cs so Unity serializes scene references as a stable script asset GUID."
        );
        
                    string inventorySource = File.ReadAllText(inventoryPath);
                    StringAssert.Contains("public sealed class PlayerInventory : MonoBehaviour", inventorySource);
        
                    string gatherSource = File.ReadAllText(gatherPath);
                    StringAssert.DoesNotContain("public sealed class PlayerInventory : MonoBehaviour", gatherSource, "GatherInteractor.cs must not also define PlayerInventory; duplicate/mismatched MonoBehaviour script assets create runtime scene script references."
        );
                }
            }
        }
        