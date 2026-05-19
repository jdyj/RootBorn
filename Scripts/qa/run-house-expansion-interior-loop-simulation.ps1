param(
    [string]$RunName = "",
    [string]$SaveSlot = "",
    [string]$EvidenceDir = "",
    [int]$ReloadCount = 1
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $repoRoot
try {
    if ([string]::IsNullOrWhiteSpace($RunName)) {
        $RunName = "house-expansion-loop-" + (Get-Date -Format "yyyyMMdd-HHmmss")
    }

    if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
        $SaveSlot = $RunName
    }

    if ([string]::IsNullOrWhiteSpace($EvidenceDir)) {
        $EvidenceDir = Join-Path "production\qa\evidence" $RunName
    }

    New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null

    $resultPath = Join-Path $EvidenceDir "playmode-results.txt"
    if (Test-Path -LiteralPath $resultPath) {
        Remove-Item -LiteralPath $resultPath -Force
    }

    $methods = @(
        "Rootborn.Tests.PlayMode.Interiors.HouseExpansionInteriorLoopPlayModeTests.HOUSE_LOOP_PM_001_HireExpansionUpdatesHouseBoundsAndPlacementSurface",
        "Rootborn.Tests.PlayMode.Interiors.HouseExpansionInteriorLoopPlayModeTests.HOUSE_LOOP_PM_003_FurniturePlaceRotateMovePersistsAfterReload"
    )

    foreach ($method in $methods) {
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            $stateInputPath = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + ".json")
            [System.IO.File]::WriteAllText($stateInputPath, "{}", [System.Text.UTF8Encoding]::new($false))
            try {
                $previousErrorActionPreference = $ErrorActionPreference
                $ErrorActionPreference = "Continue"
                $stateOutput = & unity-mcp-cli.cmd run-tool editor-application-get-state --input-file $stateInputPath --timeout 60000 2>&1
                $ErrorActionPreference = $previousErrorActionPreference
                if (($stateOutput -match '"IsPlaying": false') -and ($stateOutput -match '"IsPlayingOrWillChangePlaymode": false') -and ($stateOutput -match '"IsUpdating": false')) {
                    break
                }
            }
            finally {
                if (Test-Path -LiteralPath $stateInputPath) {
                    Remove-Item -LiteralPath $stateInputPath -Force
                }
            }

            Start-Sleep -Seconds 1
        }

        $sceneJson = @{
            sceneRef = @{
                instanceID = 0
                assetPath = "Assets/Scenes/Farm.unity"
                assetType = "UnityEngine.SceneAsset"
            }
            loadSceneMode = "Single"
        } | ConvertTo-Json -Depth 5 -Compress
        $sceneInputPath = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + ".json")
        [System.IO.File]::WriteAllText($sceneInputPath, $sceneJson, [System.Text.UTF8Encoding]::new($false))
        try {
            & unity-mcp-cli.cmd run-tool scene-open --input-file $sceneInputPath --timeout 60000 | Tee-Object -FilePath $resultPath -Append
        }
        finally {
            if (Test-Path -LiteralPath $sceneInputPath) {
                Remove-Item -LiteralPath $sceneInputPath -Force
            }
        }

        $json = @{
            testMode = "PlayMode"
            testNamespace = "Rootborn.Tests.PlayMode.Interiors"
            testClass = "HouseExpansionInteriorLoopPlayModeTests"
            testMethod = $method
            includePassingTests = $true
            includeMessages = $true
            includeStacktrace = $true
            includeLogs = $true
            logType = "Error"
        } | ConvertTo-Json -Compress

        $inputPath = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + ".json")
        [System.IO.File]::WriteAllText($inputPath, $json, [System.Text.UTF8Encoding]::new($false))
        try {
            "=== $method ===" | Tee-Object -FilePath $resultPath -Append
            $previousErrorActionPreference = $ErrorActionPreference
            $ErrorActionPreference = "Continue"
            $output = & unity-mcp-cli.cmd run-tool tests-run --input-file $inputPath --timeout 300000 2>&1
            $exitCode = $LASTEXITCODE
            $ErrorActionPreference = $previousErrorActionPreference
            $output | Tee-Object -FilePath $resultPath -Append
            if ($exitCode -ne 0 -or ($output -match "ERROR:") -or -not ($output -match '"Status": "Passed"')) {
                throw "PlayMode method failed or timed out: $method"
            }
        }
        finally {
            if (Test-Path -LiteralPath $inputPath) {
                Remove-Item -LiteralPath $inputPath -Force
            }
        }
    }

    $probe = @{
        RunName = $RunName
        SaveSlot = $SaveSlot
        ReloadCount = $ReloadCount
        EvidenceDir = (Resolve-Path $EvidenceDir).Path
        RequiredScreenshots = @(
            "after-expansion.png",
            "after-placement.png",
            "after-reload.png"
        )
    } | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText((Join-Path $EvidenceDir "simulation-probe.json"), $probe, [System.Text.UTF8Encoding]::new($false))

    [PSCustomObject]@{
        Result = "Passed"
        RunName = $RunName
        SaveSlot = $SaveSlot
        EvidenceDir = (Resolve-Path $EvidenceDir).Path
    } | Format-List
}
finally {
    Pop-Location
}
