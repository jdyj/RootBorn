using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Quests;
using Rootborn.Game.Save;
using Rootborn.Game.StudentLife;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rootborn.Tests.EditMode.StudentLife
{
    public sealed class QuestChainOwnedStoreTests
    {
        [Test]
        public void BoundOwnerLog_IsReturnedDirectly_AndSaveUsesOnlyOwnerCallback()
        {
            var method = typeof(QuestChainLog).Assembly.GetType("Rootborn.Game.Quests.QuestChainProgressStore")?.GetMethod("BindLog");
            Assert.That(method, Is.Not.Null, "Shared aggregate needs an authoritative log binding that bypasses legacy files.");
            string temp = Path.Combine(Path.GetTempPath(), "rootborn-owned-chain", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp); SaveService.SetRootDirectoryForTests(temp);
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            try
            {
                var store = Activator.CreateInstance(method.DeclaringType);
                var student = new StudentLifeProgress("owned-slot", "owned-player", 20, 20);
                var log = new QuestChainLog(Array.Empty<ScriptableObject>());
                int saves = 0;
                Action<StudentLifeProgress> save = p => { Assert.That(p, Is.SameAs(student)); saves++; };
                method.Invoke(store, new object[] { student, log, save });
                var actual = method.DeclaringType.GetMethod("GetOrCreate").Invoke(store, new object[] { registry, student });
                Assert.That(actual, Is.SameAs(log));
                method.DeclaringType.GetMethod("Save").Invoke(store, new object[] { student });
                Assert.That(saves, Is.EqualTo(1));
                Assert.That(Directory.GetFiles(temp, "quest-chain-progress*.json", SearchOption.AllDirectories), Is.Empty);
            }
            finally { Object.DestroyImmediate(registry); SaveService.SetRootDirectoryForTests(null); if (Directory.Exists(temp)) Directory.Delete(temp, true); }
        }

        [Test]
        public void BoundOwnerLog_DoesNotAcceptLegacyRecordCallsOutsideAnAggregateCommand()
        {
            var type = typeof(QuestChainLog).Assembly.GetType("Rootborn.Game.Quests.QuestChainProgressStore");
            var bind = type?.GetMethod("BindLog");
            Assert.That(bind, Is.Not.Null);
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            var chain = ScriptableObject.CreateInstance<QuestChainDefinition>();
            var step = ScriptableObject.CreateInstance<QuestStepDefinition>();
            try
            {
                step.ConfigureForTests("step", "step", "step", null, null, null, null, null);
                chain.ConfigureForTests("chain", "chain", "chain", null, new ScriptableObject[] { step }, null, null, null, null, 0);
                typeof(GameDataRegistry).GetField("_questChains", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(registry, new[] { chain });
                var student = new StudentLifeProgress("owned-slot", "owned-player", 20, 20);
                var log = new QuestChainLog(new[] { chain }); log.Accept(chain);
                var store = Activator.CreateInstance(type);
                bind.Invoke(store, new object[] { student, log, null });
                type.GetMethod("Record").Invoke(store, new object[] { registry, student, new QuestEvent(QuestEventKind.Talk, "unowned-write"), default(RewardRuntimeContext) });
                Assert.That(log.GetState(chain), Is.EqualTo(QuestChainState.Active));
                Assert.That(log.ToSaveData().Chains[0].ProcessedEventKeys, Is.Empty);
            }
            finally { Object.DestroyImmediate(registry); Object.DestroyImmediate(chain); Object.DestroyImmediate(step); }
        }
    }
}
