# Contributing to Love+

Thanks for helping improve Love+. This repository is a privacy-first reference application, so correctness and privacy boundaries matter more than feature count.

## Before you start

1. Read `README.md`, `AGENTS.md`, `SECURITY.md`, and the relevant file under `docs/`.
2. Check existing issues before opening a duplicate.
3. For non-trivial changes, open an issue first so the approach can be discussed.

## Development setup

Backend:

```sh
cp .env.example .env
set -a && source .env && set +a
docker compose up -d
dotnet restore LovePlus.slnx
dotnet build LovePlus.slnx --no-restore
dotnet test LovePlus.slnx --no-restore
```

Mobile:

```sh
cd mobile
npm ci
npm run typecheck
npm run lint
npm test -- --runInBand
```

## Pull requests

Keep PRs focused. Explain the problem, the approach, privacy/security implications, and how the change was verified. Add or update tests whenever behavior changes.

Do not commit `.env` files, API keys, signing material, Firebase service accounts, tokens, real user data, precise location samples, or production infrastructure details.

## Architecture

- Dependencies point inward: API -> Infrastructure/Application -> Domain.
- Use CQRS handlers for application use cases.
- Derive user/pair/device identity from authenticated server state, never trusted request fields.
- Keep ephemeral realtime data ephemeral unless a documented product requirement says otherwise.
- Production configuration should fail closed instead of silently degrading.

## AI-assisted contributions

AI-assisted development is welcome. Contributors remain responsible for understanding, reviewing, testing, and documenting generated changes. `AGENTS.md` contains repository-specific guidance for coding agents.
