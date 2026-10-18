<#
.SYNOPSIS
    Prints the context pack for work on one or more systems.

.DESCRIPTION
    The doc system is built so that a piece of work loads its own systems in
    full and everything else by interface only. This script is that rule as a
    command:

      - each named system's card, whole (frontmatter stripped);
      - for every neighbour named in their "Connects to" sections, only the
        Purpose and Interface sections;
      - the determinism contract, when any named system lives in Simulation/.

    An orchestrator pastes the output into a brief. An engineer may run it
    itself instead of being handed the text.

.USAGE
    powershell -File scripts\context.ps1 -Systems lobby-ui
    powershell -File scripts\context.ps1 -Systems netcode,match-log -Out brief.md
    powershell -File scripts\context.ps1 -List
#>
param(
    [string[]]$Systems = @(),
    [string]$Out,
    [switch]$List
)

$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$CardDir = Join-Path $RepoRoot "docs\systems"
$SimSystems = @('simulation', 'economy', 'commands', 'board', 'draft')

function Get-Cards {
    Get-ChildItem -Path $CardDir -Filter *.md -File |
        Where-Object { -not $_.Name.StartsWith('_') } |
        Sort-Object Name
}

function Get-Body([string]$Path) {
    $text = [IO.File]::ReadAllText($Path)
    if ($text.StartsWith('---')) {
        $end = $text.IndexOf("`n---", 3)
        if ($end -ge 0) { $text = $text.Substring($text.IndexOf("`n", $end + 1) + 1) }
    }
    return $text.Trim()
}

function Get-Summary([string]$Path) {
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        if ($line -match '^summary:\s*(.+)$') { return $Matches[1].Trim() }
    }
    return ''
}

# The text of one "## Heading" section, heading included.
function Get-Section([string]$Body, [string]$Heading) {
    $m = [regex]::Match($Body, "(?ms)^## $([regex]::Escape($Heading))\s*$.*?(?=^## |\z)")
    if ($m.Success) { return $m.Value.Trim() }
    return $null
}

$cards = @{}
foreach ($c in Get-Cards) { $cards[$c.BaseName] = $c.FullName }

if ($List -or $Systems.Count -eq 0) {
    foreach ($name in ($cards.Keys | Sort-Object)) {
        Write-Output ("{0,-16} {1}" -f $name, (Get-Summary $cards[$name]))
    }
    exit 0
}

$Systems = @($Systems | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($s in $Systems) {
    if (-not $cards.ContainsKey($s)) { throw "No card docs/systems/$s.md. Run with -List." }
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Context pack: $($Systems -join ', ')")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("Full cards for the systems in scope, then neighbours by interface only. Anything not here is out of scope; ask rather than explore.")
[void]$sb.AppendLine("")

$neighbours = New-Object System.Collections.Generic.List[string]
foreach ($s in $Systems) {
    $body = Get-Body $cards[$s]
    [void]$sb.AppendLine("<!-- card: $s -->")
    [void]$sb.AppendLine($body)
    [void]$sb.AppendLine("")
    $connects = Get-Section $body 'Connects to'
    if ($connects) {
        foreach ($m in [regex]::Matches($connects, '`([a-z0-9-]+)`')) {
            $n = $m.Groups[1].Value
            if ($cards.ContainsKey($n) -and -not ($Systems -contains $n) -and -not $neighbours.Contains($n)) {
                $neighbours.Add($n)
            }
        }
    }
}

if ($neighbours.Count -gt 0) {
    [void]$sb.AppendLine("# Neighbours (interface only)")
    [void]$sb.AppendLine("")
    foreach ($n in $neighbours) {
        $body = Get-Body $cards[$n]
        [void]$sb.AppendLine("<!-- neighbour: $n -->")
        [void]$sb.AppendLine("## $n")
        foreach ($h in @('Purpose', 'Interface')) {
            $sec = Get-Section $body $h
            if ($sec) { [void]$sb.AppendLine(($sec -replace '(?m)^## ', '### ')) }
        }
        [void]$sb.AppendLine("")
    }
}

$touchesSim = @($Systems | Where-Object { $SimSystems -contains $_ }).Count -gt 0
if ($touchesSim) {
    $contract = Join-Path $RepoRoot "docs\contracts\determinism.md"
    if (Test-Path $contract) {
        [void]$sb.AppendLine("# Determinism contract (in scope: this work touches Simulation/)")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine((Get-Body $contract))
    }
}

$text = $sb.ToString()
$words = ([regex]::Matches($text, '\S+')).Count
$text += "`n<!-- about $([int]($words * 1.4)) tokens -->`n"

if ($Out) {
    [IO.File]::WriteAllText((Join-Path (Get-Location) $Out), $text)
    Write-Host "Wrote $Out (about $([int]($words * 1.4)) tokens)"
}
else {
    Write-Output $text
}
