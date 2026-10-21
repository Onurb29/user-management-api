# Review and release checklist

## Review the dependent PRs in order

1. CRUD: `feat/06-09-crud-api` → `main` (PR #22).
2. Security: `feat/10-15-security-reliability` → CRUD branch (PR #23).
3. Verification/docs: `feat/14-18-lab-verification` → security branch.

Use merge commits for this stack to preserve shared ancestry. After merging each parent, retarget the next PR to `main` if GitHub does not do so automatically. If squash-merging instead, rebase the child onto the new main and rerun its checks before merging. Do not merge the isolated `test/ci-failure-proof` evidence branch.

## Before tagging

- [ ] Review and merge all implementation PRs, checking the current head's CI results each time.
- [ ] Supply and verify the real Microsoft Copilot prompt/suggestion/decision evidence for issue #17.
- [ ] Confirm the [API contract](API_CONTRACT.md), [security setup](SECURITY.md), [performance evidence](PERFORMANCE.md) and [test matrix](VERIFICATION.md) meet the course rubric.
- [ ] Verify a fresh clone using README setup; apply all migrations to a disposable database and demonstrate IT/HR requests.
- [ ] Verify the `Build and test` check manually before merging. On 2026-10-21 GitHub returned 403 for branch protection on this private repository, stating that GitHub Pro or a public repository is required. Repository visibility and billing were not changed. Configure enforcement later if the plan supports it.
- [ ] Verify CI is green on the merged main commit and complete issue #18's release checklist.

From a clean checkout of the reviewed `main`, after all items above are complete:

```bash
git switch main
git pull --ff-only origin main
dotnet restore UserManagementAPI.slnx
dotnet build UserManagementAPI.slnx --configuration Release --no-restore --warnaserror
dotnet test UserManagementAPI.slnx --configuration Release --no-build --no-restore
git tag -a v1.0.0 -m "User Management API lab v1.0.0"
git push origin v1.0.0
```

These commands are a release procedure, not evidence that a tag has been created. No release or deployment was performed during implementation.

## Demo sequence

1. Apply migrations and start HTTPS with the README commands.
2. Show 401 without a token and 403 with a Finance token.
3. Use an IT or HR token to create a user (201 + Location), list/get it (200), update (204), and delete (204).
4. Show invalid body (400), duplicate email (409), and missing record (404).
5. Show passing tests and the documented synthetic query comparison.
6. Show a metadata log entry and its matching X-Correlation-ID without exposing tokens or personal records.

## Scope limits

This is a local course lab. External identity-provider integration, deployment, UI, audit-history retention and optimistic concurrency are deferred. The API uses hard deletion and offset pagination; the documented contract describes their behavior.
