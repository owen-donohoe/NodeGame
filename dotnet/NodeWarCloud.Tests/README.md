# Catalog validation

`dotnet test dotnet/NodeWar.sln` validates the embedded catalog, its exact enum-derived
IDs, and the working-tree JSON against the last committed catalog (`git show HEAD`).
The latter uses both `CatalogValidation.Validate` and `ValidateAgainstPrevious`.
Keep old IDs and identity fields; retire entries instead of deleting or renaming them.

To validate a candidate file before replacing the export (PowerShell):

```powershell
$env:NODEWAR_CATALOG_CANDIDATE = 'C:\path\candidate.json'
dotnet test dotnet/NodeWarCloud.Tests --filter FullyQualifiedName~CandidateCatalog_PassesAuthoritativeValidationAgainstCommittedFile
Remove-Item Env:NODEWAR_CATALOG_CANDIDATE
```

The baseline defaults to `HEAD`. Set `NODEWAR_CATALOG_BASELINE_REF` to a commit or
branch to compare against an earlier release in CI/review (that ref must contain
the catalog and be present in the checkout). These tests require Git history; a
missing baseline fails rather than silently skipping the stability check.

Editor setup is manual: create/select a CatalogDefinition, run **Tools > Node War >
Backend > Generate Catalog**, then **Export Catalog For Server**. Generation only
adds missing IDs. Export checks shape and identity against the existing disk file;
the tests remain authoritative. No ScriptableObject asset is committed by this task.

Local fakes share one in-memory record store. They generate the enum bases' six eras
and default skins; custom/retired catalog content is tested with server rules. Equip
requires an initial player-state load, treats omitted dictionaries as unchanged,
and rejects an entire invalid patch without writing. Demotion retains ownership
and existing selections; subsequent equip requests enforce the current arena.
