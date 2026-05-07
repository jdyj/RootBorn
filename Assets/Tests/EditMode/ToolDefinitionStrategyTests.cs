using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Family;
using Rootborn.Game.Tools;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    public sealed class ToolDefinitionStrategyTests
    {
        private const BindingFlags Bind = BindingFlags.Instance | BindingFlags.NonPublic;

        private static ToolDefinition MakeTool(params ToolEffectBase[] effects)
        {
            var tool = ScriptableObject.CreateInstance<ToolDefinition>();
            typeof(ToolDefinition).GetField("_effects", Bind).SetValue(tool, effects);
            return tool;
        }

        [Test]
        public void TOOL_001_ApplyEffects_InvokesStrategyArrayInSerializedOrder()
        {
            var calls = new List<string>();
            var first = RecordingEffect.Create("first", calls);
            var second = RecordingEffect.Create("second", calls);
            var third = RecordingEffect.Create("third", calls);
            var tool = MakeTool(first, second, third);
            var ctx = new ToolUseContext(tool, null, "ground");

            tool.ApplyEffects(in ctx);

            CollectionAssert.AreEqual(new[] { "first", "second", "third" }, calls);
        }

        [Test]
        public void TOOL_002_ApplyEffects_SkipsNullEntriesAndContinues()
        {
            var calls = new List<string>();
            var first = RecordingEffect.Create("first", calls);
            var second = RecordingEffect.Create("second", calls);
            var tool = MakeTool(first, null, second);
            var ctx = new ToolUseContext(tool, null, "ground");

            tool.ApplyEffects(in ctx);

            CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
        }

        [Test]
        public void TOOL_003_ApplyEffects_PassesOriginalContextToEveryStrategy()
        {
            var target = new GameObject("Target");
            try
            {
                var calls = new List<string>();
                var first = RecordingEffect.Create("first", calls);
                var second = RecordingEffect.Create("second", calls);
                var tool = MakeTool(first, second);
                var ctx = new ToolUseContext(tool, target, "forest");

                tool.ApplyEffects(in ctx);

                Assert.AreSame(tool, first.LastTool);
                Assert.AreSame(tool, second.LastTool);
                Assert.AreSame(target, first.LastTarget);
                Assert.AreEqual("forest", first.LastSurface);
                Assert.AreEqual("forest", second.LastSurface);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void TOOL_004_CanReferenceDataDrivenCharacterPartAnimationClip()
        {
            var tool = MakeTool();
            var clip = ScriptableObject.CreateInstance<CharacterPartAnimationClipDefinition>();
            typeof(ToolDefinition).GetField("_characterPartAnimationClip", Bind).SetValue(tool, clip);

            Assert.AreSame(clip, tool.CharacterPartAnimationClip);
        }

        private sealed class RecordingEffect : ToolEffectBase
        {
            private string _label;
            private List<string> _calls;

            public ToolDefinition LastTool { get; private set; }
            public GameObject LastTarget { get; private set; }
            public string LastSurface { get; private set; }

            public static RecordingEffect Create(string label, List<string> calls)
            {
                var effect = ScriptableObject.CreateInstance<RecordingEffect>();
                effect._label = label;
                effect._calls = calls;
                return effect;
            }

            public override void Apply(in ToolUseContext ctx)
            {
                _calls.Add(_label);
                LastTool = ctx.Tool;
                LastTarget = ctx.Target;
                LastSurface = ctx.Surface;
            }
        }
    }
}
