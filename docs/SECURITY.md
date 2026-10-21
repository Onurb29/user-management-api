# Authentication, errors and logging

## Local demonstration

This lab uses framework JWT bearer validation with HS256 development tokens. Tokens authorize the caller; database user records are not login accounts. Do not deploy the lab signing arrangement as a production identity provider.

From `src/UserManagementAPI`:

```bash
dotnet dev-certs https --trust
dotnet user-jwts create --name lab-demo --role IT --audience https://localhost:7013 --valid-for 1h
dotnet run --launch-profile https
```

The SDK stores the signing key in your local user-secrets store and updates the non-secret Development issuer/audience settings. The project includes a UserSecretsId; no signing key is checked in. Copy the generated token only into your local HTTP client's authorization field. Repeat with `--role HR` to demonstrate the other authorized role, or `--role Finance` for 403. A token's role is unrelated to any managed user's `department`.

Use HTTPS at `https://localhost:7013`. On platforms without automatic certificate trust, trust the development certificate using your platform's supported procedure; do not disable certificate validation in production.

For shell testing, capture a token without putting its literal value in history:

```bash
LAB_TOKEN=$(dotnet user-jwts create --name lab-demo --role IT --audience https://localhost:7013 --valid-for 1h --output token)
curl -i https://localhost:7013/api/users -H "Authorization: Bearer $LAB_TOKEN"
unset LAB_TOKEN
```

Do not commit tokens or include them in screenshots/logs. `dotnet user-jwts clear` removes locally stored token records; already issued tokens remain valid until expiration or signing-key rotation. Use `dotnet user-jwts key --help` for deliberate key rotation.

Outside Development, user-secrets are not loaded automatically. Supply `Authentication:Schemes:Bearer` issuer, audience and signing-key values through a protected configuration provider, or integrate a real identity provider as a separate deployment task. Without valid key/issuer/audience configuration the API rejects bearer tokens. This release is a local course lab, not a hosted identity solution.

## Validation and outcomes

The framework verifies signature, issuer, audience and lifetime, requires signed tokens with an expiry, allows HS256, and uses a 30-second clock-skew tolerance. The `ManageUsers` policy requires IT **or** HR on the whole controller. Authentication runs before authorization and endpoint execution.

- 401: missing, malformed, expired, not-yet-valid, tampered, unsigned, wrong-key, wrong-issuer or wrong-audience token.
- 403: valid token without IT/HR.
- 400/404/409: expected input, missing-record or duplicate-email outcomes.
- 500: generic ProblemDetails, with no exception message or stack trace.

Cancellation is passed through service/database calls. Expected validation or duplicate failures are not logged as unexpected server errors.

## Structured logs

`RequestLoggingMiddleware` records one event per request through Serilog's compact JSON console sink. Fields: Method, Path (the matched route template), StatusCode, ElapsedMs, CorrelationId. Unknown routes use `/unmatched`. Failure events additionally identify ExceptionType, not the exception object/message.

Routing → request logger → exception handler → status-code pages → HTTPS → authentication → authorization → controller.

The logger wraps exception handling, so completed error responses report 500 rather than 200. The correlation response header is set just before headers are sent, surviving response clearing during exception handling. Ambient logging properties are stripped by an allowlist because ASP.NET scopes can otherwise add raw RequestPath. Only the two application middleware categories are emitted; framework SQL/token/request diagnostics are intentionally excluded from the normal lab log.

This sacrifices detailed stack traces for a conservative privacy boundary. Debug with synthetic data and a debugger when deeper investigation is required; do not enable sensitive-data logging on real records.

## Automated evidence

`SecurityAndLoggingTests` exercises every CRUD route with missing, malformed, expired, future, wrong-signature, tampered, unsigned, no-expiry, wrong-issuer, wrong-audience and unauthorized-role tokens. Real signing/validation runs with a new random test key per factory.

Tests also verify IT and HR CRUD, 200/201/204/400/401/403/404/409/500 log metadata, sensitive-value exclusion, correlation headers, service faults, real SQLite query faults and host recovery. Request tests use isolated migrated SQLite files and never use the developer database.
