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

### Phases 1–6 — outstanding

| Phase | What | Whose |
|---|---|---|
| 1 | `develop` branch in all three repos, set as default | **You** |
| 1 | Vercel **Production Branch → `master`** on both projects | **You** |
| 2 | S3 bucket, two IAM roles, two Lambdas, two API Gateways | **You** |
| 3 | GitHub Environments + `deploy.yml` matrix | Agent can do the workflow files |
| 4 | Vercel environment variables, preview-deploy policy | **You** |
| 5 | Google OAuth prod client, Resend DNS, Sentry alert rule | **You** |
| 6 | SuperAdmin bootstrap, full smoke test | **You** |

Phase 1's two steps must happen **in the same sitting**. Vercel promotes the
repository's default branch; making `develop` default without changing Vercel's
Production Branch starts deploying `develop` to your production domain.

Two things to sort out before Phase 1 rather than during it:

- **`develop` already exists in all three repos and is badly stale** — 57, 32 and
  20 commits behind `master`. It carries **no** unique commits, so catching it up
  is a fast-forward with nothing to resolve. Do that before making it the default
  branch, or the default branch is three months of missing work.
- **`Housing-Hub-FE`'s default branch on GitHub is `main`, not `master`**, and
  `main` is over a hundred commits behind. Deploys come from `master`. Since
  Phase 1 turns on what the default branch is, resolve which of the two is real
  first.

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
- **Nothing is tested before it merges.** `deploy.yml` runs on push to `master`
  and has no `pull_request` trigger, so the tests run *after* a merge, as the
  first half of the deploy. The two frontends have no workflows at all and have
  never had an automated build check. A PR-triggered job in each repo is the
  cheapest reliability available and belongs with the Phase 3 workflow work.
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
