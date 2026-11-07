# Runs Unity tests for ROOTBORN in a separate editor process and waits for it.
#
#   powershell -NoProfile -File Scripts/unity/run-tests.ps1 -Platform PlayMode -Name pm_x -Filter "REL_001|TOWN_POINT"
#   powershell -NoProfile -File Scripts/unity/run-tests.ps1 -Platform EditMode -Name em_full
#
# -Addressables asset (default for PlayMode): read Addressables from the source assets, no rebuild after data changes.
# -Addressables built: read the built bundles like the real game. Use before commit, for build parity and for FEVM.
#   Rebuild first after data changes:  -BuildAddressables
# Results: <OutDir>/<Name>.xml, <Name>.log, <Name>-done.txt (exit code). Unity must not already be open on this project.
# After a PlayMode run, undo evidence the tests rewrite:  git -c core.autocrlf=false restore production/qa/evidence/
param(
    [Parameter(Mandatory = $true)][ValidateSet('EditMode', 'PlayMode')][string]$Platform,
    [Parameter(Mandatory = $true)][string]$Name,
    [string]$Filter,
    [ValidateSet('asset', 'built', 'keep')][string]$Addressables = 'asset',
    [switch]$BuildAddressables,
    [string]$OutDir = (Join-Path $env:TEMP 'rootborn-tests'),
    [string]$UnityPath = $(if ($env:UNITY_EDITOR_PATH) { $env:UNITY_EDITOR_PATH } else { 'C:\Program Files\Unity\Hub\Editor\6000.3.13f1\Editor\Unity.exe' })
)

$project = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
if (Get-Process -Name Unity -ErrorAction SilentlyContinue) { throw 'Unity is already running. Close it first (tests need the project lock).' }

if ($BuildAddressables) {
    $buildLog = Join-Path $OutDir "$Name-addressables.log"
    $b = Start-Process -FilePath $UnityPath -PassThru -ArgumentList @('-batchmode', '-quit', '-projectPath', "`"$project`"",
        '-executeMethod', 'Rootborn.Editor.BuildScripts.BuildScript.BuildAddressablesContent', '-logFile', "`"$buildLog`"")
    $b.WaitForExit()
    if ($b.ExitCode -ne 0) { throw "Addressables build failed (exit $($b.ExitCode)). See $buildLog" }
}

if ($Addressables -ne 'keep') { $env:ROOTBORN_AA_PLAYMODE = $Addressables } else { Remove-Item Env:ROOTBORN_AA_PLAYMODE -ErrorAction SilentlyContinue }
$xml = Join-Path $OutDir "$Name.xml"; $log = Join-Path $OutDir "$Name.log"; $done = Join-Path $OutDir "$Name-done.txt"
Remove-Item $done -ErrorAction SilentlyContinue
$args = @('-projectPath', "`"$project`"", '-runTests', '-testPlatform', $Platform, '-testResults', "`"$xml`"", '-logFile', "`"$log`"")
if ($Filter) { $args += @('-testFilter', "`"$Filter`"") }
$p = Start-Process -FilePath $UnityPath -ArgumentList $args -PassThru
$p.WaitForExit()
"exit=$($p.ExitCode) addressables=$Addressables end=$(Get-Date -Format HH:mm:ss)" | Out-File $done -Encoding utf8
if (Test-Path $xml) {
    $run = Select-Xml -Path $xml -XPath '/test-run' | Select-Object -First 1
    if ($run) { $n = $run.Node; "total=$($n.total) passed=$($n.passed) failed=$($n.failed) skipped=$($n.skipped)" }
}
Get-Content $done
