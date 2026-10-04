# DnD Campaign Manager — Phase 1 setup

This version starts the AI Engineering Lab plan.

## What changed

- SQLite / SQL Server provider selection was removed.
- PostgreSQL is now the only EF Core database provider.
- Local PostgreSQL is supplied through Docker Compose.
- Provider-specific old migrations were removed because they were SQL Server migrations.
- The OpenAI integration now uses the official OpenAI .NET SDK and Responses API.
- AI endpoints are protected by the application's existing JWT fallback policy.
- A simple React AI chat page was added at `/ai`.
- Conversation history is still client-side only. Persistence is intentionally deferred to Phase 2.
- RAG, embeddings, vector search, tools, agents and MCP are intentionally not included yet.

## Prerequisites

- .NET 8 SDK
- Node.js / npm
- Docker Desktop
- An OpenAI API key

## 1. Start PostgreSQL

From the repository root:

```powershell
docker compose up -d
```

Check it:

```powershell
docker compose ps
```

The default local database is:

- Host: `localhost`
- Port: `5432`
- Database: `dndcampaignmanager`
- User: `dndadmin`
- Password: `dndpassword`

These are development-only defaults.

## 2. Configure the OpenAI API key

From `DnDCampaignManager.Api`:

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_API_KEY"
```

The model is configured in `appsettings.json` / `appsettings.Development.json`:

```json
"OpenAI": {
  "Model": "gpt-5.2"
}
```

You can change the model without changing application code.

Do not put the real API key in `appsettings.json` or commit it to Git.

## 3. Restore packages

```powershell
cd DnDCampaignManager.Api
dotnet restore
```

## 4. Create the PostgreSQL migration

The old migrations were SQL Server-specific, so this branch intentionally starts a clean PostgreSQL migration history.

Run:

```powershell
dotnet ef migrations add InitialPostgreSQL
dotnet ef database update
```

This creates the PostgreSQL schema from the current EF Core model.

## 5. Start the API

```powershell
dotnet run
```

Verify:

```text
GET /health
```

Swagger is available in development.

## 6. Start the React application

From `DnDCampaignManager.web`:

```powershell
npm install
npm run dev
```

Open the application and log in.

The new **AI Assistant** navigation item opens:

```text
/ai
```

## 7. Phase 1 API

AI status:

```text
GET /api/ai/status
```

AI chat:

```text
POST /api/ai/chat
```

Example body:

```json
{
  "message": "Give me an idea for an ancient temple encounter.",
  "history": []
}
```

The endpoint requires the normal JWT authentication used by the application.

## Phase 1 architecture

```text
React
  |
  | POST /api/ai/chat
  v
ASP.NET Core
  |
  v
IAIService
  |
  v
OpenAIService
  |
  v
OpenAI ResponsesClient
  |
  v
OpenAI Responses API
```

PostgreSQL is currently independent of the AI request. This is intentional.

Later phases will connect AI to application data through RAG and tools.

## Next phase

After Phase 1 is verified, the next work should be:

1. Persist conversations in PostgreSQL.
2. Add streaming responses.
3. Track basic token/usage information.
4. Improve AI error handling and request limits.
5. Then move into structured output and RAG.
