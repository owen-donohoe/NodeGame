<#
.SYNOPSIS
    SessionStart wrapper around okf-stale.ps1. Emits the freshness report as
    Claude Code hook JSON, and only when there is something to say.

.DESCRIPTION
    The point of the freshness check is that the suspect-document list is in
    front of whoever is about to work, before they start -- not available on
    request to someone who remembers to ask. This wrapper is what makes that
    automatic.

    Silence when clean is deliberate. A hook that reports "all documents
    current" at every session start trains the reader to skip it, and then it
    is not a signal any more. It speaks only when a document's sources moved.

    Output contract (see the hooks documentation):
      systemMessage       -- one line, shown to the user in the terminal
      hookSpecificOutput.additionalContext
                          -- the full report, injected into the model's context

.USAGE
    Not run by hand. Wired in .claude/settings.json under SessionStart.
    To see what it would say:

        powershell -File scripts\okf-stale-hook.ps1

    See docs/index.md for the bundle layout, and scripts/hooks/README.md for
    the git-side guard that runs the same check at commit time.
#>

$ErrorActionPreference = 'Stop'

$checker = Join-Path $PSScriptRoot "okf-stale.ps1"

# Run as a child process so the checker's console output is capturable. Its
# own Write-Host calls are not visible to the pipeline any other way.
$report = & powershell -NoProfile -ExecutionPolicy Bypass -File $checker 2>&1 | Out-String
$code = $LASTEXITCODE

# Exit 0 means nothing is suspect and nothing is past its stale_after date.
# Say nothing at all.
if ($code -eq 0) { exit 0 }

$payload = @{
    systemMessage = "OKF freshness: one or more documents have sources that moved since they were last verified. Details are in context."
    hookSpecificOutput = @{
        hookEventName     = "SessionStart"
        additionalContext = @"
The OKF document freshness check ran at session start and found documents whose
sources have changed since they were last verified. Treat these documents as
SUSPECT: read them against their current sources before relying on any claim
they make.

Re-verification is a human act. Do not stamp ``verified:`` or bump
``verified_at_commit`` on the user's behalf -- report what drifted and let them
decide.

$report
"@
    }
} | ConvertTo-Json -Depth 5 -Compress

Write-Output $payload
exit 0
