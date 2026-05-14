using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Family;
using Rootborn.Game.Tools;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Characters.Spum
{
    public sealed class SpumToolVisualMappingTests
    {
        private const BindingFlags Bind = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void SPUM_TOOL_VISUAL_001_RegistryFindsMappingByToolDefinitionReference()
        {
            var axe = ScriptableObject.CreateInstance<ToolDefinition>();
            var pickaxe = ScriptableObject.CreateInstance<ToolDefinition>();
            var axeMapping = CreateMapping(axe);
            var pickaxeMapping = CreateMapping(pickaxe);
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            ConfigureRegistryMappings(registry, axeMapping, pickaxeMapping);

            Assert.AreSame(axeMapping, registry.FindToolVisualMapping(axe));
            Assert.AreSame(pickaxeMapping, registry.FindToolVisualMapping(pickaxe));
        }

        [Test]
        public void SPUM_TOOL_VISUAL_002_MissingMappingReturnsNull()
        {
            var registeredTool = ScriptableObject.CreateInstance<ToolDefinition>();
            var missingTool = ScriptableObject.CreateInstance<ToolDefinition>();
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            ConfigureRegistryMappings(registry, CreateMapping(registeredTool));

            Assert.IsNull(registry.FindToolVisualMapping(missingTool));
            Assert.IsNull(registry.FindToolVisualMapping(null));
        }

        [Test]
        public void SPUM_TOOL_VISUAL_003_StoneAxeAndPickaxeAssetsCanBeAssignedMappingsByReference()
        {
            var axe = AssetDatabase.LoadAssetAtPath<ToolDefinition>("Assets/Data/Tools/Tool_StoneAxe.asset");
            var pickaxe = AssetDatabase.LoadAssetAtPath<ToolDefinition>("Assets/Data/Tools/Tool_StonePickaxe.asset");
            Assert.IsNotNull(axe);
            Assert.IsNotNull(pickaxe);
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var axeMapping = CreateMapping(axe);
            var pickaxeMapping = CreateMapping(pickaxe);
            ConfigureRegistryMappings(registry, axeMapping, pickaxeMapping);

            Assert.AreSame(axeMapping, registry.FindToolVisualMapping(axe));
            Assert.AreSame(pickaxeMapping, registry.FindToolVisualMapping(pickaxe));
        }

        [Test]
        public void SPUM_TOOL_VISUAL_004_RegistryLookupDoesNotSwitchOnToolId()
        {
            string source = File.ReadAllText("Assets/Scripts/Game/Common/GameDataRegistry.cs");

            StringAssert.DoesNotContain("switch", source);
            StringAssert.DoesNotContain("tool.Id ==", source);
            StringAssert.DoesNotContain("tool.Id.Equals", source);
        }

        private static ToolVisualMappingDefinition CreateMapping(ToolDefinition tool)
        {
            var clip = ScriptableObject.CreateInstance<CharacterPartAnimationClipDefinition>();
            var mapping = ScriptableObject.CreateInstance<ToolVisualMappingDefinition>();
            mapping.ConfigureForTests(tool, clip);
            return mapping;
        }

        private static void ConfigureRegistryMappings(GameDataRegistry registry, params ToolVisualMappingDefinition[] mappings)
        {
            typeof(GameDataRegistry).GetField("_toolVisualMappings", Bind).SetValue(registry, mappings);
        }
    }
}
