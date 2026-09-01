<#
.SYNOPSIS
    Runs the NodeWar EditMode test suite (Assets/Tests/EditMode/Tests) headlessly
    through Unity's batch-mode test runner and prints a readable pass/fail summary.

.DESCRIPTION
    Launches Unity in -batchmode, executes every EditMode test, writes an NUnit3
    results XML file plus a Unity log, then parses the XML and reports:
      - total tests run
      - how many passed / failed
      - the name and failure message of every failed test
    The script's own exit code reflects whether all tests passed (0) or not
    (non-zero) -- see the exit-code note near the bottom.

.USAGE
    From anywhere:
        powershell -NoProfile -ExecutionPolicy Bypass -File scripts\run-tests.ps1

    NOTE: -ExecutionPolicy Bypass is required, not decoration. Windows client
    defaults to Restricted when no scope sets a policy, and PowerShell then
    refuses to load this file at all ("running scripts is disabled on this
    system") -- the run dies before Unity is reached. Launching as
    .\scripts\run-tests.ps1 from an interactive session hits the same wall
    unless that session's policy already allows local scripts.

    NOTE: Unity locks a project while it's open in the Editor. If you already
    have this project open, close it first -- otherwise this batch-mode run
    will fail to acquire the project lock. Use run-tests-live.ps1 instead.
#>

$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe"
$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ResultsFile = "$PSScriptRoot\..\TestResults\results.xml"
$LogFile = "$PSScriptRoot\..\TestResults\unity-test.log"

# 0. Fail fast with a clear message if this machine's Unity install isn't at the
#    hardcoded path/version above (a different Hub install location, or a Unity
#    upgrade, would otherwise surface as an opaque "term not recognized" error).
if (-not (Test-Path $UnityExe)) {
    Write-Host "Unity executable not found at: $UnityExe"
    Write-Host "Update `$UnityExe at the top of this script to match your local Unity install."
    exit 1
}

# 1. Ensure the TestResults/ output folder exists.
$resultsDir = Split-Path $ResultsFile -Parent
if (-not (Test-Path $resultsDir)) {
    New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null
}

# 2. Run Unity's EditMode test runner in batch mode.
Write-Host "Running Unity EditMode tests..."
Write-Host "  Project: $ProjectPath"
Write-Host "  Results: $ResultsFile"
Write-Host "  Log:     $LogFile"
Write-Host ""

& $UnityExe `
    -batchmode `
    -runTests `
    -testPlatform EditMode `
    -projectPath $ProjectPath `
    -testResults $ResultsFile `
    -logFile $LogFile `
    -quit

$unityExitCode = $LASTEXITCODE
Write-Host "Unity process exited with code $unityExitCode."
Write-Host ""

# 3. Parse the results XML and report a human-readable summary.
$finalExitCode = 0

if (Test-Path $ResultsFile) {
    [xml]$xml = Get-Content -Path $ResultsFile -Raw
    $testRun = $xml.'test-run'

    if ($null -eq $testRun) {
        Write-Host "Results file did not contain a <test-run> root element -- treating as a failed run."
        Write-Host "Check the log for details: $LogFile"
        $finalExitCode = if ($unityExitCode -ne 0) { $unityExitCode } else { 1 }
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
        }
        else {
            Write-Host "All tests passed."
            Write-Host ""
        }

        # Determine success/failure from the parsed results rather than Unity's raw
        # process exit code: Unity's -runTests exit code reflects whether the test
        # RUN completed, not whether the tests themselves passed (it returns 0 even
        # when tests fail, and only goes non-zero if the run couldn't start/finish
        # at all). So: any failed test -> non-zero; otherwise fall back to Unity's
        # own exit code in case the run itself errored out.
        if ($failed -gt 0) {
            $finalExitCode = 1
        }
        elseif ($unityExitCode -ne 0) {
            $finalExitCode = $unityExitCode
        }
        else {
            $finalExitCode = 0
        }
    }
}
else {
    Write-Host "No results file found at $ResultsFile -- Unity likely failed to run the tests."
    Write-Host "Check the log for details: $LogFile"
    $finalExitCode = if ($unityExitCode -ne 0) { $unityExitCode } else { 1 }
}

# 4. Exit with a code reflecting overall pass/fail (0 = all passed,
#    non-zero = failures or a Unity error).
exit $finalExitCode
