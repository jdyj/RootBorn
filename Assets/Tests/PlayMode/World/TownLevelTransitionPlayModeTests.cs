using System.Collections;
        using System.IO;
        using NUnit.Framework;
        using Rootborn.Game.Player;
        using Rootborn.Tests.PlayMode.EndToEnd.Support;
        using UnityEngine;
        using UnityEngine.InputSystem;
        using UnityEngine.SceneManagement;
        using UnityEngine.TestTools;
        using UnityEngine.Tilemaps;
        
        namespace Rootborn.Tests.PlayMode.World
        {
            public sealed class TownLevelTransitionPlayModeTests : Rootborn.Tests.PlayMode.PlayModeInputFixture
            {
                private string _saveRoot;

                [UnitySetUp]
                public IEnumerator SetUp()
                {
                    // Scene travel needs the slot owner a new game binds; a directly loaded Town has none and refuses to travel.
                    _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-level-transition-" + System.Guid.NewGuid().ToString("N"));
                    Rootborn.Game.Save.SaveService.SetRootDirectoryForTests(_saveRoot);
                    Rootborn.Game.Save.ActiveSaveContext.Clear();
                    yield return Rootborn.Tests.PlayMode.HousePlacementTestDriver.EnterWorld("level-transition", "Town", "town-home-entry");
                    yield return WaitForTownReady(10f);
                }

                [UnityTearDown]
                public IEnumerator TearDownSave()
                {
                    Rootborn.Game.Save.ActiveSaveContext.Clear();
                    Rootborn.Game.Save.SaveService.SetRootDirectoryForTests(null);
                    yield return null;
                }
        
                [UnityTest]
                public IEnumerator TOWN_LEVEL_PM_001_WalkingDownFromLevel0LoadsBeachAtTopEntry()
                {
                    GameObject trigger = GameObject.Find("TownLevelTransition_Level0ToLevel1");
                    Assert.IsNotNull(trigger, "Town must expose a saved level transition trigger from Level_0 to Beach.");
                    Collider2D triggerCollider = trigger.GetComponent<Collider2D>();
                    Assert.IsNotNull(triggerCollider, "Level transition must have a Collider2D.");
                    Assert.IsTrue(triggerCollider.isTrigger, "Level transition collider must be a trigger.");
                    var transition = trigger.GetComponent<Rootborn.Game.World.TownLevelTransitionTrigger>();
                    Assert.IsNotNull(transition, "Level transition must use TownLevelTransitionTrigger.");
                    Assert.AreEqual("Beach", transition.DestinationScene);
                    Assert.AreEqual("beach-top-entry", transition.DestinationSpawnId);
        
                    Tilemap townBeach = GameObject.Find("Town2LdtkTilemaps").transform.Find("Town2LdtkBeachTilemap").GetComponent<Tilemap>();
                    Assert.AreEqual(0, CountTiles(townBeach), "Town scene must not render Level_1 beach tiles below the lower boundary.");
        
                    GameObject player = GameObject.Find("Player");
                    Assert.IsNotNull(player?.GetComponent<PlayerController>(), "Town must expose a controllable player.");
                    player.transform.position = new Vector3(37.5f, -61.2f, 0f);
                    Physics2D.SyncTransforms();
                    yield return null;
        
                    Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                    yield return PlayerFlowE2EDriver.WalkPlayerWithKeyboardTo(player, keyboard, new Vector3(37.5f, -63.4f, 0f), 0.3f, 240, "Player must reach the lower Level_0 boundary through actual keyboard movement.");
                    yield return WaitForScene("Beach", 10f);
                    yield return null;
        
                    GameObject beachPlayer = GameObject.Find("Player");
                    Assert.IsNotNull(beachPlayer, "Player must persist into the Beach scene.");
                    Assert.LessOrEqual(Vector3.Distance(beachPlayer.transform.position, new Vector3(37.5f, -70.5f, 0f)), 0.35f, "Player must arrive at the Beach top entry point.");
                    Assert.IsNotNull(GameObject.Find("BeachSpawnPoint"), "Beach scene must expose BeachSpawnPoint.");
                    Assert.IsNotNull(GameObject.Find("BeachLevelTransition_BeachToTown"), "Beach scene must expose a return trigger to Town.");
        
                    Tilemap beach = GameObject.Find("Town2LdtkTilemaps").transform.Find("Town2LdtkBeachTilemap").GetComponent<Tilemap>();
                    Assert.AreEqual(933, CountTiles(beach), "Beach scene must render the Level_1 beach tiles.");
                    Vector3Int cell = beach.WorldToCell(beachPlayer.transform.position + new Vector3(0f, -10f, 0f));
                    Assert.IsNotNull(beach.GetTile(cell), "Beach visual area must be present below the entry point, sample cell=" + cell);
        
                    string screenshotPath = Path.Combine(Application.temporaryCachePath, "BeachSceneTransitionVisual.png");
                    if (File.Exists(screenshotPath)) File.Delete(screenshotPath);
                    ScreenCapture.CaptureScreenshot(screenshotPath);
                    float elapsed = 0f;
                    while (!File.Exists(screenshotPath) && elapsed < 3f)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    Assert.IsTrue(File.Exists(screenshotPath), "Beach scene transition Game View screenshot was not captured: " + screenshotPath);
                }
        
                private static IEnumerator WaitForTownReady(float timeoutSeconds)
                {
                    float elapsed = 0f;
                    while (elapsed < timeoutSeconds)
                    {
                        bool ready = GameObject.Find("Player")?.GetComponent<PlayerController>() != null && GameObject.Find("Town2LdtkTilemaps") != null;
                        if (ready) yield break;
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    Assert.Fail("Town runtime did not become ready for level transition testing.");
                }
        
                private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds)
                {
                    float elapsed = 0f;
                    while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeoutSeconds)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
                }
        
                private static int CountTiles(Tilemap tilemap)
                {
                    int count = 0;
                    foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
                    {
                        if (tilemap.GetTile(cell) != null) count++;
                    }
                    return count;
                }
            }
        }
        