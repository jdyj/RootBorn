using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;

namespace Rootborn.Game.Managers
{
    /// <summary>
    /// Addressables 래퍼. 캐시 + Release 명시 + async/await.
    /// SlimeMaster의 ResourceManager를 차용하되 다음을 보강:
    ///  - async/await (콜백 지옥 회피)
    ///  - Release 명시 (메모리 누수 차단)
    ///  - 에러 핸들링 (실패 시 default(T) 반환 + 경고)
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

        /// <summary>
        /// 사전 로드된 sheet 에서 sub-sprite 동기 조회. Preload 안 됐으면 null.
        /// FarmHudController 같은 UI 가 Awake/Start 에서 즉시 sprite 결정해야 할 때 사용.
        /// </summary>
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

        /// <summary>
        /// Sheet (Pixelwood multi-sprite PNG)에서 sub-sprite를 이름으로 찾는다.
        /// 첫 호출 시 LoadAssetsAsync로 sheet의 sub-sprite 전체를 로드하고 cache.
        /// </summary>
        public async Task<Sprite> LoadSubSpriteAsync(string sheetAddress, string subName)
        {
            IList<Sprite> sprites;
            if (!_sheetSprites.TryGetValue(sheetAddress, out sprites))
            {
                try
                {
                    var handle = Addressables.LoadAssetAsync<IList<Sprite>>(sheetAddress);
                    await handle.Task;
                    if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                    {
                        Debug.LogWarning($"[ROOTBORN/ResourceManager] LoadSubSpriteAsync sheet '{sheetAddress}' failed.");
                        return null;
                    }
                    sprites = handle.Result;
                    _sheetSprites[sheetAddress] = sprites;
                    _handles[$"sheet:{sheetAddress}"] = handle;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ROOTBORN/ResourceManager] LoadSubSpriteAsync exception '{sheetAddress}': {e.Message}");
                    return null;
                }
            }
            for (int i = 0; i < sprites.Count; i++)
            {
                if (sprites[i] != null && sprites[i].name == subName) return sprites[i];
            }
            return null;
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
    }
}
