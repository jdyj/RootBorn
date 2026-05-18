param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe",
    [int]$SmokePort = 7841,
    [int]$SmokeWaitSeconds = 125,
    [string]$SmokeRunName = "",
    [switch]$SkipClientBuild,
    [switch]$SkipServerBuild,
    [switch]$RequireDedicatedServer
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $repoRoot
try {
    if ([string]::IsNullOrWhiteSpace($SmokeRunName)) {
        $SmokeRunName = "direct-multiplayer-ci-" + (Get-Date -Format "yyyyMMdd-HHmmss")
    }

    $logsRoot = Resolve-Path "Builds\Logs"
    $clientBuildLog = Join-Path $logsRoot "$SmokeRunName-client-build.log"
    $serverBuildLog = Join-Path $logsRoot "$SmokeRunName-server-build.log"

    function Assert-UnityProjectUnlocked {
        $unityProjectProcesses = Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" |
            Where-Object { $_.CommandLine -like "*$repoRoot*" }

        if ($unityProjectProcesses) {
            $summary = $unityProjectProcesses | ForEach-Object { "pid=$($_.ProcessId) $($_.CommandLine)" }
            throw "Unity project is already open. Close it before running this gate.`n$($summary -join [Environment]::NewLine)"
        }
    }

    function Invoke-UnityBuild {
        param(
            [string]$ExecuteMethod,
            [string]$LogFile
        )

        Assert-UnityProjectUnlocked
        & $UnityPath -batchmode -nographics -projectPath $repoRoot -executeMethod $ExecuteMethod -logFile $LogFile -quit

        $unityProjectProcesses = Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" |
            Where-Object { $_.CommandLine -like "*$repoRoot*" }
        foreach ($process in $unityProjectProcesses) {
            Wait-Process -Id $process.ProcessId -Timeout 1800
        }
    }

    function Assert-LogContains {
        param(
            [string]$Path,
            [string]$Pattern,
            [string]$Message
        )

        if (-not (Test-Path $Path)) {
            throw "Missing log file: $Path"
        }

        if (-not (Select-String -Path $Path -Pattern $Pattern -Quiet)) {
            throw "$Message ($Path :: $Pattern)"
        }
    }

    if (-not $SkipClientBuild) {
        Invoke-UnityBuild "Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64" $clientBuildLog
        Assert-LogContains $clientBuildLog "Build Finished, Result: Success" "Client build did not finish successfully"
        Assert-LogContains $clientBuildLog "\[ROOTBORN\] Client build .* result=Succeeded" "Client build did not report Rootborn success"
    }
    else {
        Write-Host "Skipping client build."
    }

    powershell.exe -ExecutionPolicy Bypass -File "Scripts\qa\run-direct-multiplayer-smoke.ps1" `
        -Port $SmokePort `
        -RunName $SmokeRunName `
        -WaitSeconds $SmokeWaitSeconds

    if (-not $SkipServerBuild) {
        Invoke-UnityBuild "Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64" $serverBuildLog

        if (Select-String -Path $serverBuildLog -Pattern "\[ROOTBORN\] Server build .* result=Succeeded" -Quiet) {
            Write-Host "Dedicated server build passed."
            if ($RequireDedicatedServer) {
                powershell.exe -ExecutionPolicy Bypass -File "Scripts\qa\run-dedicated-multiplayer-smoke.ps1" `
                    -Port ($SmokePort + 1) `
                    -RunName "$SmokeRunName-dedicated" `
                    -WaitSeconds $SmokeWaitSeconds
            }
        }
        elseif (Select-String -Path $serverBuildLog -Pattern "Dedicated Server support for Win is not installed" -Quiet) {
            $message = "Dedicated server build is blocked because Unity Windows Dedicated Server support is not installed."
            if ($RequireDedicatedServer) {
                throw $message
            }

            Write-Warning $message
        }
        else {
            throw "Dedicated server build failed for an unexpected reason. See $serverBuildLog"
        }
    }
    else {
        Write-Host "Skipping dedicated server build."
    }

    [PSCustomObject]@{
        Result = "Passed"
        ClientBuildLog = if ($SkipClientBuild) { "" } else { $clientBuildLog }
        SmokeLogDir = Join-Path $logsRoot $SmokeRunName
        DedicatedSmokeLogDir = if ($RequireDedicatedServer -and -not $SkipServerBuild) { Join-Path $logsRoot "$SmokeRunName-dedicated" } else { "" }
        ServerBuildLog = if ($SkipServerBuild) { "" } else { $serverBuildLog }
        DedicatedServerRequired = [bool]$RequireDedicatedServer
    } | Format-List
}
finally {
    Pop-Location
}
