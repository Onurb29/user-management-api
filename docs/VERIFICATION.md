# Verification evidence

Recorded 2026-10-21. Scope: .NET 10 / EF Core SQLite course lab.

## Automated results

Local Release build: **0 warnings, 0 errors**. Full xUnit suite: **154 passed, 0 failed, 0 skipped**. Tests use synthetic records, temporary migrated SQLite databases, and randomly generated test JWT keys.

```bash
dotnet restore UserManagementAPI.slnx
dotnet build UserManagementAPI.slnx --configuration Release --no-restore --warnaserror
dotnet test UserManagementAPI.slnx --configuration Release --no-build --no-restore
```

| Area | Evidence |
| --- | --- |
| Persistence | `UserPersistenceTests`: migration creation, reopen persistence, uniqueness, timestamps, legacy upgrade |
| Service | `UserServiceTests`: CRUD, missing records, duplicate checks, stable/default/max paging, invalid offsets |
| Service boundary and race | `UserWriteBoundaryTests`: invalid input rejected without controller; competing inserts forced between duplicate precheck and save for both create and update; context remains reusable |
| HTTP | `UserEndpointTests`: all five routes, DTO shape, Location, 200/201/204/400/404/409, empty pages, malformed JSON/IDs, missing/invalid/oversized fields, unchanged DB after rejection |
| Authentication | `SecurityAndLoggingTests`: real JWTs; IT and HR CRUD; all routes reject missing/malformed/expired/future/wrong-key/tampered/wrong-issuer/wrong-audience/unsigned/no-expiry tokens and missing/wrong roles |
| Errors and privacy | `SecurityAndLoggingTests`: sanitized service and actual SQLite faults, recovery, 200/201/204/400/401/403/404/409/500 metadata, no synthetic sensitive values in logs |
| Documentation | `DocumentationTests`: OpenAPI lists routes, success/error codes, bearer requirement; not exposed outside Development |
| Query behavior | `ReadQueryTests`: projected columns, LIMIT/OFFSET, no tracking, composite index used without a temporary sort |

The race test is a deterministic competing-write interleaving, not a concurrent load benchmark. Paging measurements are [documented separately](PERFORMANCE.md).

## Fresh-copy onboarding smoke

Copied the API sources into a temporary project with a unique temporary UserSecretsId, generated an IT token using the real `dotnet user-jwts` tool, built the app, applied every migration to a new temporary database, and started the real Kestrel host. Verified missing token → 401, create → 201, retrieve → 200, delete → 204, and OpenAPI → 200. No tokens were printed in evidence; temporary secrets and database were removed.

Transport for this automated smoke was isolated loopback HTTP. The documented interactive workflow uses HTTPS and platform development-certificate trust; certificate installation was not changed on the user's machine. This is a fresh-source-copy check, not a claim that a separate Windows machine was tested.

## CI proof

- CRUD PR: [passing workflow](https://github.com/Onurb29/user-management-api/actions/runs/39681270018).
- Deliberately failing test: [failed workflow](https://github.com/Onurb29/user-management-api/actions/runs/39682363271), on isolated branch `test/ci-failure-proof`, commit `05dac573815631e6f62707c2c849a5eec382376e`. The job log confirms 151 tests passed and exactly the deliberate test failed, with process exit code 1. This branch is not part of any implementation PR and must not be merged.
- Security and final PR checks must be green at the head being reviewed; use the Checks tab for current status.

## Remaining human evidence

The user reports using Microsoft Copilot, but has not supplied the real prompts/suggestions yet. [COPILOT_USAGE.md](COPILOT_USAGE.md) distinguishes verified Codex work from that pending evidence. Review/merge and the final release tag are deliberately left to the user.
