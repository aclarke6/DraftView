# Sprint 4 — Email Privacy and Controlled Access

All phases complete. Merged to main 2026-04-18.

## Goal
Protect user email addresses throughout the system. Align with UK GDPR data minimisation principles.

## Email Handling Model
- Email stored encrypted (`EmailCiphertext`) with deterministic HMAC lookup (`EmailLookupHmac`)
- Encryption and HMAC in infrastructure layer — domain owns no crypto mechanics
- Plaintext email never stored in database
- Email only visible in `Views/Account/Settings.cshtml` (whitelist)
- Controlled access for SystemSupport via explicit privileged access policy

## Phases — All Complete
- [x] Phase 1 — Governing red tests, whitelist definition
- [x] Phase 2 — Web surface cleanup (remove email from non-whitelisted views)
- [x] Phase 3 — Infrastructure: `EmailCiphertext`, `EmailLookupHmac`, unique index, migration
- [x] Phase 4 — Application layer: `IUserEmailAccessService`, `IControlledUserEmailService`, deny-by-default rules
- [x] Phase 5 — Domain refinement: `User` invariants around protected email state
- [x] Phase 6 — End-to-end integration: DB-backed login, invite provisioning, no-plaintext assertion
- [x] Phase 7 — Audit and security hardening: audit logging, plaintext-log prevention, access control
- [x] Phase 8 — Production key management: `appsettings.Production.json`, key persistence across deploys

## Key Architecture Decisions
- `EmailProtection:EncryptionKey` and `EmailProtection:LookupHmacKey` in user-secrets (dev) and `appsettings.Production.json` (prod)
- Password reset tokens now bind to `UserId` — not plaintext email
- Duplicate invitations supersede older pending invites (no resend/cancel UI needed)
- `ProtectedEmailPersistenceContractTests` retained as permanent regression coverage

## Production Key Recovery
If keys are lost, existing encrypted emails are unrecoverable. Users must reset email via support.
Key rotation requires: generate new keys → re-encrypt all `EmailCiphertext` rows → update `appsettings.Production.json` → restart service.
