using System.Threading.Tasks;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Family;
using Rootborn.Game.Managers;
using UnityEditor;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Family
{
    public sealed class CharacterPartResourceManagerTests
    {
        [Test]
        public async Task LoadSubSpriteAsync_ResolvesRegisteredCharacterPartSubSpriteByAddress()
        {
            var part = AssetDatabase.LoadAssetAtPath<CharacterPartDefinition>("Assets/Data/Family/CharacterParts/character_body_02.asset");
            Assert.IsNotNull(part);

            var resource = new ResourceManager();
            try
            {
                await resource.InitializeAsync();

                Sprite sprite = await AwaitWithTimeout(
                    resource.LoadSubSpriteAsync(part.SheetAddress, part.SubSpriteName),
                    "ResourceManager did not resolve the registered character part sub-sprite before timeout.");

                Assert.IsNotNull(sprite, part.SheetAddress + " -> " + part.SubSpriteName);
                Assert.AreEqual(part.SubSpriteName, sprite.name);
                Assert.AreEqual(16f, sprite.rect.width);
                Assert.AreEqual(16f, sprite.rect.height);
                Assert.AreSame(sprite, resource.GetCachedSubSprite(part.SheetAddress, part.SubSpriteName));
            }
            finally
            {
                resource.ReleaseAll();
            }
        }

        [Test]
        public async Task LoadSubSpriteAsync_ResolvesModernUiStyle2CommonPanelSubSpriteByAddress()
        {
            var resource = new ResourceManager();
            try
            {
                await resource.InitializeAsync();

                Sprite sprite = await AwaitWithTimeout(
                    resource.LoadSubSpriteAsync(ModernUiStyle2Sprites.SheetAddress, ModernUiStyle2Sprites.CommonPanel.FillName),
                    "ResourceManager did not resolve the Style2 common panel fill sub-sprite before timeout.");

                Assert.IsNotNull(sprite, ModernUiStyle2Sprites.SheetAddress + " -> " + ModernUiStyle2Sprites.CommonPanel.FillName);
                Assert.AreEqual(ModernUiStyle2Sprites.CommonPanel.FillName, sprite.name);
                Assert.AreEqual(16f, sprite.rect.width);
                Assert.AreEqual(16f, sprite.rect.height);
                Assert.AreSame(sprite, resource.GetCachedSubSprite(ModernUiStyle2Sprites.SheetAddress, ModernUiStyle2Sprites.CommonPanel.FillName));
            }
            finally
            {
                resource.ReleaseAll();
            }
        }

        private static async Task<T> AwaitWithTimeout<T>(Task<T> task, string message)
        {
            var timeout = Task.Delay(5000);
            Task completed = await Task.WhenAny(task, timeout);
            if (completed == timeout)
            {
                Assert.Fail(message);
            }

            return await task;
        }
    }
}
