# DraftView — UAT Plan
Version: 1.0 | Date: 2026-04-19

---

## Test Project Structure

The **Test** Scrivener project has this structure (from Test.scrivx):

```
Manuscript
├── Book 1
│   ├── Part 1
│   │   ├── Chapter 1
│   │   │   ├── Scene 1  (Status: To Do)
│   │   │   └── Scene 2  (Status: First Draft)
│   │   └── Chapter 2
│   │       └── Scene 1  (Status: Revised Draft)
│   └── Part 2
│       └── Chapter 3
│           └── Scene 1  (no status)
└── Book 2
    └── Act 1
        └── Chapter 1
            └── Scene 1  (Status: In Progress)
```

---

## Database Reset — Run Before Each UAT Session

This script resets the Test project to a known state.

**On Windows — connect to production:**

`ssh -i C:\Users\alast\.ssh\draftview-prod.key ubuntu@193.123.182.208`

**Once on the server — find the Test project ID:**
```bash
/tmp/run-query.sh -c "SELECT \"Id\", \"Name\" FROM \"Projects\" WHERE \"Name\" = 'Test';"
```

**Then reset Test project data only (replace {TEST_PROJECT_ID} with the actual UUID):**
```bash
/tmp/run-query.sh <<'EOF'
DO $
DECLARE
    v_project_id UUID := '{TEST_PROJECT_ID}';
BEGIN
    DELETE FROM "SectionVersions"
    WHERE "SectionId" IN (
        SELECT "Id" FROM "Sections" WHERE "ProjectId" = v_project_id
    );

    UPDATE "Sections"
    SET "IsPublished" = false,
        "PublishedAt" = null,
        "ContentChangedSincePublish" = false,
        "IsLocked" = false,
        "LockedAt" = null,
        "ScheduledPublishAt" = null
    WHERE "ProjectId" = v_project_id;

    UPDATE "ReadEvents"
    SET "LastReadVersionNumber" = null,
        "BannerDismissedAtVersion" = null
    WHERE "SectionId" IN (
        SELECT "Id" FROM "Sections" WHERE "ProjectId" = v_project_id
    );

    UPDATE "Comments"
    SET "SectionVersionId" = null
    WHERE "SectionId" IN (
        SELECT "Id" FROM "Sections" WHERE "ProjectId" = v_project_id
    );
END $;
EOF
```

**Back on Windows — trigger a fresh Dropbox sync** from the Author Dashboard to pull
current Scrivener content into Section.HtmlContent.

---

## UAT Scenarios

### A — Sync and Initial State

| # | Action | Expected |
|---|--------|----------|
| A1 | Sync the Test project | All sections appear in Sections view with correct tree structure |
| A2 | Verify Book 1 / Part 1 / Chapter 1 shows Scene 1 and Scene 2 | Both scenes visible, unpublished |
| A3 | Verify Book 2 / Act 1 / Chapter 1 shows Scene 1 | Scene visible, unpublished |
| A4 | Verify Scrivener status labels show on scenes | "To Do", "First Draft", "Revised Draft", "In Progress" visible |

---

### B — Publishing Flow

| # | Action | Expected |
|---|--------|----------|
| B1 | Publish Chapter 1 (Book 1 / Part 1) | Both scenes become reader-visible. Badge shows "Published" |
| B2 | As reader, open Scene 1 of Chapter 1 | Content visible. No update banner (first read) |
| B3 | As reader, open Scene 2 of Chapter 1 | Content visible. No update banner (first read) |
| B4 | Publish Chapter 2 (Book 1 / Part 1) | Scene becomes reader-visible |
| B5 | Publish Chapter 3 (Book 2 via Part 2) | Scene becomes reader-visible |

---

### C — Republish and Versioning

| # | Action | Expected |
|---|--------|----------|
| C1 | In Scrivener, edit Scene 1 of Chapter 1. Sync. | ContentChangedSincePublish = true. Change indicator appears on Sections view |
| C2 | Verify change indicator shows Polish/Revision/Rewrite label | Colour-coded label visible next to "Publish changes" link |
| C3 | Click "Publish changes" → go to Publishing Page | Publishing Page shows Chapter 1 with changes |
| C4 | Republish Chapter 1 | New SectionVersion created. Change indicator clears |
| C5 | As reader, open Scene 1 again | Update banner appears showing version number |
| C6 | Verify AI summary appears in banner (if Anthropic:ApiKey configured) | One-line summary names characters/locations from the scene |
| C7 | Reader dismisses banner | Banner gone. Does not reappear on next open of same version |
| C8 | Reader opens scene again after dismiss | No banner. Content shows correctly |

---

### D — Per-Document Publishing (Publishing Page)

| # | Action | Expected |
|---|--------|----------|
| D1 | In Scrivener, edit Scene 2 of Chapter 1 only. Sync. | Only Scene 2 shows ContentChangedSincePublish |
| D2 | Go to Publishing Page for Test project | Chapter 1 shows with per-document controls expanded |
| D3 | Republish Scene 2 only (not Scene 1) | Scene 2 gets new version. Scene 1 version unchanged |
| D4 | As reader, open Scene 2 | Update banner appears for Scene 2 |
| D5 | As reader, open Scene 1 | No update banner (Scene 1 not changed) |
| D6 | Revoke Scene 2's latest version | Previous version restored. Scene 2 shows previous content |

---

### E — Locking and Scheduling

| # | Action | Expected |
|---|--------|----------|
| E1 | Lock Chapter 2 on Publishing Page | Lock indicator shown. Republish button disabled |
| E2 | Try to republish Chapter 2 while locked | Error shown. Version not created |
| E3 | Unlock Chapter 2 | Republish available again |
| E4 | Set a suggested publish date on Chapter 3 | Date shown on Publishing Page |
| E5 | Clear the suggested date | Date removed |
| E6 | Republish Chapter 3 with suggested date set | Republish still works — schedule is advisory only |

---

### F — Reader Experience

| # | Action | Expected |
|---|--------|----------|
| F1 | Reader opens a scene for the first time | No update banner. No "updated since you last read" notice |
| F2 | Author republishes. Reader opens same scene again | "Updated since you last read" notice shown |
| F3 | Update banner shown with version number | Banner is non-blocking. Reader can continue reading |
| F4 | Reader dismisses banner | Banner cleared. TempData or AJAX call to DismissBanner succeeds |
| F5 | Reader switches between scenes | Each scene tracks read state independently |
| F6 | Reader reading preferences (font/size) | Applied correctly in desktop and mobile views |

---

### G — Diff Highlighting

| # | Action | Expected |
|---|--------|----------|
| G1 | After republish with changes, reader opens scene | Changed paragraphs highlighted |
| G2 | Added paragraphs | Shown with added styling |
| G3 | Removed paragraphs | Shown as thin visual markers (not strikethrough) |
| G4 | Unchanged paragraphs | No highlight |
| G5 | Reader who has not previously read | No diff shown (no baseline to diff against) |

---

### H — Incremental Sync (V-Sprint 8)

| # | Action | Expected |
|---|--------|----------|
| H1 | After first sync, check Project.DropboxCursor is set | Cursor stored in database |
| H2 | Make a small change in Scrivener (one scene). Sync again | Only changed file downloaded. Log shows incremental sync |
| H3 | Verify unchanged scenes were not re-downloaded | Log shows fewer files processed than full sync |

---

### I — Version Retention (V-Sprint 9)

| # | Action | Expected |
|---|--------|----------|
| I1 | With Free tier (limit=3), republish a scene 3 times | All 3 versions created successfully |
| I2 | Attempt 4th republish | Error: "Version limit of 3 reached" shown as toast |
| I3 | Go to Publishing Page — version history shown | 3 versions listed. Delete button on versions 1 and 2 |
| I4 | Delete version 1 | Version removed. Now 2 versions remain |
| I5 | Republish again | 4th version (now 3rd after deletion) created successfully |
| I6 | Attempt to delete current version via version history | Delete button not shown for current version |

---

### J — Unpublish Flow

| # | Action | Expected |
|---|--------|----------|
| J1 | Unpublish Chapter 1 | Scenes no longer visible to readers |
| J2 | As reader, attempt to access Chapter 1 scene | Redirected or shown as unavailable |
| J3 | Re-publish Chapter 1 | Scenes visible again with existing versions intact |

---

### K — Mobile Reader

| # | Action | Expected |
|---|--------|----------|
| K1 | Reader opens project on mobile | Mobile chapter list shown |
| K2 | Reader opens scene on mobile | Content readable. No UI breakage |
| K3 | Update banner shown on mobile | Banner visible and dismissible |
| K4 | Diff highlighting on mobile | Changed paragraphs highlighted correctly |

---

## Pass Criteria

All scenarios marked ✅ Pass before go-live sign-off.
Any ❌ Fail must be logged as an open bug with reproduction steps.

| Scenario | Result | Notes | Date |
|----------|--------|-------|------|
| A — Sync and Initial State | | | |
| B — Publishing Flow | | | |
| C — Republish and Versioning | | | |
| D — Per-Document Publishing | | | |
| E — Locking and Scheduling | | | |
| F — Reader Experience | | | |
| G — Diff Highlighting | | | |
| H — Incremental Sync | | | |
| I — Version Retention | | | |
| J — Unpublish Flow | | | |
| K — Mobile Reader | | | |
