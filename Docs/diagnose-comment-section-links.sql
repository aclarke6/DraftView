-- diagnose-comment-section-links.sql
--
-- Purpose: Determine whether comments are linked to wrong sections.
--          Two scenarios possible:
--            A) Wrong import  -- StartsWith("Chapter 3") matched "Chapter 30 - Summons"
--                               because both start with "Chapter 3". Comment SectionId
--                               was wrong from day one.
--            B) Wrong section rename -- comment was correctly linked at import time but
--                               the section was later renamed/renumbered by a sync,
--                               so the link is structurally correct but the title changed.
--
-- Key diagnostic: if a "Chapter 3 - ..." section EXISTS but has NO comments,
--                 and "Chapter 30 - Summons" has March 2026 comments, scenario A is confirmed.
--                 If no "Chapter 3 - ..." section exists at all, scenario B is more likely.
--
-- Run: sudo -u postgres psql -d draftview -f diagnose-comment-section-links.sql
-- ─────────────────────────────────────────────────────────────────────────────


-- ── Part 1 ──────────────────────────────────────────────────────────────────
-- All Folder sections whose title starts with "Chapter 3".
-- Check "CreatedAt" vs the March 2026 import window to determine timeline.

\echo ''
\echo '=== Part 1: Sections whose title starts with Chapter 3 ==='
\echo ''

SELECT
    s."Id",
    s."Title",
    s."IsPublished",
    s."IsSoftDeleted",
    s."CreatedAt"::date        AS "Created",
    COUNT(c."Id")              AS "CommentCount"
FROM "Sections" s
LEFT JOIN "Comments" c ON c."SectionId" = s."Id"
WHERE s."Title" ILIKE 'Chapter 3%'
  AND s."NodeType" = 'Folder'
GROUP BY s."Id", s."Title", s."IsPublished", s."IsSoftDeleted", s."CreatedAt"
ORDER BY s."Title";


-- ── Part 2 ──────────────────────────────────────────────────────────────────
-- All comments currently linked to any "Chapter 3x" section.
-- CommentPosted before SectionCreated would indicate an import timing anomaly.

\echo ''
\echo '=== Part 2: Comments on Chapter 3x sections ==='
\echo ''

SELECT
    s."Title"                  AS "Section",
    s."CreatedAt"::date        AS "SectionCreated",
    u."DisplayName"            AS "Commenter",
    c."CreatedAt"::date        AS "CommentPosted",
    c."Status",
    LEFT(c."Body", 100)        AS "BodyExcerpt"
FROM "Comments" c
JOIN "Sections" s ON s."Id"  = c."SectionId"
JOIN "AppUsers" u ON u."Id"  = c."AuthorId"
WHERE s."Title" ILIKE 'Chapter 3%'
ORDER BY s."Title", c."CreatedAt";


-- ── Part 3 ──────────────────────────────────────────────────────────────────
-- Full comment inventory across all chapters: section title, comment date,
-- commenter. Ordered by chapter then date so mismatches are visible by eye.

\echo ''
\echo '=== Part 3: All comments by section (full picture) ==='
\echo ''

SELECT
    s."Title"                  AS "Section",
    u."DisplayName"            AS "Commenter",
    c."CreatedAt"::date        AS "CommentPosted",
    c."Status",
    LEFT(c."Body", 80)         AS "BodyExcerpt"
FROM "Comments" c
JOIN "Sections" s ON s."Id"  = c."SectionId"
JOIN "AppUsers" u ON u."Id"  = c."AuthorId"
ORDER BY s."Title", c."CreatedAt";
