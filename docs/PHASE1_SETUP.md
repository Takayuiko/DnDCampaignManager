# Local setup — PostgreSQL and optional AI

This guide describes the current application setup. The original Phase 1 has since been extended with persisted chat, RAG, read-only tools, maps and inventory. See the [project overview](../README.md) for feature guides.

## Prerequisites

- .NET 8 SDK and the EF Core CLI when applying migrations manually.
- Node.js/npm.
- Docker Desktop for the supplied PostgreSQL 16 service, or another PostgreSQL instance.
- An OpenAI key only when using chat generation, embeddings or transcription.

## 1. Start PostgreSQL

From the repository root:

```powershell
docker compose up -d
docker compose ps
```

`docker-compose.yml` defaults to port `5432`, database `dndcampaignmanager`, user `dndadmin` and development password `dndpassword`. Its named volume retains database data across container restarts. Compose accepts `POSTGRES_DB`, `POSTGRES_USER` and `POSTGRES_PASSWORD` overrides; update the API connection string to match. Changing those values does not change credentials in an already initialized volume.

## 2. Configure the API

Run the following from the repository root, replacing the signing-key placeholder with a private random value of at least 32 bytes:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=dndcampaignmanager;Username=dndadmin;Password=dndpassword" --project DnDCampaignManager.Api
dotnet user-secrets set "Jwt:SigningKeys:0:Kid" "local-dev" --project DnDCampaignManager.Api
dotnet user-secrets set "Jwt:SigningKeys:0:Key" "REPLACE_WITH_A_PRIVATE_RANDOM_SIGNING_KEY" --project DnDCampaignManager.Api
```

`Jwt:Issuer` and `Jwt:Audience` must also be present; the checked-in configuration supplies defaults. `ConnectionStrings:DefaultConnection` and at least one signing key are required for startup. The development example file does not supply a signing key. Keep private values in User Secrets or environment variables, outside source control. Environment equivalents include `ConnectionStrings__DefaultConnection`, `Jwt__SigningKeys__0__Kid` and `Jwt__SigningKeys__0__Key`.

To enable AI, optionally set:

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_API_KEY" --project DnDCampaignManager.Api
```

Alternatively use `OpenAI__ApiKey`. Restart after changing configuration. Without a key, login, campaigns, characters, items, inventories, maps, text notes, saved conversations and read-only MCP tools remain available. AI sending and transcription return 503; saved map/note text remains pending for indexing. See [RAG setup](RAG_SETUP.md) for recovery behavior. Model and estimate settings remain in the `OpenAI` configuration section.

## 3. Restore and apply existing migrations

```powershell
dotnet restore DnDCampaignManager.sln
dotnet ef database update --project DnDCampaignManager.Api
```

Migrations are already committed under `DnDCampaignManager.Api/Migrations`; do not create `InitialPostgreSQL` or `Phase2AIConversations` during setup. The API also applies pending migrations at startup. Create a new migration only when changing the persistent model. Existing data should be upgraded through migrations without deleting the database or Docker volume.

## 4. Start the API

```powershell
dotnet run --project DnDCampaignManager.Api --launch-profile MyApp.Api
```

The development profile listens at `http://localhost:5000`. `GET /health` is anonymous; Swagger is available at `/swagger` in Development. Application endpoints use the existing JWT authentication.

## 5. Start the frontend

In a separate terminal, from the repository root:

```powershell
cd DnDCampaignManager.web
npm install
npm run dev
```

Open `http://localhost:5173`. Vite proxies `/api` to `http://localhost:5000`; development CORS allows the frontend origin. Register/sign in using the existing authentication flow. For DM access, follow [development DM setup](DEVELOPMENT_DM.md).

The AI page is `/ai`. Its current routes and campaign conversation behavior are documented in [Phase 2](PHASE2_SETUP.md); the old client-history `/api/ai/chat` endpoint is no longer present.

## Production frontend

From `DnDCampaignManager.web`, `npm run build` type-checks and writes the frontend into `DnDCampaignManager.Api/wwwroot`. The API serves this build outside Development. Deployment requires its own PostgreSQL connection and private JWT configuration; the Docker defaults are for local development.
