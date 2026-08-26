# Handoff — where things stand

Written at the point of moving this work into Claude Code. Read this before
picking anything up; it says what is done, what is half-done, and what is
waiting on a human.

**Date:** 18 August 2026
**Head commits (master):** `HousingHub` `c8a363d` · `Housing-Hub-FE` `1eab97c` ·
`Housing-Hub-Admin` `600be69`

---

## The one thing to understand first

**The beta has not started.** No real users. Everything built so far runs in a
single environment doing duty as both dev and production.

That matters for prioritisation. Phases 3–6 of the product roadmap (payments,
affordability, in-app signing, protected payment) all rest on an assumption
nobody has tested: that owners and renters will transact *through* the platform
rather than treat it as a place to find a phone number. Getting real listings
and real users onto what exists tells you which of those four is worth building
first — and possibly that the answer is none of them.

This is an opinion, not an instruction. But do not let the roadmap's existence
imply the sequence is settled.

---

## Immediate: nothing

`NEXT_PUBLIC_S3_ORIGIN` is set on both Vercel projects, and everything below has
shipped. The next move is Phase 1, and Phase 1 is entirely yours.

---

## Environment separation

Full plan: `environment-separation-plan.md`. Decisions already taken — one AWS
account with prefixed resources, `develop` → dev and `master` → production,
custom domain on production only, both frontends on Vercel.

### Phase 0 — code made environment-aware ✅ shipped and verified

Every change defaults to today's values, so deploying it should change nothing.
That is the point: it is the only part of the split that can break what exists,
so it lands alone and first.

What changed:

- `Dynamo:TablePrefix` — the SDK's `TableNamePrefix`, read through
  `DynamoDbNaming` by both the `IDynamoDBContext` and `DynamoDbTableInitializer`
  so they cannot be configured apart. **Empty in dev and must stay empty.**
- `Api:PathBase` — replaces `UsePathBase("/dev")` and `UsePathBase("/admin")`.
- `Sentry:Environment` — was `production` in the shared file, so every event so
  far has been mislabelled. Now `development`.
- `NEXT_PUBLIC_S3_ORIGIN` in both frontends, feeding the CSP and
  `images.remotePatterns` from one variable.
- Production builds now throw on a missing API URL instead of falling back to
  dev.
- Incidental: the admin Scalar docs pointed at `/admin/openapi/v1.json` locally
  where there is no path base, so the schema pane has been loading empty.

**Verified 18 August 2026.** `dotnet build` clean, 649 tests passing, both
frontends producing real production builds, and PR #36 deployed both Lambdas
green. The smoke walk was done and nothing had moved.

Two things shipped alongside it:

- `Microsoft.OpenApi` 2.3.0 → 2.7.5 on both APIs, closing GHSA-v5pm-xwqc-g5wc
  (a circular schema reference can terminate OpenAPI parsing).
- `HousingHub.Data` was pinning `Microsoft.Extensions.Configuration.Abstractions`
  at 8.0.0 while `HousingHub.Core` pinned 10.0.0. CI never minded; **SDK 10.0.302
  fails the restore outright where 10.0.400 does not**, so a local build can fail
  on a tree that deploys perfectly. Both are on 10.0.0 now. If a restore fails
  locally and CI is green, compare `dotnet --version` against the SDK the deploy
  workflow installs before believing the tree is broken.

### Phases 1–6

| Phase | What | Status |
|---|---|---|
| 1 | `develop` default in all three repos, `master` protected | ✅ 18 Aug |
| 1 | Vercel **Production Branch → `master`** on both projects | ✅ |
| 2 | S3 bucket, IAM roles, two Lambdas, two API Gateways | ✅ except admin env vars |
| 3 | GitHub Environments + `deploy.yml` matrix | Workflows written; **Environments not created** |
| 4 | Vercel environment variables, preview-deploy policy | Outstanding — **you** |
| 5 | Google OAuth prod client, Resend DNS, Sentry alert rule | Outstanding — **you** |
| 6 | SuperAdmin bootstrap, full smoke test | Outstanding — **you** |

### Production resources, as built

Roughly fifteen console-set values per Lambda are still invisible to review (see
the debt list). These are the ones needed to find anything:

| | Value |
|---|---|
| Account / region | `289291307029` · `af-south-1` |
| Consumer API | `https://e2dteg0k7i.execute-api.af-south-1.amazonaws.com/prod` |
| Admin API | `https://kpwiufp5r8.execute-api.af-south-1.amazonaws.com/admin` |
| Lambdas | `HousingHub-API-prod` (1024 MB) · `HousingHub-Admin-API-prod` (512 MB) |
| Execution role | `HousingHub-Lambda-Prod`, inline policy `HousingHub-Prod-Scoped` |
| Bucket | `housinghub-files-prod` |
| Table prefix | `prod_` |

Both APIs return **200** on `/health` as of 26 August. Note `Cors:AllowedOrigins`
is an array and must be set as `Cors__AllowedOrigins__0` — written without the
index it binds as a string and fails the check.

Still to do on Phase 2: fourteen of the sixteen `prod_` tables. See the
`AutoCreateTables` note below for why the running API will not finish the job.

### What Phase 2 taught, at some cost

**The scoped IAM role paid for itself on day one.** `Dynamo__TablePrefix` was set
to `prod` rather than `prod_`, so the initializer tried to create `prodAdmins`,
`prodCustomers` and so on. Every call was refused — `not authorized to perform:
dynamodb:CreateTable on ... table/prodVerificationDocuments` — because the policy
grants `table/prod_*` and that pattern needs the literal underscore. Sixteen
tables under names nothing would ever read did not get created. This is the
argument against skipping the resource scoping to save time.

**`Dynamo:AutoCreateTables` does not converge under Lambda.** `InitializeAsync`
is deliberately backgrounded — its own remarks say schema convergence "must not
stop the API accepting traffic," which is right for an API. But Lambda freezes
the execution environment when the handler returns, so the background task gets
milliseconds per cold start and stops two tables in. Repeated invocations do not
help: reconciliation runs at *startup*, and a warm container has already started.
Sixteen tables therefore need either eight forced cold starts or, better, the
same work run from a process that is not frozen. Treat it as a bootstrap
convenience, not schema management — which is what the `appsettings.json` comment
means by "set false once the schema lives in real infrastructure code."

**A missing Sentry DSN stopped the app booting.** `SentryOptionsConfigurator` set
`options.Dsn = null` when the DSN was absent; the SDK reads null as "not
configured" and throws, where an empty string means "disabled". The comment two
lines above promised the opposite — "a missing environment variable should cost
you monitoring, never a boot failure". No configuration could work around it,
because a blank env var mapped back to null. Fixed in `b2eec50`, with tests for
the no-DSN path that nothing had covered — the path every environment takes until
a DSN exists. API Gateway reported it as a 502 containing no mention of Sentry.

**The admin API had no health endpoint.** Until `70b7cbc` the only way to ask
whether it had started was to call a real endpoint and read 401 as "yes" — which
is unsound, since 401 is equally what a broken token configuration returns. From
outside, 401 and 502 look similarly broken and mean opposite things: started and
refused you, versus never started. Telling them apart needed CloudWatch. This is
why `deploy.yml` health-checks both APIs.

**Nothing about a healthy-looking 200 tells you which tables you are on.** The
consumer API returned 200 while zero `prod_` tables existed, because `/health`
does not touch DynamoDB. The single line
`Reconciling DynamoDB schema for 16 tables with prefix 'prod_'` is the only thing
that says which data a process is about to touch. Read it, not the status code.

### Phase 3 — what is left to do by hand

`deploy.yml` and `scheduled-workers.yml` are updated and expect these to exist:

1. Two **GitHub Environments** named exactly `dev` and `production`.
2. In each, a secret `AWS_DEPLOY_ROLE_ARN` for that environment's deploy role.
   `scheduled-workers.yml` also needs `ADMIN_API_URL` and
   `INTERNAL_WORKER_SECRET` per environment.
3. In each, two **variables** (not secrets) `CONSUMER_HEALTH_URL` and
   `ADMIN_HEALTH_URL`. The deploy fails if they return anything but 200, which is
   what catches a setting added in code and never added in AWS.
4. A **required reviewer** on `production`. That approval is the only thing
   between a merge to `master` and real users.

Until the Environments exist, a push to `master` will fail at the credentials
step rather than deploy anything — which is the safe direction to fail.

Phase 1's two steps must happen **in the same sitting**. Vercel promotes the
repository's default branch; making `develop` default without changing Vercel's
Production Branch starts deploying `develop` to your production domain.

`develop` existed in all three repos but sat 57, 32 and 20 commits behind
`master` with no unique commits of its own. **Fast-forwarded to `master` in all
three on 18 August**, so Phase 1 can now make it the default branch without that
meaning three months of missing work.

One trap worth naming, because it cost time here: `git branch -a` shows
`origin/HEAD -> origin/<branch>`, which is a pointer **cached at clone time and
never refreshed by `git fetch`**. In this clone it still read `origin/main` for
`Housing-Hub-FE` long after GitHub's default became `master`, which reads exactly
like a misconfigured repository. `git remote set-head origin -a` refreshes it.
Confirm a default branch against `gh api repos/<owner>/<repo> --jq
.default_branch`, never against the local ref.

A stale `main` still exists on `Housing-Hub-FE` and `HousingHub`, far behind
`master` and unused. Deleting them is safe but nobody has decided to.

Generate the three production secrets fresh — `openssl rand -base64 48` for
`Jwt:Secret`, `AdminJwt:Secret`, `Internal:WorkerSecret`. Do not copy dev's;
`SECRET-ROTATION-REQUIRED.md` explains why dev's are tainted.

---

## Product phases

`transaction-lifecycle-plan.md` has the full reasoning.

| | Status |
|---|---|
| Phase 1 — listing platform + manual ID verification | Built |
| Phase 2A — verification case pipeline | Built |
| Phase 2B — outcome applied, CAC lookup interface, name matching | Built |
| Phase 3 — payments | Not started |
| Phase 4 — affordability / financial verification | Not started |
| Phase 5 — documentation and signing | Not started |
| Phase 6 — protected payment (48-hour hold) | Not started |

Phase 3 onward is where the CBN licensing question becomes real. Phase 5 runs
into the Evidence Act 2011 s.93 exclusion of e-signatures for land instruments —
both covered in `transaction-lifecycle-plan.md`.

---

## Open items

**Waiting on a human:**

- **Lawyer sign-off on the title badge wording.** Gates
  `Verification:ShowTitleBadge`, currently `false`. It is the strongest claim the
  platform makes and the first thing a defrauded buyer's lawyer points at.
  Verification still runs and is recorded either way.
- **A CAC provider account.** The interface and a deferring implementation
  exist; connecting Dojah, QoreID or Mono is one class and one line in
  `ConfigureServices`. Until then the reviewer sees *"not checked automatically"*
  rather than a green tick — deliberately, because a stub returning "passed"
  would manufacture assurance nobody checked.

**Technical debt, in rough priority order:**

- **`Dynamo:UsePublishedIndex` is `false`.** Every published listing predating
  the sparse index is missing from it, so the homepage still scans the table.
  Fixing it means re-saving those rows — `data-backfill-required.md`. Note that
  production, starting empty, can set this `true` from day one.
- **No production backups.** DynamoDB point-in-time recovery is off by default
  and is per-table. Turn it on for the `prod_*` tables once they exist.
- **Configuration lives in the AWS console.** Roughly fifteen environment
  variables per Lambda, set by hand, invisible to review, lost if a function is
  recreated. Survivable at two environments, painful at three. Terraform or SAM
  when it starts hurting.
- **Workers run on GitHub Actions.** Best-effort scheduling, and scheduled
  workflows are disabled automatically after 60 days without a commit. If the
  repo goes quiet the workers stop silently. `scheduled-workers.md` has the
  EventBridge migration.
- **The frontends are still tested by nobody.** `deploy.yml` now runs its tests on
  pull requests as well as pushes, so a broken backend change is caught before it
  reaches a release branch. Neither frontend has any workflow at all, and neither
  has ever had an automated build check — despite `tsc --noEmit` plus a real
  build being the only verification those repos have. A PR-triggered job in each
  is the cheapest reliability left on this list.
- **Dependabot is disabled on `HousingHub`.** It is why the OpenAPI advisory
  above only ever surfaced as a local build warning. Both frontends have it on
  and are carrying open alerts — `nanoid`, `js-yaml`, `postcss`, all build-time
  or dev-dependency transitives rather than anything serving a user request.
- **CSP allows `'unsafe-inline'` for scripts** in both frontends. Removing it
  needs per-request nonces via middleware.

---

## Working notes

**The three repos are separate.** A feature usually spans two of them: endpoint
in `HousingHub`, then a type, a service function and a hook in the frontend.

**Verify before claiming.** Several past changes shipped broken because they
were reasoned about rather than compiled — a missing `using`, a `Guid` treated
as `Guid?`, a threshold list ordered backwards. Run `dotnet build`,
`dotnet test`, `tsc --noEmit`.

**The recurring bug in this codebase is "computed and rendered nowhere."** Three
separate fields were calculated correctly on the server and displayed to no one.
If a change produces a claim about a user, trace it to a rendered pixel.

**Comments explain why, not what.** The codebase is consistent about this.
