<#
.SYNOPSIS
    Update the Obsidian review dashboard from the system/vault freshness report.
.DESCRIPTION
    Called after a PR merges to main; can also run locally. Never stamps or
    rewrites explanatory prose. The committing agent reviews that prose against
    the source diff, preserving everything under My understanding in the vault.
#>
param(
    [string]$OutputPath = 'notes/Merge review.md',
    [string]$ReportPath = 'TestResults/doc-review.json'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root
try {
    & git rev-parse --verify HEAD | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Full git history is required for documentation review.' }
    $parent = Split-Path $ReportPath -Parent
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    if (Test-Path -LiteralPath $ReportPath) { Remove-Item -LiteralPath $ReportPath }
    & powershell -NoProfile -ExecutionPolicy Bypass -File scripts/okf-stale.ps1 -JsonPath $ReportPath
    if ($LASTEXITCODE -notin @(0, 1) -or -not (Test-Path -LiteralPath $ReportPath)) {
        throw 'Freshness check failed to produce a report.'
    }
    $report = Get-Content -LiteralPath $ReportPath -Raw | ConvertFrom-Json
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('# Merge review')
    $lines.Add('')
    $lines.Add('Generated after merges to main. Source movement is a review signal, not proof that prose is false. This dashboard never changes verification stamps.')
    $lines.Add('')
    $lines.Add('For each affected card or note, compare its sources with its verified commit, correct drift, then stamp the reviewed source commit in a follow-up `docs: reverify` commit. Preserve personal **My understanding** sections. See [[Documentation guardrails]].')
    foreach ($section in @(
        @{ Name = 'Systems needing review'; Items = @($report.review | Where-Object { $_.Doc -like 'docs\systems\*' }) },
        @{ Name = 'Vault notes needing review'; Items = @($report.review | Where-Object { $_.Doc -like 'notes\*' }) },
        @{ Name = 'Other documents needing review'; Items = @($report.review | Where-Object { $_.Doc -notlike 'docs\systems\*' -and $_.Doc -notlike 'notes\*' }) },
        @{ Name = 'Implementation changes to check'; Items = @($report.touched) }
    )) {
        $lines.Add('')
        $lines.Add("## $($section.Name)")
        $lines.Add('')
        if ($section.Items.Count -eq 0) { $lines.Add('None.'); continue }
        foreach ($group in ($section.Items | Group-Object Doc | Sort-Object Name)) {
            $doc = $group.Name.Replace('\', '/')
            $target = '../' + ($doc -replace ' ', '%20')
            $sources = ($group.Group | ForEach-Object { '`' + $_.Source + '`' } | Sort-Object -Unique) -join ', '
            $lines.Add("- [$doc]($target): $sources")
        }
    }
    $lines.Add('')
    $lines.Add('## Expired documents and evidence warnings')
    $lines.Add('')
    foreach ($entry in $report.stale) { $lines.Add("- $($entry.Doc): expired $($entry.StaleAfter)") }
    foreach ($warning in $report.warnings) { $lines.Add("- $warning") }
    if (@($report.stale).Count -eq 0 -and @($report.warnings).Count -eq 0) { $lines.Add('None.') }
    $lines | Set-Content -LiteralPath $OutputPath -Encoding UTF8
}
finally { Pop-Location }
