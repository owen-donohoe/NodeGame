# Real-git fixture: stale/clean states, vault discovery, dashboard grouping and
# personal text preservation. No packages, credentials or working-tree edits.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$fixture = Join-Path $root ('TestResults/doc-review-fixture-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
foreach ($dir in @('scripts', 'docs/systems', 'notes', '.claude/rules', '.claude/skills', '.claude/commands')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $fixture $dir) | Out-Null
}
Copy-Item (Join-Path $PSScriptRoot 'okf-stale.ps1'), (Join-Path $PSScriptRoot 'merge-doc-review.ps1') (Join-Path $fixture 'scripts')
Push-Location $fixture
try {
    git init --quiet
    git config user.name 'Documentation fixture'
    git config user.email 'fixture@example.invalid'
    'public class Before {}' | Set-Content source.cs
    git add source.cs
    git commit --quiet -m 'feat: fixture source'
    $sha = git rev-parse HEAD
    foreach ($path in @('docs/systems/sample.md', 'notes/Sample.md')) {
        "---`nsources: [source.cs]`nverified: $sha`n---`n# Sample`n`n## My understanding`n`nPersonal text stays." | Set-Content $path
    }
    git add docs notes
    git commit --quiet -m 'docs: fixture notes'
    'public class After {}' | Set-Content source.cs
    git add source.cs
    git commit --quiet -m 'refactor: changed declaration'
    $personalBefore = Get-Content notes/Sample.md -Raw
    & powershell -NoProfile -File scripts/merge-doc-review.ps1 *> stale-console.txt
    if ($LASTEXITCODE -ne 0) { throw 'Stale dashboard generation failed' }
    $report = Get-Content TestResults/doc-review.json -Raw | ConvertFrom-Json
    if (@($report.review).Count -ne 2) { throw 'Expected both card and vault note in REVIEW' }
    $dashboard = Get-Content 'notes/Merge review.md' -Raw
    if ($dashboard -notmatch '\[docs/systems/sample.md\]' -or $dashboard -notmatch '\[notes/Sample.md\]') {
        throw 'Dashboard omitted a card or note'
    }
    if ((Get-Content notes/Sample.md -Raw) -cne $personalBefore) { throw 'Personal note changed' }
    $sha = git rev-parse HEAD
    foreach ($path in @('docs/systems/sample.md', 'notes/Sample.md')) {
        (Get-Content $path -Raw) -replace 'verified: [0-9a-f]+', "verified: $sha" | Set-Content $path -NoNewline
    }
    & powershell -NoProfile -File scripts/merge-doc-review.ps1 *> clean-console.txt
    if ($LASTEXITCODE -ne 0) { throw 'Clean dashboard generation failed' }
    $report = Get-Content TestResults/doc-review.json -Raw | ConvertFrom-Json
    if (@($report.review).Count -ne 0 -or @($report.touched).Count -ne 0 -or @($report.warnings).Count -ne 0) {
        throw 'Expected clean findings and no generated-dashboard warning'
    }
    "---`nsources: [docs/systems/]`nverified: $sha`n---`n# Directory reader" | Set-Content notes/Directory.md
    git add docs notes
    git commit --quiet -m 'docs: verification stamps only in source directory'
    & powershell -NoProfile -File scripts/merge-doc-review.ps1 *> directory-console.txt
    if ($LASTEXITCODE -ne 0) { throw 'Directory-source dashboard failed' }
    $report = Get-Content TestResults/doc-review.json -Raw | ConvertFrom-Json
    if (@($report.review).Count -ne 0 -or @($report.touched).Count -ne 0) {
        throw 'Frontmatter-only directory changes created false review work'
    }
    Add-Content docs/systems/sample.md 'A changed explanation.'
    git add docs
    git commit --quiet -m 'docs: substantive source change'
    & powershell -NoProfile -File scripts/merge-doc-review.ps1 *> body-console.txt
    if ($LASTEXITCODE -ne 0) { throw 'Substantive directory-source dashboard failed' }
    $report = Get-Content TestResults/doc-review.json -Raw | ConvertFrom-Json
    if (@(@($report.review) + @($report.touched) | Where-Object Doc -eq 'notes\Directory.md').Count -ne 1) {
        throw 'Substantive directory-source change was missed'
    }
    Write-Output 'merge-doc-review: stale/clean reports, card/vault discovery and personal text preservation passed.'
}
finally { Pop-Location }
