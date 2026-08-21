# Task: Persisted Author Notifications
**Date:** 2026-04-10
**Branch:** feature/persisted-notifications

Replace the on-the-fly dashboard notification assembly with a persisted
`AuthorNotification` entity. Notifications are written at event time and
hard-deleted on dismiss. No soft delete. No migration of existing data.

---

## Architecture rules (non-negotiable)

- TDD required for all Domain, Application, and Infrastructure changes
- Every new author-scoped entity carries `AuthorId` (tenancy-agnostic rule)
- No `&&` in PowerShell — use `;`
- Run `dotnet test` after every red/green cycle and confirm count
- CSS version token must be bumped on every CSS change via regex replace

---

## Solution layout (for reference)

```
DraftView.Domain           — entities, interfaces, enumerations
DraftView.Application      — services (CommentService, UserService, SyncService, DashboardService)
DraftView.Infrastructure   — EF Core, repositories, migrations
DraftView.Web              — ASP.NET Core MVC (AuthorController, Views/Author/Dashboard.cshtml)
DraftView.Domain.Tests
DraftView.Application.Tests
DraftView.Infrastructure.Tests
```

All test projects use xUnit + Moq.

---

## PHASE 1 — Domain entity

### Step 1.1 — Create `AuthorNotification` entity (stub)

Create `DraftView.Domain/Entities/AuthorNotification.cs`:

```csharp
public sealed class AuthorNotification
{
    public Guid Id { get; private set; }
    public Guid AuthorId { get; private set; }
    public NotificationEventType EventType { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Detail { get; private set; }
    public string? LinkUrl { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private AuthorNotification() { }

    public static AuthorNotification Create(
        Guid authorId,
        NotificationEventType eventType,
        string title,
        string? detail,
        string? linkUrl,
        DateTime occurredAt)
    {
        throw new NotImplementedException();
    }
}
```

`NotificationEventType` already exists in
`DraftView.Domain/Notifications/NotificationItemDto.cs` — reuse it, do not
duplicate the enum.

### Step 1.2 — Write failing domain tests

Create `DraftView.Domain.Tests/Entities/AuthorNotificationTests.cs`.

Tests required (all must be RED before proceeding):

```
Create_SetsAllProperties
Create_Throws_WhenAuthorIdIsEmpty
Create_Throws_WhenTitleIsNullOrWhitespace
Create_AllowsNullDetailAndLinkUrl
Create_SetsOccurredAt
```

Run `dotnet test --filter AuthorNotificationTests` — confirm all RED.

### Step 1.3 — Implement `AuthorNotification.Create`

Implement the factory method. Invariants:
- `authorId` must not be `Guid.Empty` — throw `InvariantViolationException("I-NOTIF-01", ...)`
- `title` must not be null or whitespace — throw `InvariantViolationException("I-NOTIF-02", ...)`
- `detail` and `linkUrl` are optional (nullable)
- `Id` = `Guid.NewGuid()`
- `OccurredAt` = the supplied `occurredAt` value (caller sets it, not `DateTime.UtcNow`)

Use the existing `InvariantViolationException` pattern from other entities.

Run `dotnet test --filter AuthorNotificationTests` — confirm all GREEN.

---

## PHASE 2 — Repository interface and stub

### Step 2.1 — Create `IAuthorNotificationRepository`

Create `DraftView.Domain/Interfaces/Repositories/IAuthorNotificationRepository.cs`:

```csharp
public interface IAuthorNotificationRepository
{
    Task AddAsync(AuthorNotification notification, CancellationToken ct = default);
    Task<IReadOnlyList<AuthorNotification>> GetByAuthorIdAsync(Guid authorId, CancellationToken ct = default);
    Task DeleteAsync(Guid notificationId, CancellationToken ct = default);
    Task DeleteAllByAuthorIdAsync(Guid authorId, CancellationToken ct = default);
    Task PruneOlderThanAsync(Guid authorId, DateTime cutoff, CancellationToken ct = default);
}
```

### Step 2.2 — Create EF Core repository implementation (stub)

Create
`DraftView.Infrastructure/Persistence/Repositories/AuthorNotificationRepository.cs`
implementing `IAuthorNotificationRepository`. All methods throw
`NotImplementedException` initially.

### Step 2.3 — EF Core configuration

Create
`DraftView.Infrastructure/Persistence/Configurations/AuthorNotificationConfiguration.cs`:

```csharp
builder.HasKey(n => n.Id);
builder.Property(n => n.AuthorId).IsRequired();
builder.Property(n => n.EventType).IsRequired().HasConversion<string>();
builder.Property(n => n.Title).IsRequired().HasMaxLength(300);
builder.Property(n => n.Detail).HasMaxLength(500);
builder.Property(n => n.LinkUrl).HasMaxLength(500);
builder.Property(n => n.OccurredAt).IsRequired();
builder.HasIndex(n => new { n.AuthorId, n.OccurredAt });
```

Register `AuthorNotification` in `DraftViewDbContext`:

```csharp
public DbSet<AuthorNotification> AuthorNotifications => Set<AuthorNotification>();
```

### Step 2.4 — EF Core migration

```
dotnet ef migrations add AddAuthorNotifications --project DraftView.Infrastructure --startup-project DraftView.Web
dotnet ef database update --project DraftView.Infrastructure --startup-project DraftView.Web
```

### Step 2.5 — Write failing infrastructure tests

Create
`DraftView.Infrastructure.Tests/Persistence/AuthorNotificationRepositoryTests.cs`.

Use an in-memory SQLite context (same pattern as `ScrivenerProjectRepositoryTests`).

Tests required (all RED before implementing):

```
AddAsync_PersistsNotification
GetByAuthorIdAsync_ReturnsOnlyNotificationsForAuthor
GetByAuthorIdAsync_ReturnsOrderedByOccurredAtDescending
DeleteAsync_RemovesSpecificNotification
DeleteAllByAuthorIdAsync_RemovesAllForAuthor
DeleteAllByAuthorIdAsync_DoesNotAffectOtherAuthors
PruneOlderThanAsync_RemovesOldNotifications
PruneOlderThanAsync_PreservesRecentNotifications
```

### Step 2.6 — Implement repository methods

Implement all methods in `AuthorNotificationRepository`.
- `GetByAuthorIdAsync` — filter by `AuthorId`, order by `OccurredAt` descending
- `DeleteAsync` — find by Id, remove
- `DeleteAllByAuthorIdAsync` — `RemoveRange` on all matching `AuthorId`
- `PruneOlderThanAsync` — `RemoveRange` where `AuthorId` matches and `OccurredAt < cutoff`

Run `dotnet test --filter AuthorNotificationRepositoryTests` — confirm all GREEN.

### Step 2.7 — Register repository in DI

In `DraftView.Web/Extensions/ServiceCollectionExtensions.cs`, add:

```csharp
services.AddScoped<IAuthorNotificationRepository, AuthorNotificationRepository>();
```

---

## PHASE 3 — Application: DashboardService

### Step 3.1 — Update `IDashboardService`

Replace `GetRecentNotificationsAsync` signature:

```csharp
// Remove:
Task<IReadOnlyList<NotificationItemDto>> GetRecentNotificationsAsync(
    Guid authorUserId, int maxItems = 20, CancellationToken ct = default);

// Add:
Task<IReadOnlyList<AuthorNotification>> GetNotificationsAsync(
    Guid authorId, CancellationToken ct = default);

Task DismissNotificationAsync(
    Guid notificationId, CancellationToken ct = default);

Task DismissAllNotificationsAsync(
    Guid authorId, CancellationToken ct = default);
```

### Step 3.2 — Stub `DashboardService` methods

In `DashboardService`:

- Remove constructor parameters: `ICommentRepository`, `IInvitationRepository`,
  `IScrivenerProjectRepository` (no longer used for notifications)
- Add constructor parameter: `IAuthorNotificationRepository notificationRepo`
- Replace `GetRecentNotificationsAsync` with stub implementations of the three
  new methods, all throwing `NotImplementedException`
- Keep `GetProjectOverviewAsync`, `GetReaderSummaryAsync`,
  `GetEmailHealthSummaryAsync` unchanged

### Step 3.3 — Write failing DashboardService tests

In `DraftView.Application.Tests/Services/DashboardServiceTests.cs`, add:

```
GetNotificationsAsync_ReturnsNotificationsForAuthor
GetNotificationsAsync_CallsPruneBeforeReturning
DismissNotificationAsync_CallsDeleteOnRepo
DismissAllNotificationsAsync_CallsDeleteAllOnRepo
```

Keep the existing `GetProjectOverviewAsync`, `GetReaderSummaryAsync`,
`GetEmailHealthSummaryAsync` tests — they must remain GREEN throughout.

Update `CreateSut()` to inject `IAuthorNotificationRepository` mock.
Remove the now-unused `_commentRepo`, `_invRepo`, `_projectRepo` mocks from the
dashboard test class (those repos are no longer injected into DashboardService).

### Step 3.4 — Implement DashboardService methods

- `GetNotificationsAsync`: call `PruneOlderThanAsync(authorId, DateTime.UtcNow.AddDays(-90))`,
  then return `GetByAuthorIdAsync(authorId)`
- `DismissNotificationAsync`: call `DeleteAsync(notificationId)` then `SaveChangesAsync`
- `DismissAllNotificationsAsync`: call `DeleteAllByAuthorIdAsync(authorId)` then `SaveChangesAsync`

`DashboardService` needs `IUnitOfWork` added to its constructor for the dismiss operations.

Run `dotnet test --filter DashboardServiceTests` — confirm all GREEN.

---

## PHASE 4 — Application: write notifications at event time

For each service, inject `IAuthorNotificationRepository` into the constructor.
The `authorId` is available in each context as described below.

### Step 4.1 — CommentService: new root comment

After `unitOfWork.SaveChangesAsync` in `CreateRootCommentAsync`:

```csharp
// Only notify if commenter is a reader (not author commenting on own work)
if (user.Role == Role.BetaReader)
{
    var notification = AuthorNotification.Create(
        authorId:   /* resolved below */,
        eventType:  NotificationEventType.NewComment,
        title:      $"{user.DisplayName} commented on \"{section.Title}\"",
        detail:     Truncate(body),
        linkUrl:    $"/Author/Section/{sectionId}",
        occurredAt: DateTime.UtcNow);
    await notificationRepo.AddAsync(notification, ct);
    await unitOfWork.SaveChangesAsync(ct);
}
```

The `authorId` for the notification must be the site author's `Id`. Add
`IUserRepository` (already injected) — call
`userRepo.GetAuthorAsync(ct)` to resolve it. `GetAuthorAsync` already exists on
`IUserRepository`.

Failing tests required before implementing (in `CommentServiceTests.cs`):

```
CreateRootCommentAsync_WritesNewCommentNotification_WhenReaderComments
CreateRootCommentAsync_DoesNotWriteNotification_WhenAuthorComments
```

### Step 4.2 — CommentService: reply to author's comment

After `unitOfWork.SaveChangesAsync` in `CreateReplyAsync`, when the parent
comment's `AuthorId` is the site author:

```csharp
var author = await userRepo.GetAuthorAsync(ct);
if (author is not null && parent.AuthorId == author.Id && user.Role == Role.BetaReader)
{
    var notification = AuthorNotification.Create(
        authorId:   author.Id,
        eventType:  NotificationEventType.ReplyToAuthor,
        title:      $"{user.DisplayName} replied to your comment on \"{section.Title}\"",
        detail:     Truncate(body),
        linkUrl:    $"/Author/Section/{parent.SectionId}",
        occurredAt: DateTime.UtcNow);
    await notificationRepo.AddAsync(notification, ct);
    await unitOfWork.SaveChangesAsync(ct);
}
```

Failing tests required before implementing:

```
CreateReplyAsync_WritesReplyToAuthorNotification_WhenReaderRepliesToAuthorComment
CreateReplyAsync_DoesNotWriteNotification_WhenReplyIsNotToAuthorComment
```

Add a `private static string Truncate(string body, int max = 80)` helper to
`CommentService` — same implementation as in `NotificationItemDto`.

### Step 4.3 — UserService: invitation accepted

After `unitOfWork.SaveChangesAsync` in `AcceptInvitationAsync`:

```csharp
var author = await userRepo.GetAuthorAsync(ct);
if (author is not null)
{
    var notification = AuthorNotification.Create(
        authorId:   author.Id,
        eventType:  NotificationEventType.ReaderJoined,
        title:      $"{user.DisplayName} accepted their invitation",
        detail:     null,
        linkUrl:    "/Author/Readers",
        occurredAt: DateTime.UtcNow);
    await notificationRepo.AddAsync(notification, ct);
    await unitOfWork.SaveChangesAsync(ct);
}
```

`UserService` needs `IAuthorNotificationRepository` added to its constructor.

Failing tests required before implementing (in `UserServiceInvitationAcceptanceTests.cs`):

```
AcceptInvitationAsync_WritesReaderJoinedNotification
```

### Step 4.4 — SyncService: sync completed

At the end of the `try` block in `ParseProjectAsync`, just before
`project.UpdateSyncStatus(SyncStatus.Healthy, ...)`:

```csharp
var author = await userRepo.GetAuthorAsync(ct);
if (author is not null)
{
    var notification = AuthorNotification.Create(
        authorId:   author.Id,
        eventType:  NotificationEventType.SyncCompleted,
        title:      $"Sync completed for {project.Name}",
        detail:     null,
        linkUrl:    null,
        occurredAt: DateTime.UtcNow);
    await notificationRepo.AddAsync(notification, ct);
}
```

`SyncService` needs `IAuthorNotificationRepository` and `IUserRepository` added
to its constructor. `IUserRepository` is not currently injected — add it.

Failing tests required before implementing (in `SyncServiceTests.cs`):

```
ParseProjectAsync_WritesNotification_OnSuccessfulSync
ParseProjectAsync_DoesNotWriteNotification_OnSyncFailure
```

Run `dotnet test` after all Phase 4 steps — confirm full suite GREEN.

---

## PHASE 5 — Remove obsolete dashboard notification code

Only after all tests are GREEN:

- Remove `GetRecentNotificationsAsync` from `IDashboardService` and `DashboardService`
- Remove `IInvitationRepository.GetRecentlyAcceptedAsync` — check for any other
  callers before removing; if none, remove the method from the interface and
  implementation
- Remove `IScrivenerProjectRepository.GetRecentlySyncedAsync` — same check
- Remove `ICommentRepository.GetRecentCommentsForDashboardAsync` — same check
- Remove unused constructor parameters from `DashboardService` if any remain

Run `dotnet test` — confirm all GREEN, note new test count.

---

## PHASE 6 — Web layer

### Step 6.1 — Update `DashboardViewModel`

In `DraftView.Web/Models/AuthorViewModels.cs`, find `DashboardViewModel`.

Replace:
```csharp
public IReadOnlyList<NotificationItemDto> Notifications { get; init; }
```

With:
```csharp
public IReadOnlyList<AuthorNotification> Notifications { get; init; }
```

### Step 6.2 — Update `AuthorController.Dashboard` action

Find the `Dashboard` GET action in `AuthorController.cs`.

Replace the call to `GetRecentNotificationsAsync` with:

```csharp
var notifications = await _dashboardService.GetNotificationsAsync(user.Id, ct);
```

Add two new POST actions:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DismissNotification(Guid notificationId)
{
    await _dashboardService.DismissNotificationAsync(notificationId);
    return RedirectToAction(nameof(Dashboard));
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ClearAllNotifications()
{
    var user = await GetCurrentUserAsync();
    if (user is null) return Forbid();
    await _dashboardService.DismissAllNotificationsAsync(user.Id);
    return RedirectToAction(nameof(Dashboard));
}
```

### Step 6.3 — Update `Dashboard.cshtml`

**Card header** — add Clear All button alongside the notification count badge:

```html
<div class="card__header">
    <span class="card__title">Recent Activity</span>
    <div style="display:flex; align-items:center; gap:var(--space-2);">
        @if (Model.Notifications.Any())
        {
            <span class="badge badge--neutral" style="font-size:var(--text-xs);">
                @Model.Notifications.Count
            </span>
            <form asp-action="ClearAllNotifications" method="post" style="margin:0;">
                @Html.AntiForgeryToken()
                <button type="submit"
                        class="btn btn--ghost btn--sm"
                        title="Clear all notifications"
                        onclick="return confirm('Clear all notifications?')">
                    Clear all
                </button>
            </form>
        }
    </div>
</div>
```

**Each notification item** — update the loop to use `AuthorNotification`
properties directly (they match the existing `NotificationItemDto` property
names: `EventType`, `Title`, `Detail`, `LinkUrl`, `OccurredAt`).

Add a dismiss button to each item:

```html
<li class="notification-item">
    <span class="notification-item__icon notification-item__icon--@NotificationIconClass(n.EventType)"
          aria-hidden="true">
        @Html.Raw(NotificationSvgIcon(n.EventType))
    </span>
    <div class="notification-item__body">
        @if (n.LinkUrl is not null)
        {
            <a href="@n.LinkUrl" class="notification-item__title">@n.Title</a>
        }
        else
        {
            <span class="notification-item__title">@n.Title</span>
        }
        @if (!string.IsNullOrEmpty(n.Detail))
        {
            <p class="notification-item__detail">@n.Detail</p>
        }
        <time class="notification-item__time"
              datetime="@n.OccurredAt.ToString("o")"
              title="@n.OccurredAt.ToString("dd MMM yyyy HH:mm") UTC">
            @RelativeTime(n.OccurredAt)
        </time>
    </div>
    <form asp-action="DismissNotification" method="post"
          style="margin:0; align-self:flex-start;">
        @Html.AntiForgeryToken()
        <input type="hidden" name="notificationId" value="@n.Id" />
        <button type="submit"
                class="notification-item__dismiss"
                title="Dismiss"
                aria-label="Dismiss notification">
            &times;
        </button>
    </form>
</li>
```

Update the `@functions` block — change `NotificationItemDto` references to
`AuthorNotification`. The `NotificationEventType` enum is the same.

### Step 6.4 — CSS for dismiss button

In `DraftView.Web/wwwroot/css/DraftView.Notifications.css`, bump the CSS
version token via regex replace then add:

```css
.notification-item__dismiss {
    flex-shrink: 0;
    background: none;
    border: none;
    cursor: pointer;
    font-size: var(--text-base);
    line-height: 1;
    color: var(--color-ink-subtle);
    padding: 0 var(--space-1);
    opacity: 0.5;
    transition: opacity 0.15s ease;
}

.notification-item__dismiss:hover {
    opacity: 1;
    color: var(--color-danger);
}
```

Also apply the notification panel viewport fix while editing this file:

```css
.notifications-panel {
    position: sticky;
    top: var(--space-6, 1.5rem);
    max-height: calc(100vh - var(--space-6, 1.5rem) - 2rem);
    overflow: hidden;
    display: flex;
    flex-direction: column;
}

.notifications-list {
    list-style: none;
    margin: 0;
    padding: 0;
    flex: 1;
    min-height: 0;
    overflow-y: auto;
    overscroll-behavior: contain;
}
```

### Step 6.5 — Build and smoke test

```
dotnet build
dotnet test
```

Manual checks:
- Dashboard loads and shows notifications
- Dismiss (×) on a single notification removes it and reloads
- Clear All removes all notifications and reloads
- New comment from reader appears as notification on next dashboard load
- Sync completed appears as notification after a sync

---

## PHASE 7 — Cleanup and commit

```
dotnet test
git add -A
git commit -m "Persisted AuthorNotification entity, dismiss/clear all UI, viewport panel fix"
git push
.\publish-draftview.ps1
```

Update `TASKS.md`:
- Mark the Recent Activity truncation bug `[DONE]`
- Add the new notification feature to Sprint 2 `[DONE]` once verified in production
