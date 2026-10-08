# Security Policy

Love+ handles authentication, device state, relationship pairing, and optionally precise location. Security and privacy issues should be treated as high priority.

## Supported versions

Security fixes target the latest code on `main`. Until the project reaches a stable 1.0 release, older versions may not receive backported fixes.

## Reporting a vulnerability

Please do not open a public GitHub issue for vulnerabilities involving authentication, authorization, session handling, pairing isolation, refresh tokens, secrets, location privacy, notification privacy, or data exposure.

Instead, use GitHub's private vulnerability reporting feature when it is enabled for the repository. If private reporting is unavailable, contact the maintainer privately through the contact method listed on the maintainer's GitHub profile.

Include:

- the affected component and version/commit;
- reproduction steps or a minimal proof of concept;
- expected and observed behavior;
- the security or privacy impact;
- any suggested mitigation, if available.

Do not include real user credentials, real location data, production secrets, signing keys, or other personal data in the report.

## Security principles

Contributions must preserve these boundaries:

- never trust user, pair, or device identifiers supplied by a request when they can be derived from authenticated state;
- enforce pair membership for every partner-data query and realtime group join;
- never log passwords, bearer/refresh tokens, precise coordinates, private message content, or provider credentials;
- store refresh and pairing tokens only in protected/hashed form where applicable;
- keep signing keys, `.env.production`, Firebase service accounts, and other credentials outside the repository;
- production configuration must fail closed rather than silently enabling insecure fallbacks;
- disabling location or mood sharing must stop exposing the previous value to the partner;
- release builds must use HTTPS endpoints and must not target localhost or emulator loopback addresses.

## Secret hygiene

Before publishing a fork, release, log, screenshot, or diagnostic output, verify that it contains no API keys, JWT signing keys, refresh tokens, database credentials, Firebase service-account JSON, release keystores, or private endpoints.
