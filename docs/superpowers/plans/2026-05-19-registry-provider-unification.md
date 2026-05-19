# Registry Provider Unification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Unify runtime `GameDataRegistry` access behind one provider API and prove the migration through focused EditMode, PlayMode, and QA simulation gates.

**Architecture:** Add a small provider in `Rootborn.Game.Common` that resolves the active registry from an explicit override or `Managers.Data.Registry`. Migrate the highest-risk runtime registry fallbacks to that provider, then shrink the static allowlist. Add a player-facing MainMenu-to-Town PlayMode scenario and a focused QA script that runs the provider tests and evidence capture.

**Tech Stack:** Unity 6000.3.13f1, C#, NUnit EditMode/PlayMode tests, Unity MCP `script-update-or-create` for `Assets/**/*.cs`, PowerShell QA runner, existing `Scripts/ci/check-no-entity-id-branching.sh`.

---

## File Structure

- Create: `Assets/Scripts/Game/Common/IGameDataRegistryProvider.cs`
  - Interface boundary for registry providers.
- Create: `Assets/Scripts/Game/Common/GameDataRegistryProvider.cs`
  - Static facade and default provider implementation.
- Modify: `Assets/Scripts/Game/Managers/DataManager.cs`
  - Exposes initialized registry through provider without changing lookup dictionaries.
- Modify: `Assets/Scripts/UI/StudentLife/CampaignRuntimeInstaller.cs`
  - Replaces direct `Resources.Load<GameDataRegistry>` fallback with provider access.
- Modify: `Assets/Scripts/UI/StudentLife/DailyEventRuntimeInstaller.cs`
  - Replaces direct `Resources.Load<GameDataRegistry>` fallback with provider access.
- Modify: `Assets/Scripts/UI/HUD/FarmCharacterHudOverlayInstaller.cs`
  - Replaces thumbnail registry fallback with provider access.
- Modify: `Assets/Scripts/Game/WorldGeneration/SeededFarmWorldApplier.cs`
  - Replaces world-generation registry fallback with provider access.
- Modify: `Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs`
  - Replaces registry fallback with provider access while preserving non-registry loaded-object fallback review.
- Modify: `Assets/Tests/EditMode/StaticRuntimeUsageGateTests.cs`
  - Removes migrated registry fallback allowlist entries and adds a direct registry-load gate.
- Create: `Assets/Tests/EditMode/Registry/GameDataRegistryProviderTests.cs`
  - Unit coverage for provider resolution, override isolation, and failure behavior.
- Create: `Assets/Tests/PlayMode/EndToEnd/RegistryProviderUnifiedFlowE2ETests.cs`
  - Player-facing MainMenu to Town scenario that verifies provider-backed systems.
- Create: `Scripts/qa/run-registry-provider-playmode-simulation.ps1`
  - Focused QA gate for provider EditMode, PlayMode, entity branching, logs, and evidence.
- Create: `docs/superpowers/audits/2026-05-19-registry-provider-unification-verification.md`
  - Verification record created after execution.

## Task 1: Provider Unit Tests

**Files:**
- Create: `Assets/Tests/EditMode/Registry/GameDataRegistryProviderTests.cs`
- Planned implementation file: `Assets/Scripts/Game/Common/IGameDataRegistryProvider.cs`
- Planned implementation file: `Assets/Scripts/Game/Common/GameDataRegistryProvider.cs`

- [ ] **Step 1: Write the failing EditMode tests**

Use Unity MCP `script-update-or-create` to create `Assets/Tests/EditMode/Registry/GameDataRegistryProviderTests.cs` with this content:

```csharp
using NUnit.Framework;
using Rootborn.Game.Common;
using UnityEngine;

namespace Rootborn.Tests.EditMode.Registry
{
    public sealed class GameDataRegistryProviderTests
    {
        [TearDown]
        public void TearDown()
        {
            GameDataRegistryProvider.ClearOverrideForTests();
            GameDataRegistryProvider.ResetCacheForTests();
        }

        [Test]
        public void REGISTRY_PROVIDER_EDIT_001_OverrideRegistryIsReturnedAndCached()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();

            GameDataRegistryProvider.SetOverrideForTests(registry);

            Assert.IsTrue(GameDataRegistryProvider.TryGet(out var first));
            Assert.AreSame(registry, first);
            Assert.AreSame(registry, GameDataRegistryProvider.Require());
        }

        [Test]
        public void REGISTRY_PROVIDER_EDIT_002_ClearOverrideRemovesTestRegistry()
        {
            var registry = ScriptableObject.CreateInstance<GameDataRegistry>();
            GameDataRegistryProvider.SetProviderForTests(new EmptyProvider());
            GameDataRegistryProvider.SetOverrideForTests(registry);
            Assert.IsTrue(GameDataRegistryProvider.TryGet(out _));

            GameDataRegistryProvider.ClearOverrideForTests();
            GameDataRegistryProvider.ResetCacheForTests();
            GameDataRegistryProvider.SetProviderForTests(new EmptyProvider());

            Assert.IsFalse(GameDataRegistryProvider.TryGet(out var cleared));
            Assert.IsNull(cleared);
        }

        [Test]
        public void REGISTRY_PROVIDER_EDIT_003_RequireThrowsWhenNoRegistrySourceExists()
        {
            GameDataRegistryProvider.ClearOverrideForTests();
            GameDataRegistryProvider.SetProviderForTests(new EmptyProvider());
            GameDataRegistryProvider.ResetCacheForTests();
            GameDataRegistryProvider.SetProviderForTests(new EmptyProvider());

            var exception = Assert.Throws<System.InvalidOperationException>(() => GameDataRegistryProvider.Require());
            StringAssert.Contains("GameDataRegistry", exception.Message);
        }

        private sealed class EmptyProvider : IGameDataRegistryProvider
        {
            public bool TryGet(out GameDataRegistry registry)
            {
                registry = null;
                return false;
            }

            public GameDataRegistry Require()
            {
                throw new System.InvalidOperationException("empty provider");
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run through Unity MCP:

```powershell
$json = '{"testMode":"EditMode","testClass":"Rootborn.Tests.EditMode.Registry.GameDataRegistryProviderTests","includePassingTests":false,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path --timeout 300000
Remove-Item -LiteralPath $path -Force
```

Expected: FAIL with compile errors because `GameDataRegistryProvider` does not exist.

- [ ] **Step 3: Commit the red test**

```powershell
git add Assets/Tests/EditMode/Registry/GameDataRegistryProviderTests.cs Assets/Tests/EditMode/Registry/GameDataRegistryProviderTests.cs.meta
git commit -m "[TEST] Registry Provider 실패 테스트 추가"
```

## Task 2: Provider Implementation

**Files:**
- Create: `Assets/Scripts/Game/Common/IGameDataRegistryProvider.cs`
- Create: `Assets/Scripts/Game/Common/GameDataRegistryProvider.cs`

- [ ] **Step 1: Add provider interface**

Use Unity MCP `script-update-or-create` to create `Assets/Scripts/Game/Common/IGameDataRegistryProvider.cs`:

```csharp
namespace Rootborn.Game.Common
{
    public interface IGameDataRegistryProvider
    {
        bool TryGet(out GameDataRegistry registry);
        GameDataRegistry Require();
    }
}
```

- [ ] **Step 2: Add provider facade and default provider**

Use Unity MCP `script-update-or-create` to create `Assets/Scripts/Game/Common/GameDataRegistryProvider.cs`:

```csharp
using System;
using Rootborn.Game.Managers;

namespace Rootborn.Game.Common
{
    public static class GameDataRegistryProvider
    {
        private static IGameDataRegistryProvider s_provider = new DefaultGameDataRegistryProvider();
        private static GameDataRegistry s_overrideRegistry;
        private static GameDataRegistry s_cachedRegistry;

        public static bool TryGet(out GameDataRegistry registry)
        {
            if (s_overrideRegistry != null)
            {
                registry = s_overrideRegistry;
                s_cachedRegistry = registry;
                return true;
            }

            if (s_cachedRegistry != null)
            {
                registry = s_cachedRegistry;
                return true;
            }

            if (s_provider != null && s_provider.TryGet(out registry))
            {
                s_cachedRegistry = registry;
                return true;
            }

            registry = null;
            return false;
        }

        public static GameDataRegistry Require()
        {
            if (TryGet(out var registry)) return registry;
            throw new InvalidOperationException("GameDataRegistry is not available through GameDataRegistryProvider.");
        }

        public static void SetProviderForTests(IGameDataRegistryProvider provider)
        {
            s_provider = provider ?? new DefaultGameDataRegistryProvider();
            s_cachedRegistry = null;
        }

        public static void SetOverrideForTests(GameDataRegistry registry)
        {
            s_overrideRegistry = registry;
            s_cachedRegistry = registry;
        }

        public static void ClearOverrideForTests()
        {
            s_overrideRegistry = null;
        }

        public static void ResetCacheForTests()
        {
            s_cachedRegistry = null;
            s_provider = new DefaultGameDataRegistryProvider();
        }

        private sealed class DefaultGameDataRegistryProvider : IGameDataRegistryProvider
        {
            public bool TryGet(out GameDataRegistry registry)
            {
                registry = Managers.Data != null ? Managers.Data.Registry : null;
                return registry != null;
            }

            public GameDataRegistry Require()
            {
                if (TryGet(out var registry)) return registry;
                throw new InvalidOperationException("Managers.Data.Registry is not initialized.");
            }
        }
    }
}
```

- [ ] **Step 3: Run provider tests**

Run:

```powershell
$json = '{"testMode":"EditMode","testClass":"Rootborn.Tests.EditMode.Registry.GameDataRegistryProviderTests","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path --timeout 300000
Remove-Item -LiteralPath $path -Force
```

Expected: PASS, 3 tests passed.

- [ ] **Step 4: Commit provider implementation**

```powershell
git add Assets/Scripts/Game/Common/IGameDataRegistryProvider.cs Assets/Scripts/Game/Common/IGameDataRegistryProvider.cs.meta Assets/Scripts/Game/Common/GameDataRegistryProvider.cs Assets/Scripts/Game/Common/GameDataRegistryProvider.cs.meta
git commit -m "[REFACTOR][TEST] Registry Provider 기본 구현 추가"
```

## Task 3: Migrate Town Runtime Installers

**Files:**
- Modify: `Assets/Scripts/UI/StudentLife/CampaignRuntimeInstaller.cs`
- Modify: `Assets/Scripts/UI/StudentLife/DailyEventRuntimeInstaller.cs`
- Test: `Assets/Tests/EditMode/StaticRuntimeUsageGateTests.cs`

- [ ] **Step 1: Write a failing static assertion for direct registry resource loading**

Use Unity MCP `script-update-or-create` to update `Assets/Tests/EditMode/StaticRuntimeUsageGateTests.cs`. Add this regex field near the existing regex fields:

```csharp
private static readonly Regex DirectRegistryResourcesLoadPattern = new Regex(
    @"Resources\.Load\s*<\s*GameDataRegistry\s*>\s*\(",
    RegexOptions.Compiled);
```

Add this migrated-caller list near `AllowedRuntimeResourceOrFindUsages`:

```csharp
private static readonly HashSet<string> MigratedRegistryProviderCallers = new HashSet<string>
{
    "Assets/Scripts/UI/StudentLife/CampaignRuntimeInstaller.cs",
    "Assets/Scripts/UI/StudentLife/DailyEventRuntimeInstaller.cs"
};
```

Add this test after `STATIC_002_RuntimeResourcesAndFindUsages_StayInsideReviewedAllowlist`:

```csharp
[Test]
public void STATIC_003_MigratedRuntimeCallers_DoNotDirectlyResourcesLoadGameDataRegistry()
{
    var hits = FindMatches(DirectRegistryResourcesLoadPattern, "Assets/Scripts/Game", "Assets/Scripts/UI", "Assets/Scripts/Network");
    var unexpected = new List<string>();
    foreach (string hit in hits)
    {
        int firstColon = hit.IndexOf(':');
        string path = firstColon >= 0 ? hit.Substring(0, firstColon).Replace('\\', '/') : hit.Replace('\\', '/');
        if (MigratedRegistryProviderCallers.Contains(path))
        {
            unexpected.Add(hit);
        }
    }

    Assert.IsEmpty(unexpected, "Runtime code must use GameDataRegistryProvider instead of direct Resources.Load<GameDataRegistry>:\n" + string.Join("\n", unexpected));
}
```

- [ ] **Step 2: Run the static test to verify it fails**

Run:

```powershell
$json = '{"testMode":"EditMode","testClass":"Rootborn.Tests.EditMode.StaticRuntimeUsageGateTests","includePassingTests":false,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path --timeout 300000
Remove-Item -LiteralPath $path -Force
```

Expected: FAIL listing `CampaignRuntimeInstaller.cs` and `DailyEventRuntimeInstaller.cs`.

- [ ] **Step 3: Migrate `CampaignRuntimeInstaller`**

Use Unity MCP `script-update-or-create` to update the `ResolveRegistry` method in `Assets/Scripts/UI/StudentLife/CampaignRuntimeInstaller.cs`:

```csharp
private static GameDataRegistry ResolveRegistry()
{
    return GameDataRegistryProvider.TryGet(out var registry) ? registry : null;
}
```

Ensure the file still has `using Rootborn.Game.Common;` and remove `using Rootborn.Game.Managers;` only if no other symbol from that namespace is used.

- [ ] **Step 4: Migrate `DailyEventRuntimeInstaller`**

Use Unity MCP `script-update-or-create` to update the `ResolveRegistry` method in `Assets/Scripts/UI/StudentLife/DailyEventRuntimeInstaller.cs`:

```csharp
private static GameDataRegistry ResolveRegistry()
{
    return GameDataRegistryProvider.TryGet(out var registry) ? registry : null;
}
```

Ensure the file still has `using Rootborn.Game.Common;` and remove `using Rootborn.Game.Managers;` only if no longer needed.

- [ ] **Step 5: Run focused tests**

Run:

```powershell
$json = '{"testMode":"EditMode","testClass":"Rootborn.Tests.EditMode.StaticRuntimeUsageGateTests","includePassingTests":false,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path --timeout 300000
Remove-Item -LiteralPath $path -Force
```

Expected: PASS. The migrated-caller list only includes `CampaignRuntimeInstaller.cs` and `DailyEventRuntimeInstaller.cs` at this task boundary.

- [ ] **Step 6: Commit Town installer migration**

```powershell
git add Assets/Scripts/UI/StudentLife/CampaignRuntimeInstaller.cs Assets/Scripts/UI/StudentLife/DailyEventRuntimeInstaller.cs Assets/Tests/EditMode/StaticRuntimeUsageGateTests.cs
git commit -m "[REFACTOR][TEST] Town 런타임 Registry Provider 경유"
```

## Task 4: Migrate Farm and World Runtime Callers

**Files:**
- Modify: `Assets/Scripts/UI/HUD/FarmCharacterHudOverlayInstaller.cs`
- Modify: `Assets/Scripts/Game/WorldGeneration/SeededFarmWorldApplier.cs`
- Modify: `Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs`
- Modify: `Assets/Tests/EditMode/StaticRuntimeUsageGateTests.cs`

- [ ] **Step 1: Migrate `FarmCharacterHudOverlayInstaller`**

Use Unity MCP `script-update-or-create` to update `BuildCharacterThumbnail` in `Assets/Scripts/UI/HUD/FarmCharacterHudOverlayInstaller.cs`. Replace the registry line with:

```csharp
if (!GameDataRegistryProvider.TryGet(out var registry)) return;
```

Keep `using Rootborn.Game.Common;` at the top.

- [ ] **Step 2: Migrate `SeededFarmWorldApplier`**

Use Unity MCP `script-update-or-create` to replace the registry resolution block in `ApplyFromActiveContext`:

```csharp
if (!GameDataRegistryProvider.TryGet(out var registry))
{
    return;
}
```

The method should then continue:

```csharp
if (registry == null || registry.DefaultFarmTerrainGeneration == null)
{
    return;
}
```

- [ ] **Step 3: Migrate `FarmAutoFiller`**

Use Unity MCP `script-update-or-create` to update `FillIfEmpty`:

```csharp
var data = Managers.Managers.Data;
var registry = GameDataRegistryProvider.TryGet(out var activeRegistry) ? activeRegistry : LoadRegistryFallback();
```

Then update `LoadRegistryFallback` so it no longer directly loads `GameDataRegistry` from Resources:

```csharp
private static GameDataRegistry LoadRegistryFallback()
{
    var allRegistries = UnityEngine.Resources.FindObjectsOfTypeAll<GameDataRegistry>();
    return (allRegistries != null && allRegistries.Length > 0) ? allRegistries[0] : null;
}
```

This preserves the already-reviewed loaded-object fallback for direct Farm scene play while removing direct `Resources.Load<GameDataRegistry>`.

- [ ] **Step 4: Update static allowlist**

Use Unity MCP `script-update-or-create` to remove these entries from `AllowedRuntimeResourceOrFindUsages` in `Assets/Tests/EditMode/StaticRuntimeUsageGateTests.cs`:

```csharp
"Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs :: var fromResources = UnityEngine.Resources.Load<GameDataRegistry>(\"GameDataRegistry\");",
"Assets/Scripts/Game/Managers/DataManager.cs :: Registry = UnityEngine.Resources.Load<GameDataRegistry>(\"GameDataRegistry\");",
"Assets/Scripts/Game/WorldGeneration/SeededFarmWorldApplier.cs :: registry = global::UnityEngine.Resources.Load<GameDataRegistry>(\"GameDataRegistry\");",
"Assets/Scripts/UI/HUD/FarmCharacterHudOverlayInstaller.cs :: var registry = Rootborn.Game.Managers.Managers.Data?.Registry ?? Resources.Load<GameDataRegistry>(\"GameDataRegistry\");",
```

At this task boundary, also extend `MigratedRegistryProviderCallers` to include the newly migrated runtime files while leaving `DataManager` out until Task 5:

```csharp
private static readonly HashSet<string> MigratedRegistryProviderCallers = new HashSet<string>
{
    "Assets/Scripts/UI/StudentLife/CampaignRuntimeInstaller.cs",
    "Assets/Scripts/UI/StudentLife/DailyEventRuntimeInstaller.cs",
    "Assets/Scripts/UI/HUD/FarmCharacterHudOverlayInstaller.cs",
    "Assets/Scripts/Game/WorldGeneration/SeededFarmWorldApplier.cs",
    "Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs"
};
```

If `DataManager` still has a temporary compatibility fallback, keep the `DataManager` allowlist entry until Task 5 removes it.

- [ ] **Step 5: Run static gate**

Run:

```powershell
$json = '{"testMode":"EditMode","testClass":"Rootborn.Tests.EditMode.StaticRuntimeUsageGateTests","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path --timeout 300000
Remove-Item -LiteralPath $path -Force
```

Expected: PASS. `DataManager` is intentionally outside `MigratedRegistryProviderCallers` until Task 5.

- [ ] **Step 6: Commit Farm and World migration**

```powershell
git add Assets/Scripts/UI/HUD/FarmCharacterHudOverlayInstaller.cs Assets/Scripts/Game/WorldGeneration/SeededFarmWorldApplier.cs Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs Assets/Tests/EditMode/StaticRuntimeUsageGateTests.cs
git commit -m "[REFACTOR][TEST] Farm 런타임 Registry fallback 축소"
```

## Task 5: DataManager Fallback Boundary

**Files:**
- Modify: `Assets/Scripts/Game/Managers/DataManager.cs`
- Modify: `Assets/Tests/EditMode/StaticRuntimeUsageGateTests.cs`

- [ ] **Step 1: Remove direct `Resources.Load<GameDataRegistry>` from `DataManager`**

Use Unity MCP `script-update-or-create` to remove this block from `InitAsync`:

```csharp
if (Registry == null)
{
    Registry = UnityEngine.Resources.Load<GameDataRegistry>("GameDataRegistry");
    if (Registry != null)
    {
        Debug.Log("[ROOTBORN/DataManager] Registry loaded via Resources.Load fallback.");
    }
}
```

Keep the existing editor registry path and Addressables load path intact:

```csharp
Registry = LoadEditorRegistry();
if (Registry == null)
{
    Registry = await resource.LoadAsync<GameDataRegistry>(AddrRegistry);
}
if (Registry == null)
{
    Debug.LogError("[ROOTBORN/DataManager] GameDataRegistry not found. Run 'Rootborn -> Setup Everything (One Click)'.");
    return;
}
```

- [ ] **Step 2: Remove DataManager allowlist entry**

Use Unity MCP `script-update-or-create` to remove this line from `AllowedRuntimeResourceOrFindUsages`:

```csharp
"Assets/Scripts/Game/Managers/DataManager.cs :: Registry = UnityEngine.Resources.Load<GameDataRegistry>(\"GameDataRegistry\");",
```

Extend `MigratedRegistryProviderCallers` to include `DataManager`:

```csharp
private static readonly HashSet<string> MigratedRegistryProviderCallers = new HashSet<string>
{
    "Assets/Scripts/UI/StudentLife/CampaignRuntimeInstaller.cs",
    "Assets/Scripts/UI/StudentLife/DailyEventRuntimeInstaller.cs",
    "Assets/Scripts/UI/HUD/FarmCharacterHudOverlayInstaller.cs",
    "Assets/Scripts/Game/WorldGeneration/SeededFarmWorldApplier.cs",
    "Assets/Scripts/Game/Bootstrap/FarmAutoFiller.cs",
    "Assets/Scripts/Game/Managers/DataManager.cs"
};
```

- [ ] **Step 3: Run provider and static tests**

Run:

```powershell
$json = '{"testMode":"EditMode","testClass":"Rootborn.Tests.EditMode.Registry.GameDataRegistryProviderTests","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path --timeout 300000
Remove-Item -LiteralPath $path -Force

$json = '{"testMode":"EditMode","testClass":"Rootborn.Tests.EditMode.StaticRuntimeUsageGateTests","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path --timeout 300000
Remove-Item -LiteralPath $path -Force
```

Expected: both suites PASS.

- [ ] **Step 4: Commit DataManager boundary**

```powershell
git add Assets/Scripts/Game/Managers/DataManager.cs Assets/Tests/EditMode/StaticRuntimeUsageGateTests.cs
git commit -m "[REFACTOR][TEST] DataManager Registry fallback 경계 정리"
```

## Task 6: Player-Facing PlayMode Scenario

**Files:**
- Create: `Assets/Tests/PlayMode/EndToEnd/RegistryProviderUnifiedFlowE2ETests.cs`

- [ ] **Step 1: Write the PlayMode E2E test**

Use Unity MCP `script-update-or-create` to create `Assets/Tests/PlayMode/EndToEnd/RegistryProviderUnifiedFlowE2ETests.cs`:

```csharp
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rootborn.Game.Common;
using Rootborn.Game.Managers;
using Rootborn.Game.Player;
using Rootborn.Game.Save;
using Rootborn.UI.MainMenu;
using Rootborn.UI.StudentLife;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rootborn.Tests.PlayMode.EndToEnd
{
    public sealed class RegistryProviderUnifiedFlowE2ETests : InputTestFixture
    {
        private const string TestSlotId = "slot-0";
        private string _saveRoot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            _saveRoot = Path.Combine(Application.temporaryCachePath, "rootborn-registry-provider-e2e", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);
            SaveService.SetRootDirectoryForTests(_saveRoot);
            SaveSlotSelectPanel.SetSaveRootForTests(_saveRoot);
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            GameDataRegistryProvider.ClearOverrideForTests();
            GameDataRegistryProvider.ResetCacheForTests();
            InputSystemUiModulePlayModeTestGuard.UseRealInputSystemForCurrentTest();
            yield return null;
        }

        [UnityTearDown]
        public new IEnumerator TearDown()
        {
            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            SaveService.SetRootDirectoryForTests(null);
            SaveSlotSelectPanel.SetSaveRootForTests(null);
            GameDataRegistryProvider.ClearOverrideForTests();
            GameDataRegistryProvider.ResetCacheForTests();
            InputSystemUiModulePlayModeTestGuard.UninstallForCurrentTest();
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        [UnityTest]
        public IEnumerator REGISTRY_PROVIDER_PM_001_MainMenuTownFlowUsesUnifiedProviderAfterReload()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return StartNewTownFromSaveSlotUi();
            yield return WaitForManagersRegistry(10f);

            Assert.IsTrue(GameDataRegistryProvider.TryGet(out var registry), "Unified provider must resolve the active registry after Town boot.");
            Assert.AreSame(Managers.Data.Registry, registry, "Provider must return the same registry instance as Managers.Data.");
            Assert.GreaterOrEqual(registry.Campaigns.Length, 1, "Registry must expose campaign data for migrated runtime installers.");
            Assert.GreaterOrEqual(registry.DailyEvents.Length, 1, "Registry must expose daily event data for migrated runtime installers.");

            var player = GameObject.Find("Player");
            Assert.IsNotNull(player, "Town must spawn the player through the real new-game flow.");
            yield return PressTab(keyboard);
            yield return null;

            var campaignHud = Object.FindFirstObjectByType<CampaignHudPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(campaignHud, "Campaign runtime installer must bind through provider-backed registry data.");
            yield return CaptureGameViewEvidence("registry-provider-town-flow.png");

            ActiveSaveContext.Clear();
            PlayerGlobalState.ClearForTests();
            yield return LoadScene("MainMenu");
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            yield return ClickButtonWithPointer(FindButton("SaveSlotCard_" + TestSlotId, "LoadButton"));
            yield return WaitForScene("Town", 15f);
            yield return WaitForManagersRegistry(10f);

            Assert.IsTrue(GameDataRegistryProvider.TryGet(out var reloadedRegistry), "Provider must resolve registry after save-slot reload.");
            Assert.AreSame(Managers.Data.Registry, reloadedRegistry, "Reloaded provider registry must come from Managers.Data.");
            Assert.GreaterOrEqual(reloadedRegistry.Campaigns.Length, 1);
        }

        private static IEnumerator StartNewTownFromSaveSlotUi()
        {
            yield return LoadScene("MainMenu");
            SaveSlotSelectPanel.EnsureInScene().Show();
            yield return null;
            yield return ClickButtonWithPointer(FindButton("SaveSlotCard_" + TestSlotId, "NewGameButton"));
            yield return null;
            yield return ClickButtonWithPointer(FindButton("SpumCharacterCreatorPanel", "ConfirmButton"));
            yield return WaitForScene("Town", 15f);
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, sceneName + " must be in BuildSettings.");
            while (!op.isDone) yield return null;
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

        private static IEnumerator WaitForManagersRegistry(float timeoutSeconds)
        {
            float elapsed = 0f;
            while ((Managers.Data == null || Managers.Data.Registry == null) && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsNotNull(Managers.Data, "Managers.Data must be initialized.");
            Assert.IsNotNull(Managers.Data.Registry, "Managers.Data.Registry must be initialized.");
        }

        private static Button FindButton(string rootName, string buttonName)
        {
            var root = GameObject.Find(rootName);
            Assert.IsNotNull(root, rootName + " must exist.");
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name == buttonName) return buttons[i];
            }
            Assert.Fail(buttonName + " was not found under " + rootName + ".");
            return null;
        }

        private static IEnumerator ClickButtonWithPointer(Button button)
        {
            Assert.IsNotNull(button);
            Assert.IsTrue(button.gameObject.activeInHierarchy);
            Assert.IsTrue(button.interactable);
            var eventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            Assert.IsNotNull(eventSystem, "An EventSystem is required for player-facing pointer clicks.");
            Vector2 screenPoint = FindRaycastPointFor(button.gameObject, eventSystem);
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            var driver = new GameObject("RegistryProviderPointerClickDriver").AddComponent<MouseClickFrameDriver>();
            driver.Configure(mouse, screenPoint);
            yield return null;
            yield return null;
            yield return null;
            InputSystem.RemoveDevice(mouse);
            yield return null;
        }

        private IEnumerator PressTab(Keyboard keyboard)
        {
            Press(keyboard.tabKey);
            yield return null;
            Release(keyboard.tabKey);
            yield return null;
        }

        private static Vector2 FindRaycastPointFor(GameObject target, EventSystem eventSystem)
        {
            var results = new System.Collections.Generic.List<RaycastResult>();
            for (int yStep = 1; yStep <= 9; yStep++)
            {
                for (int xStep = 1; xStep <= 9; xStep++)
                {
                    var point = new Vector2(Screen.width * xStep / 10f, Screen.height * yStep / 10f);
                    var eventData = new PointerEventData(eventSystem) { position = point };
                    results.Clear();
                    eventSystem.RaycastAll(eventData, results);
                    for (int i = 0; i < results.Count; i++)
                    {
                        var hit = results[i].gameObject;
                        if (hit == target || hit.transform.IsChildOf(target.transform))
                        {
                            return point;
                        }
                    }
                }
            }

            Assert.Fail("Could not find a screen point that raycasts to " + target.name + ".");
            return Vector2.zero;
        }

        [DefaultExecutionOrder(-10000)]
        private sealed class MouseClickFrameDriver : MonoBehaviour
        {
            private Mouse _mouse;
            private Vector2 _screenPosition;
            private int _frame;

            public void Configure(Mouse mouse, Vector2 screenPosition)
            {
                _mouse = mouse;
                _screenPosition = screenPosition;
            }

            private void Update()
            {
                if (_mouse == null)
                {
                    Destroy(gameObject);
                    return;
                }

                _mouse.MakeCurrent();
                if (_frame == 0)
                {
                    InputSystem.QueueStateEvent(_mouse, new MouseState { position = _screenPosition });
                    InputSystem.Update();
                    _frame++;
                    return;
                }

                if (_frame == 1)
                {
                    InputSystem.QueueStateEvent(_mouse, new MouseState { position = _screenPosition, buttons = 1 });
                    InputSystem.Update();
                    _frame++;
                    return;
                }

                InputSystem.QueueStateEvent(_mouse, new MouseState { position = _screenPosition });
                InputSystem.Update();
                Destroy(gameObject);
            }
        }

        private static IEnumerator CaptureGameViewEvidence(string fileName)
        {
            string directory = Path.Combine(Application.dataPath, "..", "production", "qa", "evidence");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float elapsed = 0f;
            while (!File.Exists(path) && elapsed < 3f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(File.Exists(path), "Registry provider Game View screenshot was not captured: " + path);
        }
    }
}
```

- [ ] **Step 2: Run the PlayMode test**

Run:

```powershell
$json = '{"testMode":"PlayMode","testClass":"Rootborn.Tests.PlayMode.EndToEnd.RegistryProviderUnifiedFlowE2ETests","includePassingTests":true,"includeMessages":true,"includeStacktrace":true,"includeLogs":true,"logType":"Error"}'
$path = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + '.json')
[System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
unity-mcp-cli.cmd run-tool tests-run --input-file $path --timeout 600000
Remove-Item -LiteralPath $path -Force
```

Expected: PASS and `production/qa/evidence/registry-provider-town-flow.png` exists.

- [ ] **Step 3: Commit PlayMode scenario**

```powershell
git add Assets/Tests/PlayMode/EndToEnd/RegistryProviderUnifiedFlowE2ETests.cs Assets/Tests/PlayMode/EndToEnd/RegistryProviderUnifiedFlowE2ETests.cs.meta production/qa/evidence/registry-provider-town-flow.png
git commit -m "[TEST] Registry Provider 실제 플레이 흐름 검증 추가"
```

## Task 7: QA Simulation Script

**Files:**
- Create: `Scripts/qa/run-registry-provider-playmode-simulation.ps1`

- [ ] **Step 1: Add the QA script**

Use `apply_patch` to create `Scripts/qa/run-registry-provider-playmode-simulation.ps1`:

```powershell
param(
    [string]$RunName = ("registry-provider-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string]$SaveSlot = "registry-provider-slot"
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$logDir = Join-Path $root ("Builds\Logs\" + $RunName)
$evidenceDir = Join-Path $root ("production\qa\evidence\" + $RunName)
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null

function Invoke-UnityMcpTest {
    param(
        [string]$Mode,
        [string]$ClassName,
        [string]$Name
    )

    $payload = @{
        testMode = $Mode
        testClass = $ClassName
        includePassingTests = $true
        includeMessages = $true
        includeStacktrace = $true
        includeLogs = $true
        logType = "Error"
    } | ConvertTo-Json -Compress

    $inputPath = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + ".json")
    [System.IO.File]::WriteAllText($inputPath, $payload, [System.Text.UTF8Encoding]::new($false))
    try {
        $outputPath = Join-Path $logDir ($Name + ".txt")
        unity-mcp-cli.cmd run-tool tests-run --input-file $inputPath --timeout 600000 | Tee-Object -FilePath $outputPath
        if ($LASTEXITCODE -ne 0) {
            throw "Unity MCP test failed: $Name"
        }
        $text = Get-Content -Raw -LiteralPath $outputPath
        if ($text -notmatch "Passed|pass|passed") {
            throw "Unity MCP output did not contain a pass signal: $Name"
        }
    }
    finally {
        Remove-Item -LiteralPath $inputPath -Force -ErrorAction SilentlyContinue
    }
}

Push-Location $root
try {
    Invoke-UnityMcpTest -Mode "EditMode" -ClassName "Rootborn.Tests.EditMode.Registry.GameDataRegistryProviderTests" -Name "editmode-provider"
    Invoke-UnityMcpTest -Mode "EditMode" -ClassName "Rootborn.Tests.EditMode.StaticRuntimeUsageGateTests" -Name "editmode-static-gates"
    Invoke-UnityMcpTest -Mode "PlayMode" -ClassName "Rootborn.Tests.PlayMode.EndToEnd.RegistryProviderUnifiedFlowE2ETests" -Name "playmode-provider-flow"

    & "C:\Program Files\Git\bin\bash.exe" "Scripts/ci/check-no-entity-id-branching.sh" | Tee-Object -FilePath (Join-Path $logDir "entity-branching.txt")
    if ($LASTEXITCODE -ne 0) {
        throw "Entity branching gate failed."
    }

    $sourceEvidence = Join-Path $root "production\qa\evidence\registry-provider-town-flow.png"
    if (Test-Path -LiteralPath $sourceEvidence) {
        Copy-Item -LiteralPath $sourceEvidence -Destination (Join-Path $evidenceDir "registry-provider-town-flow.png") -Force
    }

    "Result: Passed" | Tee-Object -FilePath (Join-Path $logDir "summary.txt")
    "RunName: $RunName" | Tee-Object -FilePath (Join-Path $logDir "summary.txt") -Append
    "SaveSlot: $SaveSlot" | Tee-Object -FilePath (Join-Path $logDir "summary.txt") -Append
    "EvidenceDir: $evidenceDir" | Tee-Object -FilePath (Join-Path $logDir "summary.txt") -Append
}
finally {
    Pop-Location
}
```

- [ ] **Step 2: Run PowerShell parser check**

Run:

```powershell
$tokens = $null
$errors = $null
[System.Management.Automation.Language.Parser]::ParseFile('Scripts\qa\run-registry-provider-playmode-simulation.ps1', [ref]$tokens, [ref]$errors) | Out-Null
if ($errors.Count -gt 0) { $errors | Format-List; exit 1 }
```

Expected: no parser errors.

- [ ] **Step 3: Run the QA simulation**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts\qa\run-registry-provider-playmode-simulation.ps1 -RunName registry-provider-verify-20260519 -SaveSlot registry-provider-verify-20260519
```

Expected: `Result: Passed`, logs in `Builds/Logs/registry-provider-verify-20260519`, screenshot copied under `production/qa/evidence/registry-provider-verify-20260519`.

- [ ] **Step 4: Commit QA script**

```powershell
git add Scripts/qa/run-registry-provider-playmode-simulation.ps1
git commit -m "[TEST] Registry Provider QA 시뮬레이션 추가"
```

## Task 8: Final Verification and Audit

**Files:**
- Create: `docs/superpowers/audits/2026-05-19-registry-provider-unification-verification.md`

- [ ] **Step 1: Run final focused gates**

Run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts\qa\run-registry-provider-playmode-simulation.ps1 -RunName registry-provider-final-20260519 -SaveSlot registry-provider-final-20260519
```

Expected: PASS.

- [ ] **Step 2: Run diff checks**

Run:

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors. `git status --short` may still show pre-existing `D Assets/AddressableAssetsData/link.xml` and `.meta`; do not stage or revert those unless explicitly assigned.

- [ ] **Step 3: Write verification audit**

Use `apply_patch` to create `docs/superpowers/audits/2026-05-19-registry-provider-unification-verification.md`:

```markdown
# Registry Provider Unification Verification

Date: 2026-05-19

## Scope

Verified runtime `GameDataRegistry` access through `GameDataRegistryProvider`, migrated high-risk direct registry fallbacks, and proved the change through player-facing PlayMode flow.

## Commands

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts\qa\run-registry-provider-playmode-simulation.ps1 -RunName registry-provider-final-20260519 -SaveSlot registry-provider-final-20260519
```

## Result

Result: Passed

- Provider EditMode tests: Passed.
- Static runtime usage gates: Passed.
- Registry provider PlayMode E2E: Passed.
- Entity branching gate: Passed.
- Game View evidence captured.

## Evidence

- Logs: `Builds/Logs/registry-provider-final-20260519`
- Screenshot: `production/qa/evidence/registry-provider-final-20260519/registry-provider-town-flow.png`

## Remaining Legacy Scope

`Assets/Resources/GameDataRegistry.asset` remains in the project for legacy tests and old Farm/Crop paths. The completed slice removes migrated runtime direct registry fallback paths but does not delete the legacy asset.

## Dirty Worktree Note

Pre-existing `Assets/AddressableAssetsData/link.xml` deletion was not part of this work and was left untouched.
```

- [ ] **Step 4: Commit final audit**

```powershell
git add docs/superpowers/audits/2026-05-19-registry-provider-unification-verification.md
git commit -m "[DOCS][TEST] Registry Provider 검증 결과 기록"
```

## Self-Review Checklist

- Spec coverage:
  - Provider API: Tasks 1 and 2.
  - Migrated callers: Tasks 3, 4, and 5.
  - PlayMode player-facing scenario: Task 6.
  - QA simulation: Task 7.
  - Static gates and entity branching: Tasks 3, 5, 7, and 8.
  - Performance constraints: Provider caches result and callers resolve at initialization/refresh points.
  - Remaining legacy paths documented: Task 8.
- Completeness scan: no incomplete task markers beyond executable checkbox steps.
- Type consistency:
  - `GameDataRegistryProvider.TryGet(out var registry)` is used consistently.
  - Test reset methods are `ClearOverrideForTests` and `ResetCacheForTests`.
  - PlayMode class name matches QA script: `Rootborn.Tests.PlayMode.EndToEnd.RegistryProviderUnifiedFlowE2ETests`.
