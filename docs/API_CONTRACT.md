# User Management API contract

## Managed records

A managed record is not an authentication account. The response DTO contains `id` (GUID), `firstName`, `lastName`, `email`, and `department`. Internal normalized email and audit timestamps are not exposed.

Create and full-replacement update bodies contain four required fields:

```json
{
  "firstName": "Alice",
  "lastName": "Martin",
  "email": "alice@example.com",
  "department": "IT"
}
```

Names and department must be non-whitespace and at most 100 characters. Email must be a valid ASCII email address, at most 254 characters after trimming, with no embedded spaces. Editable text is trimmed. Department is a free-text organizational field, not the caller's authorization role. Server-generated ID and audit timestamps cannot be changed through these DTOs.

Email uniqueness uses the existing `Trim().ToUpperInvariant()` convention and SQLite unique index. A failed create/update does not overwrite an existing record.

## Routes

| Method | Route | Success | Expected errors |
| --- | --- | --- | --- |
| GET | `/api/users?page=1&pageSize=25` | 200, DTO array | 400 invalid paging |
| GET | `/api/users/{id}` | 200, DTO | 400 invalid ID; 404 missing |
| POST | `/api/users` | 201, DTO and Location | 400 invalid input; 409 duplicate email |
| PUT | `/api/users/{id}` | 204, empty body | 400 invalid input/ID; 404 missing; 409 duplicate email |
| DELETE | `/api/users/{id}` | 204, empty body | 400 invalid ID; 404 missing |

IDs must be non-empty GUIDs. Malformed IDs and the all-zero GUID consistently return 400. Unknown valid IDs return 404. PUT replaces all four editable fields; it is not a partial update. Deletion is permanent in this lab.

## Paging contract

Defaults: `page=1`, `pageSize=25`. Page is at least 1; page size is between 1 and 100. Non-integers, integer overflow, and offsets above `int.MaxValue` return 400. Results are ordered by `LastName`, `FirstName`, then unique `Id`, using SQLite's default text collation. Ordering is deterministic for unchanged data.

The response is an array, with no count envelope. Empty databases and pages beyond the last record return 200 and `[]`. Continue requesting pages until fewer than `pageSize` records are returned. Offset paging is not a snapshot: concurrent inserts, deletes or name changes can shift records between requests.

```http
GET /api/users?page=2&pageSize=10
Accept: application/json
```

## Errors and security roadmap

Expected controller errors use `application/problem+json`; validation responses additionally contain an `errors` object. Clients should inspect `status` and validation keys, not depend on exact English messages.

JWT authentication, the IT-or-HR policy, sanitized unexpected 500 responses, and structured request logging are implemented in the next security slice. No authentication endpoint or managed-user login is part of this API.
