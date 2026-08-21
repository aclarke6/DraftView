# DraftView — Legacy Sprints

Completed sprints prior to the V-Sprint versioning series. All merged to main.

---

## Sprint 3 — Reader Font Preferences
Completed 2026-04-10.

Users choose preferred reading font and size, persisted per user, applied via CSS variables.

- `ProseFont` and `ProseFontSize` on `UserPreferences`
- Google Fonts (Merriweather, Lato)
- CSS variables `--font-prose`, `--text-prose-base`
- Reading Preferences card in Account/Settings for Authors and BetaReaders

---

## Sprint 2 — Reader Experience
Completed 2026-04-10.

- Project switcher (sidebar, query string, per-project progress)
- Kindle-style resume on login (redirects to correct scene; exact scroll position deferred)
- Persisted `AuthorNotification` entity (dismiss, clear all, 90-day prune)
- `CommentStatus` enum and SetStatus domain method
- Author comment response UI on Reader/Read and Author/Section
- Login redirect fix: Author → Author/Dashboard, SystemSupport → Support/Dashboard

---

## Sprint 1 — Pre-Beta Push
Completed 2026-04-08.

- Prose font fix in reader view
- Comment author display name (live lookup against AppUsers.DisplayName)
- Reader reactivation flow

---

## Email Sprint
Completed 2026-04-08.

- Oracle Email Delivery SMTP, DKIM, SPF, Cloudflare routing
- MailKit replacing `System.Net.Mail` (fixes Oracle STARTTLS)
- `DraftView.Integration.Tests` with SMTP integration test
- ForgotPassword SMTP failure caught and logged

---

## Role Migration — Stages 1–4
Completed 2026-04-06.

- Identity roles as canonical source
- `IAuthorizationFacade` injected into `UserService`
- `SystemSupport` role, `SystemStateMessage` entity + service + footer
- `ReaderController` secured (Author, BetaReader only)
- `HomeController` role-based routing
- CSS versioning automation (`Update-CssVersion.ps1`)
- Mobile reader flow: `IsMobile()`, `MobileChapters`/`MobileScenes`/`MobileRead`
- Desktop reader views renamed to Desktop prefix

---

## ScrivenerProject → Project Rename
Completed 2026-04-17. Full solution-wide rename.

- `ScrivenerProject` → `Project`
- `ScrivenerRootUuid` → `SyncRootId`
- `IScrivenerProjectRepository` → `IProjectRepository`
- `IScrivenerProjectDiscoveryService` → `IProjectDiscoveryService`
- EF migration: `RenameScrivenerProjectToProject`

Retained (Scrivener-specific, names remain correct):
- `ScrivenerProjectDiscoveryService`
- `IScrivenerProjectParser`
- `Section.ScrivenerUuid` (moves to sync mapping table in future extraction sprint)

---

## UserNotificationPreferences → UserPreferences Rename
Completed 2026-04-10.

- Entity, repository, configuration, DI all renamed
- `DisplayTheme` (light/dark) added to `UserPreferences`
- EF migration: `RenameNotificationPreferencesToUserPreferences`
- Theme toggle in Account/Settings

---

## Earlier Infrastructure Work
- Production VM: Oracle Cloud Free Tier, Ubuntu, Nginx, Cloudflare SSL, systemd service
- `IDropboxFileDownloader` — full Dropbox sync working end to end in production
- `UseForwardedHeaders` — fixes OAuth behind Nginx
- `AuthorId` added to Project (migration with backfill)
- `ReaderAccess` entity + repository (TDD, migration)
- Per-author Dropbox OAuth connection (`DropboxConnection` entity, `IDropboxClientFactory`)
- BetaBooks comment importer (54 comments, `Comment.CreateForImport`)
- Toast notifications (fixed position, auto-dismiss)
- Reply threading, comment edit and delete
- `PublishAsPartOfChapter` domain invariant (TDD)
- CSS split into 7 files by concern; Heroicons as static C# class
- Rebrand: DraftReader → DraftView
- `pg.ps1`, `PowerShell.md`, `PRINCIPLES.md`
