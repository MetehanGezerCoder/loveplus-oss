# Love+ — Claude/Codex handoff

This repository is prepared for AI-assisted development with Claude, Codex, and similar coding agents.

Start with:

1. `README.md` for project purpose and setup.
2. `AGENTS.md` for architecture, security rules, commands, and definition of done.
3. `CONTRIBUTING.md` for contribution expectations.
4. The relevant document under `docs/` for the feature you are changing.

## Project intent

Love+ is an open-source, privacy-first reference application for secure two-person mobile experiences. It is not intended to be a general social network. Privacy, pair isolation, honest delivery state, secure authentication, and real-device behavior take priority over feature count.

## Current stack

- .NET 10 modular monolith
- Clean Architecture boundaries
- CQRS / MediatR / FluentValidation
- PostgreSQL + PostGIS
- Redis
- SignalR
- React Native + TypeScript
- Native Android Kotlin service/widget integration
- Docker-based deployment path

## Agent expectations

- Never introduce real credentials, private endpoints, signing keys, personal e-mail addresses, real location data, or other private user data.
- Derive authenticated identity from claims; never trust client-supplied user/pair/device identifiers where authenticated state is authoritative.
- Preserve pair isolation in HTTP, persistence, and realtime paths.
- Keep precise coordinates, tokens, passwords, private payloads, and provider credentials out of logs.
- Preserve fail-closed production configuration.
- Update tests and documentation when behavior or architecture changes.
- Run the relevant validation commands from `AGENTS.md` before declaring work complete.

## Current release line

The project is in active pre-1.0 development. The current documented milestone is v0.5.0.

Historical machine-specific notes, private deployment topology, private domains, device serials, and personal demo identities should not be added to this file. Put reusable technical knowledge in `docs/` and track project work through GitHub issues and pull requests.
