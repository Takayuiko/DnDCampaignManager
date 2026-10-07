# Phase 2 - Persistent AI Chat, Streaming and Usage Tracking

This guide describes the current persisted chat built on the [PostgreSQL setup](PHASE1_SETUP.md). Later additions include campaign RAG and read-only tools; see [RAG setup](RAG_SETUP.md) and [AI tools/MCP](AI_TOOLS_AND_MCP.md).

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

- Campaign selector, with one personal conversation per user per accessible campaign.
- Automatic conversation initialization.
- Clear conversation history while retaining the conversation and campaign association.
- Load persisted conversations.
- Stream assistant responses token-by-token.
- Display token count, estimated cost and response duration.

## Database migration

`Phase2AIConversations` and `OneChatPerCampaign` are already committed. Apply existing migrations from the repository root:

```powershell
dotnet ef database update --project DnDCampaignManager.Api
```

The API also applies pending migrations at startup. Do not generate another setup migration.

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

An OpenAI key is optional for startup and viewing saved conversations. Sending and streaming require a configured provider. Keep the key in .NET User Secrets or an environment variable, outside source control. Run the command below from `DnDCampaignManager.Api`.

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

Run the API and frontend commands in separate terminals from the repository root. Then open **AI Assistant** after logging in and choose an accessible campaign. General chats are inaccessible.

## API endpoints

```text
GET    /api/ai/status
GET    /api/ai/conversations
POST   /api/ai/conversations
POST   /api/ai/conversations/default
GET    /api/ai/conversations/{id}
DELETE /api/ai/conversations/{id}
POST   /api/ai/conversations/{id}/messages
POST   /api/ai/conversations/{id}/messages/stream
```

`POST /conversations` requires an accessible campaign and returns that user's existing conversation when present. `/default` initializes the first accessible campaign, or returns no content if there is none. The DELETE route clears messages and resets the title; it preserves the conversation ID and campaign. Access requires both conversation ownership and current campaign membership/ownership.

Sending, streaming and clearing use a PostgreSQL session advisory lock per conversation. An overlapping operation returns 409 instead of racing. Provider calls do not hold a database transaction open.

The streaming endpoint returns SSE events:

```text
event: token
data: {"text":"Hello"}

event: done
data: {"conversationId":"...","messageId":123,"model":"gpt-5.2","usage":{...}}
```

## Important architectural decision

Conversation history is stored and managed by **our application**, not by the frontend. This is intentional. Current campaign context combines live character facts, session/map retrieval and authorized read-only tools. These services read controlled data; generated messages are not automatically campaign knowledge and tools cannot modify campaign state.

## Production frontend build

The Vite configuration now outputs the React production build to the API's `wwwroot` folder. Run:

```powershell
cd DnDCampaignManager.web
npm install
npm run build
```

The ASP.NET Core API serves that build outside Development.
