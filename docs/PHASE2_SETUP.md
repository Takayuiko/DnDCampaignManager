# Phase 2 - Persistent AI Chat, Streaming and Usage Tracking

Phase 2 builds on the PostgreSQL + OpenAI foundation from Phase 1.

## What changed

### Backend

- AI conversations and messages are persisted in PostgreSQL.
- Conversations belong to the authenticated user.
- The API loads the last 30 completed messages from PostgreSQL for each model request.
- Added streaming responses over Server-Sent Events (SSE).
- Added token usage tracking.
- Added configurable estimated cost tracking.
- Added request duration tracking.
- Added model and OpenAI response ID tracking.
- Added AI endpoint rate limiting: 20 requests/minute per authenticated user.
- Added safer provider error responses (the provider exception is logged server-side but not returned to the browser).
- Added message length validation (8,000 characters).

### Frontend

- Conversation list.
- Create conversation.
- Delete conversation.
- Load persisted conversations.
- Stream assistant responses token-by-token.
- Display token count, estimated cost and response duration.

## Database migration

The Phase 1 project intentionally did not contain a provider-specific migration. After adding the Phase 2 models, create a new migration from the API project:

```powershell
cd DnDCampaignManager.Api
dotnet ef migrations add Phase2AIConversations
dotnet ef database update
```

The API also calls `Database.Migrate()` at startup, so future migrations will be applied automatically after they have been created and committed.

## OpenAI pricing configuration

The sample configuration currently contains GPT-5.2 pricing:

```json
"Pricing": {
  "gpt-5.2": {
    "InputPer1M": 1.75,
    "OutputPer1M": 14.0
  }
}
```

These values are only used to calculate an **estimated** application-side cost. They are not an invoice or billing source. Update them when changing models or pricing.

## OpenAI key

Keep the API key in .NET User Secrets or an environment variable. Do not put it in source control.

```powershell
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_KEY"
```

## Run locally

Start PostgreSQL:

```powershell
docker compose up -d
```

Run the API:

```powershell
cd DnDCampaignManager.Api
dotnet run
```

Run the React app:

```powershell
cd DnDCampaignManager.web
npm install
npm run dev
```

Then open **AI Assistant** after logging in.

## API endpoints

```text
GET    /api/ai/status
GET    /api/ai/conversations
POST   /api/ai/conversations
GET    /api/ai/conversations/{id}
DELETE /api/ai/conversations/{id}
POST   /api/ai/conversations/{id}/messages
POST   /api/ai/conversations/{id}/messages/stream
```

The streaming endpoint returns SSE events:

```text
event: token
data: {"text":"Hello"}

event: done
data: {"conversationId":"...","messageId":123,"model":"gpt-5.2","usage":{...}}
```

## Important architectural decision

Conversation history is stored and managed by **our application**, not by the frontend. This is intentional. It gives us a clean foundation for the next phases:

```text
Phase 2
PostgreSQL conversation state
        ↓
Phase 3
Structured AI output
        ↓
Phase 4
RAG / embeddings / vector search
        ↓
Phase 5
Application tools
        ↓
Phase 6
Agents
        ↓
Phase 7
MCP
```

## Production frontend build

The Vite configuration now outputs the React production build to the API's `wwwroot` folder. Run:

```powershell
cd DnDCampaignManager.web
npm install
npm run build
```

The ASP.NET Core API serves that build outside Development.
