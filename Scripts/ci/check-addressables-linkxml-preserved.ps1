param()

$ErrorActionPreference = "Stop"

$restoreCommand = "git restore -- Assets/AddressableAssetsData/link.xml Assets/AddressableAssetsData/link.xml.meta"

function Throw-WithRemediation {
    param([string] $Message)

    throw "$Message`nRemediation: run '$restoreCommand' from the repository root, then re-run this guard."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $repoRoot
try {
    $requiredPaths = @(
        "Assets/AddressableAssetsData/link.xml",
        "Assets/AddressableAssetsData/link.xml.meta"
    )

    foreach ($path in $requiredPaths) {
        if (-not (Test-Path -LiteralPath $path)) {
            Throw-WithRemediation "Missing required Addressables linker asset: $path"
        }

        git ls-files --error-unmatch -- $path *> $null
        if ($LASTEXITCODE -ne 0) {
            Throw-WithRemediation "Required Addressables linker asset is not tracked by git: $path"
        }

        $status = git status --porcelain -- $path
        foreach ($line in $status) {
            if ($line.Length -ge 2 -and $line.Substring(0, 2).Contains("D")) {
                Throw-WithRemediation "Required Addressables linker asset is marked deleted in git status: $path :: $line"
            }
        }
    }

    Write-Host "OK: Addressables link.xml and meta are present, tracked, and not marked deleted."
}
finally {
    Pop-Location
}
