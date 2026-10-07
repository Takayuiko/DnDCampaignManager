# Browser request timeouts and cancellation

The shared Axios client uses a 30-second timeout per HTTP attempt. Refresh requests retain their shorter 15-second timeout. Map image uploads/downloads, session-note save/index operations, and audio transcription use explicit 180-second limits to allow larger transfers and external AI calls. Thumbnails and ordinary CRUD use the default. Audio transcription previously allowed eleven minutes.

AI fetch streams use one 150-second deadline covering headers, response reads, and any token refresh/retry. The deadline is longer than the server's 120-second AI budget. An AbortController cancels fetch and its response reader; timers and listeners are cleaned up on completion, errors or cancellation. The deadline does not restart when a token is refreshed. A stalled stream rejects without claiming completion or inventing usage.

Page loads for campaigns, character sheets/options, inventory, item catalogs, maps, session notes, DM administration, and authentication pass cancellation signals. Cleanup aborts obsolete work when the page unmounts or its resource changes; active/generation guards also prevent late responses from changing current state. Map polling aborts outstanding requests during cleanup. Long map and note operations are cancelled on leaving their campaign page.

Chat offers **Stop** during generation, keeps partial text marked as interrupted, and enables sending again once the request settles. Leaving chat aborts generation without updating the departed page. Selecting another campaign cancels the obsolete chat selection. **Cancel transcription** keeps the current note draft and ignores late responses; leaving the note page also aborts transcription.

Cancelled Axios requests do not enter the refresh path. Cancelling a caller already waiting for refresh stops only that caller; the shared refresh continues for other tabs/requests. No cancelled request is retried after refresh. See [refresh coordination](AUTH_REFRESH.md).

Timeouts report a safe message. A timeout or cancellation does not prove a mutation was rolled back: the server may have saved data before the connection ended. Reload the relevant list or conversation to check before resubmitting. There are no automatic retries for timeouts or cancellation, and no database migration or new dependency is required.

Verification from `DnDCampaignManager.web`: `npx tsc -b`, `node --test tests/*.test.cjs`, and `npx vite build`. Targeted cases in `requestTimeouts.test.cjs`, `chatCancellation.test.cjs`, `authRefresh.test.cjs`, and `campaignRag.test.cjs` cover stalled headers/readers, deadlines, stopped generation, unmount, cancellation during refresh, long-operation policies, and preserved transcription drafts. Mock deadlines are fired directly instead of waiting minutes.
