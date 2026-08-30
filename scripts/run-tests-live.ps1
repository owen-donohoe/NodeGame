<#
.SYNOPSIS
    Runs the NodeWar EditMode test suite inside your ALREADY-OPEN Unity Editor,
    instead of spawning a second batch-mode Unity process. Use this when you want
    to keep the Editor open (run-tests.ps1's batch-mode approach can't run
    concurrently with an open Editor -- Unity locks the project to one process).

.DESCRIPTION
    Requires Assets/Scripts/Editor/TestBridge.cs to be compiled into the open
    Editor session (it's an [InitializeOnLoad] watcher, so it's live as soon as
    the project finishes compiling -- no extra setup per run).

    Protocol:
      1. This script writes a fresh GUID into TestResults/trigger.txt.
      2. TestBridge notices the new GUID and, once the Editor is idle (not
         compiling, not in Play Mode -- it waits rather than interrupting
         either), runs all EditMode tests inside the live Editor session.
      3. TestBridge writes TestResults/results.xml (real NUnit3 XML, the same
         format Unity's own -testResults batch-mode flag produces), then
         TestResults/done.txt containing the same GUID as a completion signal.
      4. This script polls for done.txt, confirms the GUID matches, then parses
         results.xml and prints a summary -- same output shape as run-tests.ps1.

    If the Editor isn't open, isn't compiled cleanly, or TestBridge.cs has a
    compile error, nothing will ever consume the trigger file and this script
    will time out.

.USAGE
    From anywhere:
        powershell -File scripts\run-tests-live.ps1
    or, from the project root in a PowerShell session:
        .\scripts\run-tests-live.ps1
#>

$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$TestResultsDir = Join-Path $ProjectRoot "TestResults"
$TriggerFile = Join-Path $TestResultsDir "trigger.txt"
$ResultsFile = Join-Path $TestResultsDir "results.xml"
$DoneFile = Join-Path $TestResultsDir "done.txt"
$TimeoutSeconds = 300

# 1. Ensure the TestResults/ output folder exists.
if (-not (Test-Path $TestResultsDir)) {
    New-Item -ItemType Directory -Path $TestResultsDir -Force | Out-Null
}

# 2. Write a fresh trigger with a unique run ID, so we can tell our own run's
#    completion signal apart from a stale done.txt left by a previous run.
$runId = [guid]::NewGuid().ToString()
Set-Content -Path $TriggerFile -Value $runId -NoNewline

Write-Host "Requesting EditMode test run from the live Editor (id=$runId)..."
Write-Host "  Trigger: $TriggerFile"
Write-Host "  Results: $ResultsFile"
Write-Host "  Waiting up to $TimeoutSeconds seconds for the Editor to pick this up."
Write-Host "  (Make sure the project is open in Unity and TestBridge.cs compiled cleanly.)"
Write-Host ""

# 3. Poll for TestBridge's completion signal.
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$matched = $false

while ((Get-Date) -lt $deadline) {
    if (Test-Path $DoneFile) {
        try {
            $doneId = (Get-Content -Path $DoneFile -Raw -ErrorAction Stop).Trim()
            if ($doneId -eq $runId) {
                $matched = $true
                break
            }
        }
        catch {
            # done.txt exists but is mid-write by TestBridge (it writes via a
            # temp file + move, so this should be rare/momentary) -- treat as
            # not-yet-ready and retry next poll rather than crashing.
        }
    }
    Start-Sleep -Milliseconds 500
}

if (-not $matched) {
    Write-Host "Timed out after $TimeoutSeconds seconds waiting for the Editor to finish the test run."
    Write-Host "Check that the Editor is open on this project and not stuck compiling."
    exit 2
}

Write-Host "Test run complete. Parsing results..."
Write-Host ""

# 4. Parse the results XML and report a human-readable summary.
$finalExitCode = 0

if (Test-Path $ResultsFile) {
    [xml]$xml = Get-Content -Path $ResultsFile -Raw
    $testRun = $xml.'test-run'

    if ($null -eq $testRun) {
        Write-Host "Results file did not contain a <test-run> root element -- treating as a failed run."
        $finalExitCode = 1
    }
    else {
        $total = [int]$testRun.total
        $passed = [int]$testRun.passed
        $failed = [int]$testRun.failed

        Write-Host "=============================="
        Write-Host " Unity EditMode Test Results"
        Write-Host "=============================="
        Write-Host "Total:  $total"
        Write-Host "Passed: $passed"
        Write-Host "Failed: $failed"
        Write-Host ""

        if ($failed -gt 0) {
            $failedCases = $xml.SelectNodes("//test-case[@result='Failed']")
            Write-Host "FAILED TESTS:"
            foreach ($case in $failedCases) {
                $name = $case.fullname
                if ([string]::IsNullOrEmpty($name)) { $name = $case.name }
                $message = $case.failure.message
                Write-Host "  - $name"
                if (-not [string]::IsNullOrEmpty($message)) {
                    Write-Host "    $message"
                }
            }
            Write-Host ""
            $finalExitCode = 1
        }
        else {
            Write-Host "All tests passed."
            Write-Host ""
            $finalExitCode = 0
        }
    }
}
else {
    Write-Host "TestBridge signaled completion but $ResultsFile is missing -- treating as a failed run."
    $finalExitCode = 1
}

# 5. Exit with a code reflecting overall pass/fail (0 = all passed,
#    non-zero = failures, timeout, or a missing results file).
exit $finalExitCode
