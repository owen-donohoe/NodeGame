<#
.SYNOPSIS
    Scans the simulation layer for constructs the determinism contract forbids.
    Deterministic, read-only, no LLM.

.DESCRIPTION
    This is the MECHANICAL SUBSET of .claude/skills/determinism-guard.md -- the
    items a regex can decide on its own. It is not a replacement for that
    checklist. Items 6 (sort tiebreakers), 7 (tick order), 8 (hasher
    registration), 9 (command/serializer pairing) and 10 (view boundary) all
    need a reader who understands the change, and they stay where they are.

    What this catches is the careless half: a `float` that slipped in, a
    `Dictionary` iterated in tick order, a `DateTime` used to measure anything.
    Those are unambiguous, and unambiguous violations should not need a human
    to notice them.

    Comments and string literals are stripped before matching. This is not
    fussiness -- GameSimulation.cs contains the phrase "double-counting" in a
    doc comment, which a naive grep reports as a floating-point violation.

    Exit code is 0 when clean, 1 when any violation is found.

.USAGE
        powershell -File scripts\sim-guard.ps1
        powershell -File scripts\sim-guard.ps1 -Path Assets/Scripts/Game/Simulation

    Runs under Windows PowerShell 5.1 and under pwsh on Linux (the CI gate calls
    it on both). Paths are written with forward slashes for that reason -- on
    Linux a literal "\" is an ordinary filename character, not a separator.

    Run automatically by the pre-commit hook in scripts/hooks/, but only for
    commits that actually touch the simulation layer.

    See docs/simulation-rules.md for the contract, and
    .claude/skills/determinism-guard.md for the full review checklist.
#>

param(
    [string]$Path = "Assets/Scripts/Game/Simulation"
)

$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ScanRoot = Join-Path $RepoRoot $Path

# Each rule cites the contract clause it enforces, so a failure teaches the
# reason rather than just naming a banned word.
$Rules = @(
    @{
        Name    = "UnityEngine reference"
        Pattern = 'UnityEngine'
        Why     = "No UnityEngine references of any kind in Simulation/."
    },
    @{
        Name    = "Floating-point type"
        Pattern = '\b(float|double|decimal)\b'
        Why     = "Integer math only. Float results differ across platforms and desync peers."
    },
    @{
        Name    = "Wall-clock API"
        Pattern = '\b(DateTime|DateTimeOffset|Stopwatch)\b|\bEnvironment\.TickCount\b'
        Why     = "No wall-clock time. Simulation advances by tick count and nothing else."
    },
    @{
        Name    = "Frame/time API"
        Pattern = '\bTime\.(deltaTime|fixedDeltaTime|time|unscaledTime|unscaledDeltaTime|timeScale|frameCount|realtimeSinceStartup)\b'
        Why     = "No frame-rate-dependent APIs. Two peers do not share a frame rate."
    },
    @{
        Name    = "Nondeterministic collection"
        Pattern = '\b(Dictionary|HashSet|SortedDictionary|ConcurrentDictionary|ConcurrentBag)\s*<'
        Why     = "Arrays or List<T> only. Hash-ordered iteration is not stable across runtimes."
    },
    @{
        Name    = "Unseeded RNG"
        Pattern = '\bnew\s+(System\.)?Random\s*\(\s*\)'
        Why     = "Only the seeded RNG stored in SimulationState, advanced inside SimulateTick."
    }
)

# Blanks out comments and string literals while preserving line numbers, so a
# reported line number still points at the real line in the file.
#
# Hand-written scanner rather than a regex pass: a `//` inside a string and a
# `"` inside a comment each break the naive version, and they break it in the
# direction that hides violations.
function Get-CodeOnlyLines {
    param([string[]]$Lines)

    $inBlockComment = $false
    $result = @()

    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $line = $Lines[$i]
        $sb = New-Object System.Text.StringBuilder
        $j = 0
        $inString = $false
        $inVerbatim = $false
        $inChar = $false

        while ($j -lt $line.Length) {
            $c = $line[$j]
            if ($j + 1 -lt $line.Length) { $next = $line[$j + 1] } else { $next = [char]0 }

            if ($inBlockComment) {
                if ($c -eq '*' -and $next -eq '/') { $inBlockComment = $false; $j += 2 }
                else { $j++ }
                continue
            }

            if ($inString) {
                if ($inVerbatim) {
                    # "" is an escaped quote inside a verbatim string.
                    if ($c -eq '"' -and $next -eq '"') { $j += 2; continue }
                    if ($c -eq '"') { $inString = $false; $inVerbatim = $false }
                    $j++
                    continue
                }
                if ($c -eq '\') { $j += 2; continue }
                if ($c -eq '"') { $inString = $false }
                $j++
                continue
            }

            if ($inChar) {
                if ($c -eq '\') { $j += 2; continue }
                if ($c -eq "'") { $inChar = $false }
                $j++
                continue
            }

            # Normal code.
            if ($c -eq '/' -and $next -eq '/') { break }   # rest of the line is a comment
            if ($c -eq '/' -and $next -eq '*') { $inBlockComment = $true; $j += 2; continue }
            if ($c -eq '@' -and $next -eq '"') { $inString = $true; $inVerbatim = $true; $j += 2; continue }
            if ($c -eq '"') { $inString = $true; $j++; continue }
            if ($c -eq "'") { $inChar = $true; $j++; continue }

            [void]$sb.Append($c)
            $j++
        }

        $result += [pscustomobject]@{
            Number = $i + 1
            Code   = $sb.ToString()
        }
    }

    return $result
}

if (-not (Test-Path $ScanRoot)) {
    Write-Host "sim-guard: path not found: $Path"
    exit 1
}

$violations = @()
$scannedFiles = 0
$scannedLines = 0

$files = Get-ChildItem -Path $ScanRoot -Filter "*.cs" -Recurse -File
foreach ($file in $files) {
    $rel = $file.FullName.Substring($RepoRoot.Length + 1)
    $lines = @(Get-Content -Path $file.FullName)
    if ($lines.Count -eq 0) { continue }

    $scannedFiles++
    $scannedLines += $lines.Count

    foreach ($entry in (Get-CodeOnlyLines -Lines $lines)) {
        if ([string]::IsNullOrWhiteSpace($entry.Code)) { continue }

        foreach ($rule in $Rules) {
            $match = [regex]::Match($entry.Code, $rule.Pattern)
            if ($match.Success) {
                $violations += [pscustomobject]@{
                    File   = $rel
                    Line   = $entry.Number
                    Rule   = $rule.Name
                    Why    = $rule.Why
                    Text   = $entry.Code.Trim()
                }
            }
        }
    }
}

Write-Host ""
Write-Host "=============================="
Write-Host " Simulation determinism guard"
Write-Host "=============================="
Write-Host "Scanned: $scannedFiles file(s), $scannedLines line(s) under $Path"
Write-Host ""

if ($violations.Count -eq 0) {
    Write-Host "No forbidden constructs found."
    Write-Host ""
    Write-Host "This is the mechanical subset only. Sort tiebreakers, tick order, hasher"
    Write-Host "registration, command/serializer pairing and the view boundary still need"
    Write-Host "determinism-guard.md and a reader."
    Write-Host ""
    exit 0
}

Write-Host "VIOLATIONS -- the determinism contract forbids these:"
Write-Host ""
foreach ($v in $violations) {
    Write-Host "  $($v.File):$($v.Line)  [$($v.Rule)]"
    Write-Host "      $($v.Text)"
    Write-Host "      $($v.Why)"
    Write-Host ""
}

Write-Host "$($violations.Count) violation(s). See docs/simulation-rules.md."
exit 1
