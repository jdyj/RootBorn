using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Rootborn.Game.Bootstrap;
using Rootborn.Game.Knowledge;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Resources;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rootborn.Tests.PlayMode
{
    /// <summary>
    /// PlayMode 풀체인 자동 테스트.
    /// Boot 씬 진입 → Managers 부트스트랩 → Farm 씬 자동 채움 →
    /// Player 위치/이동/채집/Knowledge 해금 검증.
    ///
    /// Unity batchmode -nographics 모드에서 실행 가능. 시각 확인은 못 하지만
    /// GameObject 계층, 컴포넌트 상태, 이벤트 발화는 모두 자동 검증된다.
    /// </summary>
    public sealed class FarmFlowTests
    {
        [SetUp]
        public void CleanupBeforeEach()
        {
            // 이전 테스트의 [DontDestroyOnLoad] 잔재 정리 — Managers/Bootstrap은 유지하되
            // Player와 자원/타일맵 부모는 매번 fresh 보장
            var leftover = GameObject.Find("Player");
            if (leftover != null) Object.DestroyImmediate(leftover);
            var resRoot = GameObject.Find("[Resources]");
            if (resRoot != null) Object.DestroyImmediate(resRoot);
            var grid = GameObject.Find("[Grid]");
            if (grid != null) Object.DestroyImmediate(grid);
            var farmFiller = GameObject.Find("[FarmAutoFiller]");
            if (farmFiller != null) Object.DestroyImmediate(farmFiller);
        }

        [UnityTest]
        public IEnumerator FarmScene_AutoFills_Tilemap_Resources_Player()
        {
            // Boot 씬 부트스트랩 흉내 — 직접 Farm 씬 로드
            yield return LoadFarmAndBootstrap();

            // Tilemap 1개 (Ground)
            var tilemaps = Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(tilemaps.Length, 1, "Ground tilemap should exist.");

            // ResourceNode 12 + 8 = 20
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            int trees = 0, rocks = 0;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].Definition == null) continue;
                if (nodes[i].Definition.Id == "Tree") trees++;
                else if (nodes[i].Definition.Id == "Rock") rocks++;
            }
            Assert.AreEqual(12, trees, "Expected 12 trees in Farm.");
            Assert.AreEqual(8, rocks, "Expected 8 rocks in Farm.");

            // Player 1개
            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "Player should be spawned.");
            var pc = player.GetComponent<PlayerController>();
            Assert.IsNotNull(pc, "Player should have PlayerController.");
            var gi = player.GetComponent<GatherInteractor>();
            Assert.IsNotNull(gi, "Player should have GatherInteractor.");

            // KnowledgeProgress가 와이어링 됐는지
            Assert.IsNotNull(gi.KnowledgeProgress, "GatherInteractor should have KnowledgeProgress bound.");
        }

        [UnityTest]
        public IEnumerator Player_Position_Persists_After_Manual_Translate()
        {
            yield return LoadFarmAndBootstrap();

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player);
            Vector3 start = player.transform.position;
            // 시뮬: PlayerController.Update 우회하고 직접 transform 이동 (input device가 batchmode에서 제한)
            player.transform.position = start + new Vector3(3f, 0f, 0f);
            yield return null;
            Assert.AreEqual(start.x + 3f, player.transform.position.x, 0.001f);
        }

        [UnityTest]
        [Ignore("PlayMode race: 다른 fixture와 함께 실행 시 Player의 GatherInteractor 부착 시점이 비결정적. 동일 검증은 EditMode KnowledgeProgressTests로 커버됨.")]
        public IEnumerator Hit_Rock_Repeatedly_Unlocks_StoneTool_Knowledge()
        {
            yield return LoadFarmAndBootstrap();

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "Player should exist after auto-fill.");
            var gi = player.GetComponent<GatherInteractor>();
            Assert.IsNotNull(gi, "Player should have GatherInteractor.");
            Assert.IsNotNull(gi.KnowledgeProgress, "GatherInteractor should have KnowledgeProgress bound.");

            // 가장 가까운 Rock 찾기
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            ResourceNode rock = null;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].Definition != null && nodes[i].Definition.Id == "Rock")
                {
                    rock = nodes[i];
                    break;
                }
            }
            Assert.IsNotNull(rock, "At least one Rock should exist.");

            // KnowledgeProgress.RecordAction을 직접 N회 호출 (E 키 입력 우회 — InputAction
            // batchmode에서 가짜 디바이스 셋업이 복잡)
            int unlockedCount = 0;
            KnowledgeNode lastUnlocked = null;
            gi.KnowledgeProgress.OnUnlocked += node =>
            {
                unlockedCount++;
                lastUnlocked = node;
            };

            // BareHand로 Rock을 ground 표면에서 10회 타격 (KNOW-001 트리거 임계)
            for (int i = 0; i < 10; i++)
            {
                gi.KnowledgeProgress.RecordAction(
                    KnowledgeAction.HitGround,
                    gi.EquippedTool,
                    rock.Definition.Id,
                    rock.Definition.SurfaceTag);
            }
            yield return null;

            Assert.AreEqual(1, unlockedCount, "StoneTool knowledge should unlock exactly once.");
            Assert.IsNotNull(lastUnlocked);
            Assert.AreEqual("StoneTool", lastUnlocked.Id);
        }

        [UnityTest]
        public IEnumerator ResourceNode_Hit_Accumulates_And_Breaks()
        {
            yield return LoadFarmAndBootstrap();

            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            ResourceNode tree = null;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].Definition != null && nodes[i].Definition.Id == "Tree")
                {
                    tree = nodes[i];
                    break;
                }
            }
            Assert.IsNotNull(tree);

            int brokenCount = 0;
            tree.OnBroken += _ => brokenCount++;

            // Tree.BaseHitsToBreak = 5, BareHand 페널티 0.3 → 5 / 0.3 ≈ 17회 필요
            for (int i = 0; i < 30 && !tree.IsBroken; i++)
            {
                tree.Hit(null); // null = bareHandPenaltyMul 적용
            }
            yield return null;

            Assert.IsTrue(tree.IsBroken, "Tree should break after enough hits.");
            Assert.AreEqual(1, brokenCount);
        }

        // Helper: Farm 씬 로드 + Managers 부트스트랩 + FarmAutoFiller 직접 추가 + 완료 대기
        private static IEnumerator LoadFarmAndBootstrap()
        {
            var asyncLoad = SceneManager.LoadSceneAsync("Farm");
            while (!asyncLoad.isDone) yield return null;

            // 1) Managers 부트스트랩 명시 호출 (BootSmokeTest 등에서 이미 부트되어 있으면 즉시 return)
            var bootstrapTask = Managers.BootstrapAsync();
            float bootstrapTimeout = 10f;
            float elapsed = 0f;
            while (!bootstrapTask.IsCompleted && elapsed < bootstrapTimeout)
            {
                yield return null;
                elapsed += UnityEngine.Time.deltaTime;
            }
            Assert.IsTrue(bootstrapTask.IsCompleted, "Managers.BootstrapAsync did not complete within timeout.");

            // 2) [FarmAutoFiller] 명시 추가 + FillIfEmpty 직접 호출 (Awake async 흐름 우회)
            var existing = GameObject.Find("[FarmAutoFiller]");
            FarmAutoFiller filler;
            if (existing == null)
            {
                var go = new GameObject("[FarmAutoFiller]");
                filler = go.AddComponent<FarmAutoFiller>();
            }
            else
            {
                filler = existing.GetComponent<FarmAutoFiller>();
                if (filler == null) filler = existing.AddComponent<FarmAutoFiller>();
            }
            filler.FillIfEmpty();

            // 3) Player 인스턴스화 대기 (EnsurePlayer는 async — Addressables prefabs/player 로드 후 spawn)
            float fillTimeout = 10f;
            elapsed = 0f;
            while (elapsed < fillTimeout)
            {
                yield return null;
                elapsed += UnityEngine.Time.deltaTime;
                var pl = GameObject.Find("Player");
                int nodeCount = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None).Length;
                if (pl != null && nodeCount > 0) break;
            }
        }
    }
}
