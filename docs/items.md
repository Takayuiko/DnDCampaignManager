# Campaign items and character inventories

Each campaign owns a catalog. The campaign owner with the DM role can create,
read, update, and delete catalog items and assign them to characters in that campaign.
Campaign members can read the catalog. A character's player can read their own
inventory; the campaign DM can read all of its character inventories.

Use **Items** on the dashboard campaign card to manage the catalog. A saved
character sheet has **Character** and **Inventory** tabs in Edit View. Read View
shows the character summary and inventory together without assignment controls.
Use the **Inventory** tab to assign items. Each
assignment creates an inventory entry with its own quantity and notes. Repeated
assignments of the same item are separate entries. Catalog edits apply to all
entries referencing that item. Deleting an assigned catalog item returns 409;
it never deletes character possessions.

The DM can also use **Delete all items** to review the catalog/inventory counts,
then explicitly confirm deletion of the entire campaign catalog and every
character's inventory entries in that campaign. This runs atomically. A stale
count preview is rejected; other campaigns are unaffected.

Player removal and transfers are deferred. No inventory delete, update, or
transfer endpoint is exposed in this stage. Equipment does not automatically
change AC, attacks, encumbrance, or currency.

## Database

Migration: `CampaignItemsAndInventory`. Adds `CampaignItems` and `CharacterItems`
and composite keys that prevent an inventory entry from referencing a character
or catalog item in another campaign. Existing campaigns and characters are kept.
The API's existing startup migration behavior applies the migration;
otherwise run `dotnet ef database update --project DnDCampaignManager.Api`.

## API

- `GET /api/campaigns/{campaignId}/items` — catalog and `canManage` permission.
- `GET /api/campaigns/{campaignId}/items/{itemId}` — item details.
- `POST /api/campaigns/{campaignId}/items` — create item.
- `PUT /api/campaigns/{campaignId}/items/{itemId}` — update item.
- `DELETE /api/campaigns/{campaignId}/items/{itemId}` — delete unassigned item.
- `GET /api/campaigns/{campaignId}/items/deletion-preview` — DM-only catalog and inventory counts.
- `DELETE /api/campaigns/{campaignId}/items` — DM-only confirmed bulk deletion with `{ expectedItemCount, expectedInventoryEntryCount }`.
- `POST /api/campaigns/{campaignId}/items/import-srd` — import missing starters and backfill missing SRD prices/weights; returns `{ added, updated }`.
- `GET /api/campaigns/{campaignId}/characters/{characterId}/inventory` — inventory and `canAssign` permission.
- `POST /api/campaigns/{campaignId}/characters/{characterId}/inventory` — assign `{ itemId, quantity, notes }`.

Item input: `{ name, category, description, weightLb, costGp }`. Weight and cost
are optional (`null` means unspecified); zero means zero. Names are unique within
a campaign after trimming and case normalization. Inventory quantities must be
positive integers. Writes within a campaign are serialized to prevent competing
imports, duplicate names, or assignment/deletion races.

## Starter equipment and attribution

The optional import supplies 74 weapons, armor, and adventuring gear names,
categories, prices, and weights from official 5e SRD 5.1 Equipment (pages 64–69).
Prices are converted to gold pieces (1 sp = 0.1 gp; 1 cp = 0.01 gp); fractional
weights are preserved in pounds. Source weight dashes are represented as 0 lb
(negligible weight). Waterskin weight is 5 lb when full, noted in its description.
This is a starter subset, not a complete rules or magic-item compendium.
Combat rules are omitted. Reimporting fills only missing (`null`) prices/weights
on existing SRD entries, preserving zero and any DM-supplied values, names,
categories, and descriptions. Custom entries with matching names are left alone.
It also adds a full-waterskin note when its description is empty. No network
request or additional schema migration is needed. To upgrade earlier names-only
imports, use the import button again in each campaign's Items page.

This work includes material taken from the System Reference Document 5.1
(“SRD 5.1”) by Wizards of the Coast LLC and available at
https://www.dndbeyond.com/srd. The SRD 5.1 is licensed under the Creative Commons
Attribution 4.0 International License available at
https://creativecommons.org/licenses/by/4.0/legalcode.

Equipment names, category grouping, prices, and weights have been adapted into
a starter catalog; game rules have been omitted. Imported entries retain
`SRD 5.1 (modified)` provenance when edited. Attribution is displayed on the
catalog page and inventories containing imported equipment.

Official source: https://www.dndbeyond.com/attachments/39j2li89/SRD5.1-CCBY4.0License.pdf

Prices/weights cross-checked against the official 2014 5e equipment tables:
https://www.dndbeyond.com/sources/dnd/basic-rules-2014/equipment
