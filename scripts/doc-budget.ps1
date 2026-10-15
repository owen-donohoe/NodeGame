<#
.SYNOPSIS
    Keeps the docs small enough to load and true enough to trust. Blocks in CI.

.DESCRIPTION
    Docs drift when they grow into history books and when they repeat each
    other. This check makes both a failing build instead of a slow decay:

      - word budgets per file (cards, map, router, entry files, contracts);
      - every card has the template's headings, in order, and its frontmatter
        (system = file name, a one-line summary, sources, a verified commit);
      - no history in cards or the map: no dates, PR numbers or line numbers.
        Git and Notion hold history; a card says what is true now;
      - every card is listed in docs/README.md and named in docs/map.md, so a
        new system cannot be orphaned;
      - the simulation boundary block is identical in CLAUDE.md and AGENTS.md.

    Exit 0 when clean, 1 with a list of violations otherwise.

.USAGE
    powershell -File scripts\doc-budget.ps1
    powershell -File scripts\doc-budget.ps1 -Only netcode,cloud   # just those cards
#>
param([string[]]$Only = @())
$ErrorActionPreference = 'Stop'
$Only = @($Only | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$errors = New-Object System.Collections.Generic.List[string]

$CardWords = 1100
$Budgets = [ordered]@{
    'CLAUDE.md'                    = 1100
    'AGENTS.md'                    = 900
    'docs/README.md'               = 600
    'docs/map.md'                  = 2000
    'docs/game.md'                 = 3200
    'docs/contracts/determinism.md'= 2600
    'docs/roles/engineer.md'       = 700
}
$Headings = @('Purpose', 'Interface', 'Connects to', 'Invariants', 'How it works',
              'Decisions', 'Pressure points', 'Change recipes', 'Tests')

function Read-Text([string]$rel) { [IO.File]::ReadAllText((Join-Path $RepoRoot $rel)) }

function Split-Frontmatter([string]$text) {
    if (-not $text.StartsWith('---')) { return @($null, $text) }
    $end = $text.IndexOf("`n---", 3)
    if ($end -lt 0) { return @($null, $text) }
    $fm = $text.Substring(3, $end - 3)
    $bodyStart = $text.IndexOf("`n", $end + 1)
    $body = if ($bodyStart -ge 0) { $text.Substring($bodyStart + 1) } else { '' }
    return @($fm, $body)
}

function Count-Words([string]$text) {
    $noComments = [regex]::Replace($text, '(?s)<!--.*?-->', '')
    return ([regex]::Matches($noComments, '\S+')).Count
}

# History has a home, and it is not here.
function Test-History([string]$rel, [string]$body) {
    $clean = [regex]::Replace($body, '(?s)<!--.*?-->', '')
    $clean = [regex]::Replace($clean, '(?s)```.*?```', '')
    if ($clean -match '\b20\d\d-\d\d-\d\d\b') { $errors.Add("${rel}: contains a date ($($Matches[0])). History belongs in git or Notion.") }
    if ($clean -match '(?<![\w&])#\d{2,}\b')  { $errors.Add("${rel}: contains a PR or issue number ($($Matches[0])).") }
    if ($clean -match '\.(cs|uxml|uss|ps1):\d+') { $errors.Add("${rel}: contains a line number ($($Matches[0])). Line numbers rot.") }
    if ($clean -match '(?i)\bline \d{2,}\b')  { $errors.Add("${rel}: contains a line reference ($($Matches[0])).") }
}

$global = $Only.Count -eq 0
if ($global) {
    foreach ($rel in $Budgets.Keys) {
        $path = Join-Path $RepoRoot $rel
        if (-not (Test-Path $path)) { $errors.Add("${rel}: missing"); continue }
        $parts = Split-Frontmatter (Read-Text $rel)
        $words = Count-Words $parts[1]
        if ($words -gt $Budgets[$rel]) { $errors.Add("${rel}: $words words, budget $($Budgets[$rel]). Cut, or move detail into a card.") }
    }
    Test-History 'docs/map.md' (Split-Frontmatter (Read-Text 'docs/map.md'))[1]
    $readme = Read-Text 'docs/README.md'
    $map = Read-Text 'docs/map.md'
}

$cards = Get-ChildItem (Join-Path $RepoRoot 'docs/systems') -Filter *.md -File | Where-Object { -not $_.Name.StartsWith('_') }
if (-not $global) { $cards = @($cards | Where-Object { $Only -contains $_.BaseName }) }
if ($cards.Count -eq 0) { $errors.Add("docs/systems: no cards") }

foreach ($card in $cards) {
    $rel = "docs/systems/$($card.Name)"
    $name = $card.BaseName
    $parts = Split-Frontmatter ([IO.File]::ReadAllText($card.FullName))
    $fm = $parts[0]; $body = $parts[1]
    if ($null -eq $fm) { $errors.Add("${rel}: no frontmatter"); continue }

    if ($fm -notmatch "(?m)^system:\s*$([regex]::Escape($name))\s*$") { $errors.Add("${rel}: frontmatter 'system:' must be '$name'") }
    if ($fm -match '(?m)^summary:\s*(.+)$') {
        if ($Matches[1].Trim().Length -gt 140) { $errors.Add("${rel}: summary longer than 140 characters") }
    } else { $errors.Add("${rel}: no summary") }
    if ($fm -notmatch '(?m)^sources:') { $errors.Add("${rel}: no sources") }
    if ($fm -notmatch '(?m)^verified:\s*[0-9a-f]{7,40}\s*$') { $errors.Add("${rel}: no 'verified: <sha>'") }

    $words = Count-Words $body
    if ($words -gt $CardWords) { $errors.Add("${rel}: $words words, budget $CardWords. Cut detail the code already says.") }

    $found = @([regex]::Matches($body, '(?m)^## (.+?)\s*$') | ForEach-Object { $_.Groups[1].Value })
    if (($found -join '|') -ne ($Headings -join '|')) {
        $errors.Add("${rel}: headings must be exactly: $($Headings -join ', '). Found: $($found -join ', ')")
    }
    Test-History $rel $body

    if ($global) {
        if ($readme -notmatch "systems/$([regex]::Escape($name))\.md") { $errors.Add("${rel}: not listed in docs/README.md") }
        if ($map -notmatch "``$([regex]::Escape($name))``") { $errors.Add("${rel}: not named in docs/map.md") }
    }
}

if ($global) {
    # The one block deliberately written twice must stay identical.
    $pattern = '(?s)<!-- boundary:start -->(.*?)<!-- boundary:end -->'
    $a = [regex]::Match((Read-Text 'CLAUDE.md'), $pattern)
    $b = [regex]::Match((Read-Text 'AGENTS.md'), $pattern)
    if (-not $a.Success -or -not $b.Success) { $errors.Add("CLAUDE.md/AGENTS.md: boundary block markers missing") }
    elseif ($a.Groups[1].Value.Trim() -ne $b.Groups[1].Value.Trim()) { $errors.Add("CLAUDE.md and AGENTS.md: the simulation boundary blocks differ. Copy CLAUDE.md's into AGENTS.md.") }
}

if ($errors.Count -eq 0) {
    Write-Host "doc-budget: clean ($($cards.Count) cards)"
    exit 0
}
Write-Host "doc-budget: $($errors.Count) violation(s)"
foreach ($e in $errors) { Write-Host "  $e" }
exit 1
