param(
    [string]$CatalogPath = "docs/superpowers/audits/2026-05-21-student-life-scenario-registry-coverage.md"
)

$ErrorActionPreference = "Stop"

$requiredCategories = @("TOWN", "LIFE", "CAREER", "REL", "JOB", "KNOW", "STATUS", "NET", "UI", "HEIR")
$root = (& git rev-parse --show-toplevel).Trim()
Set-Location $root

if (-not (Test-Path -LiteralPath $CatalogPath)) {
    throw "Missing scenario catalog: $CatalogPath"
}

$lines = Get-Content -LiteralPath $CatalogPath
$rows = @()

foreach ($line in $lines) {
    if ($line -notmatch '^\|\s*(Active|Deferred)\s*\|') {
        continue
    }

    $cells = $line.Trim().Trim('|').Split('|') | ForEach-Object { $_.Trim() }
    if ($cells.Count -lt 9) {
        throw "Malformed scenario catalog row: $line"
    }

    $rows += [pscustomobject]@{
        Status = $cells[0]
        Id = $cells[1]
        Category = $cells[2]
        Owner = $cells[3]
        EvidenceType = $cells[4]
        Evidence = $cells[5]
        PlayerFacing = $cells[6]
        DeferredReason = $cells[7]
        FutureValidation = $cells[8]
    }
}

if ($rows.Count -eq 0) {
    throw "Missing active scenario mapping rows in $CatalogPath"
}

$categories = $rows | ForEach-Object { $_.Category } | Sort-Object -Unique
foreach ($category in $requiredCategories) {
    if ($categories -notcontains $category) {
        throw "Missing scenario category $category in $CatalogPath"
    }
}

$activeRows = $rows | Where-Object { $_.Status -eq "Active" }
$deferredRows = $rows | Where-Object { $_.Status -eq "Deferred" }

foreach ($row in $activeRows) {
    if ([string]::IsNullOrWhiteSpace($row.Id) -or [string]::IsNullOrWhiteSpace($row.Evidence)) {
        throw "Missing active scenario mapping for $($row.Id)"
    }

    if ($row.Evidence -match '^(Assets|Scripts|docs)/') {
        if (-not (Test-Path -LiteralPath $row.Evidence)) {
            throw "Missing mapped evidence path for $($row.Id): $($row.Evidence)"
        }
    }
}

foreach ($row in $deferredRows) {
    if ([string]::IsNullOrWhiteSpace($row.DeferredReason)) {
        throw "Missing deferred scenario reason for $($row.Id)"
    }

    if ([string]::IsNullOrWhiteSpace($row.FutureValidation)) {
        throw "Missing deferred scenario future validation for $($row.Id)"
    }
}

$testFiles = Get-ChildItem -Path "Assets/Tests" -Recurse -File -Include "*.cs" | ForEach-Object { $_.FullName }
$testText = foreach ($file in $testFiles) { Get-Content -LiteralPath $file -Raw }
$knownCategorySignals = @{
    TOWN = "TOWN_"
    LIFE = "LIFE_"
    CAREER = "CAREER_"
    REL = "REL_"
    JOB = "JOB_"
    KNOW = "KNOW_"
    STATUS = "STATUS_"
    NET = "NET_"
    UI = "OBJECTIVE_JOURNAL_"
    HEIR = "HEIR_"
}

foreach ($entry in $knownCategorySignals.GetEnumerator()) {
    if (($testText -match [regex]::Escape($entry.Value)) -and ($categories -notcontains $entry.Key)) {
        throw "Scenario category $($entry.Key) appears in tests but is absent from the catalog"
    }
}

Write-Host "OK: scenario registry sync complete. Active=$($activeRows.Count) Deferred=$($deferredRows.Count)"
