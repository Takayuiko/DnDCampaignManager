# CI and deployment

The GitHub Actions workflow runs on pull requests targeting `master`, pushes to `master`, and manual dispatches. Its required `regression` job runs `npm test`, frontend lint, and every backend `tests/**/*Checks.csproj` console regression suite. These projects execute assertions directly; `dotnet test` alone would not run them.

Backend checks run sequentially against a disposable PostgreSQL 16 service. The membership suite runs first and applies the committed migrations. A temporary JWT key is generated per run. No production credentials or OpenAI key are needed, and provider checks use fakes without paid calls.

Only after regression succeeds does the Windows build compile the frontend and publish the API, including checks for the bundled frontend files. Deployment requires this build to succeed and only runs for `master` pushes or manual runs on `master`. Pull requests validate without deploying. A failed check blocks publishing and deployment.

To run the same backend checks locally, configure `ConnectionStrings__DefaultConnection` to a dedicated test PostgreSQL database and configure JWT signing keys as described in local setup, then run from the repository root:

```powershell
./scripts/Run-RegressionChecks.ps1
```

Use a disposable database: checks apply migrations and create temporary fixtures. Optional AI checks start a real API process using the freshly built assembly. The frontend equivalent is `npm test` and `npm run lint` in `DnDCampaignManager.web`.

Configure the repository's branch protection/ruleset to require the `regression` and `build` checks if merges must also be blocked. The workflow itself gates deployment; repository merge rules are configured separately in GitHub.
