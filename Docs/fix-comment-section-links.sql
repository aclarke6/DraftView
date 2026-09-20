-- fix-comment-section-links.sql
--
-- Issue #164: Two comments were linked to "Chapter 30 - Summons" by the
-- BetaBooks importer due to a StartsWith("Chapter 3") match that hit
-- "Chapter 30..." before "Chapter 3 - Under Pressure".
--
-- Correct target: Chapter 3 - Under Pressure (82a9ae2f-5f9c-48ef-9c46-477a7a19bd5c)
-- Wrong target:   Chapter 30 - Summons       (08bbb476-98f3-4ac0-85a3-d7b469e7188a)
--
-- Usage:
--   Step 1 — validate (no changes made):
--     sudo -u postgres psql -d draftview -f fix-comment-section-links.sql
--
--   Step 2 — apply (pass implement=true):
--     sudo -u postgres psql -d draftview -v implement=true -f fix-comment-section-links.sql
--
-- Always take a pg_dump before running Step 2.
-- ─────────────────────────────────────────────────────────────────────────────

\echo ''
\echo '=== Validation: comments that will be relinked ==='
\echo ''

SELECT
    c."Id"                      AS "CommentId",
    u."DisplayName"             AS "Commenter",
    c."CreatedAt"::date         AS "Posted",
    s."Title"                   AS "CurrentSection",
    'Chapter 3 - Under Pressure' AS "TargetSection",
    LEFT(c."Body", 80)          AS "BodyExcerpt"
FROM "Comments" c
JOIN "Sections" s ON s."Id" = c."SectionId"
JOIN "AppUsers" u ON u."Id"  = c."AuthorId"
WHERE c."SectionId" = '08bbb476-98f3-4ac0-85a3-d7b469e7188a'
ORDER BY c."CreatedAt";

\echo ''

\if :{?implement}
    \echo '=== Applying corrections ==='

    BEGIN;

    UPDATE "Comments"
    SET    "SectionId" = '82a9ae2f-5f9c-48ef-9c46-477a7a19bd5c'
    WHERE  "SectionId" = '08bbb476-98f3-4ac0-85a3-d7b469e7188a';

    \echo ''
    \echo '--- Rows updated ---'
    SELECT COUNT(*) AS "UpdatedRows"
    FROM "Comments"
    WHERE "SectionId" = '82a9ae2f-5f9c-48ef-9c46-477a7a19bd5c';

    \echo ''
    \echo '--- Chapter 30 comment count (should be 0) ---'
    SELECT COUNT(*) AS "Chapter30CommentCount"
    FROM "Comments"
    WHERE "SectionId" = '08bbb476-98f3-4ac0-85a3-d7b469e7188a';

    COMMIT;

    \echo ''
    \echo '=== Done. Changes committed. ==='
\else
    \echo '=== Dry run only. To apply, run with: -v implement=true ==='
\endif
