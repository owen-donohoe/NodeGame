# Server balances

After merging, open the Unity project and run **Tools > Node War > Backend >
Export Balance For Server**. Commit the resulting `<signed-content-hash>.json`
here before publishing the module. The menu reads `GameBalance.LoadShared()`;
`GameBalanceData.Default()` is test tuning, not the shipped asset.

Every JSON file here is embedded in the Cloud Code assembly at build time.
Restart/redeploy the module after adding a balance. Keep older files so logs
from older builds with a supported simulation version remain verifiable.
Exporting an existing hash overwrites only that file.

VerifyMatch refuses any log whose balance hash has no file here, with
`unknown balance`. A balance edit that is not exported and deployed makes
every match played on it unverifiable.
