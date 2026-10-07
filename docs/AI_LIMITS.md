# AI request budgets and deadlines

Configure `OpenAI:Limits` in server configuration. Environment variables use names such as `OpenAI__Limits__RequestTimeoutSeconds`. Defaults are explicit in `appsettings.json`:

- `MaxContextCharacters`: 48,000 characters across structured reference JSON, retrieved passages and status text.
- `MaxHistoryCharacters`: 24,000 characters across the most recent contiguous suffix of at most 30 messages. The current question is retained; oversized older messages stop history selection.
- `MaxRequestBytes`: 256,000 bytes for each serialized Responses request, including developer instructions, tools, reference context, history, replayed reasoning, calls and tool results. Every continuation is checked before sending.
- `MaxOutputTokens`: 4,096 total generated tokens across the complete tool loop. Each Responses request receives the remaining allowance through `max_output_tokens`; provider usage includes reasoning as well as visible text.
- `RequestTimeoutSeconds`: 120 seconds for chat history/context loading and generation, including sequential tools and continuations. Caller cancellation is linked to the deadline. The provider also enforces a deadline for callers outside the controller.
- `EmbeddingTimeoutSeconds`: 30 seconds per provider batch, including retrieval and ingestion.
- `TranscriptionTimeoutSeconds`: 120 seconds per transcription provider call.

Character/byte budgets are deterministic application size bounds, not exact model input-token counts. They do not guarantee a particular bill. Configuration is clamped to supported ranges: context 1,000–100,000 characters; history 8,000–100,000; requests 16,000–1,000,000 bytes; output 256–32,000 tokens; chat/transcription 1–600 seconds; embeddings 1–120 seconds. Existing tool caps remain four continuation rounds and eight calls.

Reference reduction removes whole character/location records and reports omissions instead of slicing JSON. Retrieved passages are removed as whole records if necessary; their retained source list stays consistent with the prompt. Oversized campaign details use an explicit omission marker. Large old history is excluded with an instruction acknowledging missing history. Requests that still exceed the final byte budget stop before another provider call, returning a readable 422 error or stream error.

Responses with missing usage or an incomplete status are rejected, rather than persisted as successful assistant replies. Streaming may display partial text before a failure; it emits an error rather than a successful `done` event. Chat deadlines return HTTP 504 for nonstreaming or an SSE error for streaming. The submitted user message remains, but no successful assistant reply is stored. Cancellation/timeout releases the existing conversation lock. Model usage for failed/incomplete calls is not currently persisted as a separate billing ledger.

The [inventory tool](AI_TOOLS_AND_MCP.md) is read-only and queries live data. Inventory is not part of general shared context. At most 100 assignments within a 16,000-character serialized item-list budget are returned; `omittedEntries` prevents truncated results from being presented as complete. Access rules remain the same as the inventory API.

`tests/ToolingChecks` uses fake provider responses for request limits, tool continuations, incomplete responses and stalled-call cancellation. `tests/ConversationChecks` checks nonstreaming/streaming deadlines, persistence and lock release using separate PostgreSQL connections. No schema migration is required.

The output cap follows the official [Responses API reference](https://developers.openai.com/api/reference/python/resources/responses/methods/create), which defines it as covering visible output and reasoning tokens. These tests verify application behavior with fakes; actual model tool choice and reply quality still need a live smoke test.
