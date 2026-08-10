# Full sweep — security and performance

Date: 2026-08-10. Branch `feat/bu-fixes` across all three repos.

This is a read-only audit. Nothing in it has been changed yet.

**Caveat that applies to everything below:** none of this session's code has been
compiled or run. `dotnet build && dotnet test` and `npm ci && npm run build` are
still outstanding. Static reading catches logic errors; it does not catch
compile errors.

---

## 1. What came back clean

Listing these explicitly, because "no findings" is only useful if you know what
was actually looked at.

**Object-level authorization.** Every action in all eleven consumer controllers
derives the caller's identity from the JWT `sub` claim and passes it down. Not
one endpoint accepts a `customerId` or `ownerId` from the request body or query
string. The service layer independently re-checks ownership before mutating:
`PropertyCommandService.cs:322, :404, :442` for update/delete/publish, and
`PropertyFileCommandService.cs:96, :151` for file upload/delete. `CustomerController`
returns 404 rather than 403 on an ownership failure, which is the right call —
403 turns GUID enumeration into an existence oracle.

**Privilege escalation to `CustomerType.Admin`.** Closed at all three entry
points. Registration is gated by `IsSelectableAtOnboarding()`
(`RegisterAuthCommandValidator.cs:22`); `SetAccountType` re-checks the same
predicate *and* refuses to run on an account whose type is already set
(`AuthService.cs:504, :514`); and there is no exposed endpoint that binds
`UpdateCustomerCommand`, so its weaker `IsInEnum()` validator is unreachable.

**Open redirect / token leak via Google.** `IsAllowedReturnUrl` compares scheme,
host *and* port against `Cors:AllowedOrigins` and is called on both
`google-login` and `google-callback`. Substring matching would have been the
usual bug here; this isn't that.

**Refresh tokens.** 32 bytes from `RandomNumberGenerator`
(`AuthService.cs:773`), SHA-256 hashed at rest (`:706`), rotated on use, and
presenting a revoked token revokes the entire family. That is the correct
replay response.

**Password hashing.** PBKDF2-SHA512, 500,000 iterations, 16-byte random salt,
`CryptographicOperations.FixedTimeEquals` on compare. Above OWASP's floor. (See
P7 — it may be *too* far above it.)

**Cross-API token forgery.** The Admin API validates against `AdminJwt:Secret`
and requires a `role=Admin` claim; the consumer API signs with `Jwt:Secret` and
never issues that claim. A consumer token cannot reach an admin endpoint.

**Admin API default posture.** `FallbackPolicy` requires an authenticated user
*with* `role=Admin`, so the five admin controllers that carry no attribute at
all (Chat, Customer, Dashboard, Property, and most of Inspection) are closed by
default, not open.

**Frontend.** No committed `.env` in either app — only `.env.example`. No
`NEXT_PUBLIC_*` variable holds a secret. The single `dangerouslySetInnerHTML`
(`Housing-Hub-FE/src/app/layout.tsx:33`) is a static string with no
interpolation. Security headers — HSTS, X-Frame-Options, nosniff,
Referrer-Policy, Permissions-Policy — are set in both `next.config.ts` files.

**Index/table drift.** The 22 GSIs declared on entities match the 22 in
`DynamoDbTableInitializer` exactly, name for name and attribute for attribute.
This was the highest-risk thing to get wrong and it is right.

---

## 2. Security findings

No Critical or High remain. Five Medium, three Low.

### M1 — `InternalController` is unauthenticated, unthrottled, and compares its secret non-constant-time

`src/HousingHub.Admin.API/Controllers/InternalController.cs:38` and `:59`

```csharp
if (string.IsNullOrEmpty(expectedSecret) || secret != expectedSecret)
    return Unauthorized();
```

Three problems compounding:

- `PUT /api/Internal/admins/promote` grants SuperAdmin to any email, and the
  only thing between the internet and that is one header value. It is documented
  as a "one-time bootstrap" but it is a permanent endpoint.
- The controller carries no `[EnableRateLimiting]`, unlike every other sensitive
  endpoint in both APIs. The secret can be guessed at full speed.
- `!=` on strings is not constant-time. On its own that is a weak oracle across
  a network; combined with unlimited attempts it stops being theoretical.

Fix: `CryptographicOperations.FixedTimeEquals` on the UTF-8 bytes (there is
already a helper doing exactly this at `AuthService.cs:702`), add the auth rate
limit policy, and delete the promote endpoint once you have your first
SuperAdmin — a bootstrap step should not outlive the bootstrap.

### M2 — CSP is Report-Only in both frontends

`Housing-Hub-FE/next.config.ts:50`, `Housing-Hub-Admin/next.config.ts:39`

```
{ key: 'Content-Security-Policy-Report-Only', value: contentSecurityPolicy }
```

Shipping Report-Only first was the right sequencing, but it enforces nothing.
The policy has now been in place long enough to check: open the app, watch the
console for violations, fix them, then rename the header. Note the policy still
contains `'unsafe-inline'` in `script-src`, which blunts it considerably even
once enforced — removing that needs per-request nonces via middleware.

### M3 — the JWT is delivered in a redirect query string

`src/HousingHub.API/Controllers/V1/AuthController.cs`, `GoogleResult`:

```csharp
return Redirect($"{returnUrl}{separator}{query}");   // ...?token=eyJ...
```

The `returnUrl` is validated, so this is not an open redirect. But an access
token in a URL lands in browser history, in the `Referer` header of the next
outbound request from that page, and in API Gateway / CloudFront access logs
where it will sit in plaintext far longer than the token's own lifetime.

Fix: return the token in a short-lived one-time exchange code, or post it to the
opener. Neither is a small change, which is why this is Medium and not High —
but it should not survive to production traffic.

### M4 — `PasswordHasher.Verify` throws on a malformed stored hash

`src/HousingHub.Service/Commons/Authentication/PasswordHasher.cs:23`

```csharp
string[] parts = passwordHash.Split('-');
byte[] hash = Convert.FromHexString(parts[0]);
byte[] salt = Convert.FromHexString(parts[1]);
```

No length check. A Google-registered account whose `PasswordHash` is empty makes
`parts` a one-element array, so `parts[1]` throws `IndexOutOfRangeException`.
That escapes as a 500 rather than "invalid credentials" — which is both a
crash-on-demand and, incidentally, an account-type oracle: 500 means
Google-only, 400 means local. Guard the shape and return `false`.

### M5 — no privilege tiering below SuperAdmin

Every admin endpoint that isn't explicitly `SuperAdminOnly` is reachable by any
account with `role=Admin`. That includes reading every customer's national ID
number and KYC document. For a small team that may be an acceptable deliberate
trade; it should be a decision you have made rather than one the default
inherited. Worth revisiting before you hire operations staff.

### L1 — `RequiredSecrets` doesn't validate issuer, audience, or allowed origins

`RequiredSecrets.Validate` covers signing keys and API keys but not
`Jwt:Issuer`, `Jwt:Audience`, or `Cors:AllowedOrigins`. An empty
`Cors:AllowedOrigins` doesn't fail startup — it silently disables CORS *and*
makes every Google login fail `IsAllowedReturnUrl`. That is a confusing outage
rather than a breach, but it fails quietly, which is the thing fail-fast
validation exists to prevent.

### L2 — rate limiter runs after authentication

`Program.cs`: `UseAuthentication()` → `UseAuthorization()` → `UseRateLimiter()`.
Signature validation happens before the throttle decision, so a flood still
costs CPU per request. Move `UseRateLimiter()` above `UseAuthentication()`.

### L3 — limiters are per-Lambda-instance

Already documented honestly in `RateLimitingExtensions.cs:10-22`. Repeating it
here only so it appears on one list: the real fix is API Gateway usage plans,
which see every request regardless of which execution environment serves it.

---

## 3. Performance findings

The repository refactor removed the *unconditional* scan. What it did not
remove — and could not, by design — is scans caused by predicates it cannot
narrow. That is where nearly all of the remaining cost is.

### P1 — the batching pattern defeats index narrowing (11 sites)

This is the big one, and it is subtle. Code like this looks efficient:

```csharp
// PropertyQueryService.cs:352
var files = await _unitOfWOrk.PropertyFileQueries.GetAllAsync(
    f => propertyIds.Contains(f.PropertyId));
```

It avoids an N+1 — one call instead of twenty. But `Contains` is a method call,
so `EqualityPredicateExtractor` correctly refuses to narrow it, and the
repository falls through to **a full scan of `PropertyFiles`**. Twenty GetItems
were replaced with one whole-table read.

For a few hundred rows the scan is genuinely cheaper. It stops being cheaper
somewhere in the low thousands, and it never gets better after that.

Affected, ordered by how hot the path is:

| Site | Scans | Triggered by |
|---|---|---|
| `PropertyQueryService.cs:352` | PropertyFiles | every listing page, every homepage load |
| `PropertyQueryService.cs:153` | PropertyAddresses | any city/state filter |
| `PropertyQueryService.cs:224, :376` | PropertyInspections | owner's "my properties" |
| `InspectionQueryService.cs:183, :185, :390` | PropertyInspections, PropertyFiles | owner inspection list |
| `InspectionQueryService.cs:334-336` | Properties, Customers, PropertyAddresses | admin inspection detail |
| `ChatQueryService.cs:42, :112` | Customers | conversation list, message list |

The fix already exists in this codebase. `InspectionQueryService.cs:252-258`
does it correctly:

```csharp
var propertyTasks = propertyIds.Select(id => _unitOfWOrk.PropertyQueries.GetByIdAsync(id)).ToList();
var customerTasks = customerIds.Select(id => _unitOfWOrk.CustomerQueries.GetByIdAsync(id)).ToList();
await Task.WhenAll(propertyTasks.Cast<Task>().Concat(customerTasks));
```

Twenty parallel single-row reads, one round trip's worth of latency. For the
by-foreign-key cases (files, addresses by `PropertyId`) the equivalent is
parallel `QueryByIndexAsync` calls against the existing `PropertyId-index`.

Cleanest version: add `GetManyByIdsAsync` and `QueryByIndexManyAsync` to
`IGenericQueryRepository`, so the pattern is named once rather than re-derived
at eleven call sites.

### P2 — the public listing scans `Properties` on every request

`PropertyQueryService.cs:110` and `:243`

```csharp
var allProperties = await _unitOfWOrk.PropertyQueries.GetAllAsync(x => x.IsPublished);
```

A bare bool property — no GSI, nothing to narrow to, so this is a full scan of
`Properties` on every anonymous homepage load and every listing page. Then
search, features, type and price are all filtered in memory afterwards.

Fix: a sparse GSI keyed on a string `PublishedStatus` attribute set only when a
listing is published. DynamoDB GSIs are sparse, so unpublished rows never enter
the index and the query reads only what it returns. This one needs a data
backfill and a change to the publish path, so it's the largest item on this
list — but it is also the single hottest query in the product.

### P3 — pagination happens in memory

`GenericQueryRepository.GetPagedAsync` materialises the candidate set, then
`Skip`/`Take`. Returning page 1 of 20 costs the same as returning everything,
and `totalCount` requires the full set by construction.

Real cursor pagination means exposing `LastEvaluatedKey` and giving up
random-access page numbers and exact total counts — an API contract change the
frontends would have to follow. Worth planning, not worth rushing.

### P4 — duplicate detection is the most expensive single call in the app

`PropertyCommandService.cs:221` and `:238`

```csharp
var candidates = await _unitOfWOrk.PropertyQueries.GetAllAsync();   // full scan
...
foreach (var candidate in candidates.Where(p => !p.Latitude.HasValue || !p.Longitude.HasValue))
{
    var candidateAddress = await _unitOfWOrk.PropertyAddressQueries.GetByIdAsync(candidate.AddressId);
```

A full table scan *plus* a sequential GetItem per coordinate-less candidate, on
every property creation. At 1,000 properties with 10% missing coordinates that
is one scan and 100 serialised round trips.

Fix: geohash the coordinates onto the entity and query a `Geohash-index` prefix
instead of scanning; for the fallback path, index normalised `city|state` and
query that rather than walking every candidate.

### P5 — table initialization runs on every cold start

`Program.cs` in both APIs calls `InitializeAsync()`, which calls
`ListTablesAsync()` before the app accepts traffic. That is a DynamoDB round
trip added to every cold start, in exchange for creating tables that have
existed since the first deploy. It also means the Lambda execution role needs
`dynamodb:CreateTable` and `ListTables` in production, which it otherwise
wouldn't.

Move it behind an environment flag, or better, into your infrastructure
definition where it belongs.

### P6 — admin list endpoints scan and filter in memory

`InspectionQueryService.cs:217` and `CustomerQueryService.cs:48, :84` load the
entire table and apply every filter with LINQ. Lower priority than the consumer
paths — few callers, and admin tolerance for latency is higher — but the
customer search at `:96-99` does four `Contains` per row over every customer,
which will be the first admin screen to feel slow.

### P7 — 500,000 PBKDF2 iterations is ~2.4× OWASP's figure

`PasswordHasher.cs:9`. OWASP currently puts PBKDF2-SHA512 at 210,000. At 500k
you are spending roughly 2.4× the CPU on every login, registration and password
change — on Lambda that is directly billed latency, and it applies to failed
attempts too, which makes each rejected credential-stuffing request more
expensive for you than for the attacker.

Dropping to 210,000 stays within current guidance. Note that changing the
constant invalidates every existing hash unless you store the iteration count
alongside the hash and verify with the stored value — worth doing anyway, since
you will want to raise it again in a few years.

### P8 — property alerts fan out sequentially

`PropertyCommandService.cs:501`: a `GetByIdAsync` per matched preference, in
series, inside the property-creation request. A popular new listing matching 200
saved searches means 200 serialised round trips before the owner's request
returns. Batch the customer reads with `Task.WhenAll`, and consider moving the
whole notification fan-out off the request path.

---

## 4. Suggested order

**Before beta traffic**

1. M1 — `InternalController`: constant-time compare, rate limit, delete the promote endpoint
2. M4 — guard `PasswordHasher.Verify` (one-line fix, removes a live 500)
3. L2 — move `UseRateLimiter()` above `UseAuthentication()` (one-line fix)
4. P1 — replace the `Contains` batching at the six hot sites
5. P4 — duplicate detection, at minimum stop the sequential per-candidate reads
6. P5 — take table initialization off the cold-start path
7. M2 — enforce the CSP

**Before real volume**

8. P2 — sparse published-listings index (needs a backfill)
9. P7 — iteration count, stored alongside the hash
10. P8 — batch and/or defer alert fan-out
11. M3 — stop putting the JWT in a URL
12. L1 — extend `RequiredSecrets`

**Decisions, not fixes**

13. M5 — whether any Admin should see every customer's national ID
14. P3 — cursor pagination, and what it does to the frontend contract
