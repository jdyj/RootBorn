param(
    [string]$ProjectPath,
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe",
    [switch]$CleanStaleProcesses,
    [switch]$Strict
)

$ErrorActionPreference = "Stop"

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "== $Title =="
}

function Resolve-ProjectPath {
    param([string]$InputPath)
    if (-not [string]::IsNullOrWhiteSpace($InputPath)) {
        return (Resolve-Path -LiteralPath $InputPath).Path
    }

    $scriptDir = Split-Path -Parent $MyInvocation.ScriptName
    return (Resolve-Path -LiteralPath (Join-Path $scriptDir "..\..")).Path
}

$failures = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]
$root = Resolve-ProjectPath $ProjectPath

Write-Section "Project"
Write-Host "ProjectPath: $root"
Write-Host "UnityPath:   $UnityPath"

Write-Section "Unity Processes"
$processes = Get-Process Unity,rootborn,unity-mcp-server,Unity.ILPP.Runner -ErrorAction SilentlyContinue |
    Select-Object ProcessName, Id, StartTime

if ($processes) {
    $processes | Format-Table -AutoSize
    $warnings.Add("Unity/rootborn/MCP/ILPP process is already running.")

    if ($CleanStaleProcesses) {
        Write-Host "Stopping stale Unity/rootborn/MCP/ILPP processes..."
        $processes | ForEach-Object {
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }
    }
} else {
    Write-Host "No Unity/rootborn/MCP/ILPP process is running."
}

Write-Section "ILPP Lock"
$ilppPid = Join-Path $root "Library\ilpp.pid"
if (Test-Path -LiteralPath $ilppPid) {
    $warnings.Add("Library\ilpp.pid exists.")
    Get-Item -LiteralPath $ilppPid | Select-Object FullName, Length, LastWriteTime | Format-Table -AutoSize

    if ($CleanStaleProcesses) {
        Remove-Item -LiteralPath $ilppPid -Force
        Write-Host "Removed Library\ilpp.pid."
    }
} else {
    Write-Host "No Library\ilpp.pid lock file."
}

Write-Section "Unity Playback Engines"
if (-not (Test-Path -LiteralPath $UnityPath)) {
    $failures.Add("Unity executable not found: $UnityPath")
} else {
    $playbackEngines = Join-Path (Split-Path -Parent $UnityPath) "Data\PlaybackEngines"
    if (Test-Path -LiteralPath $playbackEngines) {
        $engines = Get-ChildItem -LiteralPath $playbackEngines -Directory | Select-Object -ExpandProperty Name
        $engines | ForEach-Object { Write-Host $_ }

        $hasDedicatedServerModule = $engines | Where-Object { $_ -match "server" }
        if (-not $hasDedicatedServerModule) {
            $warnings.Add("Dedicated Server playback module is not visible under PlaybackEngines. Skip Dedicated Server validation on this machine unless the module is installed.")
        }
    } else {
        $failures.Add("PlaybackEngines folder not found: $playbackEngines")
    }
}

Write-Section "Latest Direct Validation Evidence"
$logsRoot = Join-Path $root "Builds\Logs"
$evidenceFolders = @(
    "multi-direct-run-quest-inventory-snapshot",
    "multi-direct-run-relationship-condition-rebind"
)

foreach ($folder in $evidenceFolders) {
    $path = Join-Path $logsRoot $folder
    if (Test-Path -LiteralPath $path) {
        $latest = Get-ChildItem -LiteralPath $path -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        Write-Host "$folder : present, latest file $($latest.Name) at $($latest.LastWriteTime)"
    } else {
        $warnings.Add("Missing direct validation evidence folder: $folder")
    }
}

Write-Section "Recommended Fast Path"
Write-Host "1. Run this preflight before opening Unity or building."
Write-Host "2. If Dedicated Server module is missing, record it as environment-blocked and use Host + two Client direct validation."
Write-Host "3. Prefer Unity MCP targeted tests: EditMode full, PlayMode EndToEnd, Quests, World, and EditMode Network."
Write-Host "4. Avoid unfiltered PlayMode CLI in batchmode/nographics for this project; previous runs produced no XML or render-loop failures."
Write-Host "5. Always clean Unity/rootborn/MCP/ILPP processes and Library\ilpp.pid before reporting or handing back."

Write-Section "Summary"
foreach ($warning in $warnings) {
    Write-Host "WARN: $warning"
}

foreach ($failure in $failures) {
    Write-Host "FAIL: $failure"
}

if ($failures.Count -gt 0 -or ($Strict -and $warnings.Count -gt 0)) {
    exit 1
}

exit 0
