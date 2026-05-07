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
            return resourceManager != null
                ? resourceManager.GetCachedSubSprite(key.SheetAddress, key.SubSpriteName)
                : null;
        }
    }
}
