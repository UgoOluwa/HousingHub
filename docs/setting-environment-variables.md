# Setting the environment variables

Both APIs now refuse to start unless these are present and not placeholders. This
is where to put them.

Deployment is AWS Lambda in `af-south-1` — functions `HousingHub-API` and
`HousingHub-Admin-API` — deployed by GitHub Actions via `dotnet lambda
deploy-function`.

---

## Naming: use double underscores

.NET maps configuration paths to environment variables by replacing `:` with `__`
(two underscores). A single underscore does **not** work.

| Configuration path | Environment variable |
|---|---|
| `Jwt:Secret` | `Jwt__Secret` |
| `Google:ClientSecret` | `Google__ClientSecret` |
| `Email:ResendApiKey` | `Email__ResendApiKey` |
| `AdminJwt:Secret` | `AdminJwt__Secret` |
| `Internal:WorkerSecret` | `Internal__WorkerSecret` |

---

## What each function needs

**`HousingHub-API`**
```
Jwt__Secret              32+ random chars — signs customer access tokens
Google__ClientSecret     from Google Cloud console, OAuth 2.0 client
Email__ResendApiKey      from the Resend dashboard
```

**`HousingHub-Admin-API`**
```
AdminJwt__Secret         32+ random chars — MUST differ from Jwt__Secret
Internal__WorkerSecret   32+ random chars — gates SuperAdmin promotion
Email__ResendApiKey      same Resend key as above
```

`AdminJwt__Secret` being different from `Jwt__Secret` is the point of having two —
a forged customer token must not be accepted by the admin API.

Generate the three random ones:

```bash
openssl rand -hex 32
```

Run it separately for each. Do not reuse one value across all three.

---

## Where NOT to put them

- **`aws-lambda-tools-defaults.json`** — committed to the repo. This is exactly the
  problem the fail-fast validation exists to catch.
- **`appsettings.json` / `appsettings.Development.json`** — same reason. These are
  now intentionally empty.
- **GitHub Actions secrets** — the workflow deploys code; it does not inject runtime
  configuration. A secret there would not reach the running function. The one
  exception is `AWS_DEPLOY_ROLE_ARN`, which the workflow genuinely uses.

---

## Setting them

### Console

Lambda → the function → Configuration → Environment variables → Edit.

Fine for a one-off. Note that anyone with `lambda:GetFunctionConfiguration` can read
these values in plaintext, so keep Lambda console access tight.

### CLI — careful, this replaces the whole set

`update-function-configuration --environment` **overwrites every variable on the
function**, it does not merge. Passing one variable wipes the rest. Read, modify,
write:

```bash
REGION=af-south-1
FN=HousingHub-API

# Read what's there now
aws lambda get-function-configuration --function-name $FN --region $REGION \
  --query 'Environment.Variables' > /tmp/env-$FN.json
cat /tmp/env-$FN.json

# Edit /tmp/env-$FN.json to add the new keys, keeping everything already present,
# then write it back:
aws lambda update-function-configuration \
  --function-name $FN --region $REGION \
  --environment "Variables=$(python3 -c '
import json
print(json.dumps(json.load(open("/tmp/env-'"$FN"'.json"))))
')"
```

Or more simply, if you are confident about the full set:

```bash
aws lambda update-function-configuration \
  --function-name HousingHub-API --region af-south-1 \
  --environment 'Variables={Jwt__Secret=...,Google__ClientSecret=...,Email__ResendApiKey=...,ASPNETCORE_ENVIRONMENT=Production}'
```

**Keep `ASPNETCORE_ENVIRONMENT=Production` if it is already set.** Losing it makes
the app run as Development, which would serve the API docs publicly and load
`appsettings.Development.json`.

---

## Does deploying overwrite them?

`dotnet lambda deploy-function` without an `--environment-variables` argument
should leave existing variables alone, and the workflow does not pass one. But this
is worth *verifying rather than trusting* — after your next deploy, run:

```bash
aws lambda get-function-configuration \
  --function-name HousingHub-API --region af-south-1 \
  --query 'Environment.Variables' --output json
```

If the variables vanish after a deploy, add them to the deploy step explicitly, or
move to Secrets Manager (below), which is immune to this.

---

## Verifying it worked

The fail-fast check makes this easy: a misconfigured function will not start, and
the error names every offending key.

```bash
aws logs tail /aws/lambda/HousingHub-API --region af-south-1 --since 5m --follow
```

A successful start logs normally. A failure logs:

```
Refusing to start — required configuration is missing or unsafe:
  - Jwt:Secret is missing or is still a placeholder.
```

Then hit the health endpoint, which is anonymous:

```bash
curl -s -o /dev/null -w "%{http_code}\n" https://<your-api-host>/health
```

`200` means it booted with valid configuration.

---

## Worth doing later: Secrets Manager

Lambda environment variables are encrypted at rest, but readable in plaintext by
anyone with Lambda read access, and they are not rotatable or audited.

AWS Secrets Manager gives you rotation, CloudTrail audit of every read, and IAM
scoping separate from Lambda permissions. It needs a small code change — a
configuration provider that loads secrets at startup — and costs roughly $0.40 per
secret per month.

Not urgent at your stage. Worth doing before you hold payment credentials.

---

## Frontends

Different mechanism — these are build-time variables baked into the bundle, set in
the hosting provider.

**Housing-Hub-FE** (Netlify → Site configuration → Environment variables):
```
NEXT_PUBLIC_API_BASE_URL
NEXT_PUBLIC_GOOGLE_CLIENT_ID
```

**Housing-Hub-Admin** (whatever hosts it — the CORS allow-list mentions Vercel):
```
NEXT_PUBLIC_ADMIN_API_URL
```

Anything prefixed `NEXT_PUBLIC_` is **visible in the browser bundle**. That is fine
for these — an API URL and an OAuth client id are public by design — but never put
a real secret behind that prefix. `.env.example` in each repo lists what is needed.
