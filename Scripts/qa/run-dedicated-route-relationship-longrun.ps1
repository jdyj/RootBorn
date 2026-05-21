param(
    [string]$ServerExecutable = "Builds\Server\Windows\rootborn-server.exe",
    [string]$ClientExecutable = "Builds\Client\Windows\rootborn.exe",
    [string]$RunName = "network-dedicated-route-relationship-longrun",
    [int]$Cycles = 3,
    [int]$StartPort = 7881,
    [int]$WaitSeconds = 100,
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
            throw "Missing required dedicated long-run artifact: $Description at $Path. Rebuild with Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64 and Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64 before running run-dedicated-route-relationship-longrun.ps1."
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
            "Assets\Scripts\Game\Common\DirectValidationRouteRelationshipAutoplayInstaller.cs",
            "Scripts\qa\run-dedicated-route-relationship-longrun.ps1"
        )

        foreach ($sourcePath in $requiredSources) {
            $source = Assert-RequiredArtifact $sourcePath "required source for run-dedicated-route-relationship-longrun.ps1"
            if ($serverGameDll.LastWriteTime -lt $source.LastWriteTime) {
                throw "Stale server Rootborn.Game.dll for run-dedicated-route-relationship-longrun.ps1. $($serverGameDll.FullName) is older than $sourcePath. Rebuild with Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64."
            }

            if ($clientGameDll.LastWriteTime -lt $source.LastWriteTime) {
                throw "Stale client Rootborn.Game.dll for run-dedicated-route-relationship-longrun.ps1. $($clientGameDll.FullName) is older than $sourcePath. Rebuild with Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64."
            }
        }
    }

    Assert-FreshBuildArtifacts

    if ($Cycles -lt 2) {
        throw "Cycles must be at least 2 so the long-run includes one action pass and one observe-only reload pass."
    }

    if ([string]::IsNullOrWhiteSpace($RunName)) {
        $RunName = "network-dedicated-route-relationship-longrun"
    }

    if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
        $SaveSlot = $RunName
    }

    $logsRoot = "Builds\Logs"
    New-Item -ItemType Directory -Path $logsRoot -Force | Out-Null
    $logRoot = Resolve-Path $logsRoot
    $logDir = Join-Path $logRoot $RunName
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null

    $allLogs = New-Object System.Collections.Generic.List[string]
    $allScreenshots = New-Object System.Collections.Generic.List[string]

    function Assert-PortAvailable {
        param([int]$Port)

        $portUse = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
        if ($portUse) {
            $summary = $portUse | Select-Object -First 5 | ForEach-Object { "local=$($_.LocalAddress):$($_.LocalPort) state=$($_.State) owningProcess=$($_.OwningProcess)" }
            throw "Port $Port is already in use.`n$($summary -join [Environment]::NewLine)"
        }
    }

    function Start-ServerProcess {
        param(
            [int]$Port,
            [string]$LogFile
        )

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
            [int]$Port,
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

    function Assert-NoTrackedProcessesAlive {
        param([System.Diagnostics.Process[]]$Processes)

        foreach ($process in $Processes) {
            if ($process -and -not $process.HasExited) {
                throw "Tracked process survived cleanup: id=$($process.Id) name=$($process.ProcessName)"
            }
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

    function Invoke-LongRunCycle {
        param(
            [int]$Cycle,
            [switch]$ObserveOnly
        )

        $port = $StartPort + $Cycle - 1
        Assert-PortAvailable $port

        $cycleName = "network-dedicated-route-relationship-longrun-cycle-{0:D2}" -f $Cycle
        $serverLog = Join-Path $logDir "$cycleName-server.log"
        $clientLog = Join-Path $logDir "$cycleName-client.log"
        $screenshot = Join-Path $logDir "$cycleName.png"
        Remove-Item -LiteralPath $serverLog, $clientLog, $screenshot -Force -ErrorAction SilentlyContinue

        $processes = @()
        try {
            $processes += Start-ServerProcess $port $serverLog
            Start-Sleep -Seconds 10
            $processes += Start-ClientProcess $port $clientLog $screenshot -ObserveOnly:$ObserveOnly
            Start-Sleep -Seconds $WaitSeconds
        }
        finally {
            Stop-TrackedProcesses $processes
        }

        Assert-NoTrackedProcessesAlive $processes
        Assert-LogContains $serverLog "Dedicated server started - port=$port maxPlayers=4 saveSlot=$SaveSlot" "Cycle $Cycle dedicated server did not start"
        Assert-LogContains $serverLog "Dedicated server client connected id=1" "Cycle $Cycle dedicated server did not observe client"
        Assert-LogContains $clientLog "Client started - joinIp=127.0.0.1 port=$port" "Cycle $Cycle client did not start"
        Assert-LogContains $clientLog "Client connected callback" "Cycle $Cycle client did not connect"
        Assert-AnyLogContains @($serverLog, $clientLog) "scene=Town|scene event type=LoadEventCompleted scene=Town" "Cycle $Cycle did not synchronize Town"
        Assert-Screenshot $screenshot "Cycle $Cycle screenshot evidence missing"

        if ($ObserveOnly) {
            Assert-LogContains $clientLog "autoplay completed route-relationship-observe" "Cycle $Cycle observe-only autoplay did not complete"
        }
        else {
            Assert-LogContains $clientLog "autoplay completed route-relationship" "Cycle $Cycle route/relationship autoplay did not complete"
        }

        Assert-LogContains $clientLog "dailyPlan=day:.*selected:(?!empty)" "Cycle $Cycle did not log a selected Daily Plan route"
        Assert-LogContains $clientLog "relationships=(?!empty)" "Cycle $Cycle did not log relationship state"

        $allLogs.Add($serverLog) | Out-Null
        $allLogs.Add($clientLog) | Out-Null
        $allScreenshots.Add($screenshot) | Out-Null
    }

    Invoke-LongRunCycle 1
    Start-Sleep -Seconds 3

    for ($cycle = 2; $cycle -le $Cycles; $cycle++) {
        Invoke-LongRunCycle $cycle -ObserveOnly
        Start-Sleep -Seconds 3
    }

    Assert-NoFatalLogs $allLogs.ToArray()

    [PSCustomObject]@{
        Result = "Passed"
        ServerExecutable = $serverExe.Path
        ClientExecutable = $clientExe.Path
        LogDir = $logDir
        Cycles = $Cycles
        StartPort = $StartPort
        SaveSlot = $SaveSlot
        Screenshots = $allScreenshots.Count
    } | Format-List
}
finally {
    Pop-Location
}
