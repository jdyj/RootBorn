using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using UnityEngine;

namespace Rootborn.UI.Modern
{
    public interface IModernUiSpriteResolver
    {
        Sprite Resolve(ModernUiSpriteKey key);
    }

    public sealed class ModernUiSpriteResolver : IModernUiSpriteResolver
    {
        public Sprite Resolve(ModernUiSpriteKey key)
        {
            var resourceManager = Managers.Resource;
            if (resourceManager == null)
            {
                return null;
            }

            var sprite = resourceManager.GetCachedSubSprite(key.SheetAddress, key.SubSpriteName);
            if (sprite != null)
            {
                return sprite;
            }

            var load = resourceManager.LoadSubSpriteAsync(key.SheetAddress, key.SubSpriteName);
            return load.IsCompleted && !load.IsFaulted && !load.IsCanceled ? load.Result : null;
        }
    }
}
