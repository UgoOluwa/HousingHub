# Security Remediation Plan

Approach for review. Nothing implemented yet.

Scope: delete the consumer-app admin UI, fix all known + newly-discovered
security issues, add rate limiting, fix the §2.2 functional list, remove
switch-account. Monitoring explicitly out of scope.

---

## Ground rules

- **I cannot rotate secrets — you must.** I'll remove them from the repos and
  add fail-fast startup validation, but every exposed key stays compromised
  until *you* rotate it in AWS/Resend/CBN-adjacent consoles. Deleting from HEAD
  does not undo exposure; the values remain in git history.
- Work lands in small, reviewable commits grouped by workstream below.
- I'll re-run `tsc --noEmit` on both frontends after each FE workstream. I
  **cannot compile the .NET** (no dotnet in sandbox) — you'll need to run
  `dotnet build && dotnet test` before merging.

---

## Workstream A — Delete consumer admin UI

**Approach:** delete `Housing-Hub-FE/src/app/admin/` entirely, plus
`AdminNavbar.tsx`, `AdminMetrics.tsx` and any admin-only components that become
orphaned. Grep for dangling imports and route links, remove those too. Admin
lives in `Housing-Hub-Admin` only.

Also removes the mock data flagged in §2.2 (MOCK_PROPERTIES, MOCK_OWNERS,
INITIAL_USERS, "Priscilla Ighodaro", etc.) since it all lived under that tree.

**Risk:** low. Nothing else imports it.

---

## Workstream B — Authorization (the structural fix)

This is the important one. Rather than patching endpoints one at a time, mirror
what the Admin API already does correctly.

**B1. Fallback-deny on the consumer API.**
Add a `FallbackPolicy` requiring an authenticated user, then explicitly mark
genuinely-public endpoints `[AllowAnonymous]`. This inverts the default from
"open unless secured" to "closed unless opened" and structurally kills the
whole class of bug.

Endpoints to keep anonymous — **verified against the actual controllers**, this
is the complete current anonymous surface:

| Controller | Anonymous endpoints | Verdict |
|---|---|---|
| `Auth` | register, login, refresh-token, verify-email, resend-otp, forgot-password, reset-password, google, google-login, google-callback | Correct — keep |
| `Property` | all, `{id}`, new, trending, nearby, `{id}/files` | Correct — public listings. `{id}/files` needs the `IsPublished` check (B2) |
| `PropertyAddress` | `{id}`, `property/{propertyId}` | Keep anonymous but add the `IsPublished` check (B2) |
| `Faq` | GET | Correct — keep |
| `Utility` | enums, `enums/{enumName}` | Correct — keep |
| `Customer` | **POST (create)** | **Wrong** — gets `[Authorize]` per B3 |

Everything else in the consumer API already carries `[Authorize]` at class or
method level, so fallback-deny should be a safe net rather than a behaviour
change. Also noted while checking: `DELETE /Property/{id}` carries only
`[Authorize]` rather than the owner policy, but the service does verify
ownership — so that one is fine as-is.

**B2. Ownership-check helper.**
Add one `EnsureOwnership(callerId, resourceOwnerId)` helper returning a
consistent 404 (not 403 — 403 confirms the resource exists). Apply to every
endpoint the audits flagged:

| Endpoint | Fix |
|---|---|
| `GET /Customer/{id}` | self-or-admin only |
| `GET /Customer/all` | admin only |
| `DELETE /Customer/{id}` | self-or-admin only |
| `GET /CustomerAddress/{id}` | owner only |
| `GET /Inspection/property/{propertyId}` | property owner or admin |
| `POST /PropertyAddress` | verify caller owns the property |
| `GET /PropertyAddress/*`, `GET /Property/{id}/files` | `IsPublished \|\| caller is owner` |

**B3. Fix (not delete) the second account-creation path.** — *revised after
checking callers*

`POST /api/v1/Customer` is anonymous and takes `CustomerType` from the body.
My first instinct was to delete it, but it **is** in use: `PersonalInfoForm.tsx:88`
calls it during KYC to backfill a customer record when one doesn't exist yet.
Deleting it would break onboarding.

Revised fix — it's a "create *my own* record" operation, so treat it as one:

- Add `[Authorize]`.
- Drop `CustomerType` and `Email` from the bound command; derive both from the
  caller's claims.
- Only ever create a record for the authenticated caller.

The FE already passes the user's own `customerType`, so this is transparent to
the existing flow while closing the escalation path.

**B4. Validate `CustomerType` on register.**
Call the `IsSelectableAtOnboarding()` helper that already exists and excludes
`Admin`. Reject anything else at the validator.

**B5. SignalR ChatHub membership check.**
`JoinConversation` and `SendTypingIndicator` currently join arbitrary groups
with no check — live eavesdropping on any conversation. Load the conversation,
reject unless `HasParticipant(Context.UserIdentifier)`.

---

## Workstream C — Secrets & config

**C1.** Delete secrets from `appsettings.json` / `appsettings.Development.json`.
**C2.** Fail-fast at startup: throw if `Jwt:Secret`, `AdminJwt:Secret` or
`Internal:WorkerSecret` is null, short, or matches a known placeholder. Today a
missing env var means the API boots signing tokens with a key that's public in
the repo.
**C3.** Delete `Housing-Hub-Admin/src/utils/` seeders, `scratch_customers.js`,
and the committed `seed_*.json` files containing live bearer tokens.
**C4.** Add `appsettings*.json` (except the sanitised base) to `.gitignore`.
**C5.** Move localhost out of the production CORS allow-list into
`appsettings.Development.json` — currently `http://localhost:3000` is a trusted
*credentialed* origin in prod on both APIs.
**C6.** Gate Admin API Swagger/Scalar behind `IsDevelopment()`. The consumer API
already does this; the admin one exposes its full surface anonymously.

**→ Your action: rotate the JWT signing keys, `ADMIN_SEED_KEY`,
`Internal:WorkerSecret`, and every account password in those seeder files.**

---

## Workstream D — File upload & storage

**D1.** KYC documents to a **private** bucket (or private prefix), served via
short-lived presigned URLs. Changes `IFileStorageService` — add
`UploadPrivateFileAsync` and `GetPresignedUrlAsync`.
**D2.** Derive `Content-Type` **server-side** from the validated extension.
Currently taken verbatim from the multipart part, so a `.jpg` can be uploaded
as `text/html` and served as HTML from the bucket origin — stored XSS with
same-origin read access to everything else in that bucket, including KYC docs.
**D3.** Apply the existing property-file validation (extension allow-list +
size cap) to the profile-photo and KYC paths, which currently have **none**.
Add magic-byte verification.
**D4.** `SubmitKycCommand.IdDocumentUrl` is client-supplied — a user can point
their KYC record at someone else's approved document. Drop the field; accept
only the key returned by the upload endpoint for that same customer.

---

## Workstream E — Session & token lifecycle

**E1.** Add `POST /Auth/logout` that revokes the presented refresh token.
Today logout only clears client state; the 30-day refresh token stays valid
server-side.
**E2.** Revoke all refresh tokens on password reset and password change.
Currently a compromised session survives the victim changing their password.
**E3.** Check account status in `AuthService.RefreshToken` — a suspended
customer can currently still refresh. The admin path already does this.
**E4.** Revoke tokens on admin deactivate and customer suspend.
**E5.** Set an explicit short `AdminJwt:ExpirationInMinutes` (currently defaults
to 480 when unset).
**E6.** Stop returning the access token in a URL query string on the OAuth
callback — lands in browser history, Referer headers and access logs. Switch to
a fragment or short-lived one-time code.

**Decision needed — see Q2 below** on whether to also migrate refresh tokens
from `localStorage` to `HttpOnly` cookies.

---

## Workstream F — Rate limiting

Targets: `/login`, `/register`, `/forgot-password`, plus `/resend-otp`,
`/verify-email` and `/Chat/send` (currently an unthrottled mail-bomb — every
message fires an email with no per-sender throttle).

**Caveat you need to know:** .NET's built-in `AddRateLimiter` is **in-memory
and per-instance**. On Lambda that means each concurrent execution environment
keeps its own counter, so effective limits are looser than configured and reset
on cold start. Options in **Q1** below.

Also fixing here: cap `Chat/send` content length (currently unbounded), and
add a unique constraint on property reports (currently one user can file
unlimited reports against the same listing).

---

## Workstream G — Input validation & DoS

**G1.** Clamp pagination globally: `pageSize` ≤ 100, `pageNumber` ≥ 1. Today
`pageSize=100000000` is accepted, and a negative `pageNumber` reaches
`Skip(negative)` → unhandled exception → 500.
**G2.** Stop returning raw `ex.Message` to clients (~85 sites). Replace with a
generic message + a correlation ID, log the detail server-side.
**G3.** Uniform responses on the enumeration-prone endpoints — register, login,
verify-email, reset-password, resend-otp, and chat-send-to-unknown-recipient.
`ForgotPassword` already does this correctly; I'll mirror its pattern.
**G4.** De-duplicate the view-count write on `GET /Property/{id}` — currently an
unauthenticated DynamoDB write on every anonymous request, so trending rank is
trivially inflatable and it's free write amplification for a cost attack.

**Not in scope — see Q3.** The repository layer does a **full table scan** on
every read and filters in memory (`GenericQueryRepository`). That's the real
DoS and AWS-bill vector, but converting to GSI-backed `Query` is a substantial
refactor and I'd rather not fold it into a security pass.

---

## Workstream H — Crypto & timing

**H1.** Admin login OTP uses `Random.Shared.Next()` — a seeded, process-wide
PRNG. Since admin login is OTP-only, that value *is* the credential. Switch to
`RandomNumberGenerator.GetInt32()`.
**H2.** `CryptographicOperations.FixedTimeEquals` for verification-token,
reset-token and OTP comparison.
**H3.** Admin OTP lockout resets the attempt counter, so a new code after the
60s cooldown restores a fresh 5-guess budget. Track cumulative failures and
lock the account.

---

## Workstream I — Headers & transport

Add to both frontends' `next.config.ts`: CSP, HSTS, `X-Frame-Options: DENY`,
`X-Content-Type-Options: nosniff`, `Referrer-Policy`. The admin dashboard is
currently clickjackable and there's no CSP to blunt the stored-XSS vector in D2.

CSP will start in `Report-Only` so it doesn't break the app silently — I'll
flag when to flip it to enforcing.

---

## Workstream J — §2.2 functional fixes

| Issue | Approach |
|---|---|
| Register → verify-email dead-end | Pass `?email=` on the redirect so resend works |
| Free-text search doesn't exist | Wire the search input to set `?q=`; make the magnifier a real button |
| Property-type filter returns nothing | Send the integer enum, not the name |
| Hardcoded `SPH-12024`, "Dec 1 2024", 4 bed/3 bath on every listing | Use real property data; add the missing bed/bath fields end-to-end if absent |
| `/kyc` 404 | Redirect to `/kyc/personal-info` |
| `/switch-account` | **Remove the link entirely** (per your instruction) |
| `/forgot-password` 404 | Point existing links at `/reset-password` |
| `alert()` in owner flow (3 sites) | Replace with the existing toast component |
| Hardcoded postal code / country | Derive from the state/city selection |
| ToS / Privacy `href="#"` | Point at `/terms` and `/privacy` |
| `"uploaded_url_placeholder"` submitted on upload failure | Fail properly with an error |
| No "Message owner" on property page | Add entry point |

---

## Workstream K — Dependencies

`npm audit` reports 11 HIGH in the FE, 8 HIGH in Admin. The one that matters
given both apps rely on client-side route guards is the **Next.js
middleware/proxy-bypass** class.

Approach: patch-level `npm audit fix` first, verify build + typecheck, then bump
`next` and `axios` deliberately and re-verify. **See Q4** on major-version
appetite.

Also: both .NET projects target `net10.0` but pin ASP.NET Core **8.0.x** auth
packages — off the servicing train. I'd align to 10.0.x.

---

## Order of execution

1. **A** (delete admin UI) — removes attack surface immediately, zero risk
2. **B** (authorization) — the criticals
3. **C** (secrets) — then you rotate
4. **D** (file upload) — the stored-XSS chain
5. **E** (sessions) + **F** (rate limiting)
6. **G** (validation) + **H** (crypto) + **I** (headers)
7. **J** (functional) — lowest risk, do last
8. **K** (dependencies) — separate commit, easy to revert

---

## Decisions I need from you

**Q1 — Rate limiting on Lambda.**
In-memory is per-instance and resets on cold start.
(a) In-memory anyway — imperfect but blocks naive attacks, zero infra *(my
recommendation for now)*; (b) API Gateway throttling — real limits, no app
code, but coarse per-route; (c) DynamoDB-backed counters — accurate,
distributed, adds latency and cost per auth request.

**Q2 — Refresh tokens: `localStorage` → `HttpOnly` cookies?**
Currently 30-day refresh tokens in `localStorage`, readable by any XSS. Cookies
are strictly safer but it's a real refactor across both frontends plus CORS and
CSRF handling.
(a) Add revocation now (E1–E4), migrate to cookies as separate work *(my
recommendation — captures most of the risk reduction cheaply)*; (b) do the full
cookie migration in this pass.

**Q3 — The full-table-scan repository.**
Real DoS and cost vector, but a big refactor.
(a) Pagination clamps now, GSI refactor as its own project *(my
recommendation)*; (b) include it here.

**Q4 — Dependency upgrades.**
(a) Patch/minor only, keep `next` on its current major *(my recommendation for
a security pass)*; (b) go to latest including majors, accept the regression
testing.

**Q5 — Confirm the anonymous-endpoint list in B1** above, and confirm **B3**
(deleting `POST /api/v1/Customer`) breaks nothing external.
