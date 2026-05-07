using System.Collections.Generic;
using Rootborn.Game.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rootborn.Editor.Tools
{
    public static class PlayerSetup
    {
        private const string FarmScenePath = "Assets/Scenes/Farm.unity";
        private const string AnimationsFolder = "Assets/Data/Animations";
        private const string ControllerPath = "Assets/Data/Animations/PlayerAnimator.controller";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string PrefabPath = "Assets/Prefabs/Player.prefab";

        private const string IdleDownPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Idle/Down.png";
        private const string IdleSidePath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Idle/Side.png";
        private const string IdleUpPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Idle/Up.png";
        private const string WalkDownPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Walk/Down.png";
        private const string WalkSidePath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Walk/Side.png";
        private const string WalkUpPath = "Assets/Pixelwood Valley/Pixelwood Valley 1.1.2/Player Character/Walk/Up.png";

        [MenuItem("Rootborn/Player/Setup Player Prefab + Spawn In Farm")]
        public static void Setup()
        {
            if (!OneClickSetup.EnsureNotPlaying()) return;
            EnsureFolder(AnimationsFolder);
            EnsureFolder(PrefabFolder);

            var idleDown = LoadFrames(IdleDownPath);
            var idleSide = LoadFrames(IdleSidePath);
            var idleUp = LoadFrames(IdleUpPath);
            var walkDown = LoadFrames(WalkDownPath);
            var walkSide = LoadFrames(WalkSidePath);
            var walkUp = LoadFrames(WalkUpPath);

            if (idleDown.Length == 0 || walkDown.Length == 0)
            {
                Debug.LogError("[ROOTBORN] Player sprites not sliced. Run 'Rootborn/Pixelwood/Slice Sprite Sheets' first.");
                return;
            }

            var clips = new Dictionary<string, AnimationClip>
            {
                { "Idle_Down", BuildLoopClip("Idle_Down", idleDown, 4f) },
                { "Idle_Side", BuildLoopClip("Idle_Side", idleSide, 4f) },
                { "Idle_Up", BuildLoopClip("Idle_Up", idleUp, 4f) },
                { "Walk_Down", BuildLoopClip("Walk_Down", walkDown, 8f) },
                { "Walk_Side", BuildLoopClip("Walk_Side", walkSide, 8f) },
                { "Walk_Up", BuildLoopClip("Walk_Up", walkUp, 8f) }
            };

            var controller = BuildController(clips);

            var prefab = BuildPlayerPrefab(controller, idleDown[0]);

            SpawnInFarm(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ROOTBORN] Player prefab built and spawned in Farm scene.");
        }

        private static Sprite[] LoadFrames(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var list = new List<Sprite>();
            foreach (var a in assets)
            {
                if (a is Sprite s) list.Add(s);
            }
            list.Sort((x, y) => string.CompareOrdinal(x.name, y.name));
            return list.ToArray();
        }

        private static AnimationClip BuildLoopClip(string name, Sprite[] frames, float fps)
        {
            var path = $"{AnimationsFolder}/{name}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = name, frameRate = fps };
                AssetDatabase.CreateAsset(clip, path);
            }
            else
            {
                clip.frameRate = fps;
                clip.ClearCurves();
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

            var keyframes = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i / fps,
                    value = frames[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            if (System.IO.File.Exists(ControllerPath))
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);

            var sm = controller.layers[0].stateMachine;

            var idleDown = sm.AddState("IdleDown");
            idleDown.motion = clips["Idle_Down"];
            var idleSide = sm.AddState("IdleSide");
            idleSide.motion = clips["Idle_Side"];
            var idleUp = sm.AddState("IdleUp");
            idleUp.motion = clips["Idle_Up"];

            var walkDown = sm.AddState("WalkDown");
            walkDown.motion = clips["Walk_Down"];
            var walkSide = sm.AddState("WalkSide");
            walkSide.motion = clips["Walk_Side"];
            var walkUp = sm.AddState("WalkUp");
            walkUp.motion = clips["Walk_Up"];

            sm.defaultState = idleDown;

            AddBidirectional(idleDown, walkDown, "Speed", AnimatorConditionMode.Greater, 0.01f, "Less", 0.01f);
            AddBidirectional(idleSide, walkSide, "Speed", AnimatorConditionMode.Greater, 0.01f, "Less", 0.01f);
            AddBidirectional(idleUp, walkUp, "Speed", AnimatorConditionMode.Greater, 0.01f, "Less", 0.01f);

            // Direction switch among idle states based on MoveY (last input direction held by Speed-zero)
            AddDirectionalIdle(sm, idleDown, idleSide, idleUp);
            AddDirectionalWalk(sm, walkDown, walkSide, walkUp);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddBidirectional(
            AnimatorState a, AnimatorState b,
            string param, AnimatorConditionMode aToBMode, float aToBThreshold,
            string _unusedMode, float bToAThreshold)
        {
            var aToB = a.AddTransition(b);
            aToB.hasExitTime = false;
            aToB.duration = 0f;
            aToB.AddCondition(aToBMode, aToBThreshold, param);

            var bToA = b.AddTransition(a);
            bToA.hasExitTime = false;
            bToA.duration = 0f;
            bToA.AddCondition(AnimatorConditionMode.Less, bToAThreshold, param);
        }

        private static void AddDirectionalIdle(AnimatorStateMachine sm, AnimatorState down, AnimatorState side, AnimatorState up)
        {
            // From any idle to up if MoveY > 0.5 and Speed < 0.01
            AddDirectional(down, up, "MoveY", AnimatorConditionMode.Greater, 0.5f);
            AddDirectional(side, up, "MoveY", AnimatorConditionMode.Greater, 0.5f);
            // To down if MoveY < -0.5
            AddDirectional(side, down, "MoveY", AnimatorConditionMode.Less, -0.5f);
            AddDirectional(up, down, "MoveY", AnimatorConditionMode.Less, -0.5f);
            // To side if abs(MoveX) > 0.5
            AddDirectional(down, side, "MoveX", AnimatorConditionMode.Greater, 0.5f);
            AddDirectional(up, side, "MoveX", AnimatorConditionMode.Greater, 0.5f);
            AddDirectional(down, side, "MoveX", AnimatorConditionMode.Less, -0.5f);
            AddDirectional(up, side, "MoveX", AnimatorConditionMode.Less, -0.5f);
        }

        private static void AddDirectionalWalk(AnimatorStateMachine sm, AnimatorState down, AnimatorState side, AnimatorState up)
        {
            AddDirectional(down, up, "MoveY", AnimatorConditionMode.Greater, 0.5f);
            AddDirectional(side, up, "MoveY", AnimatorConditionMode.Greater, 0.5f);
            AddDirectional(side, down, "MoveY", AnimatorConditionMode.Less, -0.5f);
            AddDirectional(up, down, "MoveY", AnimatorConditionMode.Less, -0.5f);
            AddDirectional(down, side, "MoveX", AnimatorConditionMode.Greater, 0.5f);
            AddDirectional(up, side, "MoveX", AnimatorConditionMode.Greater, 0.5f);
            AddDirectional(down, side, "MoveX", AnimatorConditionMode.Less, -0.5f);
            AddDirectional(up, side, "MoveX", AnimatorConditionMode.Less, -0.5f);
        }

        private static void AddDirectional(AnimatorState from, AnimatorState to, string param, AnimatorConditionMode mode, float threshold)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0f;
            t.AddCondition(mode, threshold, param);
        }

        private static GameObject BuildPlayerPrefab(AnimatorController controller, Sprite defaultSprite)
        {
            var template = new GameObject("Player");
            var sr = template.AddComponent<SpriteRenderer>();
            sr.sprite = defaultSprite;
            sr.sortingOrder = 5;
            template.transform.localScale = Vector3.one;

            var toolPart = new GameObject("Part_tool");
            toolPart.transform.SetParent(template.transform, false);
            var toolRenderer = toolPart.AddComponent<SpriteRenderer>();
            toolRenderer.enabled = false;
            toolRenderer.sortingOrder = 10;

            var animator = template.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var rb = template.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.linearDamping = 8f;

            var pcol = template.AddComponent<BoxCollider2D>();
            pcol.size = new Vector2(0.6f, 0.5f);
            pcol.offset = new Vector2(0f, -0.25f);
            pcol.isTrigger = false;

            var ctrl = template.AddComponent<PlayerController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("_animator").objectReferenceValue = animator;
            so.FindProperty("_renderer").objectReferenceValue = sr;
            so.FindProperty("_toolRenderer").objectReferenceValue = toolRenderer;
            var rbProp = so.FindProperty("_rb");
            if (rbProp != null) rbProp.objectReferenceValue = rb;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(template, PrefabPath);
            Object.DestroyImmediate(template);
            return prefab;
        }

        private static void SpawnInFarm(GameObject prefab)
        {
            var scene = EditorSceneManager.OpenScene(FarmScenePath, OpenSceneMode.Single);
            var existing = GameObject.Find("Player");
            if (existing != null) Object.DestroyImmediate(existing);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Player";
            instance.transform.position = new Vector3(15f, 10f, 0f);

            int treeCount = 0, rockCount = 0;
            var resRoot = GameObject.Find("[Resources]");
            if (resRoot != null)
            {
                foreach (Transform t in resRoot.transform)
                {
                    if (t.name.StartsWith("Tree_")) treeCount++;
                    else if (t.name.StartsWith("Rock_")) rockCount++;
                }
            }
            Debug.Log($"[ROOTBORN/PlayerSetup] Spawned Player at (15, 10). Existing resources: trees={treeCount}, rocks={rockCount}");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
