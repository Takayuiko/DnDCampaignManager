# Browser refresh coordination

## Refresh-token storage

The API generates 64 random bytes per refresh token. The raw Base64 token is sent in the existing Secure, HttpOnly, SameSite=Lax cookie scoped to `/api/auth`. Database entities contain only its lowercase SHA-256 digest (`TokenHash`) and replacement digest (`ReplacedByTokenHash`). Refresh hashes the incoming cookie before its indexed lookup. A stored digest cannot itself be used as a refresh credential. Registration, login and rotation share this issuance path; expiry, account locks, revocation and logout remain unchanged.

Apply the `HashedRefreshTokens` EF migration with the API stopped before starting the updated version. It renames and hashes both existing token columns in place, preserves IDs, expirations, revocation state and replacement relationships, and adds a unique hash index. PostgreSQL's built-in SHA-256 needs no extension. Existing cookies continue working after upgrading. Rolling the migration back revokes all refresh tokens because hashes cannot be restored to their original credentials; users must log in again. Older backups may still contain plaintext tokens.

Run `./scripts/Run-RegressionChecks.ps1` against a dedicated test database. `AuthRefreshChecks` verifies hashed issuance and replacement storage, rejection of a hash presented as a cookie, cookie protection, replay rejection, concurrent rotation/logout, expiry and login behavior. It also creates a temporary isolated schema to check upgrade preservation, pre-upgrade cookie rotation and rollback revocation, then drops that schema. The test database role needs permission to create schemas.

## Browser coordination

Axios requests and AI fetch streams share one refresh promise per tab. Across tabs on the same application origin, an exclusive Web Lock named `dnd-auth-refresh` serializes cookie rotation. Each caller supplies the access token that received HTTP 401. After acquiring the lock, it rereads local storage and reuses a newer token if another tab has already refreshed. This also handles late responses sent with an older token.

The winning request publishes the new access token before releasing the lock. On failure, it removes the old token before releasing the lock, so waiting tabs reject instead of rotating the same cookie again. A failed or late refresh cannot erase or overwrite a newer login, or restore a locally cleared session. The refresh HTTP request has a 15-second timeout; locks are released when its promise settles or the owning browser context closes.

`AuthProvider` listens for token storage changes, reloads the current user after another tab publishes a token, and clears user state after another tab signs out. Older user-load responses are ignored after subsequent storage changes or unmount. The request interceptor reads the shared token on every request and removes authorization when no token remains.

Automatic refresh requires Web Locks and a secure browser context (HTTPS in production; localhost works for development). Unsupported browsers or insecure non-local origins return to login without attempting an uncoordinated rotation. Coordination is scoped to the application origin; deployments should expose the application through one canonical origin. Backend cookie rotation and permission checks remain unchanged by browser coordination.

Run `node --test tests/authRefresh.test.cjs tests/authStorage.test.cjs` from `DnDCampaignManager.web`. These checks use separate module instances with shared storage and a queued lock manager to exercise multiple tabs, shared failure, late 401s, logout during refresh, newer-login preservation, unsupported contexts, and provider synchronization. Existing stream checks cover the common refresh path.

