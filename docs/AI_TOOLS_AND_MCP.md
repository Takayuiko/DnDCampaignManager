# Read-only campaign tools and MCP

## What is implemented

The campaign AI chat can call three read-only application functions:

- `GetCampaignState`: campaign name, description, character count and session-note count.
- `ListCharacters`: up to 100 shared characters, including IDs, names, classes, races and levels.
- `GetCharacter`: current identity, ability scores, armor class, speed and hit points for one character.

These are explicit DTO projections, not serialized EF entities. They do not expose account emails, credentials, refresh tokens, other users' conversations or database queries. Inventory, NPCs and quests are not implemented as tools because their application models and flows are not ready for this slice.

Session-note retrieval continues through the existing RAG pipeline. Tools complement RAG with targeted reads of current structured data; they do not make generated text or chat questions part of campaign knowledge.

## How the chat calls tools

`AIChatController` derives the user ID from the authenticated principal and the campaign ID from the saved conversation. It passes this trusted `CampaignToolScope` into `OpenAIService`. The model cannot supply a different user or campaign in its function arguments.

`OpenAIService` advertises strict JSON function schemas through the existing Responses API. The model chooses whether to answer immediately or request a tool. The application validates the function name and arguments, calls `CampaignToolService`, and sends the result back with the original call ID. All output items, including encrypted reasoning, are preserved when continuing the response. Responses storage is disabled.

Both streaming and nonstreaming paths support tool continuations. A request allows at most four tool rounds and eight tool executions; exceeding either limit produces the existing friendly AI failure behavior. Calls execute sequentially because a scoped EF DbContext is not safe for parallel operations. Usage and estimated model cost sum every response in the loop, including tool-selection requests. Existing retrieval usage remains separately reported.

Example: ask “What is Freya's current HP?” The model can call `ListCharacters`, find Freya's ID, call `GetCharacter`, then answer from the live result. Tools are optional: the existing RAG context may already contain enough information to answer without a call. No tool can change HP or other campaign data.

Successful executions log tool name, campaign ID and user ID, without logging arguments, returned data or credentials. The chat UI retains its existing message/usage display; it does not persist a separate tool transcript.

## Shared authorization

`CampaignToolService` checks current campaign ownership or membership on every data operation, using the same access service as RAG. A character query additionally filters by campaign ID in SQL. Revoked members cannot continue reading through tools. A DM/admin has no global campaign bypass. Character facts are shared with campaign members consistently with the existing RAG context; private account and chat data remain private.

## What MCP adds

The official `ModelContextProtocol.AspNetCore` SDK hosts a stateless Streamable HTTP server at `/mcp`, in the same API process. `CampaignMcpTools` adapts the three functions to MCP and calls the same `CampaignToolService`. No separate database-access implementation is introduced.

MCP clients can discover the tools using `tools/list` and execute them using `tools/call`. The tools declare read-only, non-destructive and closed-world annotations. These describe behavior to clients; authorization is enforced independently in application code.

Every MCP HTTP request requires the existing JWT access token and shares the AI rate limit. The authenticated principal supplies the user ID; it is never a client tool argument. An external client may select a campaign ID, but the service verifies that user's access before reading it. Stateless transport avoids sharing a persistent MCP session between authenticated users or relying on Azure process/session affinity.

The campaign chat calls application services directly. It does not send your JWT to OpenAI, call its own MCP endpoint over HTTP, or connect to arbitrary third-party MCP servers. MCP is an additional interface for trusted clients, while internal function calling stays inside the backend.

## Connecting a development client

1. Start the API with its normal configuration and sign in to the application.
2. Configure an MCP client that supports Streamable HTTP and explicit request headers.
3. Use `http://localhost:5000/mcp` locally, or your deployed application's HTTPS URL followed by `/mcp`.
4. Supply `Authorization: Bearer <access-token>` on every request. Obtain your own token from the existing login flow; keep it out of source control, screenshots and logs.
5. Discover the tools, then call `GetCampaignState` with an accessible `campaignId`, or `GetCharacter` with both `campaignId` and `characterId`.

For a client issuing raw protocol requests, a tool invocation looks like:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "GetCharacter",
    "arguments": { "campaignId": 23, "characterId": 33 }
  }
}
```

Use the client's normal protocol initialization/negotiation. Streamable HTTP POST requests need appropriate JSON content type and Accept headers for JSON and event streams. The automated checks exercise authenticated discovery and execution over the real SDK transport using protocol version `2025-11-25`.

This first integration is bearer-token authentication, not a complete MCP OAuth authorization server. Access tokens expire after 15 minutes and permission changes invalidate them. A client must obtain a fresh access token using the existing application login/refresh flow. Clients requiring automatic OAuth discovery/sign-in, including some hosted integrations, cannot simply connect without additional authentication work. This change does not install a client integration or publish the endpoint to Azure automatically.

## Verification and deployment

No persistent models changed; no EF migration is required. Restore the new MCP package, build the frontend and API, then deploy through the existing workflow when ready. No additional OpenAI or Neon settings are required beyond the current configuration.

Run the focused checks against the local development PostgreSQL database:

```powershell
dotnet build tests/ToolingChecks/ToolingChecks.csproj -c ToolVerification -p:UseAppHost=false
dotnet tests/ToolingChecks/bin/ToolVerification/net8.0/ToolingChecks.dll C:/Users/Taka/source/repos/DnDCampaignManager/DnDCampaignManager.Api
```

The checks use transaction-scoped fixtures that roll back, fake OpenAI HTTP responses, and a temporary localhost MCP server. No paid OpenAI calls are made. They cover scoped reads, invalid arguments, cross-campaign IDs, membership revocation, streaming/nonstreaming continuations, reasoning replay, aggregated usage, loop limits, and MCP authentication/discovery/execution. A real-model smoke test is still useful after restart/deployment; model tool choice is not deterministic.

References: [OpenAI function calling](https://developers.openai.com/api/docs/guides/function-calling), [official MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk), [stateless HTTP transport](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/stateless/stateless.md).
