# Separating dev and production

**Decisions taken:** one AWS account with prefixed resources · `develop` → dev,
`master` → production · custom domain on production only.

**Goal:** the environment you have today keeps running as dev/staging, unchanged
and with its data intact. A second, empty environment becomes production.

---

## What you actually have today

One environment, wearing two hats.

| Thing | Today | Notes |
|---|---|---|
| Consumer Lambda | `HousingHub-API` | deployed from `master` |
| Admin Lambda | `HousingHub-Admin-API` | deployed from `master` |
| API Gateway stage | `/dev` and `/admin` | **hardcoded in `Program.cs`** |
| DynamoDB | `Customers`, `Properties`, … | **hardcoded in `[DynamoDBTable]`** |
| S3 | `housinghub-files-dev` | config-driven ✅ |
| FE | Vercel | `netlify.toml` still in the repo — stale |
| Admin UI | Vercel | |
| Sentry | one project per API | `Environment` says `production` — wrong today |

Two of those are the real work. Everything else is a variable with a new value.

---

## The two hard blockers

### Blocker 1 — table names are compile-time constants

```csharp
[DynamoDBTable("Customers")]   // Customer.cs
[DynamoDBTable("Properties")]  // Property.cs
```

Sixteen of these. They are attributes, so they cannot be changed per
environment at runtime — which means today both environments would read and
write the *same tables*. This is the single thing that makes prod not exist yet.

**The fix is one line per API**, not sixteen file edits. The AWS SDK supports a
context-level prefix:

```csharp
builder.Services.AddSingleton<IDynamoDBContext>(sp =>
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    return new DynamoDBContextBuilder()
        .WithDynamoDBClient(() => client)
        .ConfigureContext(c =>
        {
            // Prepended to every [DynamoDBTable] name. Empty in dev, so the
            // existing tables keep their existing names and no data moves.
            c.TableNamePrefix = builder.Configuration["Dynamo:TablePrefix"] ?? "";
        })
        .Build();
});
```

**Leave the prefix empty in dev.** Setting `dev_` there would orphan every row
you already have. Production gets `prod_`, dev gets nothing. It is asymmetric
and slightly ugly, and it is much better than a data migration you did not need.

`DynamoDbTableInitializer` needs the same prefix — it calls `ListTablesAsync`
and `CreateTableAsync` with raw names from `TableDefinitions`, so without this
it would create unprefixed tables in prod and then the context would never find
them. Inject `IConfiguration`, prefix at the point of use, keep the dictionary
keys as they are.

### Blocker 2 — the stage path is hardcoded

```csharp
if (isLambda) app.UsePathBase("/dev");        // HousingHub.API/Program.cs:301
if (isLambda) app.UsePathBase("/admin");      // Admin.API/Program.cs:204
```

`"/dev"` is a *stage name* being used as a path. If the production stage is
called anything else, every route 404s. Make it configuration:

```csharp
var pathBase = builder.Configuration["Api:PathBase"];
if (isLambda && !string.IsNullOrEmpty(pathBase)) app.UsePathBase(pathBase);
```

Dev sets `Api__PathBase=/dev`, prod sets `/prod`. This is also the root of the
CSP bug from last month — the stage path leaking into places that expected an
origin. Worth removing the hardcode even if you never add prod.

---

## Everything else that differs, and whether it already works

| Setting | Config-driven? | Dev value | Prod value |
|---|---|---|---|
| `Dynamo:TablePrefix` | ❌ **new** | *(empty)* | `prod_` |
| `Api:PathBase` | ❌ **new** | `/dev` | `/prod` |
| `AWS:S3:BucketName` | ✅ | `housinghub-files-dev` | `housinghub-files-prod` |
| `Cors:AllowedOrigins` | ✅ | Netlify/Vercel preview URLs | `https://housinghub.ng` |
| `Jwt:Secret` / `AdminJwt:Secret` | ✅ | existing | **freshly generated** |
| `Internal:WorkerSecret` | ✅ | existing | **freshly generated** |
| `Email:BaseUrl` / `AdminBaseUrl` | ✅ | dev URLs | prod URLs |
| `Email:ResendApiKey` | ✅ | existing | separate key |
| `Sentry:Dsn` | ✅ | same project | same project |
| `Sentry:Environment` | ✅ | `development` ← **wrong today** | `production` |
| `Dynamo:UsePublishedIndex` | ✅ | `false` (needs backfill) | **`true` from day one** |
| `Verification:ShowTitleBadge` | ✅ | `false` | `false` until legal signs off |
| `Internal:EnableSuperAdminBootstrap` | ✅ | `false` | `true` for one deploy, then `false` |

Two rows there are quiet wins:

**`UsePublishedIndex` can be true in production immediately.** It is false in
dev only because existing rows predate the sparse index and would need
re-saving (`docs/data-backfill-required.md`). Production has no legacy rows, so
every published listing enters the index correctly from the first write. The
homepage reads an index instead of scanning the table, from day one.

**`Sentry:Environment` is currently `production` in the shared
`appsettings.json`**, so everything you have seen in Sentry so far is
mislabelled. Flip the base file to `development` and let the prod Lambda
override it.

### Frontend hardcodes

Both `next.config.ts` files bake dev into the build:

```ts
'https://pk1wr06fr1.execute-api.af-south-1.amazonaws.com'      // FE:30  fallback
const S3_ORIGIN = 'https://housinghub-files-dev.s3...'          // FE:52  CSP
hostname: 'housinghub-files-dev.s3...'                          // FE:141 next/image
```

Plus `src/services/apiClient.ts:25`, which falls back to the dev API when
`NEXT_PUBLIC_API_BASE_URL` is unset. Same four in Housing-Hub-Admin.

The S3 origin must become `NEXT_PUBLIC_S3_ORIGIN`, because it appears in both
the CSP (`img-src`, `media-src`) *and* `images.remotePatterns`. Miss either and
production shows no photos — and the CSP failure is silent in a way that reads
as "the images are broken" rather than "the policy is wrong".

**Make the API URL fallback fatal in production builds.** Right now an unset
variable on Vercel means the production site quietly serves dev data to real
users. A build that throws is a far better outcome:

```ts
const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL
  ?? (process.env.NODE_ENV === 'production'
        ? (() => { throw new Error('NEXT_PUBLIC_API_BASE_URL is required'); })()
        : 'http://localhost:5000');
```

---

## The IAM problem you are buying

One account is faster to set up and it costs you the boundary. Nothing except
IAM stops the dev Lambda writing to `prod_Customers`. So the execution roles
have to be resource-scoped — this is the part not to skip:

```json
{
  "Effect": "Allow",
  "Action": ["dynamodb:*"],
  "Resource": [
    "arn:aws:dynamodb:af-south-1:<acct>:table/prod_*",
    "arn:aws:dynamodb:af-south-1:<acct>:table/prod_*/index/*"
  ]
}
```

and the dev role gets a matching policy with an explicit **Deny** on
`table/prod_*`, because dev's tables are unprefixed and a wildcard would
otherwise cover them.

Same for S3: each role scoped to its own bucket ARN, no `s3:*` on `*`.

`ListTables` cannot be resource-scoped — it is account-wide. That is acceptable:
the initializer only needs to *see* the list, and `CreateTable`/`UpdateTable`
are still restricted to the prefix.

---

## Step by step

### Phase 0 — make the code environment-aware *(do this entirely in dev)*

Nothing here creates a production resource. Every change is a no-op against your
current setup, which is the point: you verify dev still works before prod
exists.

1. `Dynamo:TablePrefix` in both `Program.cs` files + `DynamoDbTableInitializer`.
   Default `""`.
2. `Api:PathBase` in both `Program.cs` files. Set `/dev` and `/admin` in the
   respective `appsettings.json` so behaviour is unchanged.
3. `Sentry:Environment` → `development` in both base `appsettings.json`.
4. `NEXT_PUBLIC_S3_ORIGIN` in both frontends — CSP, `remotePatterns`, and the
   `.env.example` files.
5. Remove the dev API fallbacks; throw on a production build.
6. Add `Dynamo:TablePrefix` and `Api:PathBase` to `RequiredSecrets.Validate`'s
   `otherRequired` list? **No** — an empty prefix is legitimate. But *do* add
   `Api:PathBase` guidance to `docs/setting-environment-variables.md`.

**Verify:** deploy to dev, confirm the site still loads, photos still render,
login still works, and the worker still returns 200. If any of that breaks, it
breaks now, against data you can afford to lose.

### Phase 1 — branch structure

```bash
git checkout -b develop master
git push -u origin develop
```

Then in GitHub settings: make `develop` the default branch, and protect
`master` (require a PR, no direct pushes). From here `develop` is where you
work, and merging to `master` is the act of releasing.

Do this in all three repos on the same day, or you will lose track of which repo
is on which model.

### Phase 2 — AWS production resources

Create, in this order:

1. **S3** `housinghub-files-prod`, same region. Apply the bucket policy from
   `docs/s3-bucket-policy-runbook.md` — the one that denies anonymous
   `s3:GetObject` on `private/*`. Do it now, before any KYC document exists,
   rather than after.
2. **IAM roles** `HousingHub-Lambda-Prod` and (revised)
   `HousingHub-Lambda-Dev`, scoped as above.
3. **Lambdas** `HousingHub-API-prod` and `HousingHub-Admin-API-prod`. Same
   runtime, memory and timeout as `aws-lambda-tools-defaults.json`.
4. **API Gateway** — a new REST API for each, stage `prod` and `admin`
   respectively. (Adding a `prod` stage to the *existing* API would share the
   throttle and WAF settings with dev; a separate API is cleaner and free.)
5. **Environment variables** on both prod Lambdas. Double underscore for
   nesting — `Dynamo__TablePrefix`, `Api__PathBase`, `AWS__S3__BucketName`,
   `Jwt__Secret`, and so on.
6. **DynamoDB: nothing.** `AutoCreateTables` is on, so the first prod cold start
   creates all sixteen `prod_` tables and their indexes. Expect this to take a
   few minutes and several invocations — DynamoDB builds one GSI per table at a
   time, so the schema converges over a handful of requests rather than one.
   Watch CloudWatch for `Creating DynamoDB table` lines.

Generate the prod secrets fresh — do not copy dev's. `openssl rand -base64 48`
for each of `Jwt:Secret`, `AdminJwt:Secret`, `Internal:WorkerSecret`. Note that
`docs/SECRET-ROTATION-REQUIRED.md` exists because dev's secrets were once
committed; production should start without inheriting that.

### Phase 3 — deploy pipeline

Create two **GitHub Environments** (`dev` and `production`) and move the secrets
into them, so the same secret *name* resolves to a different value depending on
which job is running. Put a required reviewer on `production`.

`deploy.yml` becomes:

```yaml
on:
  push:
    branches: [develop, master]

jobs:
  test: # unchanged

  deploy:
    needs: test
    environment: ${{ github.ref == 'refs/heads/master' && 'production' || 'dev' }}
    env:
      SUFFIX: ${{ github.ref == 'refs/heads/master' && '-prod' || '' }}
    steps:
      # ...
      - run: dotnet lambda deploy-function HousingHub-API${SUFFIX} --region ${{ env.AWS_REGION }}
        working-directory: src/HousingHub.API
      - run: dotnet lambda deploy-function HousingHub-Admin-API${SUFFIX} --region ${{ env.AWS_REGION }}
        working-directory: src/HousingHub.Admin.API
```

`deploy-function` updates code only — it does not touch environment variables —
so the values you set in Phase 2 survive every deploy. That is convenient now
and a trap later: a new setting added in code will be missing in AWS until
someone remembers. Add a line to the PR checklist.

`scheduled-workers.yml` needs the same treatment. It currently has one
`ADMIN_API_URL`. Point it at production (that is where the badges that matter
live), and add a `workflow_dispatch` input to target dev for testing.

### Phase 4 — frontends

Both apps are on Vercel, so both follow the same shape.

**First, delete `Housing-Hub-FE/netlify.toml`.** It is left over from an earlier
host and is not doing anything. It matters because it *looks* authoritative:
someone debugging a build later reads `command = "npm run build"` and
`publish = ".next"` and reasons about a platform the app has not been on for
some time. Two comments referencing "Netlify env vars" in `.env.example` and
`next.config.ts` should say Vercel for the same reason.

**Per project in Vercel → Settings → Environments:**

| Vercel environment | Git branch | `NEXT_PUBLIC_SENTRY_ENVIRONMENT` |
|---|---|---|
| Production | `master` | `production` |
| Preview | `develop` | `development` |

Set **Production Branch** to `master` under Settings → Git. By default Vercel
promotes the repository's default branch, and Phase 1 makes that `develop` —
so if you skip this step, `develop` starts deploying to your production domain.
Do it in the same sitting as Phase 1, not after.

Scope every variable to its environment rather than setting it globally. The
Production environment needs `NEXT_PUBLIC_API_BASE_URL` on the prod stage and
`NEXT_PUBLIC_S3_ORIGIN` on the prod bucket; Preview keeps today's dev values.

The three `SENTRY_*` build-time variables must be set in **both** environments —
they are read during `next build`, not at runtime, and a missing
`SENTRY_AUTH_TOKEN` silently ships without source maps. You then get events that
arrive but point at `main-8f3a.js:1:99213`, which is monitoring in name only.

**Preview deployments need a decision.** Vercel gives every branch and PR its
own URL, and each one is a new origin the API has never seen. Three ways out,
in order of preference:

1. Turn preview deploys off for anything but `develop`.
2. Give `develop` a stable alias domain and allow only that.
3. Allow the `*.vercel.app` wildcard — **don't.** `Cors:AllowedOrigins` is
   credentialed (`AllowCredentials`), so this makes anyone's Vercel project a
   trusted origin against your API.

Then add the deployed frontend origins to each API's `Cors:AllowedOrigins`. Same
reasoning as above: a stale dev origin left in the prod list is a real hole, not
untidiness.

### Phase 5 — third-party

- **Google OAuth.** Add the prod redirect URIs to the existing client, or make
  a second client. A second client is better: it means a leaked dev secret
  cannot mint prod sessions.
- **Resend.** Verify `housinghub.ng` as a sending domain. Use a *different*
  sender for dev — test emails bouncing off invented addresses damage the
  sending reputation of the domain your real users' mail depends on.
- **Sentry.** Same projects, distinguished by the `environment` tag. Free tier
  allows unlimited environments; a second project would double the quota
  pressure for no benefit. Set an alert rule filtered to
  `environment:production` so dev noise does not page you.

### Phase 6 — bootstrap and smoke test

1. Set `Internal__EnableSuperAdminBootstrap=true` on the prod admin Lambda.
2. Register the first admin, promote via
   `PUT /api/Internal/admins/promote`, set the flag back to `false`.
3. Walk the full journey against production with the browser console open:
   sign up, verify email, create a listing with photos and a video, publish
   (KYC gate should block until identity is verified), search from a signed-out
   browser, book an inspection, open a verification case, review it from the
   admin UI, approve, confirm the badge and the email.
4. Trigger both workers manually from Actions and confirm 200.
5. Confirm in the DynamoDB console that only `prod_*` tables gained rows.

That last check is the one that catches a missed prefix, and it is much easier
to fix on day one than after a week of mixed data.

---

## What this does not give you

Being clear about the boundary you chose:

- **A dev mistake can still reach prod data.** The only thing standing between
  them is an IAM policy you wrote by hand. Two accounts would make it
  physically impossible. If prod ever holds real KYC documents and tenancy
  agreements, revisit this — the migration is easier before there is data.
- **Configuration lives in the AWS console, not in git.** Fifteen environment
  variables per Lambda, set by hand, invisible to code review, and lost if the
  function is recreated. This is survivable at two environments and painful at
  three. Terraform or SAM is the answer when it starts hurting.
- **No production database backups configured.** DynamoDB point-in-time
  recovery is off by default and is a per-table setting. Turn it on for the
  `prod_*` tables once they exist — it is cheap and it is the difference between
  a bad afternoon and losing the business.

---

## Effort

| Phase | Work |
|---|---|
| 0 — code | Half a day. Mostly mechanical; the initializer prefix is the fiddly part. |
| 1 — branches | 20 minutes across three repos. |
| 2 — AWS | Half a day, most of it IAM. |
| 3 — pipeline | 2 hours. |
| 4 — frontends | 1 hour. |
| 5 — third-party | 2 hours, plus waiting on DNS for Resend. |
| 6 — smoke test | 2 hours if it goes well. |

Call it two focused days. Phase 0 is the only part with a real chance of
breaking what you have, which is why it lands in dev first and alone.
