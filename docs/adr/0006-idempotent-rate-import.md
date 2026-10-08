# ADR-0006: An idempotent, partial-success rate import contract

- **Status:** Accepted
- **Date:** 2026-10-01

## Context
The separate **freight-data-pipeline** project (Python) crawls carrier rates on a schedule and pushes them here. Networks fail and jobs get retried, and some crawled rows will be bad: unknown ports, negative prices, duplicates.

## Decision
`POST /api/rates/import` works like this:
- **Idempotent upsert on a natural key:** `(origin, destination, carrier, container type, valid from)`.
  - The same row sent twice updates the existing record instead of duplicating it, so the pipeline can safely retry a whole batch.
  - A **unique index** on the key backs this up, so even two racing imports can't create duplicates.
- **Partial success.** Valid rows are saved; invalid rows come back with their index and a reason:
  ```json
  { "inserted": 0, "updated": 1, "rejected": [{ "row": 1, "reason": "Unknown origin port 'XXXXX'." }] }
  ```
  One bad row shouldn't block a thousand good ones. The pipeline logs the rejects to its data-quality report.
- **Bounded batches:** at most 1000 rows per request (validated up front).
- **Normalisation:** codes, currency and container type are upper-cased, so `thlch` / `usd` / `40hc` are accepted.
- **Auth:** an `X-Api-Key` header, separate from user JWTs ([ADR-0004](0004-jwt-and-role-policies.md)).

## Consequences
- ✅ The Python side stays simple: send the batch, retry on network errors, log the rejects.
- ✅ Covered by the integration test `Import_is_idempotent_and_reports_bad_rows`.
- ⚠️ Rows are checked one by one (an N+1 lookup). That's fine for hundreds of rows a day; for large volumes, switch to a staging table plus `MERGE`.
