# User Management API

> **Status: In development** | **Framework: ASP.NET Core / .NET 10** | **Target release: v1.0.0**

A course capstone built with a professional issue-driven software development workflow. The goal is to deliver a secure REST API that lets authorized **IT** and **HR** personnel manage user records.

## Project management

- **[Development roadmap](docs/ROADMAP.md)**: architecture, phases, test expectations, and Definition of Done.
- **[GitHub Issues](https://github.com/Onurb29/user-management-api/issues)**: 18 actionable tasks with acceptance criteria.
- **GitHub Project**: `User Management API` (Board/Roadmap). Use `scripts/bootstrap-github-project.sh` to associate issues, milestones, labels, and project fields using the GitHub CLI.

## API

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/users` | List users with pagination |
| GET | `/api/users/{id}` | Retrieve user by ID |
| POST | `/api/users` | Create and validate a user |
| PUT | `/api/users/{id}` | Update existing user |
| DELETE | `/api/users/{id}` | Delete user |

## Planned stack

ASP.NET Core Controllers, C#, EF Core, SQLite, JWT Bearer authentication, IT/HR authorization policies, Serilog, ProblemDetails, OpenAPI, xUnit integration tests, and GitHub Actions.

**Architecture:** HTTP request → ASP.NET Core pipeline → Controller → Service → EF Core → SQLite.

## Working agreement

- Take one GitHub Issue at a time.
- Create a small feature branch (e.g. `feat/06-get-users`).
- Implement and test the requirement; update documentation when relevant.
- Open a PR, review the change, and merge only after the acceptance criteria pass.
- Record **actual** Microsoft Copilot contributions for the lab.
- Never commit secrets, development JWT signing keys, bearer tokens, or personal user data.

All existing issues have been imported into the `User Management API` GitHub Project by linking the repository. No bootstrap script is required for imported issues.

## Database setup (EF Core + SQLite)

**Prerequisite:** .NET 10 SDK. The repository includes the EF Core CLI tool manifest.

From the repository root:

```bash
dotnet tool restore
dotnet restore UserManagementAPI.slnx
cd src/UserManagementAPI
dotnet ef database update
dotnet run --urls http://localhost:5144
```

The database connection string in `src/UserManagementAPI/appsettings.json` uses `Data Source=users.db`. Run the API and EF commands from `src/UserManagementAPI` so the local SQLite file is created there; the path is relative to the process working directory. Only development data belongs in this file.

### Migrations and schema changes

```bash
cd src/UserManagementAPI
dotnet ef migrations list
dotnet ef migrations add DescriptiveMigrationName
dotnet ef database update
```

Commit migration source files, **not** `users.db`, `*.db-shm`, or `*.db-wal`. The `.gitignore` excludes these. Do not commit secrets or real user data. The connection string here points to a local development file and does not contain credentials.

### Existing databases

The migration `AddUserNormalizationAndTimestamps` creates `NormalizedEmail`, `CreatedAtUtc`, and `UpdatedAtUtc` and backfills pre-existing rows before enforcing the unique normalized-email index. Historical creation dates were not tracked, so the migration uses the UTC migration time as an approximation. SQLite's SQL `UPPER` is ASCII-oriented; the lab's case-insensitive email normalization assumes ordinary ASCII email addresses. If legacy email records normalize to duplicates, the migration will reject the conflict rather than silently lose data.

**If you already applied an earlier copy of the second migration:** Editing its source does not rerun it on your database. For a disposable empty lab DB, recreate it with `dotnet ef database update` after deleting just the local SQLite file; if it contains data, keep it and reconcile legacy rows separately.

### Automated tests

From the repository root:

```bash
dotnet test UserManagementAPI.slnx
```

The SQLite tests create isolated temporary databases, apply real EF Core migrations, verify records survive reopening the database, check UTC audit timestamp behavior, and verify that a duplicate normalized email is rejected by SQLite itself. A GitHub Actions workflow also runs them on pushes and pull requests.

## Current implementation

Issues #3–#5 provide the .NET 10 API, EF Core SQLite persistence, and scoped `IUserService`/`UserService`. The CRUD slice (#6–#9) exposes all five operations through DTOs, with HTTP integration tests.

- `GET /api/users?page=1&pageSize=25` returns a JSON array, ordered by last name, first name, then ID. Page size is 1–100; an empty or past-end page is `[]` with 200. There is no total-count metadata; request the next page until fewer than `pageSize` records are returned.
- `GET /api/users/{id}` returns a user DTO or 404.
- `POST /api/users` returns 201, the DTO, and a Location header.
- `PUT /api/users/{id}` replaces editable fields and returns 204 or 404.
- `DELETE /api/users/{id}` returns 204 or 404.
- Invalid input, paging, malformed IDs and the empty GUID return 400. Duplicate normalized emails return 409.

See [API contract](docs/API_CONTRACT.md) for examples and limits. Authentication and centralized error/logging middleware are the next slice; this branch is not a secured deployment.

Tests exercise the actual HTTP pipeline with `WebApplicationFactory` and migrated temporary SQLite databases. They cannot write to the developer's `users.db`. Service tests also force an email collision between the duplicate precheck and save, verifying that the unique index still returns a useful business outcome.
