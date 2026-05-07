using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Rootborn.Game.Managers
{
    /// <summary>
    /// Addressables helper with explicit cache/release ownership.
    /// </summary>
    public sealed class ResourceManager
    {
        private readonly Dictionary<string, UnityEngine.Object> _cache = new Dictionary<string, UnityEngine.Object>();
        private readonly Dictionary<string, AsyncOperationHandle> _handles = new Dictionary<string, AsyncOperationHandle>();
        private readonly Dictionary<string, IList<Sprite>> _sheetSprites = new Dictionary<string, IList<Sprite>>();

        public bool IsInitialized { get; private set; }

        public async Task InitializeAsync()
        {
            if (IsInitialized) return;
            try
            {
                var op = Addressables.InitializeAsync(false);
                await op.Task;
                IsInitialized = true;
                Debug.Log("[ROOTBORN/ResourceManager] Addressables initialized.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ROOTBORN/ResourceManager] Addressables initialization failed: {e}");
            }
        }

        public T Load<T>(string address) where T : UnityEngine.Object
        {
            if (_cache.TryGetValue(address, out var cached)) return cached as T;
            return null;
        }

        public Sprite GetCachedSubSprite(string sheetAddress, string subName)
        {
            if (!_sheetSprites.TryGetValue(sheetAddress, out var sprites)) return null;
            for (int i = 0; i < sprites.Count; i++)
            {
                if (sprites[i] != null && sprites[i].name == subName) return sprites[i];
            }
            return null;
        }

        public async Task<T> LoadAsync<T>(string address) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(address)) return null;
            if (_cache.TryGetValue(address, out var cached)) return cached as T;

            try
            {
                var handle = Addressables.LoadAssetAsync<T>(address);
                await handle.Task;
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    Debug.LogWarning($"[ROOTBORN/ResourceManager] LoadAsync failed for '{address}': status={handle.Status}");
                    Addressables.Release(handle);
                    return null;
                }
                _cache[address] = handle.Result;
                _handles[address] = handle;
                return handle.Result;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ROOTBORN/ResourceManager] LoadAsync exception for '{address}': {e.Message}");
                return null;
            }
        }

        public async Task<IList<T>> LoadByLabelAsync<T>(string label) where T : UnityEngine.Object
        {
            try
            {
                var handle = Addressables.LoadAssetsAsync<T>(label, null);
                await handle.Task;
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    Debug.LogWarning($"[ROOTBORN/ResourceManager] LoadByLabelAsync failed for label '{label}'.");
                    return Array.Empty<T>();
                }
                _handles[$"label:{label}"] = handle;
                return handle.Result;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ROOTBORN/ResourceManager] LoadByLabelAsync exception for label '{label}': {e.Message}");
                return Array.Empty<T>();
            }
        }

        public Task<Sprite> LoadSubSpriteAsync(string sheetAddress, string subName)
        {
            if (string.IsNullOrEmpty(sheetAddress)) return Task.FromResult<Sprite>(null);

            Sprite cached = GetCachedSubSprite(sheetAddress, subName);
            if (cached != null) return Task.FromResult(cached);

            IList<Sprite> sprites;
            if (!_sheetSprites.TryGetValue(sheetAddress, out sprites))
            {
                sprites = LoadSpriteSheet(sheetAddress);
                if (sprites == null || sprites.Count == 0)
                {
                    return Task.FromResult<Sprite>(null);
                }
            }

            if (string.IsNullOrEmpty(subName))
            {
                return Task.FromResult(sprites.Count > 0 ? sprites[0] : null);
            }

            for (int i = 0; i < sprites.Count; i++)
            {
                if (sprites[i] != null && sprites[i].name == subName) return Task.FromResult(sprites[i]);
            }

            return Task.FromResult(LoadAddressableSubSprite(sheetAddress, subName));
        }

        public void Release(string address)
        {
            if (_handles.TryGetValue(address, out var handle))
            {
                Addressables.Release(handle);
                _handles.Remove(address);
            }
            _cache.Remove(address);
        }

        public void ReleaseAll()
        {
            foreach (var kv in _handles)
            {
                Addressables.Release(kv.Value);
            }
            _handles.Clear();
            _cache.Clear();
            _sheetSprites.Clear();
        }

        private IList<Sprite> LoadSpriteSheet(string sheetAddress)
        {
            try
            {
                var handle = Addressables.LoadAssetAsync<IList<Sprite>>(sheetAddress);
                IList<Sprite> result = handle.WaitForCompletion();
                if (handle.Status != AsyncOperationStatus.Succeeded || result == null)
                {
                    Debug.LogWarning($"[ROOTBORN/ResourceManager] LoadSubSpriteAsync sheet '{sheetAddress}' failed: status={handle.Status}");
                    Addressables.Release(handle);
                    return null;
                }

                var sprites = new List<Sprite>(result.Count);
                for (int i = 0; i < result.Count; i++)
                {
                    if (result[i] != null)
                    {
                        sprites.Add(result[i]);
                    }
                }

                _sheetSprites[sheetAddress] = sprites;
                _handles[$"sheet:{sheetAddress}"] = handle;
                return sprites;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ROOTBORN/ResourceManager] LoadSubSpriteAsync exception '{sheetAddress}': {e.Message}");
                return null;
            }
        }

        private Sprite LoadAddressableSubSprite(string sheetAddress, string subName)
        {
            if (string.IsNullOrEmpty(subName)) return null;

            string subAddress = $"{sheetAddress}[{subName}]";
            try
            {
                var handle = Addressables.LoadAssetAsync<Sprite>(subAddress);
                Sprite result = handle.WaitForCompletion();
                if (handle.Status != AsyncOperationStatus.Succeeded || result == null)
                {
                    Addressables.Release(handle);
                    return null;
                }

                CacheSubSprite(sheetAddress, result);
                _handles[$"sub:{subAddress}"] = handle;
                return result;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ROOTBORN/ResourceManager] LoadSubSpriteAsync sub-sprite exception '{subAddress}': {e.Message}");
                return null;
            }
        }

        private void CacheSubSprite(string sheetAddress, Sprite sprite)
        {
            if (sprite == null) return;
            if (!_sheetSprites.TryGetValue(sheetAddress, out var sprites))
            {
                sprites = new List<Sprite>();
                _sheetSprites[sheetAddress] = sprites;
            }

            for (int i = 0; i < sprites.Count; i++)
            {
                if (sprites[i] == sprite || (sprites[i] != null && sprites[i].name == sprite.name))
                {
                    return;
                }
            }

            if (sprites is List<Sprite> list)
            {
                list.Add(sprite);
            }
        }
    }
}
