param(
    [string]$ServerExecutable = "Builds\Server\Windows\rootborn-server.exe",
    [string]$ClientExecutable = "Builds\Client\Windows\rootborn.exe",
    [int]$Port = 7871,
    [int]$WaitSeconds = 100,
    [string]$RunName = "",
    [string]$SaveSlot = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $repoRoot
try {
    $serverExe = Resolve-Path $ServerExecutable
    $clientExe = Resolve-Path $ClientExecutable

    function Assert-RequiredArtifact {
        param(
            [string]$Path,
            [string]$Description
        )

        if (-not (Test-Path -LiteralPath $Path)) {
            throw "Missing required dedicated smoke artifact: $Description at $Path. Rebuild with Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64 and Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64 before running run-dedicated-route-relationship-smoke.ps1."
        }

        return Get-Item -LiteralPath $Path
    }

    function Assert-FreshBuildArtifacts {
        $serverGameDll = Assert-RequiredArtifact "Builds\Server\Windows\rootborn-server_Data\Managed\Rootborn.Game.dll" "server Rootborn.Game.dll"
        $clientGameDll = Assert-RequiredArtifact "Builds\Client\Windows\rootborn_Data\Managed\Rootborn.Game.dll" "client Rootborn.Game.dll"
        Assert-RequiredArtifact $serverExe.Path "server executable" | Out-Null
        Assert-RequiredArtifact $clientExe.Path "client executable" | Out-Null
        Assert-RequiredArtifact "Builds\Server\Windows\rootborn-server_Data\StreamingAssets\aa\settings.json" "server Addressables runtime settings" | Out-Null
        Assert-RequiredArtifact "Builds\Client\Windows\rootborn_Data\StreamingAssets\aa\settings.json" "client Addressables runtime settings" | Out-Null

        $requiredSources = @(
            "Assets\Scripts\Game\Common\DirectValidationRouteRelationshipAutoplayInstaller.cs"
        )

        foreach ($sourcePath in $requiredSources) {
            $source = Assert-RequiredArtifact $sourcePath "required source for run-dedicated-route-relationship-smoke.ps1"
            if ($serverGameDll.LastWriteTime -lt $source.LastWriteTime) {
                throw "Stale server Rootborn.Game.dll for run-dedicated-route-relationship-smoke.ps1. $($serverGameDll.FullName) is older than $sourcePath. Rebuild with Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64."
            }

            if ($clientGameDll.LastWriteTime -lt $source.LastWriteTime) {
                throw "Stale client Rootborn.Game.dll for run-dedicated-route-relationship-smoke.ps1. $($clientGameDll.FullName) is older than $sourcePath. Rebuild with Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64."
            }
        }
    }

    Assert-FreshBuildArtifacts

    if ([string]::IsNullOrWhiteSpace($RunName)) {
        $RunName = "network-dedicated-route-relationship-e2e"
    }

    if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
        $SaveSlot = $RunName
    }

    $portUse = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
    if ($portUse) {
        $summary = $portUse | Select-Object -First 5 | ForEach-Object { "local=$($_.LocalAddress):$($_.LocalPort) state=$($_.State) owningProcess=$($_.OwningProcess)" }
        throw "Port $Port is already in use.`n$($summary -join [Environment]::NewLine)"
    }

    $logsRoot = "Builds\Logs"
    New-Item -ItemType Directory -Path $logsRoot -Force | Out-Null
    $logRoot = Resolve-Path $logsRoot
    $logDir = Join-Path $logRoot $RunName
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null

    $firstScreenshot = Join-Path $logDir "network-dedicated-route-relationship-e2e.png"
    $reloadScreenshot = Join-Path $logDir "network-dedicated-route-relationship-e2e-reload.png"
    $serverFirstLog = Join-Path $logDir "server-first-player.log"
    $clientFirstLog = Join-Path $logDir "client-first-player.log"
    $serverReloadLog = Join-Path $logDir "server-reload-player.log"
    $clientReloadLog = Join-Path $logDir "client-reload-player.log"

    Remove-Item -LiteralPath $firstScreenshot, $reloadScreenshot -Force -ErrorAction SilentlyContinue

    function Start-ServerProcess {
        param([string]$LogFile)

        return Start-Process -FilePath $serverExe -ArgumentList @(
            "-batchmode", "-nographics",
            "-logFile", $LogFile,
            "-mode", "server",
            "-port", "$Port",
            "-maxPlayers", "4",
            "-saveSlot", $SaveSlot,
            "-directValidationTrace"
        ) -PassThru -WindowStyle Hidden
    }

    function Start-ClientProcess {
        param(
            [string]$LogFile,
            [string]$ScreenshotPath,
            [switch]$ObserveOnly
        )

        $autoplayFlag = if ($ObserveOnly) {
            "-directValidationAutoplayRouteRelationshipObserveOnly"
        }
        else {
            "-directValidationAutoplayRouteRelationship"
        }

        return Start-Process -FilePath $clientExe -ArgumentList @(
            "-batchmode", "-nographics",
            "-logFile", $LogFile,
            "-mode", "client",
            "-joinIp", "127.0.0.1",
            "-port", "$Port",
            "-saveSlot", $SaveSlot,
            "-directValidationTrace",
            $autoplayFlag,
            "-directValidationScreenshot", $ScreenshotPath
        ) -PassThru -WindowStyle Hidden
    }

    function Stop-TrackedProcesses {
        param([System.Diagnostics.Process[]]$Processes)

        foreach ($process in $Processes) {
            if ($process -and -not $process.HasExited) {
                Stop-Process -Id $process.Id -Force
                $process.WaitForExit(5000) | Out-Null
            }
        }
    }

    function Invoke-SmokePass {
        param(
            [string]$Phase,
            [string]$ServerLog,
            [string]$ClientLog,
            [string]$ScreenshotPath,
            [switch]$ObserveOnly
        )

        $processes = @()
        try {
            $processes += Start-ServerProcess $ServerLog
            Start-Sleep -Seconds 10
            $processes += Start-ClientProcess $ClientLog $ScreenshotPath -ObserveOnly:$ObserveOnly
            Start-Sleep -Seconds $WaitSeconds
        }
        finally {
            Stop-TrackedProcesses $processes
        }

        Assert-LogContains $ServerLog "Dedicated server started - port=$Port maxPlayers=4 saveSlot=$SaveSlot" "$Phase dedicated server did not start"
        Assert-LogContains $ServerLog "Dedicated server client connected id=1" "$Phase dedicated server did not observe client"
        Assert-LogContains $ClientLog "Client started - joinIp=127.0.0.1 port=$Port" "$Phase client did not start"
        Assert-LogContains $ClientLog "Client connected callback" "$Phase client did not connect"
        Assert-AnyLogContains @($ServerLog, $ClientLog) "scene=Town|scene event type=LoadEventCompleted scene=Town" "$Phase did not synchronize Town"
        Assert-Screenshot $ScreenshotPath "$Phase screenshot evidence missing"
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

    function Assert-AnyLogContains {
        param(
            [string[]]$Paths,
            [string]$Pattern,
            [string]$Message
        )

        if (-not (Select-String -Path $Paths -Pattern $Pattern -Quiet)) {
            throw "$Message ($($Paths -join ', ') :: $Pattern)"
        }
    }

    function Assert-Screenshot {
        param(
            [string]$Path,
            [string]$Message
        )

        if (-not (Test-Path $Path)) {
            throw "${Message}: $Path"
        }

        $item = Get-Item $Path
        if ($item.Length -le 0) {
            throw "${Message}: zero-byte screenshot at $Path"
        }
    }

    function Assert-NoFatalLogs {
        param([string[]]$Paths)

        $fatalPatterns = "StartClient failed|StartHost failed|StartServer failed|Unhandled|NullReferenceException|InvalidOperationException|Exception|ERROR|Failed to bind|Address already in use|autoplay failed"
        $fatalMatches = Select-String -Path $Paths -Pattern $fatalPatterns -CaseSensitive
        if ($fatalMatches) {
            $summary = $fatalMatches | Select-Object -First 12 | ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line)" }
            throw "Fatal log pattern found:`n$($summary -join [Environment]::NewLine)"
        }
    }

    Invoke-SmokePass "first-pass" $serverFirstLog $clientFirstLog $firstScreenshot
    Assert-LogContains $clientFirstLog "autoplay completed route-relationship" "First pass route/relationship autoplay did not complete"
    Assert-LogContains $clientFirstLog "dailyPlan=day:.*selected:(?!empty)" "First pass did not log a selected Daily Plan route"
    Assert-LogContains $clientFirstLog "relationships=(?!empty)" "First pass did not log relationship state"

    Start-Sleep -Seconds 3
    Invoke-SmokePass "reload-observe" $serverReloadLog $clientReloadLog $reloadScreenshot -ObserveOnly
    Assert-LogContains $clientReloadLog "autoplay completed route-relationship-observe" "Reload observe pass did not complete"
    Assert-LogContains $clientReloadLog "dailyPlan=day:.*selected:(?!empty)" "Reload observe pass did not restore selected Daily Plan route"
    Assert-LogContains $clientReloadLog "relationships=(?!empty)" "Reload observe pass did not restore relationship state"

    Assert-NoFatalLogs @($serverFirstLog, $clientFirstLog, $serverReloadLog, $clientReloadLog)

    [PSCustomObject]@{
        Result = "Passed"
        ServerExecutable = $serverExe.Path
        ClientExecutable = $clientExe.Path
        LogDir = $logDir
        Port = $Port
        SaveSlot = $SaveSlot
        FirstScreenshot = $firstScreenshot
        ReloadScreenshot = $reloadScreenshot
    } | Format-List
}
finally {
    Pop-Location
}
