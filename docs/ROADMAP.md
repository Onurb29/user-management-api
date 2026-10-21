# User Management API - Development Roadmap

> **Status:** Implementation prepared for review; Copilot evidence and final release remain pending.
> **Target:** ASP.NET Core / .NET 10 | v1.0.0  
> **Working model:** GitHub Issues → feature branch → implementation + tests → pull request → review → merge  
> **Source of truth for execution:** [GitHub Issues](https://github.com/Onurb29/user-management-api/issues)

## Goal

Build a REST API for authorized **IT** and **HR** users to manage user records. The application must support CRUD, input validation, secure access, structured HTTP logging, consistent errors, persistent storage, efficient queries, and automated tests. The API manages user records; these are **not** the same thing as authentication identities.

## Planned technical design

| Concern | Decision |
| --- | --- |
| Runtime / web framework | .NET 10, ASP.NET Core Controllers |
| Business logic | `IUserService` + `UserService` via dependency injection |
| Database | EF Core + SQLite with migrations |
| API contracts | Separate request/response DTOs |
| Authentication | ASP.NET Core JWT Bearer middleware |
| Authorization | Policy allowing role `IT` **or** `HR` |
| Error handling | ASP.NET Core exception handler + ProblemDetails |
| Logging | Serilog + structured request logging |
| Tests | xUnit + WebApplicationFactory integration tests |
| CI | GitHub Actions |

**Avoid unnecessary abstractions:** No microservices, MediatR, CQRS, or generic repository wrapper for this lab without an actual requirement.

## API surface

| HTTP method | Path | Expected successful status |
| --- | --- | --- |
| GET | `/api/users` | 200 (paginated) |
| GET | `/api/users/{id}` | 200 |
| POST | `/api/users` | 201 + Location |
| PUT | `/api/users/{id}` | 204 |
| DELETE | `/api/users/{id}` | 204 |

Common errors: 400 malformed/invalid input, 401 missing or invalid token, 403 authenticated but unauthorized, 404 missing record, 409 duplicate email, and sanitized 500 for unexpected server failure. Specify precise contract in `docs/API_CONTRACT.md` as part of issue #1.

## Review status

Issues #1–#5 are closed in GitHub. Remaining implementation is grouped into dependent CRUD, security/reliability, and verification/documentation PRs. Checkboxes below remain open until their changes are reviewed and merged; they do not imply the code is missing. See [verification evidence](VERIFICATION.md). Issue #17 awaits the user's actual Copilot details; issue #18 awaits final review and tagging.

## Milestones and issues

### M0 - Requirements & Architecture

**Outcome:** Define scope, contracts and workflow.

- [x] [#1 - Define user entity and API contract](https://github.com/Onurb29/user-management-api/issues/1)
- [x] [#2 - Establish project development workflow](https://github.com/Onurb29/user-management-api/issues/2)

**Exit criteria:** Planning complete; endpoint rules and development workflow are clear.

### M1 - Project Foundation & Persistence

**Outcome:** Build an executable, persisted service boundary.

- [x] [#3 - Scaffold ASP.NET Core Web API project](https://github.com/Onurb29/user-management-api/issues/3)
- [x] [#4 - Implement EF Core and SQLite persistence](https://github.com/Onurb29/user-management-api/issues/4)
- [x] [#5 - Implement user service with dependency injection](https://github.com/Onurb29/user-management-api/issues/5)

**Exit criteria:** Application builds; SQLite migrations run; user service supports CRUD.

### M2 - CRUD API & Validation

**Outcome:** Expose correct, validated HTTP behavior.

- [ ] [#6 - Implement GET user endpoints](https://github.com/Onurb29/user-management-api/issues/6)
- [ ] [#7 - Implement POST user endpoint and validation](https://github.com/Onurb29/user-management-api/issues/7)
- [ ] [#8 - Implement PUT user endpoint](https://github.com/Onurb29/user-management-api/issues/8)
- [ ] [#9 - Implement DELETE user endpoint](https://github.com/Onurb29/user-management-api/issues/9)

**Exit criteria:** All five operations return documented success and error responses; tests pass.

### M3 - Security, Middleware & Error Handling

**Outcome:** Apply secure and observable request processing.

- [ ] [#10 - Configure centralized exception handling and ProblemDetails](https://github.com/Onurb29/user-management-api/issues/10)
- [ ] [#11 - Implement JWT authentication and IT/HR authorization](https://github.com/Onurb29/user-management-api/issues/11)
- [ ] [#12 - Implement structured request/response logging](https://github.com/Onurb29/user-management-api/issues/12)

**Exit criteria:** Correct 401/403/500 behavior; consistent safe JSON errors; accurate logs.

### M4 - Quality Assurance & Performance

**Outcome:** Demonstrate reliability and efficient retrieval.

- [ ] [#13 - Build automated API integration test suite](https://github.com/Onurb29/user-management-api/issues/13)
- [ ] [#14 - Measure and optimize GET endpoint performance](https://github.com/Onurb29/user-management-api/issues/14)
- [ ] [#15 - Run negative, failure, and security test matrix](https://github.com/Onurb29/user-management-api/issues/15)

**Exit criteria:** Tests pass against isolated DB; security and performance evidence is recorded.

### M5 - Documentation, CI & Release

**Outcome:** Deliver a reproducible v1.0.0.

- [ ] [#16 - Complete API documentation and onboarding README](https://github.com/Onurb29/user-management-api/issues/16)
- [ ] [#17 - Document actual Microsoft Copilot contributions](https://github.com/Onurb29/user-management-api/issues/17)
- [ ] [#18 - Add GitHub Actions CI and prepare v1.0.0 release](https://github.com/Onurb29/user-management-api/issues/18)

**Exit criteria:** Cold clone can build/run/test; CI passes; release acceptance is complete.

## Middleware architecture

```text
HTTP request
  → Global exception handling
  → Request logging
  → HTTPS/routing
  → Authentication (validate JWT)
  → Authorization (IT/HR policy)
  → Controller
  → Service
  → EF Core / SQLite
```

**Important:** A request logger inside an outer exception handler might see an exception before a final HTTP 500 is assigned. Design and test exception paths so logs show the correct outcome. Do not implement your own JWT validation cryptography just because the lab uses the phrase "authentication middleware". Prefer the framework's proven components.

## Test matrix (minimum)

| Scenario | Expected |
| --- | --- |
| Existing / missing user | 200 / 404 |
| Create valid / invalid / duplicate | 201 / 400 / 409 |
| Update valid / missing | 204 / 404 |
| Delete valid / missing | 204 / 404 |
| Missing / bad / expired JWT | 401 |
| Other authenticated role | 403 |
| IT or HR | Authorized |
| Unexpected service/database error | 500, sanitized ProblemDetails, useful log |
| Large list | Bounded, stable pagination; measured query performance |

## GitHub workflow

**Project board:** Backlog → Ready → In Progress → In Review → Done.

**Branches:** `feat/06-get-users`, `feat/11-jwt-authentication`, `test/13-api-integration-tests`.

**Commits:** `feat(users): add paginated GET endpoint`, `test(api): cover expired JWT`.

**Definition of Done:**

- [ ] Acceptance criteria met.
- [ ] Relevant automated tests pass.
- [ ] Validation and failures tested.
- [ ] No secrets, JWTs, or sensitive user values in code/logs.
- [ ] Documentation updated where required.
- [ ] PR reviewed and merged.

## Version checkpoints

| Version | Content |
| --- | --- |
| v0.1.0 | Foundation + persistence |
| v0.2.0 | CRUD + validation |
| v0.3.0 | JWT + authorization + logging + error handling |
| v1.0.0 | Verified tests + docs + passing CI |

Milestones are *progress gates*, not fixed delivery dates. The GitHub Project Roadmap view only needs dates if dates genuinely help your course plan.

## Deferred ideas (not v1.0)

Entra ID, differentiated HR/IT permissions, immutable audit trails, concurrency control, SQL Server, Docker, Azure deployment, and a Blazor frontend.

## Microsoft Copilot evidence

The lab requests a record of how Microsoft Copilot helped. Capture **actual** prompts, suggestions, decisions, and verification in `docs/COPILOT_USAGE.md` while implementing the issues. Do not invent Copilot usage or confuse it with ChatGPT assistance.
