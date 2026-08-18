---
description: Add an endpoint across the API and its frontend
argument-hint: "<what the endpoint should do>"
---

Endpoint: $ARGUMENTS

This spans two repos. Plan the whole thing before writing any of it, and say
which API it belongs to — consumer (`HousingHub.API`) or admin
(`HousingHub.Admin.API`).

**Backend, in this repo:**

1. Command or query record in `HousingHub.Application/<Domain>/`, implementing
   `IRequest<BaseResponse<T>>`, with a FluentValidation validator alongside.
   Handlers and validators are assembly-scanned — no registration.
2. Service method in `HousingHub.Service`. Business logic lives here, not in the
   handler.
3. Repository work through `IUnitOfWork`. Remember `SaveAsync()` is a no-op:
   writes land immediately and there is no rollback.
4. Controller action dispatching through MediatR. Return `BaseResponse<T>`;
   message strings come from `ResponseMessages`.
5. Tests in `src/HousingHub.Test/<Domain>/`. Mock at `IUnitOfWork`, never at the
   DynamoDB client. Note that test namespaces shadow entity names — you will
   need `using` aliases.

If the query reads a new access pattern, add the GSI to `TableDefinitions` in
`DynamoDbTableInitializer` rather than scanning. If the index should be sparse,
the backing attribute is a derived string property with a discarding setter —
`Property.PublishedStatus` is the pattern. Existing rows will not appear until
re-saved; say so explicitly.

**Frontend**, in `../Housing-Hub-FE` or `../Housing-Hub-Admin`:

6. Type in `src/types/` mirroring the DTO.
7. Function in the matching `src/services/xService.ts` — thin axios wrapper,
   returns `response.data`.
8. Query or mutation in `src/hooks/useX.ts`. Mutations invalidate the keys they
   affect; prefer invalidating over hand-updating the cache, because the server
   recomputes derived state.
9. Wire it into a component. Components never call a service directly.

**Then:** if this endpoint produces a claim about a user — verified, trusted, a
badge, a tier — it is not finished until it is rendered. That specific gap has
happened three times in this codebase.

Verify with `/verify` here and `npx tsc --noEmit` in the frontend.
