# DraftView — UAT Plan
Version: 1.1 | Date: 2026-04-19

---

## Test Project Structure

The **Test** Scrivener project:

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

**Step 1** — On Windows, connect to production:

`ssh -i C:\Users\alast\.ssh\draftview-prod.key ubuntu@193.123.182.208`

**Step 2** — On the server, find the Test project ID:
```bash
/tmp/run-query.sh -c "SELECT \"Id\", \"Name\" FROM \"Projects\" WHERE \"Name\" = 'Test';"
```

**Step 3** — Reset Test project data only (replace {TEST_PROJECT_ID} with the UUID from Step 2):
```bash
/tmp/run-query.sh <<'EOF'
DO $$
DECLARE
    v_project_id UUID := '{TEST_PROJECT_ID}';
BEGIN
    DELETE FROM "SectionVersions"
    WHERE "SectionId" IN (
        SELECT "Id" FROM "Sections" WHERE "ProjectId" = v_project_id
    );
    UPDATE "Sections"
    SET "IsPublished" = false, "PublishedAt" = null,
        "ContentChangedSincePublish" = false, "IsLocked" = false,
        "LockedAt" = null, "ScheduledPublishAt" = null
    WHERE "ProjectId" = v_project_id;
    UPDATE "ReadEvents"
    SET "LastReadVersionNumber" = null, "BannerDismissedAtVersion" = null
    WHERE "SectionId" IN (
        SELECT "Id" FROM "Sections" WHERE "ProjectId" = v_project_id
    );
    UPDATE "Comments"
    SET "SectionVersionId" = null
    WHERE "SectionId" IN (
        SELECT "Id" FROM "Sections" WHERE "ProjectId" = v_project_id
    );
END $$;
EOF
```

**Step 4** — Back on Windows, go to Author Dashboard and trigger a Dropbox sync for the Test project.

---

## A — Sync and Initial State

| Step | Action | Expected |
|------|--------|----------|
| A.1 | Go to Author Dashboard. Click Sync on the Test project | Sync completes. No error shown |
| A.2 | Open Sections view for Test project | Tree shows: Book 1 / Part 1 / Chapter 1 / Scene 1 + Scene 2 |
| A.3 | Check Chapter 2 under Part 1 | Scene 1 visible, unpublished |
| A.4 | Check Book 1 / Part 2 / Chapter 3 | Scene 1 visible, unpublished |
| A.5 | Check Book 2 / Act 1 / Chapter 1 | Scene 1 visible, unpublished |
| A.6 | Check Scrivener status labels on scenes | To Do, First Draft, Revised Draft, In Progress visible |

---

## B — Publishing Flow

| Step | Action | Expected |
|------|--------|----------|
| B.1 | On Sections view, publish Chapter 1 (Book 1 / Part 1) | Both scenes get Published badge |
| B.2 | Publish Chapter 2 (Book 1 / Part 1) | Scene 1 gets Published badge |
| B.3 | Publish Chapter 3 (Book 1 / Part 2) | Scene 1 gets Published badge |
| B.4 | Publish Book 2 / Act 1 / Chapter 1 | Scene 1 gets Published badge |
| B.5 | Log in as reader. Open Chapter 1 Scene 1 | Content visible. No update banner. No diff notice |
| B.6 | Open Chapter 1 Scene 2 | Content visible. No update banner. No diff notice |

---

## C — Republish and Versioning

| Step | Action | Expected |
|------|--------|----------|
| C.1 | In Scrivener, make a small edit to Chapter 1 Scene 1. Save | Scene modified in Scrivener |
| C.2 | Back on DraftView, sync the Test project | Sync completes |
| C.3 | Open Sections view | Chapter 1 shows "Publish changes" link and change indicator (Polish/Revision/Rewrite) |
| C.4 | Click "Publish changes" | Publishing Page opens showing Chapter 1 with changes |
| C.5 | Click Republish Chapter on Publishing Page | Chapter republished. Change indicator clears |
| C.6 | Log in as reader. Open Chapter 1 Scene 1 | Update banner shows version number |
| C.7 | Check banner content | Version number shown. AI summary shown if Anthropic:ApiKey configured |
| C.8 | Click dismiss on the banner | Banner disappears immediately |
| C.9 | Reload the scene | Banner does not reappear |

---

## D — Per-Document Publishing

| Step | Action | Expected |
|------|--------|----------|
| D.1 | In Scrivener, edit only Scene 2 of Chapter 1. Save | Scene 2 modified |
| D.2 | Sync the Test project | Sync completes |
| D.3 | Open Publishing Page for Test project | Chapter 1 shows per-document controls. Scene 2 shows change |
| D.4 | Republish Scene 2 only (not Scene 1) | Scene 2 gets new version. Scene 1 unchanged |
| D.5 | Log in as reader. Open Scene 2 | Update banner appears |
| D.6 | Open Scene 1 | No update banner |
| D.7 | On Publishing Page, click Revoke on Scene 2 | Previous version restored |
| D.8 | As reader, open Scene 2 again | Shows previous version content |

---

## E — Locking and Scheduling

| Step | Action | Expected |
|------|--------|----------|
| E.1 | On Publishing Page, click Lock on Chapter 2 | Lock indicator shown. Republish button hidden |
| E.2 | In Scrivener, edit Chapter 2 Scene 1. Sync | Sync completes. ContentChangedSincePublish set |
| E.3 | Attempt to republish Chapter 2 | Error toast: chapter is locked |
| E.4 | Click Unlock on Chapter 2 | Republish available again |
| E.5 | On Publishing Page, set a suggested date on Chapter 3 | Date shown next to Chapter 3 |
| E.6 | Click Republish Chapter 3 | Republish succeeds — schedule is advisory only |
| E.7 | Clear the suggested date on Chapter 3 | Date removed |

---

## F — Reader Experience

| Step | Action | Expected |
|------|--------|----------|
| F.1 | As reader, open a scene not yet read | No banner. No "updated since you last read" notice |
| F.2 | Author republishes that scene | New version created |
| F.3 | Reader opens same scene again | "Updated since you last read" notice shown above content |
| F.4 | Update banner shown | Banner non-blocking. Reader can scroll and read |
| F.5 | Reader dismisses banner | Banner gone |
| F.6 | Reader opens a different scene | Banner state is independent per scene |
| F.7 | Change reading font in Account Settings | Font applies in reader view |
| F.8 | Change reading font size | Size applies in reader view |

---

## G — Diff Highlighting

| Step | Action | Expected |
|------|--------|----------|
| G.1 | Reader opens a scene after republish | Changed paragraphs highlighted in green/red |
| G.2 | Identify an added paragraph | Shown with added styling (green) |
| G.3 | Identify a removed paragraph | Shown as thin visual marker, not strikethrough |
| G.4 | Identify an unchanged paragraph | No highlight |
| G.5 | Open the same scene as a different reader who has never read it | No diff shown |

---

## H — Incremental Sync

| Step | Action | Expected |
|------|--------|----------|
| H.1 | After any sync, SSH to production and check DropboxCursor on Test project | Cursor is set (non-null) |
| H.2 | In Scrivener, edit one scene only. Sync | Sync completes quickly |
| H.3 | Check production service logs | Log shows incremental sync, not full download |

Check cursor:
```bash
/tmp/run-query.sh -c "SELECT \"Name\", LEFT(\"DropboxCursor\", 20) FROM \"Projects\" WHERE \"Name\" = 'Test';"
```

Check logs:
```bash
sudo journalctl -u draftview --since "5 minutes ago" | grep -i "incremental\|cursor\|changed"
```

---

## I — Version Retention

| Step | Action | Expected |
|------|--------|----------|
| I.1 | Ensure `DraftView:SubscriptionTier` is not set in config (defaults to Free, limit=3) | Config check only |
| I.2 | Republish Chapter 1 Scene 1 three times (edit in Scrivener, sync, republish each time) | All 3 versions created |
| I.3 | Edit Scene 1 again. Sync. Attempt 4th republish | Error toast: version limit of 3 reached |
| I.4 | Go to Publishing Page. Expand Scene 1 version history | 3 versions listed. Delete button on versions 1 and 2 only |
| I.5 | Delete version 1 | Version removed. 2 remain |
| I.6 | Republish Scene 1 | New version created successfully |
| I.7 | Attempt to delete the current version | No delete button shown on current version |

---

## J — Unpublish Flow

| Step | Action | Expected |
|------|--------|----------|
| J.1 | On Sections view, unpublish Chapter 1 | Published badge removed |
| J.2 | As reader, attempt to open Chapter 1 Scene 1 | Scene unavailable or redirected |
| J.3 | Re-publish Chapter 1 | Scenes visible again |
| J.4 | As reader, open Scene 1 | Content shows. Existing versions intact |

---

## K — Mobile Reader

| Step | Action | Expected |
|------|--------|----------|
| K.1 | Log in as reader on a mobile device or narrow browser | Mobile chapter list shown |
| K.2 | Open a published scene | Content readable. No layout breakage |
| K.3 | Republish a scene as author. Reader opens it on mobile | Update banner visible |
| K.4 | Dismiss banner on mobile | Banner dismissed cleanly |
| K.5 | Check diff highlighting on mobile | Changed paragraphs highlighted correctly |

---

## Pass Criteria

All steps marked ✅ Pass before go-live sign-off.
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
