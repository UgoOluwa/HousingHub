---
description: Trigger or diagnose a scheduled worker endpoint
argument-hint: "[verification-expiry|inspection-reminders]"
---

Worker: `$1` (default `verification-expiry` if not given).

Background in `docs/scheduled-workers.md`. The endpoints live on the **admin**
API at `POST /api/Internal/{worker}/run`, gated by an `X-Worker-Secret` header
and rate limited to 10 requests / 5 minutes per IP.

**To run it:** trigger the `Scheduled workers` workflow in GitHub Actions with
`workflow_dispatch`, selecting the worker. Do not run it from here — that would
need the production secret in this session.

**To diagnose a failure, read the status code first. It narrows things sharply.**

| Code | Meaning |
|---|---|
| **401** | Reached the controller; the secret did not match. Routing is fine. |
| **403** | Usually the URL is missing the stage path (`/admin`). |
| **429** | Rate limited. Unlikely on a schedule; possible when testing in a loop. |
| **500** | An exception escaped. Check Sentry. |

For a **401**, look in CloudWatch for `Internal worker call rejected: {Reason}`.
It names the shape of the mismatch — not configured, no header, whitespace only,
same length (a rotation applied in one place), or different lengths — without
printing either value. Compare against the length the workflow logs.

For a **500** shortly after a deploy, the most likely cause is a GSI still in
`CREATING` state: the schema initializer runs in the background, and querying a
non-ACTIVE index throws. It resolves itself once the index is built. Confirm in
Sentry that it is a `ResourceNotFoundException` naming the index before assuming
that is what happened.

A **200 with a non-zero `failed`** is still a problem. On the expiry side it
means somebody is showing a badge they are no longer entitled to; on the
reminder side it means a warning did not reach someone whose badge is about to
drop. Neither should be left in a log.
