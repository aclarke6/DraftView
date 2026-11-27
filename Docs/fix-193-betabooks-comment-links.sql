-- =============================================================================
-- Issue #193 — BetaBooks importer placed comments on the wrong chapters
-- =============================================================================
-- APPLIED MANUALLY TO PRODUCTION: 2026-11-27
--   Backup taken first: ~/draftview-pre-193.dump (pg_dump -Fc draftview)
--   Result: UPDATE 5, UPDATE 1
--
-- Cause: BetaBooksImporter matched sections with Title.StartsWith(chapter), so
-- "Chapter 1" matched "Chapter 14 - Meetings" and "Chapter 2" matched
-- "Chapter 29 - Maldon". (Same defect as #164 — Chapter 3 → Chapter 30.)
--
-- Mapping verified against Docs/betabooks-export.json by reader + posted_at.
-- Comments are targeted by Id so legitimate comments on the source sections
-- are untouched.
--
-- No EF migration: the data is already corrected in production. Do NOT apply
-- the Up block again.
-- =============================================================================

-- Up (applied) ----------------------------------------------------------------

-- Chapter 14 - Meetings (d6cb2d33-…) → Chapter 1 - Ashes and Old Roads
UPDATE "Comments" SET "SectionId" = '3b9c9df0-1a39-47e5-9040-9d27d6c22cce'
WHERE "Id" IN ('00a773db-2101-43ec-a456-8b241beac278',  -- Becca Dunlop   2026-02-10 22:40
               'ddade17b-5436-42d7-a59a-4890a356e380',  -- Becca Dunlop   2026-02-10 22:45
               '60dde11a-6e58-4b91-9539-d522a0abdf02',  -- Becca Dunlop   2026-02-11 22:30
               '6d82c053-752d-4a0b-b7cb-eeda9de9362d',  -- Hilary R-B     2026-03-11 17:37
               '9f4bc85d-c6c1-471d-a9ca-3f14327de0dc'); -- Author reply   2026-03-13 23:25

-- Chapter 29 - Maldon (21e06932-…) → Chapter 2 - Old Fires, New Lines
UPDATE "Comments" SET "SectionId" = '04807068-2e15-4c85-8bb5-e18f43022ba9'
WHERE "Id" = '46aa9577-0294-42a2-be3c-f88c3cb4ec72';    -- Becca Dunlop   2026-02-22 22:44

-- Down (rollback only — not applied) ------------------------------------------
/*
UPDATE "Comments" SET "SectionId" = 'd6cb2d33-23e3-4ea7-9465-f8e40ba56a59'
WHERE "Id" IN ('00a773db-2101-43ec-a456-8b241beac278',
               'ddade17b-5436-42d7-a59a-4890a356e380',
               '60dde11a-6e58-4b91-9539-d522a0abdf02',
               '6d82c053-752d-4a0b-b7cb-eeda9de9362d',
               '9f4bc85d-c6c1-471d-a9ca-3f14327de0dc');
UPDATE "Comments" SET "SectionId" = '21e06932-cc8d-4646-aad7-291aa42edd73'
WHERE "Id" = '46aa9577-0294-42a2-be3c-f88c3cb4ec72';
*/
