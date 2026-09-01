# HousingHub — backend (consumer API + admin API)

Nigerian proptech platform. This repo holds **both** .NET APIs; the two frontends
are separate repos (see [Sibling repos](#sibling-repos)).

**Read [`AGENTS.md`](AGENTS.md) first** — it covers architecture, CQRS, response
conventions, controllers and DI registration, and is still accurate on all of
that. This file covers what it does not: the parts added since, the corrections,
and the traps that have actually cost time.

---

## Corrections to AGENTS.md

It predates a lot of work. Where the two disagree, this file wins.

| AGENTS.md says | Actually |
|---|---|
| SendGrid for email | **Resend** (`ResendEmailService`). The SendGrid test file is a leftover name. |
| One API | **Two** — `HousingHub.API` (consumer) and `HousingHub.Admin.API`, separate JWT secrets, separate Lambdas, separate Sentry projects |
| Tables auto-created on startup | Also **reconciles missing GSIs** on existing tables, one per table per pass |
| SignalR hubs at `/hubs/*` | **Not mapped under Lambda** — API Gateway REST cannot hold a WebSocket. `/hubs/chat/negotiate` returning 404 in a deployed environment is correct. Frontends fall back to polling. |

---

## Two APIs, one solution

| | `HousingHub.API` | `HousingHub.Admin.API` |
|---|---|---|
| Audience | Customers | Internal staff |
| JWT config | `Jwt:*` | `AdminJwt:*` — **different secret** |
| Lambda | `HousingHub-API` | `HousingHub-Admin-API` |
| Stage path | `/dev` | `/admin` |
| Auth default | Anonymous unless `[Authorize]` | **Fallback policy requires `role=Admin`** — opt out with `[AllowAnonymous]` |
| SignalR | Real hubs when run locally | `NoOpRealtimeNotifiers` always |

They share everything below the API layer: Application, Service, Repository,
Data, Model, Core. A change in Service affects both — check both.

---

## Things that have actually gone wrong

Each of these was a real bug. They are the failure modes this codebase produces.

**Computed and rendered nowhere.** Three separate times a field was calculated
correctly on the server and displayed to no one: `PropertyDto.IsVerified`,
`Customer.IsBusinessVerified`, `Property.TitleVerificationTier`. When adding a
field that represents a claim about a user, trace it to a rendered pixel or it
is not done.

**Declared and never populated — the inverse of the above.** `AdminPropertyListDto.ThumbnailUrl`
and `VerificationCaseDto.SubjectLabel` were both read by clients and set by
nothing. One made every admin thumbnail fall through to `files[0]`, which is a
video as often as not; the other left every row of a submitter's verification list
reading "Property verification" with no way to tell which property. Both are found
the same way as the bug above — trace the field end to end rather than reading
either half.

**Read `IsBusinessVerified`, never `BusinessVerificationTier`.** The tier
survives expiry until a sweep revokes it. The computed property checks the date.
Same shape for title verification.

**Sparse indexes depend on a derived attribute existing.** `PublishedStatus`,
`ReviewQueueStatus`, `ExpiryWatch` are string properties present only in certain
states, with discarding setters. A row written before the attribute existed is
absent from the index forever until re-saved — which is why
`Dynamo:UsePublishedIndex` is still `false` in dev (see
`docs/data-backfill-required.md`).

**An endpoint nothing calls is not a feature.** The inspection reminder worker
existed for months and had never once run. `.github/workflows/scheduled-workers.yml`
now triggers both workers. If you add a worker endpoint, add its trigger in the
same change.

**Config-driven values that are secretly constants.** `UsePathBase("/dev")` and
`[DynamoDBTable("Customers")]` both looked configurable and were not. See
`docs/environment-separation-plan.md`.

**`UnitOfWork.SaveAsync()` is a no-op.** Writes land immediately, per repository
call. There is no transaction and no rollback. Order operations so a partial
failure leaves a recoverable state.

---

## Verification pipeline (Phase 2, complete)

Business and property-title verification. Full walkthrough:
`docs/business-verification-walkthrough.md`.

- `VerificationCase` is a state machine — `TrySubmit`, `TryBeginReview`,
  `TryDecide`, `TryExpire`. Do not set `Status` directly.
- Documents attach only while `Draft`. After submit the set is frozen, because
  the reviewer must decide on what they read.
- Files go to a **private S3 prefix**; only the object key is stored, never a
  URL. Reviewers get a 10-minute presigned URL.
- `NameMatcher.Compare()` reports `Exact/Partial/None/Unknown` and **never
  decides** — Nigerian names legitimately vary between documents.
- Approval writes the tier, `CacNumber`, `LasreraPermitNumber` and an expiry
  equal to the **earliest** approved document's expiry. Written only there; no
  user-facing path touches those fields.
- `Verification:ShowTitleBadge` is `false` pending legal sign-off on the wording.

## Scheduled workers

`POST /api/Internal/{worker}/run` on the **admin** API, gated by an
`X-Worker-Secret` header, rate limited to 10 requests / 5 min per IP.
Triggered by GitHub Actions — see `docs/scheduled-workers.md`.

- A **401** means the request reached the controller and the secret did not
  match. Check CloudWatch for `Internal worker call rejected: {Reason}`, which
  names the mismatch shape without printing either value.
- A **403** usually means the URL is missing the stage path.
- `ExpiryReminderThresholds.DaysBefore` is `[7, 30]` — **ascending**. The sweep
  takes the first threshold crossed, so the tightest must come first.

---

## Environments

Currently **one** environment doing duty as both. The split is planned in
`docs/environment-separation-plan.md`; Phase 0 (making the code
environment-aware) is committed, Phases 1–6 are outstanding. See
`docs/handoff.md` for exactly where things stand.

Two settings that must not be changed casually:

- `Dynamo:TablePrefix` — **empty in dev and must stay empty.** The existing
  tables are unprefixed; setting a prefix orphans every row.
- `Api:PathBase` — the API Gateway stage name. `/dev` and `/admin` today.

Secrets are never committed. `RequiredSecrets.Validate` refuses to boot on a
missing or placeholder value. As Lambda environment variables the keys use
double underscores: `Dynamo__TablePrefix`, `Internal__WorkerSecret`.

---

## Build, test, run

```bash
dotnet build HousingHub.sln
dotnet test src/HousingHub.Test/HousingHub.Test.csproj   # ~600 test attributes
dotnet run --project src/HousingHub.API
dotnet run --project src/HousingHub.Admin.API
```

Tests mock at `IUnitOfWork`, never at the DynamoDB client, and use real Mapster
configuration. Test files sit in namespaces that shadow entity names
(`Property`, `Admin`, `PropertyFile`, `CustomerAddress`) — you will need `using`
aliases when referencing entities from tests.

**Always run the build and the tests before claiming a change works.** Several
past changes shipped broken because they were reasoned about rather than
compiled.

---

## Conventions worth repeating

- Return `BaseResponse<T>` / `BaseResponsePagination<T>`; message strings come
  from `ResponseMessages`, never inline.
- Controllers dispatch through MediatR. No direct service calls.
- New services go in the relevant `ConfigureServices.cs`. MediatR handlers and
  validators are assembly-scanned — no registration needed.
- Comments explain **why**, not what. The existing code does this consistently;
  match it. A comment restating the line below it is noise.

---

## Sibling repos

Separate git repos, and a change here often needs one of them:

| Repo | Path | Talks to |
|---|---|---|
| `Housing-Hub-FE` | `../Housing-Hub-FE` | `HousingHub.API` |
| `Housing-Hub-Admin` | `../Housing-Hub-Admin` | `HousingHub.Admin.API` |

Adding an endpoint is usually three changes: controller here, service + hook in
the frontend, and a type definition. The frontends have their own CLAUDE.md.

---

## Docs index

| File | What it is |
|---|---|
| `environment-separation-plan.md` | Splitting dev from production, step by step |
| `handoff.md` | Current state and what to do next |
| `business-verification-walkthrough.md` | The verification pipeline end to end |
| `transaction-lifecycle-plan.md` | The full product roadmap, Phases 3–6 |
| `scheduled-workers.md` | Worker triggering, and moving to EventBridge |
| `sentry-setup.md` | Error monitoring across all four apps |
| `security-and-performance-sweep.md` | The audit that drove the hardening work |
| `data-backfill-required.md` | Why `UsePublishedIndex` is still false |
| `s3-bucket-policy-runbook.md` | Private prefix policy for KYC documents |
| `product-strategy.md` | Market positioning and comparables |
