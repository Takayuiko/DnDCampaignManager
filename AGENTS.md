\# DnD Campaign Manager — Codex Instructions



\## Project Overview



DnDCampaignManager is a full-stack web application for managing Dungeons \& Dragons campaigns.



The project also serves as a practical AI-engineering project. The long-term goal is to evolve the application from a traditional campaign-management application into an AI-assisted campaign platform where AI agents can interact with campaign data through controlled application tools.



Development should remain incremental. Preserve working functionality and the existing architecture unless there is a clear reason to change it.



\---



\## Technology Stack



\### Backend



\- C#

\- ASP.NET Core Web API

\- Entity Framework Core

\- ASP.NET Core Identity

\- JWT authentication

\- Refresh tokens

\- PostgreSQL via Npgsql (local development uses Docker Compose)



Azure or another hosted environment may be used later for production deployment.



\### Frontend



\- React

\- TypeScript

\- Vite

\- Tailwind CSS

\- Axios

\- React Router



\---



\## Solution Architecture



The solution is conceptually separated into:



\- `DnDCampaignManager.Api`

&#x20; - ASP.NET Core API

&#x20; - Controllers

&#x20; - Authentication

&#x20; - Database access

&#x20; - Application services

&#x20; - AI integration



\- `DnDCampaignManager.Shared`

&#x20; - Shared models and DTOs where appropriate



\- `DnDCampaignManager.Web`

&#x20; - React + TypeScript frontend

&#x20; - Pages

&#x20; - Components

&#x20; - Authentication context

&#x20; - API communication



Preserve separation of responsibilities between these projects.



Do not move backend logic into the React application.



Do not expose database entities directly to the frontend when a DTO is more appropriate.



\---



\## Existing Authentication Architecture



Authentication is already implemented.



The application uses:



\- ASP.NET Core Identity

\- Password hashing

\- JWT access tokens

\- Refresh tokens

\- Protected API endpoints using `\[Authorize]`

\- Axios for authenticated frontend API requests

\- An Axios interceptor for token refresh

\- React `AuthContext` for authentication state



The frontend stores the current access token and attaches it to authenticated API requests.



There is an authenticated endpoint similar to:



`GET /api/me`



used to retrieve the currently authenticated user.



\### Authentication Rule



Do NOT redesign, replace, or significantly modify authentication while implementing unrelated features.



If authentication must change:



1\. Explain why.

2\. Identify the affected backend and frontend components.

3\. Preserve existing login, logout, refresh-token, and protected-route behavior.

4\. Prefer the smallest safe change.



\---



\## Development Philosophy



This project is being developed both as a real application and as a learning project.



Therefore:



\- Prefer clear code over unnecessarily clever abstractions.

\- Prefer incremental changes over large rewrites.

\- Reuse existing patterns when they are reasonable.

\- Do not introduce libraries without explaining why they are needed.

\- Do not replace working architecture simply because another architecture is more fashionable.

\- Keep backend, frontend, database, and AI responsibilities clearly separated.

\- Avoid premature microservices.

\- Avoid unnecessary abstraction.

\- Keep features testable.

\- Maintain strong typing in both C# and TypeScript.



When a substantial architectural change would improve the application, propose it before implementing it.



\---



\## Working With Existing Code



Before implementing a feature:



1\. Inspect the relevant existing files.

2\. Understand the current data model and API flow.

3\. Search for existing implementations that can be reused.

4\. Identify which backend and frontend components will be affected.

5\. Make the smallest coherent set of changes.



Never assume a class, endpoint, DTO, service, or React component exists without checking the repository.



Do not create duplicate services or models when an appropriate implementation already exists.



\---



\## Database Changes



Entity Framework Core is used for persistence.



When modifying persistent models:



1\. Update the entity/model.

2\. Update relationships and `DbContext` configuration when required.

3\. Create or describe the required EF Core migration.

4\. Consider existing data.

5\. Avoid destructive schema changes unless explicitly requested.



Do not delete the development database merely to resolve a migration problem unless explicitly approved.



\---



\## Frontend Guidelines



Use the existing React + TypeScript + Tailwind architecture.



Prefer:



\- Functional React components

\- Hooks

\- Strong TypeScript interfaces/types

\- Reusable components when reuse is meaningful

\- Existing Axios API configuration

\- Existing authentication context

\- Existing routing conventions



Avoid `any` unless there is a strong reason.



Handle loading, success, empty, and error states where appropriate.



Do not redesign unrelated pages when implementing a feature.



\---



\## API Guidelines



Prefer REST-style endpoints for normal application operations.



Controllers should remain reasonably thin.



Business logic that becomes substantial should move into services rather than accumulating inside controllers.



Validate incoming data.



Use appropriate HTTP status codes.



Use DTOs for API contracts when appropriate.



Protected resources must verify the authenticated user has permission to access or modify them.



\---



\# AI Engineering Direction



AI functionality is a major future direction for this project.



The goal is NOT simply to add a chatbot.



The long-term goal is an AI-assisted Dungeon Master / campaign agent capable of understanding campaign context and safely interacting with application functionality.



Potential future AI tools include:



\- `GetCharacter`

\- `GetCharacterInventory`

\- `GetCampaignState`

\- `GetNPC`

\- `GetQuest`

\- `AddItem`

\- `RemoveItem`

\- `UpdateQuest`

\- `RollDice`

\- `CreateEncounter`

\- `SearchCampaignKnowledge`



The AI should interact with application data through explicit services/tools rather than receiving unrestricted database access.



Tool operations that modify campaign state should be clearly separated from read-only operations.



Potentially destructive or important state-changing operations should support confirmation or validation.



\---



\## Current AI Development Principle



Build AI functionality progressively:



\### Stage 1 — AI Chat



Basic conversation between the user and an OpenAI model.



\### Stage 2 — Campaign Context



Allow the model to receive controlled information about:



\- Campaign

\- Characters

\- NPCs

\- Items

\- Quests



\### Stage 3 — Read-Only Tools



Allow the AI to request application information through explicit tools.



Example:



User:

"What items does Freya have?"



AI:

→ `GetCharacterInventory(Freya)`



Application:

→ returns structured inventory data



AI:

→ answers the user.



\### Stage 4 — State-Changing Tools



Allow controlled actions such as:



\- Add an item

\- Update a quest

\- Modify campaign information

\- Record campaign events



\### Stage 5 — Campaign Agent



Develop an AI-assisted Dungeon Master capable of reasoning over campaign state, retrieving campaign knowledge, calling tools, and assisting with campaign management.



Do not jump directly to Stage 5 without establishing reliable foundations.



\---



\## OpenAI Development



When implementing functionality using OpenAI APIs, Codex, plugins, Agents SDK, or related OpenAI technologies:



Always consult current official OpenAI developer documentation when available rather than relying on potentially outdated API knowledge.



Prefer currently recommended OpenAI APIs and SDK patterns.



Keep API keys and secrets outside source control.



Never commit:



\- OpenAI API keys

\- JWT secrets

\- database credentials

\- production secrets



Use configuration/environment variables or appropriate secret-management mechanisms.



\---



\# Codex Working Rules



When receiving a development task:



\### 1. Inspect



Read the relevant repository files before modifying anything.



\### 2. Explain



For non-trivial work, briefly explain:



\- What you found

\- What you intend to change

\- Which files are likely to be affected



\### 3. Implement



Make focused changes.



Avoid unrelated refactoring.



\### 4. Verify



When possible:



\- Build the .NET solution.

\- Run relevant tests.

\- Build/type-check the React application.

\- Check TypeScript errors.

\- Check for obvious runtime/API contract mismatches.



\### 5. Report



After completing work, report:



\- Files changed

\- What changed

\- Database migrations required

\- Commands that were run

\- Tests/build results

\- Remaining issues or risks



Do not claim a build or test passed unless it was actually executed.



\---



\# Debugging Rules



When debugging:



1\. Start from the actual error.

2\. Trace the execution path.

3\. Inspect relevant code.

4\. Identify the root cause before changing code.

5\. Prefer fixing the cause instead of suppressing the symptom.



Do not make multiple speculative changes simultaneously unless necessary.



When possible, reproduce the failure before applying the fix and verify the same scenario afterward.



\---



\# Collaboration With the Developer



The developer is an experienced software engineer but is also using this project to develop stronger modern .NET, React, and AI-engineering skills.



Therefore, when making an important architectural decision, explain the reasoning briefly.



Do not over-explain routine syntax.



When several valid approaches exist, prefer the approach that:



1\. Fits the existing architecture.

2\. Is maintainable.

3\. Teaches useful modern engineering practices.

4\. Avoids unnecessary complexity.



\---



\# Primary Objective



Build a maintainable D\&D Campaign Manager while progressively turning it into a practical AI-engineering project.



Prioritize:



\*\*Correctness → Maintainability → Clarity → Extensibility → AI capability → Optimization\*\*



Do not sacrifice the first three merely to make the system more sophisticated.

