using System.Collections.Generic;
﻿using System.Collections;
        using UnityEngine;
        using UnityEngine.SceneManagement;
        
        namespace Rootborn.Game.World
        {
            public static class TownReturnViewCleanup
            {
                private const string RunnerName = "[TownReturnViewCleanup]";
                private static readonly string[] HiddenReturnUiNames =
                {
                    // Keep gameplay menu entries reachable; only stale panels are closed.
            "LocationStateLogButton",
                    "StudentDayResultRoot",
                    "BookPanel",
                    // The Objective Journal is rebuilt closed in every scene and opened only by the player (Tab).
                    // Sweeping it here hid a journal the player opened in the first seconds after arriving,
                    // leaving it "open" but invisible so the next Tab closed it instead.
                    "WorldStateLogPanel",
                    "WorldStateLogRoot"
                };
        
                public static void Apply(Scene scene)
                {
                    if (!scene.IsValid() || !scene.isLoaded || !IsSupportedPlayerFacingScene(scene.name))
                    {
                        return;
                    }
        
                    var cleaned = new HashSet<int>();
                    HideReturnUi(scene, cleaned);
                    if (FindByName(scene, RunnerName) != null)
                    {
                        return;
                    }
        
                    var runner = new GameObject(RunnerName);
                    SceneManager.MoveGameObjectToScene(runner, scene);
                    runner.AddComponent<Runner>().Initialize(cleaned);
                }
        
                private static bool IsSupportedPlayerFacingScene(string sceneName)
                {
                    return sceneName == "Town"
                        || sceneName == "ShopInterior"
                        || sceneName == "HospitalInterior"
                        || sceneName == "PoliceInterior"
                        || sceneName == "PoliceShootingInterior"
                        || sceneName == "KitchenInterior"
                        || sceneName == "Kitchen2Interior";
                }

                private static void HideReturnUi(Scene scene, HashSet<int> cleaned)
                {
                    for (int i = 0; i < HiddenReturnUiNames.Length; i++)
                    {
                        HideByName(scene, HiddenReturnUiNames[i], cleaned);
                    }
                }
        
                private static void HideByName(Scene scene, string objectName, HashSet<int> cleaned)
                {
                    var roots = scene.GetRootGameObjects();
                    for (int i = 0; i < roots.Length; i++)
                    {
                        HideByName(roots[i].transform, objectName, cleaned);
                    }
                }
        
                private static void HideByName(Transform root, string objectName, HashSet<int> cleaned)
                {
                    if (root.name == objectName && cleaned.Add(root.gameObject.GetInstanceID()))
                    {
                        root.gameObject.SetActive(false);
                    }
        
                    for (int i = 0; i < root.childCount; i++)
                    {
                        HideByName(root.GetChild(i), objectName, cleaned);
                    }
                }
        
                private static GameObject FindByName(Scene scene, string objectName)
                {
                    var roots = scene.GetRootGameObjects();
                    for (int i = 0; i < roots.Length; i++)
                    {
                        var match = FindByName(roots[i].transform, objectName);
                        if (match != null)
                        {
                            return match;
                        }
                    }
                    return null;
                }
        
                private static GameObject FindByName(Transform root, string objectName)
                {
                    if (root.name == objectName)
                    {
                        return root.gameObject;
                    }
        
                    for (int i = 0; i < root.childCount; i++)
                    {
                        var match = FindByName(root.GetChild(i), objectName);
                        if (match != null)
                        {
                            return match;
                        }
                    }
                    return null;
                }
        
                private sealed class Runner : MonoBehaviour
                {
                    private readonly HashSet<int> _cleaned = new HashSet<int>();
                    public void Initialize(IEnumerable<int> cleaned)
                    {
                        foreach (int id in cleaned) _cleaned.Add(id);
                    }
                    private IEnumerator Start()
                    {
                        Scene scene = gameObject.scene;
                        for (int i = 0; i < 120; i++)
                        {
                            HideReturnUi(scene, _cleaned);
                            yield return null;
                        }
                        Destroy(gameObject);
                    }
                }
            }
        }
        
