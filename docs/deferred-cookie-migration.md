# Deferred: refresh tokens to HttpOnly cookies

**Status: blocked on infrastructure, not on code.**

Refresh tokens currently live in `localStorage` in both frontends, readable by any
XSS. Moving them to `HttpOnly` cookies is the right end state. It cannot be done
safely until the API is served from a subdomain of the app's domain.

---

## Why it's blocked

| | Current |
|---|---|
| App | `housinghub.ng` (Netlify) |
| Consumer API | `pk1wr06fr1.execute-api.af-south-1.amazonaws.com` |
| Admin API | `3tgjb2crdf.execute-api.af-south-1.amazonaws.com` |

Different registrable domains, so every API call is **cross-site** to the browser.
A cookie only travels cross-site with `SameSite=None; Secure`, and that has two
consequences that make it a net downgrade:

1. **Safari and Firefox block it.** Safari's ITP has blocked third-party cookies by
   default for years; Firefox's ETP does the same; Chrome is phasing them out.
   Users on those browsers would be unable to stay signed in. This is a
   functional outage, not a theoretical risk.
2. **SameSite CSRF protection disappears**, so CSRF tokens become mandatory on
   every state-changing request — more surface, for a security position no better
   than what already shipped.

## What shipped instead

The revocation work in the same workstream captures most of the risk reduction
without the domain dependency:

- `POST /api/v1/Auth/logout` and `POST /api/AdminAuth/logout` revoke the token
  server-side. Previously "logging out" only cleared client state.
- Password reset and change revoke the whole token family.
- Customer suspension and admin deactivation revoke the token family.
- `RefreshToken` rejects inactive accounts.

A stolen refresh token is still usable until one of those fires, which is the gap
cookies would close. But the window is now bounded by an action the user or an
admin can actually take, rather than running the full 30 days unconditionally.

---

## Unblocking it

**Move the APIs behind the app's domain:**

- `api.housinghub.ng` → consumer API
- `admin-api.housinghub.ng` → admin API

Both share the registrable domain `housinghub.ng`, so `SameSite=Lax` cookies are
sent normally, no third-party cookie blocking applies, and CSRF protection stays
intact. Typically a custom domain on API Gateway plus an ACM certificate and a
DNS record.

An earlier branch already pointed the frontend at `api.housinghub.ng`, so this
may already be planned.

**The alternative** is enabling the existing Next.js proxy
(`NEXT_PUBLIC_ENABLE_PROXY=true`, rewrite already present in `next.config.ts`),
which makes calls same-origin. It works, but routes every API call through a
Netlify function — added latency and cost on every request, to solve a problem
DNS solves once.

---

## What the migration involves, once unblocked

Roughly a day, mostly frontend:

1. **Backend** — set the refresh token as a cookie on login, refresh and Google
   sign-in: `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/api/v1/Auth`, expiry
   matching the token. Read it from `Request.Cookies` in the refresh and logout
   endpoints, falling back to the request body during the transition so old
   clients keep working. Clear it on logout.
2. **Frontend** — set `withCredentials: true` on both axios clients; stop storing
   `refreshToken` in the auth store; drop it from the refresh and logout call
   bodies. The single-flight refresh logic in `apiClient.ts` stays as-is.
3. **CORS** — `AllowCredentials()` is already set on both APIs, and the origin
   allow-lists are already explicit rather than wildcard, so no change needed.
4. **Transition** — keep the body fallback for one release so sessions created
   before the change survive the deploy, then remove it.

## Test checklist for that day

- Login, hard refresh, confirm session survives
- Let the access token expire, confirm silent refresh works
- Logout, confirm the cookie is cleared and the token is revoked server-side
- Both flows in Safari specifically, since that's the browser this whole
  constraint exists for
- Concurrent tabs, to confirm single-flight refresh still holds
- Admin app end to end, which has its own cookie scope
