You are working in the DraftView repository.

Goal:
Register, investigate, and fix the production bug where submitting `/Author/InviteReader`
fails with the browser message "This page isn't working" instead of completing normally
or following the controlled application error path.

This prompt was prepared using these repository instruction documents:
- `Principles.md`
- `Refactoring.md`
- `versioning.instructions.md`
- `refactoring.instructions.md`

Apply those rules throughout the work:
- do not guess file contents
- keep changes minimal and architecture-safe
- TDD for Domain, Application, and Infrastructure changes
- no unrelated refactors
- if a refactor is needed, keep it as a separate commit from the bug fix
- respect layer boundaries: Web handles HTTP concerns, Application orchestrates,
  Infrastructure handles persistence and integrations

Branching Strategy (strictly follow):
- The Mac owns a long-running branch: `BugFix-Mac`
- All bug fixes branch from `BugFix-Mac`, not `main`
- This bug must be developed in its own branch from `BugFix-Mac`
- After completion, merge the bugfix branch back into `BugFix-Mac`
- `BugFix-Mac` will later be merged into `main` as a batch and that is not part of this task

Work in this exact order. Do not skip steps. Do not guess file contents. Inspect files before changing them.

------------------------------------------------------------
1. Ensure `BugFix-Mac` exists and is up to date
------------------------------------------------------------
- checkout `main`
- pull latest `main`
- create or update `BugFix-Mac` from `main`
- switch to `BugFix-Mac`

------------------------------------------------------------
2. Create a dedicated bugfix branch from `BugFix-Mac`
------------------------------------------------------------
Create this branch:

`bugfix/invite-reader-submit-production-crash`

This branch must contain only this bug fix.

------------------------------------------------------------
3. Update `TASKS.md`
------------------------------------------------------------
Ensure the existing open bug entry remains accurate and, if necessary, sharpen it with the
root-cause detail discovered during investigation.

Bug title:
`/Author/InviteReader` submit fails with browser "This page isn't working" on production

Type:
`Bug Fix`

Problem:
Submitting the author invitation form in production can crash instead of returning either:
- a successful redirect to `/Author/Readers`, or
- the controlled application error path for operational failures

Current observed fault seam from code inspection:
- `AuthorController.InviteReader` catches `DbUpdateException` and `InvariantViolationException`
- `UserService.IssueInvitationAsync` can also throw operational exceptions such as:
  - missing `App:BaseUrl`
  - email sending failures
  - other integration/configuration failures
- existing web test coverage currently expects some operational failures to bubble
- that behaviour is inconsistent with the project error-handling rule for system failures

Required outcome:
- invite submission must not produce an uncontrolled browser crash
- validation failures must still return the form with friendly validation feedback
- operational failures must be logged and routed through the controlled 500-style error path
- successful invite submission must still redirect to `/Author/Readers`
- no false-success response may be returned when invitation creation or email delivery fails

Acceptance criteria:
- valid invite submission redirects successfully
- invalid user input remains a validation error on the form
- operational invite failures do not surface as a raw browser crash
- operational failures are logged as system failures
- regression coverage proves the desired behaviour

------------------------------------------------------------
4. Inspect relevant files BEFORE changes
------------------------------------------------------------
Read these files before making any edits:
- `TASKS.md`
- `DraftView.Web/Controllers/AuthorController.cs`
- `DraftView.Application/Services/UserService.cs`
- `DraftView.Web/Models/AuthorViewModels.cs`
- `DraftView.Web/Views/Author/InviteReader.cshtml`
- `DraftView.Web.Tests/Controllers/AuthorControllerTests.cs`
- `DraftView.Web.Tests/InvitationProvisioningRegressionTests.cs`
- any current global exception handling setup in `DraftView.Web/Program.cs`
- any related configuration access used by invitation delivery

Do not assume the current implementation matches this prompt. Confirm it from the files.

------------------------------------------------------------
5. Reproduce and identify the real root cause
------------------------------------------------------------
Do not patch blind.

Before implementation:
- reproduce locally if possible
- inspect the invite POST flow end to end
- identify exactly which exception type and path causes the production crash behaviour
- determine whether the fault is:
  - configuration (`App:BaseUrl` or email settings)
  - email delivery failure handling
  - persistence failure handling
  - controller exception handling regression
  - middleware / global exception handling gap

Document the confirmed root cause in the final report.

------------------------------------------------------------
6. Implement the fix (architecture-first)
------------------------------------------------------------
Apply these rules:

Behaviour:
- preserve successful invite flow
- preserve validation-message behaviour for user-correctable input errors
- treat system/integration/configuration failures as operational failures
- route operational failures through the controlled application error path instead of an uncontrolled crash

Architecture:
- keep HTTP response decisions in the Web layer
- keep invitation orchestration in Application
- do not move filesystem, persistence, or integration code across layers as a side effect
- do not introduce unrelated refactors

Error handling:
- follow the project rule: system failures are 500-style operational failures
- log the failure with sufficient operational context and without exposing sensitive data
- do not convert an operational failure into a fake workflow success
- do not downgrade non-user-correctable failures into misleading validation messages unless the codebase already uses an explicitly approved pattern for that exact case

Refactoring discipline:
- if small extraction/refactoring is needed to make the fix clear, keep it separate from the behaviour change commit
- no whole-class refactor unless required and explicitly justified by the fault

------------------------------------------------------------
7. Tests (required before finalising)
------------------------------------------------------------
Follow TDD.

Required test coverage:
- successful invite submission still redirects to `/Author/Readers`
- invite validation failures still return the form with friendly feedback
- operational failure in invite issuance no longer results in an uncontrolled crash path
- if the fix depends on global exception handling, add or update the appropriate web-layer regression coverage

Read existing tests first and match established style.

Likely files to extend:
- `DraftView.Web.Tests/Controllers/AuthorControllerTests.cs`
- `DraftView.Web.Tests/InvitationProvisioningRegressionTests.cs`

If the root cause is better covered elsewhere, add tests in the correct layer instead.

------------------------------------------------------------
8. Validate
------------------------------------------------------------
- run restore, build, and tests
- confirm the relevant invite tests are green
- confirm the fix does not regress the existing invitation provisioning flow
- confirm no raw crash behaviour remains in the tested path

------------------------------------------------------------
9. Commit and merge into `BugFix-Mac`
------------------------------------------------------------
Commit in logical steps:
- `TASKS.md` update
- tests
- refactor commit if required
- implementation

Then:
- merge `bugfix/invite-reader-submit-production-crash` into `BugFix-Mac`
- remain on `BugFix-Mac`

------------------------------------------------------------
10. Final report
------------------------------------------------------------
Report:
- files changed
- confirmed root cause
- before vs after behaviour
- test results
- any follow-up risks

------------------------------------------------------------
Constraints
------------------------------------------------------------
- Do not modify behaviour outside this bug
- Do not invent production facts you have not verified
- Do not stop after `TASKS.md`
- Complete the workflow end-to-end once implementation begins
- Do not ask for confirmation mid-task