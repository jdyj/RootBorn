param(
    [string]$RunName = ("registry-provider-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string]$SaveSlot = "registry-provider-verify"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$logDir = Join-Path $repoRoot ("production\qa\registry-provider\" + $RunName)
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$env:ROOTBORN_QA_SAVE_SLOT = $SaveSlot

function New-InputFile {
    param(
        [string]$Name,
        [hashtable]$Payload
    )

    $path = Join-Path $logDir ($Name + ".input.json")
    $json = $Payload | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
    return $path
}

function Assert-PassedOutput {
    param(
        [string]$Name,
        [string]$Output
    )

    if ($Output -notmatch '"Status"\s*:\s*"Passed"' -and $Output -notmatch "Status=Passed" -and $Output -notmatch "PassedTests") {
        throw "$Name did not report a passing result. See $logDir"
    }
}

function Invoke-UnityMcpTest {
    param(
        [string]$Name,
        [string]$Mode,
        [string]$Namespace,
        [string]$Method
    )

    $payload = @{
        testMode = $Mode
        includePassingTests = $true
        includeMessages = $true
        includeStacktrace = $true
        includeLogs = $true
        logType = "Error"
    }

    if (-not [string]::IsNullOrWhiteSpace($Namespace)) {
        $payload.testNamespace = $Namespace
    }

    if (-not [string]::IsNullOrWhiteSpace($Method)) {
        $payload.testMethod = $Method
    }

    $inputPath = New-InputFile -Name $Name -Payload $payload
    $outputPath = Join-Path $logDir ($Name + ".txt")
    Write-Host "[registry-provider] running $Name"
    $command = 'unity-mcp-cli.cmd run-tool tests-run --input-file "' + $inputPath + '" --timeout 600000 2>&1'
    $output = & cmd.exe /d /c $command
    $exitCode = $LASTEXITCODE
    $output | Tee-Object -FilePath $outputPath | Out-Host
    if ($exitCode -ne 0) {
        throw "$Name failed with exit code $exitCode. See $outputPath"
    }
    Assert-PassedOutput -Name $Name -Output ($output -join "`n")
}

Push-Location $repoRoot
try {
    Invoke-UnityMcpTest `
        -Name "editmode-registry-provider" `
        -Mode "EditMode" `
        -Namespace "Rootborn.Tests.EditMode.Registry" `
        -Method ""

    Invoke-UnityMcpTest `
        -Name "static-runtime-allowlist" `
        -Mode "EditMode" `
        -Namespace "" `
        -Method "Rootborn.Tests.EditMode.StaticRuntimeUsageGateTests.STATIC_002_RuntimeResourcesAndFindUsages_StayInsideReviewedAllowlist"

    Invoke-UnityMcpTest `
        -Name "static-migrated-registry-callers" `
        -Mode "EditMode" `
        -Namespace "" `
        -Method "Rootborn.Tests.EditMode.StaticRuntimeUsageGateTests.STATIC_003_MigratedRuntimeCallers_DoNotDirectlyResourcesLoadGameDataRegistry"

    Invoke-UnityMcpTest `
        -Name "playmode-mainmenu-town-provider-flow" `
        -Mode "PlayMode" `
        -Namespace "" `
        -Method "Rootborn.Tests.PlayMode.EndToEnd.RegistryProviderUnifiedFlowE2ETests.REGISTRY_PROVIDER_E2E_001_MainMenuNewGameEntersTownWithProviderBackedRuntimeInstallers"

    $entityOutputPath = Join-Path $logDir "entity-branching.txt"
    Write-Host "[registry-provider] running entity branching gate"
    $gitBash = "C:\Program Files\Git\bin\bash.exe"
    if (Test-Path $gitBash) {
        $entityOutput = & $gitBash "Scripts/ci/check-no-entity-id-branching.sh" 2>&1
    } else {
        $entityOutput = & bash "Scripts/ci/check-no-entity-id-branching.sh" 2>&1
    }
    $entityOutput | Tee-Object -FilePath $entityOutputPath | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Entity branching gate failed with exit code $LASTEXITCODE. See $entityOutputPath"
    }

    Write-Host "[registry-provider] PASS logs=$logDir"
}
finally {
    Pop-Location
}
