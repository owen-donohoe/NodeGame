<#
.SYNOPSIS
    Type-checks the Unity-side C# without opening Unity.

.DESCRIPTION
    Compiles the real sources under Assets/ against the real Unity assemblies
    and the project's already-built package assemblies, then throws the output
    away. Nothing is written into Assets/ and Unity never has to be running.

    Why this exists: dotnet/NodeWar.sln only covers Assets/Scripts/Game/Simulation,
    because that is the only assembly with no UnityEngine references. Everything
    else - the lobby, the HUD, the networking layer, the view - could previously
    only be compiled by opening the editor. During the UI Toolkit migration that
    is most of the code being changed, so "it compiles" was an assumption rather
    than a fact.

    What it is NOT: a faithful reproduction of Unity's assembly graph. Runtime
    and editor code are compiled into one assembly here, so it will not catch an
    editor-only API used from runtime code, and it does not run any tests. It
    catches syntax errors, missing usings, wrong API names and broken call sites,
    which is the great majority of what goes wrong when editing blind.

    Assembly-CSharp.dll from Library/ScriptAssemblies is deliberately excluded:
    it is the stale output of the very code being compiled, and referencing it
    makes every type in the project ambiguous with itself.

.PARAMETER ShowAll
    Show the full compiler output rather than just errors.

.EXAMPLE
    powershell -File scripts/compile-check.ps1
#>

[CmdletBinding()]
param(
    [switch]$ShowAll
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$AssetsDir = Join-Path $RepoRoot 'Assets'

Write-Host '=============================='
Write-Host ' Unity compile check'
Write-Host '=============================='

# --- Locate the editor that matches this project -------------------------

$versionFile = Join-Path $RepoRoot 'ProjectSettings/ProjectVersion.txt'
if (-not (Test-Path $versionFile)) {
    Write-Host "ProjectVersion.txt not found; is this the repo root?" -ForegroundColor Red
    exit 1
}

$versionLine = Get-Content $versionFile | Where-Object { $_ -match '^m_EditorVersion:' }
$unityVersion = ($versionLine -split ':\s*')[1].Trim()

$managed = "C:/Program Files/Unity/Hub/Editor/$unityVersion/Editor/Data/Managed"
if (-not (Test-Path $managed)) {
    Write-Host "Unity $unityVersion not found at:" -ForegroundColor Red
    Write-Host "  $managed" -ForegroundColor Red
    Write-Host "Install it through Unity Hub, or edit the path in this script."
    exit 1
}

Write-Host "Unity:   $unityVersion"

# --- Sources -------------------------------------------------------------
#
# Anything inside a folder carrying an .asmdef compiles into its own assembly,
# so it is excluded here and referenced as a built DLL instead. Assets/Plugins
# is excluded for the same reason: it becomes Assembly-CSharp-firstpass.

$asmdefDirs = @(
    Get-ChildItem -Path $AssetsDir -Recurse -Filter '*.asmdef' -File |
        ForEach-Object { $_.DirectoryName }
)

$pluginsDir = Join-Path $AssetsDir 'Plugins'

$sources = Get-ChildItem -Path $AssetsDir -Recurse -Filter '*.cs' -File | Where-Object {
    $path = $_.FullName
    if ($path.StartsWith($pluginsDir, [System.StringComparison]::OrdinalIgnoreCase)) { return $false }
    foreach ($dir in $asmdefDirs) {
        if ($path.StartsWith($dir, [System.StringComparison]::OrdinalIgnoreCase)) { return $false }
    }
    return $true
}

Write-Host "Sources: $($sources.Count) .cs file(s)"

if ($sources.Count -eq 0) {
    Write-Host "Nothing to compile." -ForegroundColor Yellow
    exit 0
}

# --- References ----------------------------------------------------------
#
# Collected most-specific first and de-duplicated by file name. Two assemblies
# with the same name are a guaranteed CS0433 ("type exists in both"), and the
# package cache holds several duplicates of what ScriptAssemblies already has.

function Add-Refs {
    param($Table, $Files)
    foreach ($f in $Files) {
        # OrderedDictionary exposes Contains, not ContainsKey.
        if (-not $Table.Contains($f.Name)) { $Table[$f.Name] = $f.FullName }
    }
}

$refs = [ordered]@{}

$scriptAssemblies = Join-Path $RepoRoot 'Library/ScriptAssemblies'
if (Test-Path $scriptAssemblies) {
    Add-Refs $refs (Get-ChildItem $scriptAssemblies -Filter '*.dll' -File |
        Where-Object { $_.Name -ne 'Assembly-CSharp.dll' })
}

Add-Refs $refs (Get-ChildItem (Join-Path $managed 'UnityEngine') -Filter '*.dll' -File)

if (Test-Path $pluginsDir) {
    Add-Refs $refs (Get-ChildItem $pluginsDir -Recurse -Filter '*.dll' -File)
}

$packageCache = Join-Path $RepoRoot 'Library/PackageCache'
if (Test-Path $packageCache) {
    Add-Refs $refs (Get-ChildItem $packageCache -Recurse -Filter '*.dll' -File)
}

Write-Host "Refs:    $($refs.Count) assembly/assemblies"
Write-Host ''

# --- Generate the project and build --------------------------------------

$workDir = Join-Path $RepoRoot 'Library/CompileCheck'
New-Item -ItemType Directory -Force -Path $workDir | Out-Null

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('<Project Sdk="Microsoft.NET.Sdk">')
[void]$sb.AppendLine('  <PropertyGroup>')
[void]$sb.AppendLine('    <TargetFramework>netstandard2.1</TargetFramework>')
# Unity pins C# 9; matching it means this build cannot accept syntax Unity rejects.
[void]$sb.AppendLine('    <LangVersion>9.0</LangVersion>')
[void]$sb.AppendLine('    <Nullable>disable</Nullable>')
[void]$sb.AppendLine('    <ImplicitUsings>disable</ImplicitUsings>')
[void]$sb.AppendLine('    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>')
[void]$sb.AppendLine('    <AssemblyName>CompileCheck</AssemblyName>')
[void]$sb.AppendLine('    <EnableDefaultItems>false</EnableDefaultItems>')
[void]$sb.AppendLine('    <ProduceReferenceAssembly>false</ProduceReferenceAssembly>')
# Unused fields and hidden members are Unity idioms, not defects worth failing on.
[void]$sb.AppendLine('    <NoWarn>CS0169;CS0414;CS0649;CS0108;CS0114;CS0162;CS0219;CS1591;CS0436;CS0067</NoWarn>')
[void]$sb.AppendLine('  </PropertyGroup>')
[void]$sb.AppendLine('  <ItemGroup>')

foreach ($s in $sources) {
    $escaped = [System.Security.SecurityElement]::Escape($s.FullName)
    [void]$sb.AppendLine("    <Compile Include=`"$escaped`" />")
}

[void]$sb.AppendLine('  </ItemGroup>')
[void]$sb.AppendLine('  <ItemGroup>')

foreach ($name in $refs.Keys) {
    $escaped = [System.Security.SecurityElement]::Escape($refs[$name])
    $alias = [System.Security.SecurityElement]::Escape([System.IO.Path]::GetFileNameWithoutExtension($name))
    [void]$sb.AppendLine("    <Reference Include=`"$alias`"><HintPath>$escaped</HintPath><Private>false</Private></Reference>")
}

[void]$sb.AppendLine('  </ItemGroup>')
[void]$sb.AppendLine('</Project>')

$projPath = Join-Path $workDir 'CompileCheck.csproj'
Set-Content -Path $projPath -Value $sb.ToString() -Encoding utf8

$output = & dotnet build $projPath -v q --nologo 2>&1
$exitCode = $LASTEXITCODE

if ($ShowAll) {
    $output | ForEach-Object { Write-Host $_ }
} else {
    $errors = $output | Where-Object { $_ -match ': error ' } | Sort-Object -Unique
    foreach ($e in $errors) {
        # Trim the absolute repo prefix and the trailing [project] noise.
        $line = $e -replace [regex]::Escape($RepoRoot + '\'), ''
        $line = $line -replace '\s*\[.*\.csproj\]$', ''
        Write-Host $line -ForegroundColor Red
    }
}

Write-Host ''
if ($exitCode -eq 0) {
    Write-Host 'Compiles clean.' -ForegroundColor Green
    Write-Host ''
    Write-Host 'This is a type check, not a test run and not a substitute for'
    Write-Host 'opening the editor. It cannot see prefab or scene wiring, and it'
    Write-Host 'compiles editor and runtime code together.'
} else {
    Write-Host 'Compile errors above.' -ForegroundColor Red
}

exit $exitCode
