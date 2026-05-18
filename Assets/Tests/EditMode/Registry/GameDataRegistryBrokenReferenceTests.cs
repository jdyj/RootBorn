using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Registry
{
    public sealed class GameDataRegistryBrokenReferenceTests
    {
        [Test]
        public void GameDataRegistry_DoesNotContainBrokenObjectReferences()
        {
            var registry = AssetDatabase.LoadAssetAtPath<Rootborn.Game.Common.GameDataRegistry>("Assets/Data/Registry/GameDataRegistry.asset");
            Assert.IsNotNull(registry, "GameDataRegistry asset must exist.");

            var broken = FindBrokenReferences(registry);
            Assert.IsEmpty(broken, "GameDataRegistry reference graph contains broken object references: " + string.Join(", ", broken));
        }

        private static List<string> FindBrokenReferences(ScriptableObject root)
        {
            var broken = new List<string>();
            var queue = new Queue<ScriptableObject>();
            var visited = new HashSet<int>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == null || !visited.Add(current.GetInstanceID()))
                {
                    continue;
                }

                var serialized = new SerializedObject(current);
                var property = serialized.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = true;
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    if (property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                    {
                        broken.Add(AssetDatabase.GetAssetPath(current) + ":" + property.propertyPath);
                        continue;
                    }

                    if (property.objectReferenceValue is ScriptableObject child && !visited.Contains(child.GetInstanceID()))
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            return broken;
        }
    }
}
