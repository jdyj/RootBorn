param(
    [string]$ServerExecutable = "Builds\Server\Windows\rootborn-server.exe",
    [string]$ClientExecutable = "Builds\Client\Windows\rootborn.exe",
    [int]$Port = 7861,
    [int]$ClientCount = 4,
    [int]$WaitSeconds = 600,
    [string]$RunName = "",
    [string]$SaveSlot = "",
    [int]$ReconnectClientIndex = 2,
    [int]$DisconnectAfterSeconds = 180,
    [int]$ReconnectAfterSeconds = 45,
    [switch]$RestartServerAfterFirstPass,
    [switch]$RequireSaveReloadEvidence
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $repoRoot
try {
    if ($ClientCount -lt 2 -or $ClientCount -gt 4) {
        throw "ClientCount must be between 2 and 4. Actual: $ClientCount"
    }

    if ($ReconnectClientIndex -lt 0 -or $ReconnectClientIndex -gt $ClientCount) {
        throw "ReconnectClientIndex must be 0 or between 1 and ClientCount. Actual: $ReconnectClientIndex"
    }

    if ($WaitSeconds -lt 30) {
        throw "WaitSeconds must be at least 30 for a meaningful long-run pass. Actual: $WaitSeconds"
    }

    if ($DisconnectAfterSeconds -lt 10) {
        throw "DisconnectAfterSeconds must be at least 10. Actual: $DisconnectAfterSeconds"
    }

    if ($ReconnectAfterSeconds -lt 5) {
        throw "ReconnectAfterSeconds must be at least 5. Actual: $ReconnectAfterSeconds"
    }

    $serverExe = Resolve-Path $ServerExecutable
    $clientExe = Resolve-Path $ClientExecutable

    if ([string]::IsNullOrWhiteSpace($RunName)) {
        $RunName = "dedicated-longrun-" + (Get-Date -Format "yyyyMMdd-HHmmss")
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

    $processes = New-Object System.Collections.Generic.List[System.Diagnostics.Process]
    $clientLogs = @{}
    $reconnectLogs = @{}
    $serverLog = Join-Path $logDir "server-player.log"
    $allowedExitedProcessIds = @{}

    function Start-ServerProcess {
        param(
            [string]$LogFile,
            [string]$Phase
        )

        Write-Host "Starting dedicated server phase=$Phase port=$Port saveSlot=$SaveSlot log=$LogFile"
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
            [int]$Index,
            [string]$LogFile,
            [string]$Phase
        )

        $delay = 35 + (($ClientCount - $Index) * 5)
        Write-Host "Starting client index=$Index phase=$Phase delay=$delay log=$LogFile"
        return Start-Process -FilePath $clientExe -ArgumentList @(
            "-batchmode", "-nographics",
            "-logFile", $LogFile,
            "-mode", "client",
            "-joinIp", "127.0.0.1",
            "-port", "$Port",
            "-saveSlot", $SaveSlot,
            "-directValidationTrace",
            "-directValidationAutoplay",
            "-directValidationAutoplayDelaySeconds", "$delay"
        ) -PassThru -WindowStyle Hidden
    }

    function Stop-TrackedProcess {
        param(
            [System.Diagnostics.Process]$Process,
            [string]$Reason
        )

        if ($Process -and -not $Process.HasExited) {
            Write-Host "Stopping process id=$($Process.Id) reason=$Reason"
            Stop-Process -Id $Process.Id -Force
            $Process.WaitForExit(5000) | Out-Null
        }
    }

    function Stop-AllTrackedProcesses {
        param([string]$Reason)

        foreach ($process in $processes) {
            Stop-TrackedProcess $process $Reason
        }
    }

    function Wait-WithReconnect {
        param(
            [hashtable]$ClientProcesses
        )

        $elapsed = 0
        $didReconnect = $false
        while ($elapsed -lt $WaitSeconds) {
            Start-Sleep -Seconds 5
            $elapsed += 5

            foreach ($process in $processes) {
                if ($process -and $process.HasExited -and $process.ExitCode -ne 0 -and -not $allowedExitedProcessIds.ContainsKey($process.Id)) {
                    throw "Process exited early id=$($process.Id) exitCode=$($process.ExitCode)"
                }
            }

            if ($ReconnectClientIndex -gt 0 -and -not $didReconnect -and $elapsed -ge $DisconnectAfterSeconds) {
                $target = $ClientProcesses[$ReconnectClientIndex]
                Stop-TrackedProcess $target "scheduled reconnect"
                $allowedExitedProcessIds[$target.Id] = $true

                Start-Sleep -Seconds $ReconnectAfterSeconds
                $elapsed += $ReconnectAfterSeconds

                $reconnectLog = Join-Path $logDir "client$ReconnectClientIndex-reconnect-player.log"
                $reconnected = Start-ClientProcess $ReconnectClientIndex $reconnectLog "reconnect"
                $processes.Add($reconnected)
                $ClientProcesses[$ReconnectClientIndex] = $reconnected
                $reconnectLogs[$ReconnectClientIndex] = $reconnectLog
                $didReconnect = $true
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

    function Assert-NoFatalLogs {
        param([string[]]$Paths)

        $fatalPatterns = "StartClient failed|StartHost failed|StartServer failed|Unhandled|NullReferenceException|InvalidOperationException|Exception|ERROR|Failed to bind|Address already in use|owner mismatch"
        $fatalMatches = Select-String -Path $Paths -Pattern $fatalPatterns -CaseSensitive
        if ($fatalMatches) {
            $summary = $fatalMatches | Select-Object -First 12 | ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line)" }
            throw "Fatal log pattern found:`n$($summary -join [Environment]::NewLine)"
        }
    }

    function Assert-PrimaryPassLogs {
        $allPrimaryLogs = @($serverLog) + @($clientLogs.Values)

        Assert-LogContains $serverLog "Dedicated server started - port=$Port maxPlayers=4 saveSlot=$SaveSlot" "Dedicated server did not start"
        Assert-LogContains $serverLog "Dedicated server requested network scene load scene=Town" "Dedicated server did not request Town scene load"

        for ($i = 1; $i -le $ClientCount; $i++) {
            Assert-LogContains $serverLog "Dedicated server client connected id=$i" "Dedicated server did not observe client $i"
            Assert-LogContains $serverLog "Network player spawned owner=$i .* playerId=client-$i" "Dedicated server did not spawn client-$i"

            $clientLog = $clientLogs[$i]
            Assert-LogContains $clientLog "Client started - joinIp=127.0.0.1 port=$Port" "Client $i did not start"
            Assert-LogContains $clientLog "Network player spawned owner=$i .* isOwner=True .* playerId=client-$i" "Client $i did not own client-$i"
            Assert-LogContains $clientLog "Student day result player=client-$i day=1 activities=1" "Client $i activity result missing"
            Assert-LogContains $clientLog "World time schedule sync .* day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart" "Client $i did not sync Day 2"
        }

        if ($ReconnectClientIndex -gt 0) {
            Assert-LogContains $serverLog "Dedicated server client disconnected id=$ReconnectClientIndex" "Dedicated server did not observe scheduled reconnect disconnect"
            Assert-LogContains $serverLog "Dedicated server client connected id=[0-9]+" "Dedicated server did not observe reconnect connection"
            $reconnectLog = $reconnectLogs[$ReconnectClientIndex]
            Assert-LogContains $reconnectLog "Client started - joinIp=127.0.0.1 port=$Port" "Reconnect client did not start"
            Assert-LogContains $reconnectLog "Network player spawned owner=[0-9]+ .* isOwner=True .* playerId=client-[0-9]+" "Reconnect client did not own a synced player"
        }

        Assert-LogContains $serverLog "World time day-end ready client=[0-9]+ ready=$ClientCount/$ClientCount" "Dedicated server did not reach all-ready day end"
        Assert-LogContains $serverLog "World time next day confirmed by server day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart" "Dedicated server did not confirm Day 2"
        Assert-NoFatalLogs $allPrimaryLogs
    }

    try {
        $server = Start-ServerProcess $serverLog "primary"
        $processes.Add($server)

        Start-Sleep -Seconds 10

        $clientProcesses = @{}
        for ($i = 1; $i -le $ClientCount; $i++) {
            $clientLog = Join-Path $logDir "client$i-player.log"
            $client = Start-ClientProcess $i $clientLog "primary"
            $processes.Add($client)
            $clientProcesses[$i] = $client
            $clientLogs[$i] = $clientLog
            Start-Sleep -Seconds 4
        }

        Wait-WithReconnect $clientProcesses
    }
    finally {
        Stop-AllTrackedProcesses "primary pass cleanup"
    }

    Assert-PrimaryPassLogs

    $restartServerLog = ""
    $restartClientLogs = @()
    if ($RestartServerAfterFirstPass) {
        $restartServerLog = Join-Path $logDir "server-restart-player.log"
        $restartProcesses = New-Object System.Collections.Generic.List[System.Diagnostics.Process]
        try {
            $restartServer = Start-ServerProcess $restartServerLog "restart"
            $restartProcesses.Add($restartServer)
            $processes.Add($restartServer)

            Start-Sleep -Seconds 10

            $restartClientCount = [Math]::Min(2, $ClientCount)
            for ($i = 1; $i -le $restartClientCount; $i++) {
                $restartClientLog = Join-Path $logDir "client$i-restart-player.log"
                $restartClient = Start-ClientProcess $i $restartClientLog "restart"
                $restartProcesses.Add($restartClient)
                $processes.Add($restartClient)
                $restartClientLogs += $restartClientLog
                Start-Sleep -Seconds 4
            }

            Start-Sleep -Seconds ([Math]::Min([Math]::Max(90, $ReconnectAfterSeconds), $WaitSeconds))
        }
        finally {
            foreach ($process in $restartProcesses) {
                Stop-TrackedProcess $process "restart pass cleanup"
            }
        }

        $allRestartLogs = @($restartServerLog) + $restartClientLogs
        Assert-LogContains $restartServerLog "Dedicated server started - port=$Port maxPlayers=4 saveSlot=$SaveSlot" "Restarted server did not use the same saveSlot"
        Assert-LogContains $restartServerLog "Dedicated server client connected id=1" "Restarted server did not observe client 1"
        foreach ($restartClientLog in $restartClientLogs) {
            Assert-LogContains $restartClientLog "Client started - joinIp=127.0.0.1 port=$Port" "Restart client did not start"
        }

        if ($RequireSaveReloadEvidence) {
            Assert-LogContains $restartServerLog "saveSlot=$SaveSlot" "Restart server log did not include saveSlot reload evidence"
            Assert-LogContains $restartClientLogs[0] "saveSlot=$SaveSlot" "Restart client log did not include saveSlot reload evidence"
        }

        Assert-NoFatalLogs $allRestartLogs
    }

    [PSCustomObject]@{
        Result = "Passed"
        ServerExecutable = $serverExe.Path
        ClientExecutable = $clientExe.Path
        LogDir = $logDir
        Port = $Port
        SaveSlot = $SaveSlot
        ClientCount = $ClientCount
        WaitSeconds = $WaitSeconds
        ReconnectClientIndex = $ReconnectClientIndex
        RestartServerAfterFirstPass = [bool]$RestartServerAfterFirstPass
        RequireSaveReloadEvidence = [bool]$RequireSaveReloadEvidence
        RestartServerLog = $restartServerLog
    } | Format-List
}
finally {
    Pop-Location
}
