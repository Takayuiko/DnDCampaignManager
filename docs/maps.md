# Campaign maps

A map is an image with one or more named locations. Open **Maps** from the campaign dashboard. All maps and locations are shared with the campaign owner and current members; there is no private-map visibility setting.

## Viewing and management

Players see map images, descriptions and numbered location pins directly, without a View maps button or management tab. The campaign owner with the DM role has **View maps** and **Manage maps** tabs. A DM role alone does not grant access to someone else's campaign.

Management starts with image preview cards: three per row on wide screens, six reserved spaces, and a bounded scrollable area for more maps. Six spaces are a layout choice, not a maximum map count. Only the next available card offers **Add map**. Add or Edit replaces the list with one form; Save or Cancel returns to the list. Failed saves keep the draft for correction.

Add a location, place its pin by clicking the image or entering coordinates, and confirm it. Added locations become read-only with an explicit Modify action. Coordinates are percentages from 0 to 100, so pins scale with the image. The management UI shows index status and retry controls; the viewer does not show a Searchable label.

## Validation and images

- Titles are required and limited to 160 characters. Names are unique per campaign after trimming and case normalization; duplicates return 409. Different campaigns can reuse a title.
- A new map requires a PNG, JPEG or WebP image of at most **50,000,000 bytes (50 MB)**. Updates can omit the image to keep the existing one. Multipart requests allow 51,000,000 bytes for metadata overhead.
- Each map needs 1–50 locations. Location names are required and limited to 120 characters; location and map descriptions allow 4,000 characters.
- Original images and JPEG thumbnails are PostgreSQL `bytea` blobs, served by authenticated endpoints rather than public static paths. Uploads undergo signature checks and preview decoding; unsupported, corrupted or unsupported-dimension images are rejected.
- Management previews load only when visible and use thumbnails bounded to 640 pixels on the longest side and 100,000 bytes. Cached preview reads do not select the original image. Legacy previews are generated on first access; image replacement replaces the preview too.

## API

All routes below are under `/api/campaigns/{campaignId}/maps`:

- `GET /` — map metadata, locations and index status.
- `GET /{mapId}/image` — original image.
- `GET /{mapId}/thumbnail` — JPEG preview.
- `POST /` — create (campaign owner with DM role).
- `PUT /{mapId}` — edit (campaign owner with DM role).
- `DELETE /{mapId}` — delete map and derived index (campaign owner with DM role).
- `POST /{mapId}/index` — queue/retry indexing (campaign owner with DM role).

Create/edit uses multipart fields `Title`, `Description`, `LocationsJson`, and `Image`. `LocationsJson` is an array such as `[{"name":"Oakbridge","description":"A small town","x":25,"y":60}]`. Reads, including images and previews, require current campaign access and a matching campaign/map pair. Create, update and indexing requests share the existing AI rate limit.

## RAG behavior

Map titles, descriptions and location text feed campaign retrieval. Images are not sent to a vision model or embedded; pin coordinates do not teach the model roads or distances.

Every pin is a location in general context. Specific requests for towns or cities match that word in the location name or description; an untyped location is not automatically a town. The current location catalog reads authorized source records and remains available when embeddings fail or are pending.

Saves and retries persist a pending revision immediately. The background worker calls the embedding provider outside a database transaction, then replaces chunks in a short transaction only if its revision and lease still match. Pending/failed maps exclude stale semantic chunks; current location text remains available. Without an OpenAI key, maps can still be managed and pending jobs resume after configuration and restart. See [RAG setup](RAG_SETUP.md) for retrieval bounds, worker leases and failure recovery.

## Database and verification

Committed migrations are `CampaignMaps`, `UniqueCampaignMapNames`, `CampaignMapThumbnails` and `BackgroundMapIndexing`. Apply them through normal startup or `dotnet ef database update --project DnDCampaignManager.Api` from the repository root. Name backfill preserves maps but suffixes conflicting names; reverting the constraint does not restore those titles.

`tests/CampaignMembershipChecks` covers map validation, naming, permissions, thumbnails and location filtering. `tests/MapIndexingChecks` uses separate database connections and fake providers to check indexing locks, revisions and recovery. Frontend tests cover maps, previews and pending-status polling. See [verification commands](RAG_SETUP.md#migration-and-verification).
