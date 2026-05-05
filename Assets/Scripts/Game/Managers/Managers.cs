using System.Threading.Tasks;
using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Game.Managers
{
    /// <summary>
    /// SlimeMaster 의 Managers.cs 패턴: 싱글톤 진입점 + 하위 매니저.
    /// `@Managers` GameObject 로 DontDestroyOnLoad. 부팅 시 한 번 Init() 호출.
    /// </summary>
    public sealed class Managers : MonoBehaviour
    {
        private static Managers s_instance;
        public static Managers Instance => s_instance;

        private readonly ResourceManager _resource = new ResourceManager();
        private readonly DataManager _data = new DataManager();

        public static ResourceManager Resource => Instance != null ? Instance._resource : null;
        public static DataManager Data => Instance != null ? Instance._data : null;

        public bool IsBootstrapped { get; private set; }

        public static Managers EnsureExists()
        {
            if (s_instance != null) return s_instance;

            var existing = GameObject.Find("@Managers");
            if (existing == null)
            {
                existing = new GameObject("@Managers");
            }
            s_instance = existing.GetComponent<Managers>();
            if (s_instance == null)
            {
                s_instance = existing.AddComponent<Managers>();
            }
            DontDestroyOnLoad(existing);
            return s_instance;
        }

        public static async Task BootstrapAsync()
        {
            var managers = EnsureExists();
            if (managers.IsBootstrapped) return;

            await managers._resource.InitializeAsync();
            await managers._data.InitAsync(managers._resource);
            await PreloadUiSpritesAsync(managers._resource);
            await PreloadPlayerToolSpritesAsync(managers._resource);
            managers.IsBootstrapped = true;
            Debug.Log("[ROOTBORN/Managers] Bootstrap complete.");
        }

        // 도구 장착 캐릭터 sheet 12개 사전 로드 — PlayerController 가 동기 GetCachedSubSprite 로 frame 조회.
        private static async Task PreloadPlayerToolSpritesAsync(ResourceManager rm)
        {
            int ok = 0, miss = 0;
            foreach (var sheetAddr in PlayerToolSpriteAddresses.AllSheets)
            {
                // sub-sprite name 은 frame 단위로 다수 — sheet 자체를 로드해서 _sheetSprites 캐시에 쌓기만 함.
                // PixelwoodSliceSetup 의 LabelPrefix (Axe_Down 등) + frame index 로 GetCachedSubSprite 가 매칭.
                var any = await rm.LoadSubSpriteAsync(sheetAddr, ""); // empty subName → null 반환하지만 sheet 캐시는 채워짐.
                // 캐시 채워졌는지 확인은 GetCachedSubSprite 로 한 번 시도.
                // 실제 frame 0 이름은 sheetAddr 끝부분에서 추정 (axe-down → Axe_Down_0).
                string subProbe = SheetAddrToSubName0(sheetAddr);
                var probe = rm.GetCachedSubSprite(sheetAddr, subProbe);
                if (probe != null) ok++; else miss++;
            }
            Debug.Log($"[ROOTBORN/Managers] Player tool sheets preloaded: {ok}/{ok + miss} (probe frame_0).");
        }

        // sheet 주소 → frame 0 sub-sprite 이름 추정. 예: "sprites/player/tool/axe-down" → "Axe_Down_0".
        private static string SheetAddrToSubName0(string sheetAddr)
        {
            // 마지막 segment "axe-down" → "Axe_Down_0"
            int slash = sheetAddr.LastIndexOf('/');
            if (slash < 0) return sheetAddr + "_0";
            string last = sheetAddr.Substring(slash + 1); // "axe-down"
            // "axe-down" → "Axe_Down"
            var parts = last.Split('-');
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            }
            return string.Join("_", parts) + "_0";
        }

        // UI sprite 일괄 사전 로드. UISpriteAddresses 의 모든 single sprite + sheet 을
        // Addressables 로 로드해 캐시. FarmHudController 가 동기 GetCached 로 조회.
        private static async Task PreloadUiSpritesAsync(ResourceManager rm)
        {
            int singleOk = 0, singleMiss = 0;
            foreach (var addr in UISpriteAddresses.AllSingleSprites)
            {
                var s = await rm.LoadAsync<Sprite>(addr);
                if (s != null) singleOk++; else singleMiss++;
            }
            int sheetOk = 0, sheetMiss = 0;
            foreach (var (sheet, subs) in UISpriteAddresses.AllSheets)
            {
                bool ok = true;
                foreach (var sub in subs)
                {
                    var s = await rm.LoadSubSpriteAsync(sheet, sub);
                    if (s == null) { ok = false; break; }
                }
                if (ok) sheetOk++; else sheetMiss++;
            }
            Debug.Log($"[ROOTBORN/Managers] UI sprites preloaded: single {singleOk}/{singleOk + singleMiss}, sheets {sheetOk}/{sheetOk + sheetMiss}.");
        }
    }
}
