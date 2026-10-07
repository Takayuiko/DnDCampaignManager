# DnD Campaign Manager frontend

React, TypeScript, Vite and Tailwind frontend for the ASP.NET Core/PostgreSQL application. See [local setup](../docs/PHASE1_SETUP.md) for database, JWT and API configuration.

## Development

Start the API at `http://localhost:5000`, then run from this directory:

```powershell
npm install
npm run dev
```

Open `http://localhost:5173`. Vite proxies `/api` to the API. Requests use the shared Axios client and existing authentication/refresh flow. An OpenAI key is optional for core campaign, character, map and inventory screens.

## Checks and build

```powershell
npx tsc -b
node --test tests/*.test.cjs
npm run lint
npm run build
```

`npm run build` type-checks and writes the production bundle to `../DnDCampaignManager.Api/wwwroot`, replacing its existing build output. The API serves that bundle outside Development.

## Feature guides

- [Dashboard](../docs/dashboard.md)
- [Maps and locations](../docs/maps.md)
- [Items and inventories](../docs/items.md)
- [Persisted campaign chat](../docs/PHASE2_SETUP.md)
- [Campaign RAG](../docs/RAG_SETUP.md)
