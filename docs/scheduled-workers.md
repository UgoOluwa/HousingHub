# Scheduled workers — how they run

Some work is not triggered by a user: reminding people about tomorrow's
inspection, expiring a verification whose documents lapsed. Those live as
endpoints on the Admin API under `/api/Internal/`, secured by a shared secret
header rather than a JWT, because a scheduler has no user session.

**Endpoints are not schedules.** Something has to call them.

---

## The state of things

**Until now, nothing did.** The inspection reminder endpoint has existed for some
time and has never been invoked — no reminder has ever been sent. The
verification expiry sweep would have had the same fate.

That failure mode is worth naming, because it is quiet: an endpoint nobody calls
looks exactly like a working feature from inside the codebase, and nobody goes
looking for a feature they believe already exists.

`.github/workflows/scheduled-workers.yml` now calls both.

---

## What runs, and when

| Worker | Schedule | Endpoint |
|---|---|---|
| Inspection reminders | Every 30 min | `POST /api/Internal/inspection-reminders/run` |
| Verification maintenance | Daily, 06:07 UTC | `POST /api/Internal/verification-expiry/run` |

Both are idempotent. Inspections are reminded once each; a case already expired
is skipped; a reminder threshold already sent is not sent again. So a duplicate
run, a retry, or a manual trigger during testing are all harmless.

The verification job does two things in one call — it expires what has lapsed,
then warns what is about to. They share an index and belong to the same job, and
expiring first means a case that lapsed overnight is expired rather than sent a
"expires in 7 days" warning it has already outlived.

---

## Setting it up

Two repository secrets, under **Settings → Secrets and variables → Actions**:

| Secret | Value |
|---|---|
| `ADMIN_API_URL` | Admin API base **including the stage path**, e.g. `https://3tgjb2crdf.execute-api.af-south-1.amazonaws.com/admin` |
| `INTERNAL_WORKER_SECRET` | Must match `Internal__WorkerSecret` on the Admin Lambda |

Then trigger it manually once — **Actions → Scheduled workers → Run workflow** —
and confirm you get a 200 rather than a 401. Do this before waiting for a
schedule; a mismatched secret otherwise fails silently at 6am for a week.

**The secret travels in a header, never a query string.** A URL would be written
into API Gateway access logs in plaintext, and this secret gates a SuperAdmin
promotion endpoint.

**Rotating `Internal:WorkerSecret` breaks these workers.** Update the GitHub
secret in the same change, or the next run 401s.

---

## Limitations you should know about

GitHub's scheduler is best-effort, and these matter more than they first appear:

- **Runs can be delayed** by minutes to over an hour at busy times. The cron
  expressions use odd minutes (`:07`, `:37`) rather than the top of the hour,
  which is the most congested slot. Fine for a daily sweep; not fine for anything
  needing precise timing.
- **Scheduled workflows are disabled after 60 days without a commit** to the
  repository. If development goes quiet, the workers stop — and nothing tells
  you. This is the strongest argument for moving to EventBridge before the
  product is live and unattended.
- **A missed run is not made up.** The next scheduled run picks up the backlog,
  which is fine here because everything is idempotent.

---

## Moving to EventBridge Scheduler

Better long-term: it lives beside the Lambda, retries properly, and does not
depend on repository activity.

It needs two pieces of AWS infrastructure that do not exist yet:

1. **An EventBridge Connection** holding the worker secret as an API-key
   credential, with header name `X-Worker-Secret`. The connection stores it in
   Secrets Manager rather than in the schedule.
2. **An API Destination** pointing at the endpoint, using that connection.

Then a schedule per worker targeting the destination.

```bash
aws events create-connection \
  --name housinghub-internal-worker \
  --authorization-type API_KEY \
  --auth-parameters 'ApiKeyAuthParameters={ApiKeyName=X-Worker-Secret,ApiKeyValue=<secret>}'

aws events create-api-destination \
  --name housinghub-verification-expiry \
  --connection-arn <connection-arn> \
  --invocation-endpoint 'https://<id>.execute-api.af-south-1.amazonaws.com/admin/api/Internal/verification-expiry/run' \
  --http-method POST

aws scheduler create-schedule \
  --name housinghub-verification-expiry \
  --schedule-expression 'cron(7 6 * * ? *)' \
  --flexible-time-window 'Mode=FLEXIBLE,MaximumWindowInMinutes=15' \
  --target '{"Arn":"<api-destination-arn>","RoleArn":"<scheduler-role-arn>"}'
```

The scheduler role needs `events:InvokeApiDestination` on the destination.

Note the **rate limit**: the internal endpoints allow 10 requests per 5 minutes
per IP. EventBridge retries count against that, so keep retry attempts low.

When you move, delete the cron triggers from the workflow but keep
`workflow_dispatch` — a manual trigger is genuinely useful for catching up after
an outage.

---

## Watching them

The daily verification run returns counts:

```json
{
  "expiry":    { "examined": 12, "expired": 3, "tiersRevoked": 3, "failed": 0 },
  "reminders": { "examined": 12, "sent": 2, "failed": 0 }
}
```

**Non-zero `failed` is the number to care about.** On the expiry side it means
somebody is still showing a badge they are no longer entitled to. On the reminder
side it means a warning did not reach someone whose badge is about to drop. The
workflow raises an annotation when it sees one, and the underlying error is logged
at `Error` level so Sentry picks it up.

One failure never aborts a run — the cases after it would otherwise keep their
badges too — so the count is how you find out anything went wrong at all.
