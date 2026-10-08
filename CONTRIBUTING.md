# Contributing to Love+

Thanks for your interest in contributing to Love+.

Love+ is a privacy-first reference application for secure two-person mobile experiences. Contributions should preserve privacy boundaries, clear architecture, testability, and a small operational footprint.

## Before you start

1. Read `README.md`, `AGENTS.md`, and the relevant document under `docs/`.
2. Search existing issues before opening a new one.
3. Keep changes focused. Large architectural changes should start with an issue or discussion before implementation.
4. Never include real credentials, personal data, production endpoints, signing material, or private test data.

## Local verification

Backend:

```sh
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

Android verification, when relevant:

```sh
JAVA_HOME=$(/usr/libexec/java_home -v 17) ANDROID_HOME="$ANDROID_HOME" \
  ./android/gradlew -p android testDebugUnitTest :app:assembleDebug
```

## Pull requests

A pull request should:

- explain the problem and the chosen solution;
- include tests for behavior or security-boundary changes;
- update documentation when configuration, runtime behavior, or architecture changes;
- avoid unrelated formatting or refactoring;
- keep secrets and personal data out of commits and logs;
- pass the repository CI checks.

## Architecture expectations

- Dependencies point inward: API -> Infrastructure/Application -> Domain.
- Use CQRS handlers for application use cases.
- Derive user, pair, and device identity from authenticated state rather than trusting request bodies.
- Keep precise coordinates and personal payloads out of logs.
- Treat Redis presence/status data as ephemeral.
- Preserve fail-closed production configuration.

## AI-assisted contributions

AI tools, including Codex and Claude, may be used to understand the codebase, draft changes, tests, documentation, and reviews. Contributors remain responsible for verifying generated code, licenses, tests, security boundaries, and behavior before submission.

If an agent is used, point it to `AGENTS.md` first and ask it to run the relevant validation commands before considering a task complete.

## Reporting security issues

Do not open a public issue for a vulnerability involving authentication, authorization, secrets, precise location, pair isolation, session handling, or other sensitive data. Follow `SECURITY.md` instead.
