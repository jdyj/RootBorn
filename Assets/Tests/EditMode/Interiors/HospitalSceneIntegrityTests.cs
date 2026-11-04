using NUnit.Framework;
        using UnityEditor;
        using UnityEditor.SceneManagement;
        using UnityEngine;
        
        namespace Rootborn.Tests.EditMode.Interiors
        {
            public sealed class HospitalSceneIntegrityTests
            {
                [Test]
                public void HOSPITAL_SCENE_EDIT_001_HospitalInteriorSavedSceneHasNoMissingMonoBehaviours()
                {
                    var previousScene = EditorSceneManager.GetActiveScene().path;
                    try
                    {
                        var scene = EditorSceneManager.OpenScene("Assets/Scenes/HospitalInterior.unity", OpenSceneMode.Single);
                        Assert.IsTrue(scene.IsValid(), "HospitalInterior scene must open for integrity validation."
        );
        
                        var offenders = new System.Text.StringBuilder();
                        var roots = scene.GetRootGameObjects();
                        for (int i = 0; i < roots.Length; i++)
                        {
                            AppendMissingScripts(roots[i].transform, offenders);
                        }
        
                        Assert.IsEmpty(offenders.ToString(), "HospitalInterior saved scene contains missing MonoBehaviour references:\n" + offenders);
                    }
                    finally
                    {
                        if (!string.IsNullOrEmpty(previousScene))
                        {
                            EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
                        }
                    }
                }
        
                private static void AppendMissingScripts(Transform transform, System.Text.StringBuilder offenders)
                {
                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    if (missing > 0)
                    {
                        offenders.Append(transform.name).Append(" missing=").Append(missing).Append('\n');
                    }
        
                    for (int i = 0; i < transform.childCount; i++)
                    {
                        AppendMissingScripts(transform.GetChild(i), offenders);
                    }
                }
            }
        }
        