# Secret rotation — action required

**Status: outstanding. Nothing here is fixed by the code changes.**

Secrets were removed from the repositories and the services now refuse to start
without them. That stops *future* leaks. It does not undo the existing one — every
value below is in git history and must be assumed compromised.

Deleting a file from `HEAD` does not remove it from history. Anyone with repository
access, past or present, can recover these with `git log -p`.

---

## 1. Consumer API JWT signing key — **highest priority**

- **Was:** `src/HousingHub.API/appsettings.Development.json` → `Jwt:Secret`
- **Exposure:** a real 64-hex-character HMAC signing key, committed.
- **Impact:** anyone holding it can forge a valid token for **any user**, including
  one with `customer_type: Admin`. Authentication is fully bypassable.

```bash
# Generate a replacement
openssl rand -hex 32
```

Set as `Jwt__Secret` in the API's environment (AWS Lambda env vars or Secrets
Manager). Note the **double underscore** — that is how .NET maps environment
variables to the `Jwt:Secret` configuration path.

**Rotating invalidates every issued access and refresh token.** All users are
signed out and must log in again. Do it during a low-traffic window.

---

## 2. Admin API JWT signing key

- **Was:** `src/HousingHub.Admin.API/appsettings.json` → `AdminJwt:Secret`
- **Exposure:** placeholder, but nothing validated it. If the production
  environment variable was ever missing or misnamed, the API booted signing admin
  tokens with the published placeholder.
- **Action:** generate as above, set `AdminJwt__Secret`.

**Verify whether this was actually the case in production.** If admin tokens were
ever signed with the placeholder, treat every admin session as compromised and
review the admin audit trail.

---

## 3. `Internal:WorkerSecret`

- **Was:** `src/HousingHub.Admin.API/appsettings.json`
- **Exposure:** shipped as the placeholder `"your-worker-secret..."`.
- **Impact:** this is the *only* gate on `PUT /api/Internal/admins/promote`, which
  grants **SuperAdmin** to any email. If production didn't override it, the
  committed placeholder was the password.
- **Action:** `openssl rand -hex 32`, set `Internal__WorkerSecret`.

Then **audit your admin table for unexpected SuperAdmins.**

---

## 4. `ADMIN_SEED_KEY`

- **Was:** `Housing-Hub-Admin/src/utils/run_seeder.js` and `scratch_customers.js`
- **Exposure:** real value, committed in plaintext, alongside the production API
  Gateway URLs it works against.
- **Impact:** gates `POST /admin/api/AdminAuth/create`, which is `[AllowAnonymous]`.
  With this key anyone could create a working admin account.
- **Action:** rotate the environment variable. **Then list all admin accounts and
  delete any you don't recognise.**

---

## 5. Account passwords in the seeder files

- **Was:** `src/utils/run_seeder.js`, `massiveSeeder.ts`, `allUserOwnerSeeder.ts`,
  `adminActivator.ts`, `scratch_customers.js`
- **Exposure:** plaintext passwords for admin, owner and customer accounts —
  including personal Gmail addresses — pointing at the live deployed API.
- **Action:** reset the password on every account referenced in those files. If any
  of those passwords is reused anywhere else personally, change it there too.

---

## 6. Bearer tokens in committed JSON

- **Was:** `src/utils/seed_output.json`, `seed_success.json`, `seed_final.json`,
  `seed_run_results.json`, `seed_error_debug.json`
- **Exposure:** live JWTs plus ~21 account email addresses.
- **Action:** superseded by rotating the signing keys (1 and 2) — that invalidates
  these tokens. No separate step, but confirm the key rotation actually happened.

---

## 7. Google OAuth client secret

- **Was:** placeholder in both appsettings files, so probably never leaked.
- **Action:** confirm the real value lives only in the environment. Rotate in the
  Google Cloud console if there is any doubt.

---

## Purging git history

Rotation is the fix; history rewriting is optional and disruptive. Once rotated,
the old values are inert.

If you rewrite anyway (`git filter-repo` or BFG), be aware it changes every commit
hash after the affected commits, requires a force-push, and every collaborator must
re-clone. **Rotate first regardless** — a rewrite alone does not help, because
clones and forks may already hold the old objects.

---

## Verifying the fix

After setting the environment variables, both services will refuse to start if any
required secret is missing or still looks like a placeholder. A failed boot with a
message listing the offending keys means the guard is working — supply the values
and restart.

Required environment variables:

**Consumer API**
```
Jwt__Secret
Email__ResendApiKey
Google__ClientSecret
```

**Admin API**
```
AdminJwt__Secret
Internal__WorkerSecret
Email__ResendApiKey
```

**Frontends** — see `.env.example` in each repo:
```
NEXT_PUBLIC_API_BASE_URL        (Housing-Hub-FE)
NEXT_PUBLIC_ADMIN_API_URL       (Housing-Hub-Admin)
```
