# Server balances

After merging, open the Unity project and run **Tools > Node War > Backend >
Export Balance For Server**. Commit the resulting `<signed-content-hash>.json`
here before publishing the module. The menu reads `GameBalance.LoadShared()`;
`GameBalanceData.Default()` is test tuning, not the shipped asset.

Every JSON file here is embedded in the Cloud Code assembly at build time.
Restart/redeploy the module after adding a balance. Keep older files so logs
from older builds with a supported simulation version remain verifiable.
Exporting an existing hash overwrites only that file.

This directory intentionally starts without a balance JSON. Until an export
is included, VerifyMatch refuses all logs with `unknown balance`.
