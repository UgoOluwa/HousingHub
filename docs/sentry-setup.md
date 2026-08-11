# Sentry — what was wired up, and what you need to do

Error monitoring across all four applications. Everything is committed and inert:
**with no DSN configured the SDK initialises and reports nothing**, so this is
already safe to deploy. Turning it on is four environment variables.

---

## 1. Create the projects

In Sentry, create **four projects** under your org:

| Project | Platform | Application |
|---|---|---|
| `housinghub-web` | Next.js | Housing-Hub-FE |
| `housinghub-admin-web` | Next.js | Housing-Hub-Admin |
| `housinghub-api` | ASP.NET Core | HousingHub.API |
| `housinghub-admin-api` | ASP.NET Core | HousingHub.Admin.API |

Separate projects rather than one shared, so "the admin dashboard is broken" and
"the public site is broken" are distinguishable at a glance rather than two
filters deep. They share one quota either way, so this costs nothing.

Each project gives you a DSN. It looks like
`https://<key>@o123456.ingest.de.sentry.io/456789`.

**The DSN is not a secret.** It is embedded in the browser bundle by design and
only permits writing events. Do not treat it like an API key — but do keep the
*auth token* in section 3 secret, because that one can read.

---

## 2. Set the DSNs

### Frontends — Netlify / Vercel environment variables

```
NEXT_PUBLIC_SENTRY_DSN=<that project's DSN>
NEXT_PUBLIC_SENTRY_ENVIRONMENT=production
```

Set `NEXT_PUBLIC_SENTRY_ENVIRONMENT=preview` on deploy previews, otherwise a bug
you introduce in a branch looks like a production incident.

### APIs — AWS Lambda environment variables

Double underscore is how .NET maps a flat environment variable onto a nested
configuration key:

```
Sentry__Dsn=<that project's DSN>
Sentry__Environment=production
```

Nothing else changes. `appsettings.json` ships with `Sentry:Dsn` empty, and an
empty DSN is what keeps the SDK inert.

---

## 3. Source maps (frontends only — strongly recommended)

Without this, a production stack trace reads `main-8f3a.js:1:99213`. With it, it
reads `PropertyCard.tsx:42`. The difference between monitoring that works and
monitoring you will stop opening.

Create an auth token in Sentry (Settings → Auth Tokens, scope `project:releases`),
then set these as **build-time** variables on Netlify/Vercel:

```
SENTRY_ORG=<your org slug>
SENTRY_PROJECT=<that project's slug>
SENTRY_AUTH_TOKEN=<the token>
```

**Do not prefix these with `NEXT_PUBLIC_`.** That would ship the auth token to
every browser, and unlike the DSN it grants read access to your Sentry org.

Maps are uploaded to Sentry and hidden from the browser (`hideSourceMaps: true`),
so your bundle stays unreadable to anyone poking at it.

---

## 4. Turn on the spend cap and an alert

Two settings in Sentry, both worth doing on day one.

**Spend cap.** The free plan has no overage billing — it simply stops accepting
events at 5,000 and you are blind for the rest of the month *without being told*.
Set an alert at ~80% consumption (Settings → Subscription → usage alerts) so you
find out before that happens rather than after.

**One issue alert.** Default "notify on a new issue" to your email. During a beta
with ~120 users the volume is low enough that per-issue email is genuinely useful,
and it means you hear about a bug from Sentry rather than from a tester who has
already given up.

---

## 5. Verify it works

Deploy, then force one error per app and confirm it lands.

- Frontends: open the browser console on the deployed site and run
  `Sentry.captureMessage('smoke test')` — or simply visit a URL that throws.
- APIs: hit any endpoint in a way that 500s.

Check for the event in Sentry. If nothing arrives, in order of likelihood:

1. **DSN not set on the deployed environment** (set locally only, or set at build
   time when it is needed at runtime).
2. **CSP blocking the request.** The frontends add the Sentry ingest origin to
   `connect-src`, derived from the DSN — so if the DSN is absent at *build* time
   the directive is missing even when the DSN is present at runtime. Set
   `NEXT_PUBLIC_SENTRY_DSN` for the build too. A `Refused to connect` line in the
   console is this.
3. **Ad blocker.** Reports are tunnelled through `/monitoring` on your own origin
   specifically to survive this, but a very aggressive blocker can still catch it.
   Test in a clean profile before concluding anything.

---

## What is filtered, and why

The free plan gives 5,000 events a month **shared across all four projects**. One
recurring error can consume that in a day. So the configuration is deliberately
aggressive about what it does *not* send.

**Dropped entirely** — none of these are bugs in our code:

- Network aborts (`AbortError`, `Failed to fetch`) — fire whenever a user
  navigates mid-request, which on an image-heavy listings page is constant
- Browser extension and injected-wallet errors, which throw inside our pages
- `ResizeObserver loop` warnings — benign, Chrome-only
- Next.js internal redirect/not-found signals
- **Validation failures.** A rejected form is the system working. The consumer
  API now logs these at Warning rather than Error specifically so they stay
  visible in logs without becoming Sentry events.

**Not enabled**, because both are metered separately and neither answers a
question we currently have:

- Performance tracing (`tracesSampleRate: 0`)
- Session Replay — also refused on privacy grounds: it records the DOM, which on
  this product means capturing a national ID number as it is typed

---

## What is scrubbed

This matters more than usual here. The apps handle national ID numbers, KYC
documents and JWTs, and under NDPA sending those to a third-party processor is a
reportable problem, not an embarrassment.

`sendDefaultPii` is **false** everywhere — no IPs, cookies or bodies attached
automatically. On top of that, both stacks redact:

- Headers: `Authorization`, `Cookie`, `Set-Cookie`, `X-Worker-Secret`, `X-Api-Key`
- Any key containing `token`, `password`, `secret`, `apikey`, `nationalid`,
  `iddocument`, `bvn`, `nin`
- **`x-amz-*` and `signature`** — presigned S3 URLs. The signature in one of those
  *is* the credential: a captured link grants whoever holds it read access to
  somebody's identity document for the URL's lifetime. This is the leak most
  likely to happen by accident, because those URLs travel as ordinary request URLs.
- Request bodies (`MaxRequestBodySize.None`, plus `request.Data = null` as a
  second line of defence)
- Local variables captured in a stack frame — the least obvious leak, since a
  handler that had the password in scope attaches it automatically

The frontends additionally strip `#token=` from the URL, because the Google
callback delivers the JWT in the fragment and Sentry reads `window.location` from
inside the page — it would otherwise ship the token that moving to a fragment was
meant to protect.

**If you add a field carrying anything sensitive, add its name to
`SENSITIVE_KEYS` / `SensitiveKeyFragments`.** Matching is by substring, so
`nationalIdNumber` and `national_id` are both caught by `nationalid`, but a new
name like `passportNo` would not be.

---

## One implementation note worth knowing

Almost every service in the APIs catches its own exceptions, logs them, and
returns a failed `BaseResponse`. Nothing propagates to the pipeline. An
out-of-the-box Sentry install that only captures *unhandled* exceptions would
therefore have reported a near-empty stream while the app quietly failed.

So `MinimumEventLevel` is bound to `LogError`, making the `_logger.LogError(ex,
...)` calls the codebase already contains the reporting mechanism. If someone
later raises that threshold to `Critical`, monitoring switches off across the
whole application — which is why it is set explicitly with a comment rather than
left to a default.
