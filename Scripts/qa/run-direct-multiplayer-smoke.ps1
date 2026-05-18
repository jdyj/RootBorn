param(
    [string]$Executable = "Builds\Client\Windows\rootborn.exe",
    [int]$Port = 7841,
    [int]$WaitSeconds = 125,
    [string]$RunName = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $repoRoot
try {
    $exe = Resolve-Path $Executable
    if ([string]::IsNullOrWhiteSpace($RunName)) {
        $RunName = "direct-multiplayer-" + (Get-Date -Format "yyyyMMdd-HHmmss")
    }

    $logRoot = Resolve-Path "Builds\Logs"
    $logDir = Join-Path $logRoot $RunName
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null

    $slot = $RunName
    $processes = @()

    try {
        $processes += Start-Process -FilePath $exe -ArgumentList @(
            "-batchmode", "-nographics",
            "-logFile", (Join-Path $logDir "host-player.log"),
            "-mode", "host",
            "-port", "$Port",
            "-maxPlayers", "4",
            "-saveSlot", $slot,
            "-directValidationTrace",
            "-directValidationAutoplay",
            "-directValidationAutoplayDelaySeconds", "70"
        ) -PassThru -WindowStyle Hidden

        Start-Sleep -Seconds 8

        $processes += Start-Process -FilePath $exe -ArgumentList @(
            "-batchmode", "-nographics",
            "-logFile", (Join-Path $logDir "client1-player.log"),
            "-mode", "client",
            "-joinIp", "127.0.0.1",
            "-port", "$Port",
            "-saveSlot", $slot,
            "-directValidationTrace",
            "-directValidationAutoplay",
            "-directValidationAutoplayDelaySeconds", "55"
        ) -PassThru -WindowStyle Hidden

        Start-Sleep -Seconds 4

        $processes += Start-Process -FilePath $exe -ArgumentList @(
            "-batchmode", "-nographics",
            "-logFile", (Join-Path $logDir "client2-player.log"),
            "-mode", "client",
            "-joinIp", "127.0.0.1",
            "-port", "$Port",
            "-saveSlot", $slot,
            "-directValidationTrace",
            "-directValidationAutoplay",
            "-directValidationAutoplayDelaySeconds", "45"
        ) -PassThru -WindowStyle Hidden

        Start-Sleep -Seconds $WaitSeconds
    }
    finally {
        foreach ($process in $processes) {
            if ($process -and -not $process.HasExited) {
                Stop-Process -Id $process.Id -Force
                $process.WaitForExit(5000) | Out-Null
            }
        }
    }

    $hostLog = Join-Path $logDir "host-player.log"
    $client1Log = Join-Path $logDir "client1-player.log"
    $client2Log = Join-Path $logDir "client2-player.log"
    $allLogs = @($hostLog, $client1Log, $client2Log)

    foreach ($log in $allLogs) {
        if (-not (Test-Path $log)) {
            throw "Missing log file: $log"
        }
    }

    function Assert-LogContains {
        param(
            [string]$Path,
            [string]$Pattern,
            [string]$Message
        )

        if (-not (Select-String -Path $Path -Pattern $Pattern -Quiet)) {
            throw "$Message ($Path :: $Pattern)"
        }
    }

    Assert-LogContains $hostLog "Host started - port=$Port" "Host did not start"
    Assert-LogContains $client1Log "Client started - joinIp=127.0.0.1 port=$Port" "Client 1 did not start"
    Assert-LogContains $client2Log "Client started - joinIp=127.0.0.1 port=$Port" "Client 2 did not start"

    Assert-LogContains $hostLog "Network player spawned owner=0 .* playerId=client-0" "Host did not spawn client-0"
    Assert-LogContains $hostLog "Network player spawned owner=1 .* playerId=client-1" "Host did not observe client-1"
    Assert-LogContains $hostLog "Network player spawned owner=2 .* playerId=client-2" "Host did not observe client-2"
    Assert-LogContains $client1Log "Network player spawned owner=1 .* isOwner=True .* playerId=client-1" "Client 1 did not own client-1"
    Assert-LogContains $client2Log "Network player spawned owner=2 .* isOwner=True .* playerId=client-2" "Client 2 did not own client-2"

    Assert-LogContains $hostLog "World time day-end ready client=0 ready=3/3" "Server did not reach all-ready day end"
    Assert-LogContains $hostLog "World time next day confirmed by server day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart" "Server did not confirm Day 2"
    Assert-LogContains $client1Log "World time schedule sync .* day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart" "Client 1 did not sync Day 2"
    Assert-LogContains $client2Log "World time schedule sync .* day=2 weekday=Tue timeOfDay=Morning schedulePhase=DayStart" "Client 2 did not sync Day 2"

    Assert-LogContains $hostLog "Student day result player=client-0 day=1 activities=1" "Host player activity result missing"
    Assert-LogContains $client1Log "Student day result player=client-1 day=1 activities=1" "Client 1 activity result missing"
    Assert-LogContains $client2Log "Student day result player=client-2 day=1 activities=1" "Client 2 activity result missing"

    $fatalPatterns = "StartClient failed|StartHost failed|Unhandled|NullReferenceException|InvalidOperationException|Exception|ERROR"
    $fatalMatches = Select-String -Path $allLogs -Pattern $fatalPatterns
    if ($fatalMatches) {
        $summary = $fatalMatches | Select-Object -First 10 | ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line)" }
        throw "Fatal log pattern found:`n$($summary -join [Environment]::NewLine)"
    }

    [PSCustomObject]@{
        Result = "Passed"
        Executable = $exe.Path
        LogDir = $logDir
        Port = $Port
        SaveSlot = $slot
        HostPid = $processes[0].Id
        Client1Pid = $processes[1].Id
        Client2Pid = $processes[2].Id
    } | Format-List
}
finally {
    Pop-Location
}
