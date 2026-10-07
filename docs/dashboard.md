# Campaign dashboard

Campaigns stack vertically in full-width cards. Each has a responsive character
grid with six spaces (one column on small screens, two on medium screens, three
on large screens). Campaign settings sit beside the title, Session Notes, Maps and
Items share a tools row, character-sheet buttons belong to their character cards,
and campaign deletion is in the footer.

The API enforces at most six characters per campaign. Character creation locks
the campaign row and checks its current count inside the same transaction, so
simultaneous requests cannot exceed the cap. The one-character-per-user rule
still applies. Full campaigns hide Add Character. Existing characters in older
campaigns above the cap are displayed and kept, but no more can be created.

No database migration is required for this capacity rule or layout change.

Maps open the [map viewer and DM management](maps.md). Items open the [campaign catalog](items.md), while saved character sheets expose their inventory. The dashboard uses the existing campaign and character endpoints through `campaignApi.ts`; there is no separate `/api/dashboard` endpoint.
