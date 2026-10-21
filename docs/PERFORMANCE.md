# GET query performance evidence

## Reproduce

From the repository root:

```bash
dotnet run --project tools/UserManagementAPI.Benchmarks --configuration Release
```

The tool creates and migrates temporary SQLite files, seeds synthetic 1,000- and 10,000-user sets, and deletes them afterward. It never opens `users.db`. The benchmark is included in the solution build, but timing measurements are deliberately not CI pass/fail thresholds.

## Method and results

Captured 2026-10-21T21:52:55.276719+00:00 on macOS 27.0.1 Arm64 / .NET 10.0.12. Two warmup queries, then 15 measured samples per case; medians reported. The baseline mirrors the original unbounded entity listing with no tracking and last/first-name ordering. The paged cases call the actual service and materialize 25 response DTOs. Tests additionally verify the maximum page size of 100.

| Database rows | Query | Returned rows | Median ms | Median allocated bytes |
| ---: | --- | ---: | ---: | ---: |
| 1,000 | Unbounded entity list, no sort index | 1,000 | 4.428 | 920,088 |
| 1,000 | 25 DTOs, no sort index | 25 | 0.841 | 39,288 |
| 1,000 | 25 DTOs, sort index | 25 | 0.650 | 39,288 |
| 10,000 | Unbounded entity list, no sort index | 10,000 | 37.829 | 9,157,944 |
| 10,000 | 25 DTOs, no sort index | 25 | 1.631 | 38,424 |
| 10,000 | 25 DTOs, sort index | 25 | 0.596 | 38,424 |

Raw results: [query-benchmark.json](evidence/query-benchmark.json).

The largest improvement comes from fetching only the requested DTO page. An additional composite index on `(LastName, FirstName, Id)` matches the full stable sort. SQLite EXPLAIN changed from `SCAN Users` plus `USE TEMP B-TREE FOR ORDER BY` to `SCAN Users USING INDEX IX_Users_LastName_FirstName_Id`. The new additive migration creates that index without changing existing fields or rows.

The existing primary-key index handles ID retrieval; the unique NormalizedEmail index handles duplicate checks. No speculative department/filter indexes were added. The sort index adds storage and write maintenance in exchange for avoiding repeated sorting on list requests.

## Limits

These are local query/materialization measurements, not network latency, requests-per-second, concurrent-load, or production SLA claims. Allocations are process-wide deltas from `GC.GetTotalAllocatedBytes`; background/runtime work can add noise. Run order and filesystem/SQLite caches also affect timing. The benchmark compares different payload sizes intentionally to show the cost of the previous unbounded contract.

Offset paging still scans skipped index entries for deep pages, and concurrent writes can shift page contents. Keyset paging is a possible future requirement, not part of this contract. `ReadQueryTests` verifies the generated query uses LIMIT/OFFSET, selects only DTO columns, avoids entity tracking, and uses the migrated sort index.
