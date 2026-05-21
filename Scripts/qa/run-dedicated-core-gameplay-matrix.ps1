param(
    [string]$ServerExecutable = "Builds\Server\Windows\rootborn-server.exe",
    [string]$ClientExecutable = "Builds\Client\Windows\rootborn.exe",
    [int]$StartPort = 7901,
    [int]$WaitSeconds = 120,
    [string]$RunName = "network-dedicated-core-gameplay-matrix"
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
            throw "Missing required dedicated matrix artifact: $Description at $Path. Rebuild with Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64 and Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64 before running run-dedicated-core-gameplay-matrix.ps1."
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
            "Assets\Scripts\Game\Common\DirectValidationCareerDailyPlanAutoplayInstaller.cs",
            "Assets\Scripts\Game\Common\DirectValidationDailyPlanCompletionAutoplayInstaller.cs",
            "Assets\Scripts\Game\Common\DirectValidationDiscoveryRouteUnlockAutoplayInstaller.cs",
            "Assets\Scripts\Game\Common\DirectValidationResourceRecipeActivityAutoplayInstaller.cs"
        )

        foreach ($sourcePath in $requiredSources) {
            $source = Assert-RequiredArtifact $sourcePath "required source for run-dedicated-core-gameplay-matrix.ps1"
            if ($serverGameDll.LastWriteTime -lt $source.LastWriteTime) {
                throw "Stale server Rootborn.Game.dll for run-dedicated-core-gameplay-matrix.ps1. $($serverGameDll.FullName) is older than $sourcePath. Rebuild with Rootborn.Editor.BuildScripts.BuildScript.BuildServerWindows64."
            }

            if ($clientGameDll.LastWriteTime -lt $source.LastWriteTime) {
                throw "Stale client Rootborn.Game.dll for run-dedicated-core-gameplay-matrix.ps1. $($clientGameDll.FullName) is older than $sourcePath. Rebuild with Rootborn.Editor.BuildScripts.BuildScript.BuildClientWindows64."
            }
        }
    }

    Assert-FreshBuildArtifacts

    $logsRoot = "Builds\Logs"
    New-Item -ItemType Directory -Path $logsRoot -Force | Out-Null
    $matrixLogDir = Join-Path (Resolve-Path $logsRoot) $RunName
    New-Item -ItemType Directory -Path $matrixLogDir -Force | Out-Null

    $slices = @(
        [PSCustomObject]@{
            Name = "route-relationship"
            Script = "Scripts\qa\run-dedicated-route-relationship-smoke.ps1"
            RunName = "$RunName\route-relationship"
            SaveSlot = "$RunName-route-relationship"
            Port = $StartPort + 0
            FirstScreenshot = "network-dedicated-route-relationship-e2e.png"
            ReloadScreenshot = "network-dedicated-route-relationship-e2e-reload.png"
            ActionMarker = "autoplay completed route-relationship"
            ObserveMarker = "autoplay completed route-relationship-observe"
            StatePattern = "dailyPlan=day:.*selected:(?!empty)"
            ExtraPatterns = @("relationships=(?!empty)")
        },
        [PSCustomObject]@{
            Name = "career-daily-plan-route"
            Script = "Scripts\qa\run-dedicated-career-daily-plan-route-smoke.ps1"
            RunName = "$RunName\career-daily-plan-route"
            SaveSlot = "$RunName-career-daily-plan-route"
            Port = $StartPort + 1
            FirstScreenshot = "network-dedicated-career-daily-plan-route-e2e.png"
            ReloadScreenshot = "network-dedicated-career-daily-plan-route-e2e-reload.png"
            ActionMarker = "autoplay completed career-daily-plan-route"
            ObserveMarker = "autoplay completed career-daily-plan-route-observe"
            StatePattern = "dailyPlan=day:.*selected:(?!empty)"
            ExtraPatterns = @("careerHints=(?!empty)")
        },
        [PSCustomObject]@{
            Name = "daily-plan-completion"
            Script = "Scripts\qa\run-dedicated-daily-plan-completion-smoke.ps1"
            RunName = "$RunName\daily-plan-completion"
            SaveSlot = "$RunName-daily-plan-completion"
            Port = $StartPort + 2
            FirstScreenshot = "network-dedicated-daily-plan-completion-e2e.png"
            ReloadScreenshot = "network-dedicated-daily-plan-completion-e2e-reload.png"
            ActionMarker = "autoplay completed daily-plan-completion"
            ObserveMarker = "autoplay completed daily-plan-completion-observe"
            StatePattern = "dailyPlan=day:.*selected:(?!empty).*completed:(?!empty)"
            ExtraPatterns = @("result=.*Daily Plan", "duplicateRows:0", "hud=.*duplicates:0")
        },
        [PSCustomObject]@{
            Name = "discovery-route-unlock"
            Script = "Scripts\qa\run-dedicated-discovery-route-unlock-smoke.ps1"
            RunName = "$RunName\discovery-route-unlock"
            SaveSlot = "$RunName-discovery-route-unlock"
            Port = $StartPort + 3
            FirstScreenshot = "network-dedicated-discovery-route-unlock-e2e.png"
            ReloadScreenshot = "network-dedicated-discovery-route-unlock-e2e-reload.png"
            ActionMarker = "autoplay completed discovery-route-unlock"
            ObserveMarker = "autoplay completed discovery-route-unlock-observe"
            StatePattern = "dailyPlan=day:.*selected:(?!empty)"
            ExtraPatterns = @("clue=.*seen:true", "interpretation=.*completed:true", "worldState=.*active:true", "journal=.*duplicateRows:0", "hud=.*duplicates:0")
        },
        [PSCustomObject]@{
            Name = "resource-recipe-activity"
            Script = "Scripts\qa\run-dedicated-resource-recipe-activity-smoke.ps1"
            RunName = "$RunName\resource-recipe-activity"
            SaveSlot = "$RunName-resource-recipe-activity"
            Port = $StartPort + 4
            FirstScreenshot = "network-dedicated-resource-recipe-activity-e2e.png"
            ReloadScreenshot = "network-dedicated-resource-recipe-activity-e2e-reload.png"
            ActionMarker = "autoplay completed resource-recipe-activity"
            ObserveMarker = "autoplay completed resource-recipe-activity-observe"
            StatePattern = "recipe=recipe.town.field-notes revealed:true"
            ExtraPatterns = @("resource=Rock seen:true", "journal=resource-recipe duplicateRows:0", "hud=tracked duplicates:0", "save=resource-recipe exists:true")
        }
    )

    function Assert-LogContains {
        param(
            [string[]]$Paths,
            [string]$Pattern,
            [string]$Message
        )

        foreach ($path in $Paths) {
            if (-not (Test-Path -LiteralPath $path)) {
                throw "$Message. Missing log: $path"
            }
        }

        if (-not (Select-String -Path $Paths -Pattern $Pattern -Quiet)) {
            throw "$Message ($($Paths -join ', ') :: $Pattern)"
        }
    }

    function Assert-Screenshot {
        param(
            [string]$Path,
            [string]$Message
        )

        if (-not (Test-Path -LiteralPath $Path)) {
            throw "${Message}: $Path"
        }

        $item = Get-Item -LiteralPath $Path
        if ($item.Length -le 0) {
            throw "${Message}: zero-byte screenshot at $Path"
        }
    }

    function Assert-SliceEvidence {
        param([object]$Slice)

        $sliceLogDir = Join-Path (Resolve-Path $logsRoot) $Slice.RunName
        if (-not (Test-Path -LiteralPath $sliceLogDir)) {
            throw "Slice '$($Slice.Name)' log directory missing: $sliceLogDir"
        }

        $clientFirstLog = Join-Path $sliceLogDir "client-first-player.log"
        $clientReloadLog = Join-Path $sliceLogDir "client-reload-player.log"
        $serverFirstLog = Join-Path $sliceLogDir "server-first-player.log"
        $serverReloadLog = Join-Path $sliceLogDir "server-reload-player.log"
        $firstScreenshot = Join-Path $sliceLogDir $Slice.FirstScreenshot
        $reloadScreenshot = Join-Path $sliceLogDir $Slice.ReloadScreenshot

        Assert-LogContains @($serverFirstLog) "Dedicated server started - port=$($Slice.Port)" "Slice '$($Slice.Name)' server readiness marker missing"
        Assert-LogContains @($clientFirstLog) "Client started - joinIp=127.0.0.1 port=$($Slice.Port)" "Slice '$($Slice.Name)' client readiness marker missing"
        Assert-LogContains @($clientFirstLog) $Slice.ActionMarker "Slice '$($Slice.Name)' action completion marker missing"
        Assert-LogContains @($clientReloadLog) $Slice.ObserveMarker "Slice '$($Slice.Name)' observe completion marker missing"
        Assert-LogContains @($clientFirstLog, $clientReloadLog) $Slice.StatePattern "Slice '$($Slice.Name)' state marker missing"

        foreach ($pattern in $Slice.ExtraPatterns) {
            Assert-LogContains @($clientFirstLog, $clientReloadLog) $pattern "Slice '$($Slice.Name)' required extra evidence missing"
        }

        Assert-Screenshot $firstScreenshot "Slice '$($Slice.Name)' action screenshot missing"
        Assert-Screenshot $reloadScreenshot "Slice '$($Slice.Name)' reload screenshot missing"

        return [PSCustomObject]@{
            Slice = $Slice.Name
            Result = "Passed"
            Port = $Slice.Port
            SaveSlot = $Slice.SaveSlot
            LogDir = $sliceLogDir
            FirstScreenshot = $firstScreenshot
            ReloadScreenshot = $reloadScreenshot
        }
    }

    function Invoke-Slice {
        param([object]$Slice)

        if (-not (Test-Path -LiteralPath $Slice.Script)) {
            throw "Slice '$($Slice.Name)' script missing: $($Slice.Script)"
        }

        Write-Host "=== Running dedicated matrix slice: $($Slice.Name) port=$($Slice.Port) saveSlot=$($Slice.SaveSlot) ==="
        $process = Start-Process -FilePath "powershell.exe" -ArgumentList @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", $Slice.Script,
            "-ServerExecutable", $serverExe.Path,
            "-ClientExecutable", $clientExe.Path,
            "-RunName", $Slice.RunName,
            "-SaveSlot", $Slice.SaveSlot,
            "-Port", "$($Slice.Port)",
            "-WaitSeconds", "$WaitSeconds"
        ) -PassThru -Wait -NoNewWindow

        if ($process.ExitCode -ne 0) {
            throw "Slice '$($Slice.Name)' failed with ExitCode=$($process.ExitCode). Inspect Builds/Logs/$($Slice.RunName)."
        }

        return Assert-SliceEvidence $Slice
    }

    $sliceResults = @()
    foreach ($slice in $slices) {
        $sliceResults += Invoke-Slice $slice
    }

    [PSCustomObject]@{
        Result = "Passed"
        RunName = $RunName
        StartPort = $StartPort
        WaitSeconds = $WaitSeconds
        MatrixLogDir = $matrixLogDir
        SliceResults = $sliceResults
    } | Format-List
}
finally {
    Pop-Location
}
