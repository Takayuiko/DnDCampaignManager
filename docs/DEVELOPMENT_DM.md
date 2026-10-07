# Development Dungeon Master setup

After a database reset, recreate the test account from the repository root:

```powershell
./scripts/Set-DevelopmentDungeonMaster.ps1 -CreateIfMissing
```

The default email is `hagges02@gmail.com`. The script prompts locally for a password,
applies existing migrations, and creates the missing account with the application's
Identity password hasher and the `DM` role. Existing account passwords are preserved.
Only `hagges02@gmail.com` also receives the separate `IsAdmin` permission. It remains a DM.
It uses the API's development database configuration and runs only in Development.
JWT and OpenAI configuration are not required for this seed command.
The password is passed temporarily through the child process environment and is not
written to a file or included in command arguments. The seed command applies pending migrations, including `AdminDungeonMasterManagement`.

To promote an already registered account using the Docker database:

```powershell
./scripts/Set-DevelopmentDungeonMaster.ps1 -Email 'your registered email'
```

The script targets the local PostgreSQL service in `docker-compose.yml`. Apply pending migrations by starting the updated API or running EF database update before using the promotion-only path. It requires
exactly one matching account, grants the `DM` role, and grants admin only when the email matches `hagges02@gmail.com`. It can be rerun without resetting passwords or campaign data.
The promotion-only command does not create accounts or reset existing data.
Sign out and sign in again afterward. Changes invalidate previous access and refresh tokens; unchanged repeat runs do not invalidate sessions.
For PostgreSQL outside Docker, use `psql -v dm_email='your registered email'
-f scripts/promote-development-dm.sql` with your local connection settings.

## Administrator DM management

After restarting the API and signing in with the seeded account, open **Manage DMs** in the navigation. Grant rights by entering an existing registered user's email. Email matching is case-insensitive and whitespace is trimmed; an unknown or ambiguous email is rejected. The user retains their password, account and player characters, and must sign in again to receive the new role. Granting DM does not grant admin.

**Review removal** shows a cleanup preview; **Confirm removal and delete owned campaign data** removes the DM role and performs cleanup in one database transaction. This is intentionally destructive, as requested:

- Delete every campaign owned by that DM, including all campaign memberships, all characters in those campaigns, their skills/attacks, campaign-specific class/race/background options, session notes, RAG chunks, and every user's chats/messages scoped to those campaigns.
- Delete the former DM's general AI conversations and revoke their refresh tokens; increment their access-token version.
- Preserve the user account and password. The account becomes `Player`.
- Preserve their characters, memberships, campaign-specific options and personal campaign conversations in campaigns owned by someone else. Those belong to their player participation.

The UI confirms campaign counts. The backend checks the expected owned-campaign count and rejects a stale preview if that count changed. The admin cannot demote itself or another admin through this UI/API, so the bootstrap administrator cannot be accidentally removed. There is no application endpoint to assign admin permission, edit passwords, or delete user accounts.

API: `GET /api/admin/dungeon-masters` lists DMs; `POST` with `{ "email": "registered@example.com" }` grants/updates DM rights; `GET /{id}/removal-preview` reviews cleanup; `DELETE /{id}` with `{ "expectedCampaignCount": 2 }` removes DM rights. Authorization is checked against the current database account as well as the Admin JWT role.

Admin is stored as a separate flag, with a database constraint requiring `Role = DM` when `IsAdmin` is true. JWTs carry both DM and Admin roles for an administrator. `/api/me` returns `isAdmin` for navigation and route checks. Public registration always creates a player without admin.

Authentication remains the existing JWT/refresh-token flow. JWT generation now includes the existing `TokenVersion`; validation checks it and current permissions against PostgreSQL on each authenticated request. This makes demotion effective for subsequent requests instead of leaving a fifteen-minute DM token active. Existing tokens issued before this change refresh or require signing in again. Requests already running are not forcibly cancelled. Campaign creation locks the owner row and checks the live DM role to serialize with demotion and prevent an in-flight creation from recreating owned data after cleanup.

Affected components: `User`, `JwtService`, `CurrentTokenValidator`, JWT configuration in `Program`, `/api/me`, `DungeonMasterManagementService`, `DungeonMastersController`, the admin page/API client/navigation/route gate, and the development seeder/scripts. Migration `20261005042355_AdminDungeonMasterManagement` adds a default-false admin flag and its constraint; no database reset is needed.

Verification uses temporary PostgreSQL fixtures within a rolled-back transaction, real JWT generation/validation, cleanup counts/cascades, preservation of foreign-campaign characters, stale-preview rejection and stale-role campaign creation rejection. Frontend tests cover promotion, safe errors, preview/cancel/confirm and the admin route gate. Run the existing integration console plus `node --test tests/dungeonMasters.test.cjs` in the frontend directory.

Longer term, campaign-specific DM permissions can replace the global DM role when multiple co-DMs are supported. This implementation deliberately retains existing ownership and authentication conventions.
