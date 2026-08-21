---
mode: agent
description: Production Reset — Clean database, empty Dropbox cache, fresh sync, reimport BetaBooks comments
---

# Production Reset — Fresh Start

## Agent Instructions

This is an **operational runbook**, not a code sprint. No new code is written.
No migrations are added. No tests are required.

Operate in **agent mode** — run terminal commands, SSH to production, execute SQL.

Before starting:
1. Read `TASKS.md` — section `POST-VSPRINT-4 — Production Database Rebuild`
2. Read `DraftView.DevTools/BetaBooksImporter.cs` — understand the import flow
3. Confirm the current branch is `main` and the working tree is clean
4. Confirm the production server SSH key is available at `C:\Users\alast\.ssh\draftview-prod.key`
5. Confirm `betabooks-export.json` exists in the repo root

Do not skip steps. Do not guess. Execute in the exact order below.

---

## Goal

Reset the production database and Dropbox local cache to a clean state, perform a
fresh Scrivener sync to populate working content, republish all chapters to create
`SectionVersion` records, then reimport the original BetaBooks reader comments from
`betabooks-export.json`.

After this runbook completes:
- Production database contains only the author, readers, and project — no stale versioning data
- Dropbox local cache is empty — next sync downloads everything fresh
- All chapters are republished — `SectionVersion` records exist for every document
- All BetaBooks reader comments are present in the database
- Readers can log in and read

---

## Step 1 — Pre-flight checks (local)

Run from the repo root on Windows:

```powershell
git status
git branch
```

Confirm:
- On `main`
- Working tree clean
- No uncommitted changes

Confirm the JSON file exists:

```powershell
Test-Path betabooks-export.json
```

Expected: `True`

---

## Step 2 — SSH to production

```powershell
ssh -i C:\Users\alast\.ssh\draftview-prod.key ubuntu@193.123.182.208
```

---

## Step 3 — Stop the application service

```bash
sudo systemctl stop draftview
sudo systemctl status draftview
```

Confirm status is `inactive (dead)` before proceeding.

---

## Step 4 — Empty the Dropbox local cache

The local cache holds downloaded Scrivener RTF files. Emptying it forces a full
re-download on next sync. Do not delete the directory itself — only its contents.

Locate the cache path from `appsettings.Production.json`:

```bash
cat /var/www/draftview/appsettings.Production.json | grep -i cache
```

Then empty the cache directory (replace `{CACHE_PATH}` with the actual path):

```bash
rm -rf {CACHE_PATH}/*
ls {CACHE_PATH}
```

Expected: empty directory listing.

---

## Step 5 — Reset the production database

Connect to PostgreSQL:

```bash
psql -U draftview -d draftview
```

Password is in `appsettings.Production.json` under `ConnectionStrings:DefaultConnection`.

Run the reset SQL:

```sql
TRUNCATE "SectionVersions" CASCADE;
TRUNCATE "Comments" CASCADE;
TRUNCATE "ReadEvents" CASCADE;
TRUNCATE "AuthorNotifications" CASCADE;
UPDATE "Sections"
    SET "IsPublished" = false,
        "PublishedAt" = null,
        "ContentChangedSincePublish" = false,
        "HtmlContent" = null,
        "ContentHash" = null;
UPDATE "Projects"
    SET "SyncStatus" = 0,
        "LastSyncedAt" = null,
        "SyncErrorMessage" = null,
        "DropboxCursor" = null;
```

Verify row counts:

```sql
SELECT COUNT(*) FROM "SectionVersions";
SELECT COUNT(*) FROM "Comments";
SELECT COUNT(*) FROM "ReadEvents";
SELECT COUNT(*) FROM "AuthorNotifications";
SELECT COUNT(*) FROM "Sections" WHERE "IsPublished" = true;
```

All counts must be zero before proceeding. Exit psql:

```sql
\q
```

---

## Step 6 — Restart the application service

```bash
sudo systemctl start draftview
sudo systemctl status draftview
```

Confirm status is `active (running)` before proceeding.

---

## Step 7 — Trigger Dropbox sync (from Windows browser)

Return to Windows. Open the production DraftView author dashboard in a browser:

```
https://draftview.co.uk/Author/Dashboard
```

Log in as the author. For each project (currently: *Book 1 — The Fractured Lattice*):

1. Navigate to the project dashboard
2. Click **Sync now** (or equivalent Dropbox sync trigger)
3. Wait for sync to complete — monitor the sync status indicator
4. Confirm `SyncStatus` shows `Healthy` and `LastSyncedAt` is populated

Verify sections have content after sync:

```powershell
ssh -i C:\Users\alast\.ssh\draftview-prod.key ubuntu@193.123.182.208 `
  "psql -U draftview -d draftview -c ""SELECT COUNT(*) FROM \""Sections\"" WHERE \""HtmlContent\"" IS NOT NULL;"""
```

Expected: count greater than zero.

---

## Step 8 — Republish all chapters

From the production author dashboard:

```
https://draftview.co.uk/Author/Sections?projectId={PROJECT_ID}
```

For every published chapter, click **Republish** (or navigate to the Publishing Page
and republish each chapter). Work through all chapters in order.

After republishing all chapters, verify `SectionVersion` records exist:

```powershell
ssh -i C:\Users\alast\.ssh\draftview-prod.key ubuntu@193.123.182.208 `
  "psql -U draftview -d draftview -c ""SELECT COUNT(*) FROM \""SectionVersions\"";"""
```

Expected: count equal to the number of published documents (one record per document).

---

## Step 9 — Reimport BetaBooks comments

Run the BetaBooks importer from the repo root on Windows. Retrieve the production
connection string from `appsettings.Production.json` on the server before running:

```bash
cat /var/www/draftview/appsettings.Production.json | grep ConnectionString
```

Then run the importer locally, substituting the production connection string:

```powershell
dotnet run --project DraftView.DevTools -- import-betabooks `
  --connection "Host=193.123.182.208;Database=draftview;Username=draftview;Password={PASSWORD}" `
  --json betabooks-export.json `
  --author alastair_clarke@yahoo.com
```

Review the importer output:
- Note how many comments were imported
- Note how many were skipped
- Note any chapters not found — investigate if unexpected chapters are missing

Verify comment count in production:

```powershell
ssh -i C:\Users\alast\.ssh\draftview-prod.key ubuntu@193.123.182.208 `
  "psql -U draftview -d draftview -c ""SELECT COUNT(*) FROM \""Comments\"";"""
```

Expected: matches the importer's reported imported count.

---

## Step 10 — Verify reader access

Log in as Becca Dunlop and Hilary Royston-Bishop (or send password reset emails from
the GO-LIVE GATE checklist) and confirm:

- Reader dashboard loads and shows *The Fractured Lattice*
- Chapters are accessible and prose renders correctly
- Their imported comments are visible on the relevant chapter pages
- No 404, 500, or decryption errors

---

## Step 11 — Final report

Report:
- Number of `SectionVersion` records created
- Number of comments imported
- Any chapters not found during import and whether they are expected
- Confirmation that Becca and Hilary can log in
- Date and time completed

---

## Rollback

If anything goes wrong before Step 6 (before the service is restarted), the database
can be restored by restarting the service and re-running migrations — no data has been
published to readers yet.

If anything goes wrong after Step 6, the safest recovery is to repeat the runbook from
Step 5 onwards — the reset SQL is idempotent.

---

## Do NOT do in this runbook

- Apply any EF migrations — schema is unchanged
- Modify any code
- Change `appsettings.Production.json` encryption keys
- Deactivate or remove reader accounts (Becca and Hilary must remain active)
- Delete the `betabooks-export.json` file from the repo
