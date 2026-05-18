param()

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $repoRoot
try {
    $longRunScript = "Scripts\qa\run-dedicated-multiplayer-longrun.ps1"
    $dedicatedSmokeScript = "Scripts\qa\run-dedicated-multiplayer-smoke.ps1"
    $ciGateScript = "Scripts\ci\run-multiplayer-validation-gate.ps1"

    function Assert-FileContains {
        param(
            [string]$Path,
            [string]$Pattern,
            [string]$Message
        )

        if (-not (Test-Path $Path)) {
            throw "Missing file: $Path"
        }

        if (-not (Select-String -Path $Path -Pattern $Pattern -Quiet)) {
            throw "$Message ($Path :: $Pattern)"
        }
    }

    Assert-FileContains $longRunScript "\[int\]\`$ClientCount = 4" "Long-run script must default to four clients"
    Assert-FileContains $longRunScript "\[int\]\`$WaitSeconds = 600" "Long-run script must default to ten minutes"
    Assert-FileContains $longRunScript "\[int\]\`$ReconnectClientIndex" "Long-run script must support reconnect target selection"
    Assert-FileContains $longRunScript "\[switch\]\`$RestartServerAfterFirstPass" "Long-run script must support server restart"
    Assert-FileContains $longRunScript "\[switch\]\`$RequireSaveReloadEvidence" "Long-run script must support save reload evidence"
    Assert-FileContains $longRunScript "Dedicated server client disconnected id=" "Long-run script must assert disconnect evidence"
    Assert-FileContains $longRunScript "World time next day confirmed by server day=2" "Long-run script must assert day progression"
    Assert-FileContains $longRunScript "Student day result player=client-" "Long-run script must assert player activity results"
    Assert-FileContains $longRunScript "isOwner=False isServer=True" "Long-run script must assert server-observed non-owner player spawn evidence"
    Assert-FileContains $longRunScript "Failed to bind|Address already in use|owner mismatch" "Long-run script must scan fatal multiplayer patterns"
    Assert-FileContains $longRunScript "Select-String -Path \`$Paths -Pattern \`$fatalPatterns -CaseSensitive" "Fatal log scanning must be case-sensitive so benign Error/error text does not match ERROR"

    Assert-FileContains $dedicatedSmokeScript "function Assert-AnyLogContains" "Dedicated smoke must support cross-client ownership assertions"
    Assert-FileContains $dedicatedSmokeScript "Assert-AnyLogContains @\(\`$client1Log, \`$client2Log\) `"Network player spawned owner=1 .* isOwner=True .* playerId=client-1`"" "Dedicated smoke must not assume the first launched client owns client-1"
    Assert-FileContains $dedicatedSmokeScript "Assert-AnyLogContains @\(\`$client1Log, \`$client2Log\) `"Network player spawned owner=2 .* isOwner=True .* playerId=client-2`"" "Dedicated smoke must not assume the second launched client owns client-2"

    Assert-FileContains $ciGateScript "\[switch\]\`$RequireDedicatedLongRun" "CI gate must expose long-run switch"
    Assert-FileContains $ciGateScript "\[int\]\`$LongRunClientCount = 4" "CI gate must default long-run client count to four"
    Assert-FileContains $ciGateScript "\[int\]\`$LongRunWaitSeconds = 600" "CI gate must default long-run wait to ten minutes"
    Assert-FileContains $ciGateScript "run-dedicated-multiplayer-longrun.ps1" "CI gate must invoke the long-run script"
    Assert-FileContains $ciGateScript "function Invoke-CheckedPowerShell" "CI gate must check child PowerShell exit codes"
    Assert-FileContains $ciGateScript "if \(\`$LASTEXITCODE -ne 0\)" "CI gate must fail when a child validation script fails"

    [PSCustomObject]@{
        Result = "Passed"
        LongRunScript = $longRunScript
        DedicatedSmokeScript = $dedicatedSmokeScript
        CiGateScript = $ciGateScript
    } | Format-List
}
finally {
    Pop-Location
}
