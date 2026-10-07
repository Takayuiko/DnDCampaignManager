-- Run with psql -v dm_email='your registered email' -f this-file.sql.
-- Explicit development setup only; never run automatically at application startup.
\set ON_ERROR_STOP on
BEGIN;
LOCK TABLE "Users" IN SHARE ROW EXCLUSIVE MODE;
SELECT count(*) = 1 AS exactly_one
FROM "Users" WHERE lower("Email") = lower(:'dm_email')
\gset
\if :exactly_one
DELETE FROM "RefreshTokens" WHERE "UserId" IN (
    SELECT "Id" FROM "Users" WHERE lower("Email") = lower(:'dm_email')
    AND ("Role" <> 'DM' OR (lower("Email") = 'hagges02@gmail.com' AND NOT "IsAdmin"))
);
UPDATE "Users" SET
    "TokenVersion" = "TokenVersion" + CASE WHEN "Role" <> 'DM' OR (lower("Email") = 'hagges02@gmail.com' AND NOT "IsAdmin") THEN 1 ELSE 0 END,
    "Role" = 'DM',
    "IsAdmin" = "IsAdmin" OR lower("Email") = 'hagges02@gmail.com'
WHERE lower("Email") = lower(:'dm_email');
SELECT "Id", "Email", "Role", "IsAdmin" FROM "Users"
WHERE lower("Email") = lower(:'dm_email');
COMMIT;
\else
\echo Expected exactly one registered account. No changes were made.
ROLLBACK;
DO $$ BEGIN
    RAISE EXCEPTION 'Expected exactly one registered account. No changes were made.';
END $$;
\endif
