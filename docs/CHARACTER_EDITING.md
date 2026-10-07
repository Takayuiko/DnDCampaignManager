# Character edit conflicts

Character reads return a `version` GUID. PUT requests must include the version loaded with the sheet. The server checks it after authorization and while holding the existing character row lock, before changing fields, skills or attacks. Every successful save generates a new version and returns HTTP 200 with `{ version }`; the editor uses it for subsequent saves.

Stale versions return HTTP 409 with `code: "character_version_conflict"`. Missing versions return HTTP 400; older clients must reload/update rather than bypass the check. EF also treats the version as a concurrency token. Conflicting saves do not silently merge or retry.

The editor preserves the unsaved draft, shows the conflict message, and blocks further saves until **Reload latest character** is confirmed. Reload replaces the draft with current server values; cancelling retains the draft. Inventory assignments do not change the sheet version because inventory is saved separately.

The additive `CharacterEditVersion` migration adds a PostgreSQL UUID column without changing existing sheet data. Existing rows begin with the zero GUID, which is a valid initial version; their first save assigns a new GUID. New characters start with a generated GUID. Apply through API startup or `dotnet ef database update --project DnDCampaignManager.Api`.

Verification: `tests/CharacterConcurrencyChecks` exercises simultaneous DM/player saves on separate PostgreSQL connections, stale collection edits, missing versions and reloaded saves. It creates committed temporary fixtures and deletes them in cleanup. Frontend checks are in `tests/characterConcurrency.test.cjs`. No provider calls are involved.

## Field limits

Create and update share server validation, with matching numeric and text constraints in the editor. Invalid input returns HTTP 400 before sheet fields, collections or the edit version change. Limits are intentionally generous for homebrew:

- Name, class and race require nonblank text up to 120 characters. Background allows 120 characters and alignment 80; both may be empty.
- Level: 1–100; XP: 0–1,000,000,000; ability scores and proficiency bonus: 0–100.
- Armor class: 0–1,000; initiative: −1,000–1,000; speed: 0–10,000.
- Maximum, current and temporary HP: 0–1,000,000. Current HP cannot exceed maximum HP; extra points belong in temporary HP.
- Hit dice: label up to 16 characters, total 0–1,000, and remaining 0–total. A positive total requires a nonblank label. Omitted hit dice and legacy empty labels with zero dice remain supported.
- At most 50 attacks: nonblank name up to 120 characters, damage text up to 500, and attack bonus −1,000–1,000.
- At most 18 unique, valid skills. Skill and saving throw miscellaneous bonuses: −1,000–1,000. All six saving throw entries must be present; omitted skills retain existing/default behavior.

No schema migration is needed for these limits. Existing stored values are preserved, but sheets outside the limits must be corrected before saving. `tests/CampaignMembershipChecks` verifies both contracts through MVC and shared service validation, inclusive boundaries, rejected creation and unchanged state after rejected updates.
