param(
    [string]$RunName = "house-upgrade-interior-loop",
    [int]$TimeoutMilliseconds = 300000
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$testMethod = "Rootborn.Tests.PlayMode.EndToEnd.HouseUpgradeInteriorLoopE2EScenarioTests.HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload"
$screenshotPath = "Builds/Logs/house-upgrade-interior-loop-e2e.png"
$addressablesCheck = "Scripts/ci/check-addressables-linkxml-preserved.ps1"

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Message
    )

    if ($Text -notmatch [regex]::Escape($Pattern)) {
        throw $Message
    }
}

function Restore-AddressablesLinkXml {
    $paths = @(
        "Assets/AddressableAssetsData/link.xml",
        "Assets/AddressableAssetsData/link.xml.meta"
    )

    $needsRestore = $false
    foreach ($path in $paths) {
        if (-not (Test-Path -LiteralPath $path)) {
            $needsRestore = $true
        }
    }

    $status = & git status --short -- Assets/AddressableAssetsData/link.xml Assets/AddressableAssetsData/link.xml.meta
    if ($status) {
        $needsRestore = $true
    }

    if ($needsRestore) {
        & git restore -- Assets/AddressableAssetsData/link.xml Assets/AddressableAssetsData/link.xml.meta
        if ($LASTEXITCODE -ne 0) {
            throw "House QA runner failed: could not restore Addressables link.xml files after Unity test run."
        }
    }
}

Push-Location $repoRoot
try {
    New-Item -ItemType Directory -Path "Builds/Logs" -Force | Out-Null

    if (Test-Path -LiteralPath $screenshotPath) {
        Remove-Item -LiteralPath $screenshotPath -Force
    }

    $json = @{
        testMode = "PlayMode"
        testMethod = $testMethod
        includePassingTests = $false
        includeMessages = $true
        includeStacktrace = $true
        includeLogs = $true
        logType = "Error"
    } | ConvertTo-Json -Compress

    $inputPath = Join-Path $env:TEMP ([System.Guid]::NewGuid().ToString() + ".json")
    [System.IO.File]::WriteAllText($inputPath, $json, [System.Text.UTF8Encoding]::new($false))

    try {
        Write-Host "HOUSE_QA_TEST_METHOD: $testMethod"
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        $output = & unity-mcp-cli.cmd run-tool tests-run --input-file $inputPath --timeout $TimeoutMilliseconds 2>&1
        $exitCode = $LASTEXITCODE
        $ErrorActionPreference = $previousErrorActionPreference
        $outputText = ($output | Out-String)
        $outputText

        if ($exitCode -ne 0) {
            throw "House QA runner failed: Unity MCP tests-run exited with $exitCode."
        }

        Assert-Contains $outputText '"Status": "Passed"' "House QA runner failed: strict House E2E did not report Passed."
    }
    finally {
        if (Test-Path -LiteralPath $inputPath) {
            Remove-Item -LiteralPath $inputPath -Force
        }
    }

    if (-not (Test-Path -LiteralPath $screenshotPath)) {
        throw "House QA runner failed: screenshot evidence is missing: $screenshotPath"
    }

    $screenshot = Get-Item -LiteralPath $screenshotPath
    if ($screenshot.Length -le 0) {
        throw "House QA runner failed: screenshot evidence is empty: $screenshotPath"
    }

    Restore-AddressablesLinkXml
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $addressablesCheck
    if ($LASTEXITCODE -ne 0) {
        throw "House QA runner failed: Addressables preservation check failed."
    }

    $markers = @(
        "HOUSE_QA_STAGE_MARKER: verified-by-HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload",
        "HOUSE_QA_PRESET_MARKER: verified-by-HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload",
        "HOUSE_QA_FURNITURE_LAYOUT_MARKER: verified-by-HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload",
        "HOUSE_QA_RUNTIME_TILEMAP_MARKER: verified-by-HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload",
        "HOUSE_QA_OVERLAY_CLEANUP_MARKER: verified-by-HOUSE_UPGRADE_E2E_001_UpgradePresetPlaceReload",
        "HOUSE_QA_ADDRESSABLES_MARKER: verified-by-check-addressables-linkxml-preserved.ps1"
    )

    $summary = [ordered]@{
        Result = "Passed"
        RunName = $RunName
        TestMethod = $testMethod
        ScreenshotPath = $screenshotPath
        ScreenshotLength = $screenshot.Length
        ScreenshotLastWriteTime = $screenshot.LastWriteTime
        EvidenceMarkers = $markers
    }

    foreach ($marker in $markers) {
        Write-Host $marker
    }

    [PSCustomObject]$summary | Format-List
}
finally {
    Pop-Location
}
