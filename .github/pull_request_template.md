## What changed?

Describe the problem and the solution.

## Why?

Explain the user, developer, security, or maintenance impact.

## Verification

- [ ] `dotnet build LovePlus.slnx --no-restore`
- [ ] `dotnet test LovePlus.slnx --no-restore`
- [ ] `cd mobile && npm run typecheck`
- [ ] `cd mobile && npm run lint`
- [ ] `cd mobile && npm test -- --runInBand`
- [ ] Relevant docs updated
- [ ] No secrets, personal data, production endpoints, or signing material added

## Security/privacy impact

Call out any change touching auth, pairing, sessions, realtime delivery, notifications, location, diagnostics, or data retention. Write `None` if not applicable.
