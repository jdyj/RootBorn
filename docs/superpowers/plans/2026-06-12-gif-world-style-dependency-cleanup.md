# Gif World Style Dependency Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the selected Gif world-style preview assets self-contained under project-owned paths, then prepare the style for a small real-scene application pass.

**Architecture:** Keep the original imported package under `Assets/Gif/` as source material, but use curated project-owned assets from `Assets/Tiles/GifWorldStyle/`, `Assets/Prefabs/GifWorldStyle/`, and `Assets/Art/GifWorldStyle/Dependencies/` in game-facing scenes. Enforce the boundary with EditMode tests, then verify saved scene state and actual PlayMode visuals.

**Tech Stack:** Unity 6000.3.13f1, Unity Tilemap, `Ruccho.Utilities.FangAutoTile`, Unity MCP tools, NUnit EditMode/PlayMode tests.

---

## File Map

- Modify: `Assets/Tests/EditMode/Visual/GifWorldStyleCuratedAssetsTests.cs`
  - Adds dependency-boundary assertions for curated prefabs.
- Create: `Assets/Art/GifWorldStyle/Dependencies/`
  - Stores copied sprite textures, animation clips, animator controllers, and other non-script dependencies used by curated prefabs.
- Modify: `Assets/Prefabs/GifWorldStyle/WorldProps/*.prefab`
  - Remaps `SpriteRenderer`, `Animator`, and animation references away from `Assets/Gif/.../Resources`.
- Modify: `Assets/Scenes/GifWorldStylePreview.unity`
  - Ensures saved scene instances use curated prefabs and curated FangAutoTile assets.
- Modify if applying to a real scene in the next pass: `Assets/Scenes/Town.unity` or another chosen small target scene.
  - Applies a tiny visual slice only after the curated dependency boundary is clean.

---

### Task 1: Add Dependency Boundary Test

**Files:**
- Modify: `Assets/Tests/EditMode/Visual/GifWorldStyleCuratedAssetsTests.cs`

- [ ] **Step 1: Add a failing test for prefab dependencies**

Add this test to `GifWorldStyleCuratedAssetsTests`:

```csharp
[Test]
public void CuratedGifWorldStylePrefabsDoNotDependOnSourceResources()
{
    foreach (var prefabPath in CuratedPrefabs)
    {
        var dependencies = AssetDatabase.GetDependencies(prefabPath, true);
        foreach (var dependency in dependencies)
        {
            if (dependency == prefabPath || dependency.EndsWith(".cs"))
            {
                continue;
            }

            Assert.False(
                dependency.StartsWith("Assets/Gif/Super_Retro_Collection/Resources/"),
                $"Curated prefab {prefabPath} still depends on source Resources asset: {dependency}");
        }
    }
}
```

- [ ] **Step 2: Run EditMode visual tests and verify RED**

Run:

```powershell
@'
{"testMode":"EditMode","testNamespace":"Rootborn.Tests.EditMode.Visual","includePassingTests":true,"includeMessages":true,"includeStacktrace":false,"includeLogs":false}
'@ | unity-mcp-cli.cmd run-tool tests-run --input-file -
```

Expected: `CuratedGifWorldStylePrefabsDoNotDependOnSourceResources` fails and names at least one dependency under `Assets/Gif/Super_Retro_Collection/Resources/`.

---

### Task 2: Copy Non-Script Dependencies

**Files:**
- Create/modify: `Assets/Art/GifWorldStyle/Dependencies/**`

- [ ] **Step 1: Execute dependency copy script**

Run an MCP `script-execute` editor script that:

```csharp
// Pseudocode contract for the execution script:
// 1. For every prefab in Assets/Prefabs/GifWorldStyle/WorldProps/*.prefab
// 2. Read AssetDatabase.GetDependencies(prefabPath, true)
// 3. Skip .cs files and the prefab itself
// 4. For dependencies under Assets/Gif/Super_Retro_Collection/Resources/
// 5. Copy to Assets/Art/GifWorldStyle/Dependencies/<same relative path>
// 6. Preserve file names and meta-generated asset import behavior through AssetDatabase.CopyAsset
// 7. SaveAssets and Refresh
```

The destination rule must be deterministic. Example:

```text
Assets/Gif/Super_Retro_Collection/Resources/Prefabs/Houses/house_01.png
-> Assets/Art/GifWorldStyle/Dependencies/Prefabs/Houses/house_01.png
```

- [ ] **Step 2: Inspect copied dependency count**

Run:

```powershell
rg --files Assets/Art/GifWorldStyle/Dependencies
```

Expected: non-empty output containing texture or animation-related assets used by the 12 curated prefabs.

---

### Task 3: Remap Curated Prefabs

**Files:**
- Modify: `Assets/Prefabs/GifWorldStyle/WorldProps/*.prefab`

- [ ] **Step 1: Execute prefab remap script**

Run an MCP `script-execute` editor script that opens each curated prefab via `PrefabUtility.LoadPrefabContents(prefabPath)` and remaps:

```csharp
// SpriteRenderer remap rule:
// old sprite path starts with Assets/Gif/Super_Retro_Collection/Resources/
// copied texture path = Assets/Art/GifWorldStyle/Dependencies/<relative old asset path>
// copied sprite = AssetDatabase.LoadAllAssetsAtPath(copiedTexturePath)
//     .OfType<Sprite>()
//     .FirstOrDefault(sprite => sprite.name == oldSprite.name)
// renderer.sprite = copied sprite
```

For Animator and AnimationClip references:

```csharp
// Animator remap rule:
// If animator.runtimeAnimatorController path is under source Resources,
// assign the copied controller from Assets/Art/GifWorldStyle/Dependencies/<relative path>.
//
// Animation clips copied as standalone assets should be remapped through copied AnimatorController dependencies.
// If a clip is embedded and cannot be remapped safely, log the prefab name and keep the test failing until handled.
```

Save each prefab with:

```csharp
PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
PrefabUtility.UnloadPrefabContents(root);
AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
```

- [ ] **Step 2: Run EditMode visual tests and verify GREEN for dependency boundary**

Run:

```powershell
@'
{"testMode":"EditMode","testNamespace":"Rootborn.Tests.EditMode.Visual","includePassingTests":true,"includeMessages":true,"includeStacktrace":false,"includeLogs":false}
'@ | unity-mcp-cli.cmd run-tool tests-run --input-file -
```

Expected: all `Rootborn.Tests.EditMode.Visual` tests pass, including the new dependency-boundary test.

---

### Task 4: Verify Saved Preview Scene References

**Files:**
- Modify if needed: `Assets/Scenes/GifWorldStylePreview.unity`

- [ ] **Step 1: Add or extend scene reference assertions**

Extend the existing scene test so all prefab instances under `Gif House Cluster`, `Gif Forest Cluster`, and `Gif Prop Cluster` resolve to curated prefab sources:

```csharp
var propRoots = new[] { "Gif House Cluster", "Gif Forest Cluster", "Gif Prop Cluster" };
foreach (var rootName in propRoots)
{
    var root = GameObject.Find(rootName);
    Assert.IsNotNull(root, $"Missing {rootName}.");

    foreach (Transform child in root.transform)
    {
        var source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
        var sourcePath = AssetDatabase.GetAssetPath(source);
        Assert.True(
            sourcePath.StartsWith("Assets/Prefabs/GifWorldStyle/WorldProps/"),
            $"Preview scene instance {child.name} should use curated prefab, not {sourcePath}.");
    }
}
```

- [ ] **Step 2: Run EditMode tests**

Run the same EditMode command.

Expected: all visual EditMode tests pass.

---

### Task 5: PlayMode Visual Verification

**Files:**
- No source files unless verification reveals a visual issue.
- Output: `Builds/Logs/gif-world-style-preview-dependency-clean.png`

- [ ] **Step 1: Run PlayMode visual tests**

Run:

```powershell
@'
{"testMode":"PlayMode","testNamespace":"Rootborn.Tests.PlayMode.Visual","includePassingTests":true,"includeMessages":true,"includeStacktrace":false,"includeLogs":false}
'@ | unity-mcp-cli.cmd run-tool tests-run --input-file -
```

Expected: 2 passed, 0 failed.

- [ ] **Step 2: Open preview scene and enter PlayMode**

Run:

```powershell
@'
{"sceneRef":{"instanceID":0,"assetPath":"Assets/Scenes/GifWorldStylePreview.unity"},"loadSceneMode":"Single"}
'@ | unity-mcp-cli.cmd run-tool scene-open --input-file -

@'
{"isPlaying":true,"isPaused":false}
'@ | unity-mcp-cli.cmd run-tool editor-application-set-state --input-file -
```

Expected: `IsPlaying` becomes `true`.

- [ ] **Step 3: Probe runtime state**

Run an MCP `script-execute` probe equivalent to the previous one and verify:

```text
occupied >= 140
fang == occupied
curated == occupied
rendererEnabled == True
taggedProps >= 12
camera == Main Camera
orthographic == True
```

- [ ] **Step 4: Capture and inspect Game View or camera screenshot**

Save:

```text
Builds/Logs/gif-world-style-preview-dependency-clean.png
```

Expected visual result: houses, trees, props, grass, sand, stone, and water render clearly with no missing sprites, magenta materials, blank tiles, stale sample/debug tilemaps, or UI overlap.

- [ ] **Step 5: Exit PlayMode**

Run:

```powershell
@'
{"isPlaying":false,"isPaused":false}
'@ | unity-mcp-cli.cmd run-tool editor-application-set-state --input-file -
```

Expected: `IsPlaying=false`, `IsCompiling=false`.

---

### Task 6: Choose First Real Scene Slice

**Files:**
- Read first: `Assets/Scenes/Town.unity`, `Assets/Scenes/Farm.unity`
- Modify later: exactly one chosen scene, preferably a small non-core visual slice.

- [ ] **Step 1: Inspect candidate scenes**

Inspect saved Tilemaps and scene roots for `Town.unity` and `Farm.unity`. Capture:

```text
root objects
tilemap names
tile counts
sorting layers/orders
camera setup
current visible sample/debug objects
```

- [ ] **Step 2: Pick one small target area**

Use this decision rule:

```text
Prefer Town if the next gameplay target is StudentLife/Town traversal.
Prefer Farm only if maintaining legacy farm regression visuals is the immediate goal.
Limit the first replacement to one small area: around spawn, a pond edge, or one path segment.
```

- [ ] **Step 3: Write scene-specific RED test before edits**

Create a scene test that verifies the selected area references `Assets/Tiles/GifWorldStyle/Autotiles/` or `Assets/Prefabs/GifWorldStyle/WorldProps/`.

Expected first run: fail because the real scene has not been migrated yet.

---

## Self-Review

- Spec coverage: The plan covers dependency separation, prefab remapping, saved scene verification, PlayMode visual verification, and the next real-scene slice.
- Placeholder scan: No task uses open-ended "TODO" work. Each task has concrete paths, commands, and expected results.
- Risk: AnimatorController and embedded AnimationClip remapping may require one extra iteration if copied controllers still reference source clips. The dependency-boundary test remains the guardrail and should not be relaxed.

