<#
.SYNOPSIS
    Reports OKF documents whose sources have moved since the document was last
    verified. Deterministic, read-only, no LLM.

.DESCRIPTION
    Every document in the OKF bundle declares, in its YAML frontmatter, the code
    files it describes (`sources:`) and the commit it was last checked against
    (`verified_at_commit:`). This script asks git one question per source:

        is the source's last commit an ancestor of the doc's verified commit?

    If not, the source changed after the doc was checked and the doc is SUSPECT.
    That is the whole freshness model -- it is what makes documentation drift a
    thing that fails a check rather than a thing you happen to notice.

    One refinement: when the source is itself a markdown document, commits that
    changed only its YAML frontmatter do not count as the source moving. Bundle
    documents cite one another, so stamping one document's verified_at_commit
    would otherwise mark every document citing it suspect -- on a change that
    says nothing about their subject -- and each re-verification would generate
    the next. See Get-LastCommit.

    Also reports documents past their `stale_after:` date, and warns about
    documents that carry sources but no `verified_at_commit` to check them
    against.

    Exit code is 0 when nothing is suspect or stale, 1 otherwise. Warnings do
    not affect the exit code.

.USAGE
        powershell -File scripts\okf-stale.ps1

    See docs/index.md for the bundle layout and the frontmatter conventions.
#>

$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

# The bundle root plus the three external member directories. The .claude files
# cannot move into docs/ -- Claude Code loads the rules into every session and
# addresses the skills and commands by path -- so the check reaches out to them
# instead. See docs/index.md.
$SearchDirs = @(
    "docs",
    ".claude\rules",
    ".claude\skills",
    ".claude\commands"
)

# Reserved OKF filenames. index.md is a navigation node and carries no signal
# layer; log.md is chronological history. Neither describes code.
$ReservedNames = @("index.md", "log.md")

# Parses the subset of YAML frontmatter this check needs: verified_at_commit,
# stale_after, and the `resource:` of each entry under `sources:`.
#
# Hand-parsed rather than pulled from a YAML module, which Windows PowerShell
# 5.1 does not ship. The `sources:` block is tracked explicitly so that a
# `resource:` nested under `executor:` or `attester:` -- which an Attested
# Computation carries -- is not mistaken for a source.
function Read-Frontmatter {
    param([string]$Path)

    $lines = Get-Content -Path $Path
    if ($lines.Count -eq 0 -or $lines[0].Trim() -ne '---') {
        return $null
    }

    $result = @{
        VerifiedAtCommit = $null
        StaleAfter       = $null
        Sources          = @()
    }

    $inSources = $false
    $closed = $false

    for ($i = 1; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]

        if ($line.Trim() -eq '---') { $closed = $true; break }

        # A non-indented key ends the sources block and starts a new top-level key.
        if ($line -match '^[A-Za-z_]') {
            $inSources = ($line -match '^sources:')
            if ($line -match '^verified_at_commit:\s*(\S+)') {
                $result.VerifiedAtCommit = $Matches[1].Trim('"', "'")
            }
            elseif ($line -match '^stale_after:\s*(\S+)') {
                $result.StaleAfter = $Matches[1].Trim('"', "'")
            }
            continue
        }

        if ($inSources -and $line -match '^\s+-?\s*resource:\s*(.+?)\s*$') {
            $result.Sources += $Matches[1].Trim('"', "'")
        }
    }

    if (-not $closed) { return $null }
    return $result
}

# The file's content at a revision, as a single string, or $null when the path
# does not exist there (a root commit, or the commit that added the file).
function Get-BlobAt {
    param([string]$Rev, [string]$RepoRelativePath)

    Push-Location $RepoRoot
    try {
        $text = & git show "${Rev}:${RepoRelativePath}" 2>$null
        if ($LASTEXITCODE -ne 0) { return $null }
    }
    catch {
        return $null
    }
    finally {
        Pop-Location
    }

    if ($null -eq $text) { return "" }
    return ($text -join "`n")
}

# Everything after the closing '---' of a YAML frontmatter block. Text with no
# frontmatter is returned unchanged, so this is safe to call on anything.
function Remove-Frontmatter {
    param([string]$Text)

    if ([string]::IsNullOrEmpty($Text)) { return "" }

    $lines = $Text -split "`n"
    if ($lines.Count -eq 0 -or $lines[0].Trim() -ne '---') { return $Text }

    for ($i = 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq '---') {
            return (($lines[($i + 1)..($lines.Count - 1)]) -join "`n")
        }
    }

    # An unterminated block is not frontmatter. Treat the whole file as body
    # rather than silently discarding it.
    return $Text
}

# True when a commit changed a markdown file's body, not only its frontmatter.
# Adding the file counts as a body change, as does any commit we cannot compare.
function Test-BodyChanged {
    param([string]$Sha, [string]$RepoRelativePath)

    $after = Get-BlobAt -Rev $Sha -RepoRelativePath $RepoRelativePath
    if ($null -eq $after) { return $true }

    $before = Get-BlobAt -Rev "${Sha}^" -RepoRelativePath $RepoRelativePath
    if ($null -eq $before) { return $true }

    return (Remove-Frontmatter $before) -ne (Remove-Frontmatter $after)
}

# How far back to walk looking for a substantive commit before giving up and
# reporting the newest one. A document whose last 50 commits were all
# frontmatter is not a case worth optimising for.
$FrontmatterWalkLimit = 50

# Last commit that substantively touched a path, or $null if git does not track
# it.
#
# For a markdown source, commits that changed only the YAML frontmatter are
# skipped. Documents in this bundle cite one another, so stamping a document's
# own verified_at_commit edits a file that other documents declare as a source
# -- which would mark every one of them suspect on a change that says nothing
# about their subject. Re-verifying then permanently generates its own next
# round of re-verification. Skipping frontmatter-only commits is what lets that
# reach a fixed point.
#
# Non-markdown sources are unaffected: a .cs file has no frontmatter, and the
# very first commit reported for it is returned as before.
function Get-LastCommit {
    param([string]$RepoRelativePath)

    Push-Location $RepoRoot
    try {
        $shas = @(& git log --format=%H -n $FrontmatterWalkLimit -- $RepoRelativePath)
        if ($LASTEXITCODE -ne 0) { return $null }
    }
    finally {
        Pop-Location
    }

    $shas = @($shas | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($shas.Count -eq 0) { return $null }

    if ($RepoRelativePath -notmatch '\.md$') { return $shas[0].Trim() }

    foreach ($sha in $shas) {
        if (Test-BodyChanged -Sha $sha.Trim() -RepoRelativePath $RepoRelativePath) {
            return $sha.Trim()
        }
    }

    # Every commit in the window was frontmatter-only. Report the newest rather
    # than claiming the file never substantively moved.
    return $shas[0].Trim()
}

# True when $Ancestor is an ancestor of $Descendant, or the same commit.
# $null means git could not answer (an unknown or ambiguous revision).
function Test-IsAncestor {
    param([string]$Ancestor, [string]$Descendant)

    Push-Location $RepoRoot
    try {
        & git merge-base --is-ancestor $Ancestor $Descendant
        $code = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($code -eq 0) { return $true }
    if ($code -eq 1) { return $false }
    return $null
}

$suspect = @()
$stale = @()
$warnings = @()
$checkedDocs = 0
$checkedSources = 0

foreach ($dir in $SearchDirs) {
    $full = Join-Path $RepoRoot $dir
    if (-not (Test-Path $full)) {
        $warnings += "Directory not found, skipped: $dir"
        continue
    }

    $files = Get-ChildItem -Path $full -Filter *.md -Recurse -File
    foreach ($file in $files) {
        if ($ReservedNames -contains $file.Name.ToLower()) { continue }

        $rel = $file.FullName.Substring($RepoRoot.Length + 1)
        $fm = Read-Frontmatter -Path $file.FullName

        if ($null -eq $fm) {
            $warnings += "$rel : no parseable YAML frontmatter"
            continue
        }

        $checkedDocs++

        if ($null -ne $fm.StaleAfter) {
            # Must be declared [datetime], not $null: Windows PowerShell 5.1 resolves
            # the TryParse overload from the [ref] target's type and cannot bind an
            # untyped one.
            [datetime]$cutoff = [datetime]::MinValue
            if ([datetime]::TryParse($fm.StaleAfter, [ref]$cutoff)) {
                if ((Get-Date) -ge $cutoff) {
                    $stale += [pscustomobject]@{ Doc = $rel; StaleAfter = $fm.StaleAfter }
                }
            }
            else {
                $warnings += "$rel : could not parse stale_after '$($fm.StaleAfter)'"
            }
        }

        if ($fm.Sources.Count -eq 0) { continue }

        if ([string]::IsNullOrWhiteSpace($fm.VerifiedAtCommit)) {
            $warnings += "$rel : declares $($fm.Sources.Count) source(s) but no verified_at_commit"
            continue
        }

        foreach ($source in $fm.Sources) {
            # Sources are repo-root-relative paths. Anything else -- a URL, an
            # external identifier -- is not ours to check.
            $sourcePath = Join-Path $RepoRoot $source
            if (-not (Test-Path $sourcePath)) {
                $warnings += "$rel : source not found in repo, skipped: $source"
                continue
            }

            $sourceCommit = Get-LastCommit -RepoRelativePath $source
            if ($null -eq $sourceCommit) {
                $warnings += "$rel : source not tracked by git, skipped: $source"
                continue
            }

            $checkedSources++

            $isAncestor = Test-IsAncestor -Ancestor $sourceCommit -Descendant $fm.VerifiedAtCommit
            if ($null -eq $isAncestor) {
                $warnings += "$rel : git could not compare $($sourceCommit.Substring(0,7)) against $($fm.VerifiedAtCommit)"
                continue
            }

            if (-not $isAncestor) {
                $suspect += [pscustomobject]@{
                    Doc          = $rel
                    Source       = $source
                    MovedIn      = $sourceCommit.Substring(0, 7)
                    VerifiedAt   = $fm.VerifiedAtCommit
                }
            }
        }
    }
}

Write-Host ""
Write-Host "=============================="
Write-Host " OKF document freshness"
Write-Host "=============================="
Write-Host "Documents checked: $checkedDocs"
Write-Host "Sources checked:   $checkedSources"
Write-Host ""

if ($suspect.Count -gt 0) {
    Write-Host "SUSPECT -- a source moved after the document was verified:"
    foreach ($s in $suspect) {
        Write-Host "  $($s.Doc)"
        Write-Host "      source:   $($s.Source)"
        Write-Host "      moved in: $($s.MovedIn)   verified at: $($s.VerifiedAt)"
    }
    Write-Host ""
}

if ($stale.Count -gt 0) {
    Write-Host "STALE -- past the document's own stale_after date:"
    foreach ($s in $stale) {
        Write-Host "  $($s.Doc)  (stale_after: $($s.StaleAfter))"
    }
    Write-Host ""
}

if ($warnings.Count -gt 0) {
    Write-Host "Warnings (do not affect exit code):"
    foreach ($w in $warnings) { Write-Host "  $w" }
    Write-Host ""
}

if ($suspect.Count -eq 0 -and $stale.Count -eq 0) {
    Write-Host "All documents current."
    Write-Host ""
    exit 0
}

# Re-verification is a human act: read the doc against its changed sources,
# correct what drifted, then bump verified_at_commit and add a `verified:` entry.
# An agent may record an agent-tier entry; only a human: actor may claim the
# human-reviewed tier. See docs/index.md.
Write-Host "Re-verify each document above against its changed sources, then bump verified_at_commit."
exit 1
