# DnD Campaign Manager

ASP.NET Core (.NET 8), Entity Framework Core/Npgsql and a React + TypeScript frontend for managing D&D campaigns. PostgreSQL is the database for development and deployment; SQLite and SQL Server are not supported by the current configuration.

## Run locally

Follow [local setup](docs/PHASE1_SETUP.md) for PostgreSQL, database/JWT configuration, committed migrations and API/frontend startup. An OpenAI key is optional for the core application.

## Current features

- Campaign membership and character sheets, with up to six characters per campaign and one character per user per campaign.
- [Campaign maps](docs/maps.md): protected images, named locations and pins, DM management, thumbnail previews and background text indexing for RAG.
- [Items and inventories](docs/items.md): campaign catalogs, SRD starter import and DM assignment of quantities and notes to characters.
- [Campaign chat](docs/PHASE2_SETUP.md): persisted personal conversations per campaign, streaming replies and usage estimates.
- [Campaign knowledge](docs/RAG_SETUP.md): session notes, reviewed audio transcripts, map text and current character facts.
- [Read-only AI tools and MCP](docs/AI_TOOLS_AND_MCP.md): explicit authorized campaign and character queries.
- [DM development setup and administration](docs/DEVELOPMENT_DM.md), and [dashboard behavior](docs/dashboard.md).

Inventory is available to the assistant through the read-only `GetCharacterInventory` tool, restricted to the character’s player or campaign DM. Inventory is not embedded into RAG. See [AI request limits](docs/AI_LIMITS.md). Map images are not analyzed by a vision model; retrieval uses authored titles, descriptions and locations.

## Verification

From the repository root:

```powershell
dotnet build DnDCampaignManager.sln -p:UseAppHost=false
```

From `DnDCampaignManager.web`:

```powershell
npx tsc -b
node --test tests/*.test.cjs
npm run build
```

The frontend production build writes to the API's `wwwroot`. Database regression checks and their fixture behavior are documented in [RAG setup](docs/RAG_SETUP.md). No paid provider calls are needed for the fake-provider checks.
