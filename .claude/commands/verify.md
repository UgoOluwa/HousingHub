---
description: Build, test and review the working diff before claiming a change works
---

Verify the current changes properly. Do not skip a step and do not report
success on a step you did not run.

1. `dotnet build HousingHub.sln --configuration Release`
2. `dotnet test src/HousingHub.Test/HousingHub.Test.csproj --configuration Release`
3. `git diff` — read the whole thing, not a summary

Then check the diff against the failure modes this codebase actually produces:

- **Computed and rendered nowhere.** If the change produces a claim about a user
  — verified, trusted, a badge, a tier — trace it to a rendered pixel in
  `../Housing-Hub-FE` or `../Housing-Hub-Admin`. This has happened three times.
- **`IsBusinessVerified` vs the raw tier.** The tier survives expiry; only the
  computed property checks the date.
- **Sparse index attributes.** If a new query reads a GSI, confirm the backing
  attribute is written in every state that should appear in it — and that
  existing rows already carry it, or say so.
- **A new worker endpoint needs a trigger** in `.github/workflows/scheduled-workers.yml`
  in the same change. An endpoint nothing calls is not a feature.
- **`UnitOfWork.SaveAsync()` is a no-op.** No transaction, no rollback. Check
  that a partial failure leaves a recoverable state.
- **Both APIs.** A change below the API layer affects the consumer API *and* the
  admin API. Check both.
- **Secrets.** Nothing real in `appsettings*.json`.

Report what passed, what failed, and anything the diff does that its commit
message would not lead a reviewer to expect. If a step fails, say so plainly
rather than working around it.
