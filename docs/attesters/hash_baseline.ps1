<#
.SYNOPSIS
    Attester for docs/computations/determinism-baseline.md. Turns a test run's
    receipt into a verdict. Deterministic, read-only, no LLM.

.DESCRIPTION
    The executor (docs/skills/run-editmode-tests.md) leaves TestResults/results.xml.
    That file is the receipt. This script decides whether it actually attests the
    determinism gate at the current commit, which is a narrower question than
    "did the test run pass":

      1. Both DeterminismBaselineTests cases are present and Passed. A receipt
         that simply omits them is NOT a pass -- a suite where they were filtered
         out, renamed, or skipped fails here rather than sliding through green.
      2. The receipt is newer than the last commit touching Simulation/. A stale
         receipt from before the change under review proves nothing about it.

    A verifier that can be talked into a pass is not a verifier, so this is plain
    PowerShell with no model in the loop.

.USAGE
        powershell -File docs\attesters\hash_baseline.ps1

    Exit 0 = PASS. Exit 1 = FAIL. Exit 2 = no receipt to judge.
#>

$ErrorActionPreference = 'Stop'

# Forward slashes throughout: Windows accepts them, Linux requires them, and a
# literal "\" in a path string is an ordinary filename character on Linux rather
# than a separator. Join-Path's multi-segment form is PowerShell 6+, and this
# script also runs under Windows PowerShell 5.1, so two arguments only.
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
$ResultsFile = Join-Path $RepoRoot "TestResults/results.xml"
$SimulationDir = "Assets/Scripts/Game/Simulation"

# The cases the computation sanctions. Both must appear and both must pass.
$RequiredCases = @(
    "EmptyTick_100Iterations_ProducesDeterministicHash",
    "MoveAndCombat_ProducesDeterministicHash"
)

function Write-Verdict {
    param([string]$Verdict, [string[]]$Reasons, [string]$Commit)

    Write-Host ""
    Write-Host "=============================="
    Write-Host " Determinism baseline attester"
    Write-Host "=============================="
    Write-Host "computation: docs/computations/determinism-baseline.md"
    Write-Host "receipt:     TestResults/results.xml"
    Write-Host "commit:      $Commit"
    Write-Host "verdict:     $Verdict"
    if ($Reasons.Count -gt 0) {
        Write-Host ""
        foreach ($r in $Reasons) { Write-Host "  - $r" }
    }
    Write-Host ""
}

Push-Location $RepoRoot
try {
    $headSha = & git rev-parse --short HEAD
    if ($LASTEXITCODE -ne 0) { $headSha = "unknown" }
    $headSha = "$headSha".Trim()

    # Commit date of the last change to the simulation layer, as a sortable ISO
    # timestamp. A receipt older than this cannot speak to the current code.
    $simCommitDate = & git log -1 --format=%cI -- $SimulationDir
    if ($LASTEXITCODE -ne 0) { $simCommitDate = "" }
    $simCommitDate = "$simCommitDate".Trim()

    $simCommitSha = & git log -1 --format=%h -- $SimulationDir
    if ($LASTEXITCODE -ne 0) { $simCommitSha = "" }
    $simCommitSha = "$simCommitSha".Trim()
}
finally {
    Pop-Location
}

# --- The receipt must exist before anything can be judged. ---
if (-not (Test-Path $ResultsFile)) {
    Write-Verdict -Verdict "NO RECEIPT" -Commit $headSha -Reasons @(
        "TestResults/results.xml not found.",
        "Produce a receipt first: see docs/skills/run-editmode-tests.md"
    )
    exit 2
}

$reasons = @()
$failed = $false

# --- Check 1: both sanctioned cases present and passed. ---
try {
    [xml]$xml = Get-Content -Path $ResultsFile -Raw
}
catch {
    Write-Verdict -Verdict "FAIL" -Commit $headSha -Reasons @(
        "results.xml could not be parsed as XML: $($_.Exception.Message)"
    )
    exit 1
}

if ($null -eq $xml.'test-run') {
    Write-Verdict -Verdict "FAIL" -Commit $headSha -Reasons @(
        "results.xml has no <test-run> root element; the run did not complete."
    )
    exit 1
}

foreach ($case in $RequiredCases) {
    $nodes = $xml.SelectNodes("//test-case[contains(@fullname,'$case')]")

    if ($null -eq $nodes -or $nodes.Count -eq 0) {
        $reasons += "Required case not present in the receipt: $case"
        $failed = $true
        continue
    }

    foreach ($n in $nodes) {
        if ($n.result -ne 'Passed') {
            $reasons += "Required case did not pass ($($n.result)): $case"
            # NUnit3 wraps the message in CDATA, so `.failure.message` is an
            # XmlElement rather than a string -- take its text, and only its first
            # line, since a failed assertion's message carries a stack trace.
            $failureNode = $n.SelectSingleNode("failure/message")
            if ($null -ne $failureNode) {
                $text = "$($failureNode.InnerText)".Trim()
                if ($text.Length -gt 0) {
                    $firstLine = ($text -split "`r?`n")[0]
                    $reasons += "    $firstLine"
                }
            }
            $failed = $true
        }
    }
}

# --- Check 2: the receipt is newer than the simulation code it claims to attest. ---
$receiptWritten = (Get-Item $ResultsFile).LastWriteTime

if ([string]::IsNullOrWhiteSpace($simCommitDate)) {
    $reasons += "Could not read the last commit touching $SimulationDir; skipped the staleness check."
}
else {
    # Must be declared [datetime], not $null: Windows PowerShell 5.1 resolves the
    # TryParse overload from the [ref] target's type and cannot bind an untyped one.
    [datetime]$simChangedAt = [datetime]::MinValue
    if ([datetime]::TryParse($simCommitDate, [ref]$simChangedAt)) {
        if ($receiptWritten -lt $simChangedAt) {
            $reasons += "Receipt is stale: written $($receiptWritten.ToString('s')), but $SimulationDir last changed in $simCommitSha at $($simChangedAt.ToString('s'))."
            $reasons += "    Re-run the suite so the receipt covers the current code."
            $failed = $true
        }
    }
    else {
        $reasons += "Could not parse the simulation commit date '$simCommitDate'; skipped the staleness check."
    }
}

# --- Check 3: the receipt is newer than the working tree it claims to attest. ---
#
# Check 2 only compares against COMMITTED state, so an uncommitted edit slips
# past it. That matters more than it sounds: run-tests-live.ps1 hands the trigger
# to an Editor that may not have noticed the file change yet, and TestBridge's
# "is the Editor idle" wait cannot distinguish "finished compiling" from "has not
# started". The suite then runs against stale assemblies and reports green.
#
# Observed, not theoretical: changing baseHP and immediately re-running produced
# 8/8 passed against code whose real fingerprint had moved to a completely
# different value. A receipt older than the sources is not evidence about them.
$watchedDirs = @(
    (Join-Path $RepoRoot "Assets/Scripts/Game/Simulation"),
    (Join-Path $RepoRoot "Assets/Tests/EditMode/Tests")
)

$newestSource = $null
foreach ($dir in $watchedDirs) {
    if (-not (Test-Path $dir)) { continue }
    $candidate = Get-ChildItem -Path $dir -Filter *.cs -Recurse -File |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $candidate) { continue }
    if ($null -eq $newestSource -or $candidate.LastWriteTime -gt $newestSource.LastWriteTime) {
        $newestSource = $candidate
    }
}

if ($null -eq $newestSource) {
    $reasons += "Found no .cs sources to compare the receipt against; skipped the working-tree check."
}
elseif ($receiptWritten -lt $newestSource.LastWriteTime) {
    $rel = $newestSource.FullName.Substring($RepoRoot.Length + 1)
    $reasons += "Receipt predates the working tree: $rel was written $($newestSource.LastWriteTime.ToString('s')), receipt is $($receiptWritten.ToString('s'))."
    $reasons += "    The suite may have run against stale assemblies. Let Unity finish compiling, then re-run."
    $failed = $true
}

# --- Verdict. ---
if ($failed) {
    Write-Verdict -Verdict "FAIL" -Commit $headSha -Reasons $reasons
    Write-Host "The determinism gate is UNATTESTED at this commit. Do not treat the simulation change as safe."
    Write-Host ""
    exit 1
}

$reasons += "Both sanctioned cases present and passed."
$reasons += "Receipt written $($receiptWritten.ToString('s')), newer than the last Simulation/ commit ($simCommitSha)."
Write-Verdict -Verdict "PASS" -Commit $headSha -Reasons $reasons
exit 0
