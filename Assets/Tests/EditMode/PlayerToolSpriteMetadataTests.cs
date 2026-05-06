using System.Reflection;
using NUnit.Framework;
using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Tests.EditMode
{
    public sealed class PlayerToolSpriteMetadataTests
    {
        [Test]
        public void ItemDefinition_CanCarryToolSpritePrefixMetadata()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            var field = typeof(ItemDefinition).GetField("_toolSpritePrefix", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(item, "Axe");

            Assert.AreEqual("Axe", item.ToolSpritePrefix);
        }
    }
}
