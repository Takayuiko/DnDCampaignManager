# Development Dungeon Master setup

After a database reset, recreate the test account from the repository root:

```powershell
./scripts/Set-DevelopmentDungeonMaster.ps1 -CreateIfMissing
```

The default email is `hagges02@gmail.com`. The script prompts locally for a password,
applies existing migrations, and creates the missing account with the application's
Identity password hasher and the `DM` role. Existing account passwords are preserved.
It uses the API's development database configuration and runs only in Development.
JWT and OpenAI configuration are not required for this seed command.
The password is passed temporarily through the child process environment and is not
written to a file or included in command arguments. No new migration is required.

To promote an already registered account using the Docker database:

```powershell
./scripts/Set-DevelopmentDungeonMaster.ps1 -Email 'your registered email'
```

The script targets the local PostgreSQL service in `docker-compose.yml`. It requires
exactly one matching account, changes only its role to `DM`, and can be rerun.
The promotion-only command does not create accounts or reset existing data.
Sign out and sign in again afterward: existing JWTs retain their previous role.
For PostgreSQL outside Docker, use `psql -v dm_email='your registered email'
-f scripts/promote-development-dm.sql` with your local connection settings.

This is temporary development provisioning. For future role management, keep
administrative permission to assign roles separate from the campaign DM role.
Campaign ownership/membership should eventually determine who can DM each campaign,
so a person can DM one campaign and play in another. Public registration must not
allow users to grant themselves administrative privileges.
