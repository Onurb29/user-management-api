# User Management API - API Contract (Draft)

> **Status: Proposed.** Finalize under [issue #1](https://github.com/Onurb29/user-management-api/issues/1) before implementation.

## Use cases

An authorized IT or HR application user can list, retrieve, create, update, and delete **managed user records**. An API-managed record is **not** automatically a login account.

## Proposed managed user fields

| Field | C# type | Notes |
| --- | --- | --- |
| `Id` | `Guid` | Generated server-side |
| `FullName` | `string` | Required, not whitespace |
| `Email` | `string` | Required, format validation, unique after normalization |
| `Department` | `string` | Required |
| `CreatedAtUtc` | `DateTime` | Server-generated UTC |
| `UpdatedAtUtc` | `DateTime` | Server-updated UTC |

## Proposed endpoints

| Method | Route | Success | Expected error cases |
| --- | --- | --- | --- |
| GET | `/api/users?page=1&pageSize=25` | 200 | 400 for invalid paging |
| GET | `/api/users/{id}` | 200 | 404 missing; 400 malformed ID (to confirm) |
| POST | `/api/users` | 201 with Location | 400 invalid; 409 duplicate |
| PUT | `/api/users/{id}` | 204 | 400 invalid; 404 missing; 409 duplicate |
| DELETE | `/api/users/{id}` | 204 | 404 missing |

## Authorization

| Caller | GET | POST | PUT | DELETE |
| --- | --- | --- | --- | --- |
| Role `IT` | Allow | Allow | Allow | Allow |
| Role `HR` | Allow | Allow | Allow | Allow |
| Other authenticated role | 403 | 403 | 403 | 403 |
| Missing or invalid JWT | 401 | 401 | 401 | 401 |

Use ASP.NET Core JWT Bearer authentication and an authorization policy for roles. Production identity-provider integration is outside v1.

## Error contract

Prefer ASP.NET Core `ProblemDetails` / validation problem responses. Client responses must not include server stack traces. Expected failures map to 400/401/403/404/409; unexpected failures to 500.

## Decisions to confirm

- [ ] Final user field names, data types, lengths and nullability.
- [ ] Email uniqueness/normalization semantics.
- [ ] Paging defaults, max page size and response envelope.
- [ ] PUT full replacement vs partial update; this design uses full-resource PUT.
- [ ] Error schema and invalid ID handling.
- [ ] Whether hard deletion is acceptable for the lab.
- [ ] Who issues local development JWTs and how test signing keys are stored safely.

**Final approval:** Completed when [issue #1](https://github.com/Onurb29/user-management-api/issues/1) is closed.
