# Business verification — what exists and how it works

End-to-end walkthrough of Phase 2A + 2B as built. Written so you can follow the
journey without reading the code.

---

## The one-paragraph version

An agent or developer opens a verification case, uploads their CAC certificate
and any supporting documents, and submits it. The case appears in an admin queue,
oldest first. A reviewer claims it, views each document through a link that
expires in ten minutes, approves or rejects each one with a reason, and then
decides the case. On approval the agent's account is marked business-verified,
their RC number is recorded from the certificate the reviewer actually saw, and
they get an email and an in-app notification. On rejection they get the reviewer's
reason and can fix it and resubmit.

---

## The journey, step by step

### 1. The agent opens a case

`POST /api/v1/Verification/cases` with `{ "subjectType": 1 }` (Business).

The subject is **always the authenticated caller** for a business case. The
request body has a `subjectId` field, and for business it is deliberately
ignored — honouring it would let somebody open a case whose approval lands a
badge on a different account.

If they already have a draft open, they get that one back rather than a second
one. Someone who navigates away mid-upload and returns finds their documents
where they left them.

### 2. They attach documents

`POST /api/v1/Verification/cases/{caseId}/documents` — multipart, one document
per call, with metadata alongside the file.

The metadata is a set of claims by the applicant, not extracted from the file:

| Field | What it is |
|---|---|
| `documentType` | CAC certificate, LASRERA permit, tax clearance… |
| `documentNumber` | RC or BN number, permit number |
| `nameOnDocument` | **The important one.** Who the document says it belongs to |
| `issuingAuthority` | "CAC", "LASRERA" |
| `issuedAt` / `expiresAt` | LASRERA registrations lapse annually |

Each file is checked by magic bytes rather than extension, so renaming something
to `.pdf` will not get it accepted. JPEG, PNG, WebP and PDF, up to 15MB —
certificate scans run large.

Files go to a **private S3 prefix**, and only the object key is stored. No URL is
ever persisted. This is the same pattern KYC was moved to, and for the same
reason: these are company records and title deeds.

Documents can only be added or removed while the case is a **Draft**. That
boundary matters — see step 4.

### 3. They submit

`PUT /api/v1/Verification/cases/{caseId}/submit`

Submission is refused unless the required documents are present. For business the
requirement is just the **CAC certificate** — everything else is corroboration.
That is deliberate: requiring tax clearance up front would stall every applicant
on the single hardest document to obtain in this market.

If something is missing, the error names it — "Please attach the following before
submitting: CAC certificate of incorporation" — rather than failing generically.
A refusal the user cannot act on is a support ticket.

### 4. Ownership passes to review

On submit, the case moves `Draft → Submitted` and the applicant loses the ability
to change it. The set of documents is now the thing being reviewed; letting it
change underneath the reviewer would mean approving a case whose contents differ
from the one they read.

The case also enters the review queue at this moment, via a **sparse index** that
only contains cases awaiting a decision. That means the queue costs what the
outstanding work costs, rather than getting slower every month as decided cases
accumulate.

### 5. The reviewer picks it up

**Admin → Verification.** Oldest first, with a filter for Business or Property
title. Each row shows how long it has been waiting, and anything past three days
is flagged amber — roughly the point where an applicant starts assuming they have
been forgotten.

Clicking **Claim this case** moves it to `UnderReview` and records who claimed
it, so two admins do not review the same submission in parallel. A second admin
attempting to claim it is refused.

### 6. Reviewing each document

For each document the reviewer sees the declared metadata, a **View** button, and
two signals they did not have to ask for.

**View** mints a presigned URL valid for **ten minutes** and opens it in a new
tab. The URL is never stored in the page — it is a bearer credential, and anyone
holding it can read the document until it expires.

**Name match.** The name on the document compared to the account holder:

| Result | Meaning |
|---|---|
| Exact | Same name, ignoring case, order, honorifics, corporate suffixes |
| Partial | One contains the other — usually a missing middle name |
| **None** | No meaningful overlap. **Escalate.** |
| Unknown | Could not compare — a side was blank, or the name is entirely generic |

This is the check that catches the fraud that actually happens. Forged documents
are rare; *real documents belonging to somebody else* are common — a genuine CAC
certificate submitted by a person with no connection to it.

It **reports rather than decides**, because Nigerian names legitimately vary
between documents: a middle name on one and not the other, an initial, a maiden
name, a diacritic dropped by a registry that only accepts ASCII. Auto-rejecting
on a string mismatch would decline honest applicants at a high rate.

Note that **Unknown is styled neutrally, not as a warning**. It is a data gap, not
a red flag, and treating it as one would bury real mismatches in noise.

**CAC lookup.** Currently reports *"not checked automatically — verify against
the certificate by hand"*, because no provider account exists yet. It says that
rather than showing a green tick, because a stub that returned "passed" would
manufacture assurance nobody checked.

When a provider is connected the same panel will show found / not found and the
registered name. Even then it stays **advisory**: registry data is inconsistent
enough that a failed lookup is often a stale record rather than a bad applicant,
and a *passing* lookup only confirms the number exists — not that the submitter
has any connection to that company.

Each document is then approved, or rejected **with a required reason** the
applicant will see.

### 7. Deciding the case

Three outcomes:

- **Approve** — disabled until every document is reviewed and none is rejected.
  A badge resting on evidence nobody looked at is worse than no badge, because
  somebody will rely on it. The button explains which condition is unmet.
- **Reject** — reason required.
- **Escalate — name mismatch** — reason required. Held apart from rejection
  because it is the strongest signal of attempted impersonation in the flow, and
  it should be visible as its own outcome rather than buried among ordinary
  rejections.

### 8. What approval actually does

This is the step that turns a decision into a badge:

- `Customer.BusinessVerificationTier` → `BusinessVerified`
- `BusinessVerifiedAt` → the decision timestamp
- `BusinessVerificationExpiresAt` → **the earliest expiry among the approved
  documents**. A verification is only as current as its shortest-lived evidence,
  and LASRERA permits are annual.
- `CacNumber` and `LasreraPermitNumber` → lifted from the approved documents, so
  the profile cannot display a number nobody checked.

Two properties of this worth knowing:

**These fields are written only here.** No user-facing update path touches them.
A profile edit that could set your own verification tier is a profile edit that
grants badges.

**Always read `IsBusinessVerified`, never the tier directly.** An expired
verification keeps its tier until something sweeps it, and a badge shown on a
lapsed LASRERA permit is a claim you cannot support. `IsBusinessVerified` checks
the expiry.

**Rejection changes nothing about the subject.** Someone whose second submission
fails should not lose the badge their first one earned.

### 9. The applicant is told

Approve and reject both send an email *and* create an in-app notification. Both
channels, because after a multi-day review most people are not on the site — email
reaches them, and the notification is what survives a spam filter.

Notifications fire **after** the decision is saved and are best-effort. A mail
outage must not make a completed review report as a failure — that was the bug
the KYC path had, where an admin saw an error for an approval that had actually
gone through and would then try again.

**Escalation notifies nobody, deliberately.** Telling a suspected impersonator
which check caught them teaches them what to fix. A human decides what to say.

---

## What is not built yet

- **CAC API lookup.** The interface and the deferring implementation exist;
  connecting Dojah, QoreID or Mono is one class and one line in
  `ConfigureServices`.
- **The applicant's own UI.** The consumer endpoints all exist and work, but
  Housing-Hub-FE has no screens for starting a case or uploading documents. Right
  now only the admin side is reachable through a UI.
- **Payment.** Verification is free at the moment. The charging model is settled
  (see `transaction-lifecycle-plan.md` §2.2) but needs the payment rail first.
- **Expiry sweep.** `TryExpire` exists on the entity but nothing calls it on a
  schedule. Until something does, a lapsed verification keeps its tier — which is
  exactly why `IsBusinessVerified` checks the date rather than trusting it.
- **Badges in the consumer app.** `IsBusinessVerified` is not rendered anywhere
  yet. Same gap the identity badge had before it was fixed: the data exists and
  nobody sees it.

---

## Before this can run

**Two DynamoDB tables need creating**, with their indexes. `DynamoDbTableInitializer`
only creates tables that are entirely absent, and it is now behind
`Dynamo:AutoCreateTables` which is false in deployed environments — so these must
be created directly.

`VerificationCases` — hash key `Id`, three GSIs, all `ProjectionType.ALL`:

- `SubjectId-index` on `SubjectId`
- `SubmittedByCustomerId-index` on `SubmittedByCustomerId`
- `ReviewQueueStatus-index` on `ReviewQueueStatus` *(sparse — this is the review queue)*

`VerificationDocuments` — hash key `Id`, one GSI:

- `VerificationCaseId-index` on `VerificationCaseId`

All attributes are string type (`S`) for indexing purposes.

Without these, every verification call fails.
