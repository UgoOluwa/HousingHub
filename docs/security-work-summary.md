# Security remediation — what shipped, what's left

All ten workstreams from `security-remediation-plan.md` are complete. This is the
handover: what changed, what you must do, and what I could not verify.

---

## Before you merge

**Nothing here has been compiled or run.** There is no dotnet in the environment
this was written in, and `npm ci` could not run either. Verification was static
inspection, twice over, by separate passes. That caught real errors — but it is not
a build.

```bash
# Backend
dotnet build && dotnet test

# Both frontends
npm ci && npm run build
```

Two places most likely to complain:

- `HousingHub.Core` now references `Microsoft.Extensions.Configuration.Abstractions`
  **10.0.0**, while the rest of the solution pins Microsoft.Extensions packages at
  8.0.0. Should unify fine on net10.0, but that is where a restore error would show.
- The `PaginationClampFilter` uses reflection to write init-only record properties.
  It compiles, but exercise a paginated admin endpoint to confirm it behaves.

---

## Your actions — code cannot do these

| # | Action | Why it matters |
|---|---|---|
| 1 | **Rotate every secret** in `SECRET-ROTATION-REQUIRED.md` | All are in git history and remain compromised. Includes a real JWT signing key |
| 2 | **Audit admins for accounts you don't recognise** | `Internal:WorkerSecret` shipped as a placeholder and is the only gate on SuperAdmin promotion. `ADMIN_SEED_KEY` was committed in plaintext |
| 3 | **Deny anonymous `s3:GetObject` on `private/*`** | Without the bucket policy, KYC documents are private in name only and workstream D achieves nothing |
| 4 | **Backfill `Customer.IsActive`** — see `data-backfill-required.md` | Every existing row reads as suspended. Also a live admin-UI bug today |
| 5 | **Migrate legacy KYC documents** into the private prefix | Those objects are still world-readable at a predictable URL |
| 6 | **Lawyer review of the verification badge wording** | Before any "Title Verified" badge ships. See `verification-design.md` §10 |
| 7 | **Deploy backend and both frontends together** | The KYC upload contract changed from URL to opaque key |

---

## What changed

**A — Consumer admin UI deleted.** Ten routes and nine modals behind no role check;
any signed-in customer could open `/admin` and reach live PII endpoints. The
admin-only service methods were removed too, so they cannot be called from a
consumer session at all.

**B — Deny-by-default authorization.** The consumer API treated a missing
`[Authorize]` as public. Inverted to match what the Admin API already did. Fixed
five IDOR holes, closed a privilege-escalation path in registration, deleted a
second undocumented one, and stopped `ChatHub` letting any authenticated user
subscribe to any conversation's live message stream.

**C — Secrets out of config, fail-fast on boot.** Both APIs read secrets with `!`
and never checked, so a missing env var meant booting with the placeholder from
source control. Also moved localhost out of the production CORS allow-lists (it was
a *credentialed* trusted origin) and gated the Admin API's Swagger, which was public.

**D — KYC documents to private storage.** Government IDs were in the public bucket
at a predictable URL. Now a private prefix with short-lived presigned URLs.
`Content-Type` is derived from verified file signatures rather than trusted from the
upload, which closes a stored-XSS chain that had same-origin read access to the
whole bucket. Profile photos and KYC uploads had no validation at all; they do now.

**E — Real logout and session revocation.** There was no server-side logout in
either API. Password change, reset, suspension and admin deactivation now all revoke
the token family. The HttpOnly cookie migration is **deferred** — see below.

**F — Rate limiting** on auth, email-sending and chat endpoints.

**G+H — Bounded pagination, no exception leakage, hardened crypto.** 84 sites
returned `ex.Message` to callers. Admin OTP now uses a CSPRNG — it was
`Random.Shared`, and that OTP *is* the admin credential.

**I — Security headers** on both frontends. CSP ships Report-Only.

**J — Functional fixes.** Search never worked (no input existed), the property-type
filter matched nothing, and every listing showed the same placeholder ID, date and
amenities. Also corrected the FAQ and privacy policy, which claimed funds are held
in escrow when there is no payment integration.

**K — Dependencies.** 13 → 0 and 9 → 0 advisories, no major version jumps.

---

## Deliberately not done

**HttpOnly cookies** — blocked on infrastructure, not code. Your API is on a
different registrable domain from the app, so the cookie would need
`SameSite=None`, which Safari and Firefox block outright. Full reasoning and the
unblock path in `deferred-cookie-migration.md`.

**The full-table-scan repository.** Every read scans the table and filters in
memory. Pagination clamps bound the damage, but this is still your largest cost and
latency problem and deserves its own project.

**Two enumeration leaks kept on purpose.** Registration reports a duplicate email
and resend-verification reports "already verified". Both confirm an account exists,
but removing them hurts real users, registration leaks it unavoidably anyway, and
both are now rate limited.

**Monitoring** — excluded at your request. Worth revisiting: there is no error
tracking of any kind, so you are blind to production failures.

---

## Caveats on the rate limiting

The limiters are in-memory and therefore per-instance. On Lambda each execution
environment keeps its own counters and they reset on cold start, so the effective
limit is looser than configured. They stop naive credential stuffing and retry
storms, which is worth having for free. They are not a distributed control — that
belongs at API Gateway.

---

## Two bugs I introduced and caught

Recorded because they are the kind of thing worth watching for in review.

**An `IsActive` check that would have logged out every user.** Added in one commit,
removed in the next. `IsActive` defaults to false and was never set at registration,
so refusing to refresh an inactive account would have signed out the entire user
base within one access-token lifetime.

**A class-level `[AllowAnonymous]` on `AuthController`.** It lands in endpoint
metadata and *suppresses* method-level `[Authorize]`, which would have made
change-password and account-type anonymous. Inverted to per-action.

A third, caught before commit: the deny-by-default policy applies to `MapHealthChecks`
too, so every load-balancer probe would have received 401.
