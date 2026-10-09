# Tenancy lifecycle — from chosen candidate to rent collected

What happens after an inspection: the owner picks someone, documents are agreed and
signed, money moves, keys change hands, and the rent is tracked from then on.

This is Phases 5–7 of `transaction-lifecycle-plan.md` made concrete, plus the
selection step that turned out not to exist. Scoped in a conversation with someone
who manages property for landlords, so the shape comes from the people who would
use it.

---

## The correction that started this

The flow was described to me as: owners already choose who to move forward with,
then that person does identity verification, then the owner picks who gets the flat.

**None of that exists.** Inspections run `Pending → Confirmed → Completed` and stop
there. There is no shortlist, no selected candidate, no record linking a person to a
property beyond a booking. Everything below hangs off *"this person is the tenant for
this property"*, so that record is the first thing built.

---

## Decisions already made

Settled in conversation; recorded so they are not relitigated.

| Question | Decision |
|---|---|
| Who bears our commission | **The tenant**, shown itemised before they pay |
| Who bears the Paystack fee | **The tenant**, as its own line |
| Agent-managed properties | **We deal with the agent only.** One payee, no multi-split |
| Money held until handover | **No.** Pay → settle → notify → handover |
| Sales and leases over three years | **Out of scope for v1** |
| Offline signing | **In scope** — download, sign, re-upload |

### Why the commission falls on the tenant

It makes the split trivial. The owner receives rent and their own fees **in full**;
our cut is added on top and taken as a flat charge to our account. Nothing is
deducted from the owner's money, which is both easier to explain and much easier to
sell to a landlord who is already suspicious of platforms.

---

## The three constraints that shape everything

### 1. We must never hold the money

Taking rent into our account and paying the landlord out of it is holding customer
funds, which under CBN rules is Mobile Money Operator territory. **Paystack split
payments avoid it entirely**: the owner is a subaccount with their own bank details,
Paystack settles their share directly to their bank, our commission lands in ours.
The money never touches our balance.

The consequence to be honest about: *"hold it until the keys are handed over"* is
escrow, and it stays out of scope until there is a licensed partner product. v1 is
pay → settle → both notified → owner confirms handover.

### 2. Not everything can be signed electronically

Nigeria's Evidence Act 2011 recognises electronic signatures but **excludes land
instruments**.

| Term | Path |
|---|---|
| Tenancy ≤ 3 years | e-signature in app |
| Lease > 3 years, or a sale | executed physically, registered, stamped |

A single path that e-signs everything would generate **void instruments**, on our
platform, with our name on them. The path is chosen from the lease type and term,
not left to the user.

An unstamped agreement is inadmissible exactly when it matters, so stamping is
tracked even where we do not perform it.

### 3. The agent is the fraud surface

"We deal with the agent only" means routing a tenant's **entire annual rent** to an
agent on the strength of the agent's own claim to represent the owner. That is the
highest-value fraud in this market and we would be the rails for it.

**Before any payout account can be created, the agent must hold an approved
`LetterOfAuthorityToLet` and be `BusinessVerified`** — or the property itself must be
title-verified with the agent as lister. Both already exist in the verification
pipeline; this points them at a new gate. A hard precondition, not a warning.

---

## Why we build the signing rather than buying DocuSign

What makes a signature hold up is **attribution** — who signed — and **integrity** —
what they signed. We already own the expensive half: the signer's government ID has
been checked against their account by an admin.

Add a timestamp, the IP, the user agent, and a SHA-256 hash of the exact bytes
signed, stored immutably, and that is defensible for a short tenancy. DocuSign
charges per envelope for a wrapper around identity we already hold. Revisit it for
sales and long leases — which we are not e-signing anyway.

### The landlord supplies their own agreement in v1

**The moment we supply a template, a defect in it is our liability.** Doing signing
and storage over a document the owner uploaded is cheaper to build and leaves us a
conduit rather than an author. Templates reviewed by a Nigerian property lawyer come
later, and that review is not optional when they do.

---

## Build order

Four increments, each shippable.

### A — Selection *(done)*

Owner sees completed inspections for a property and picks a candidate. Creates the
`Tenancy` record; candidate notified in-app and by email. Property moves to
`UnderOffer`. Owner can withdraw; candidate can decline. Either returns the property
to `Available`.

The spine. Nothing else can proceed without it, and it is small.

### B — Documents and fees *(done, bar the stamped PDF)*

The owner composes one request: the compulsory agreement plus any number of
custom-named documents, each in one of three modes —

- **Upload** — the tenant supplies a document they already have
- **Sign in app** — e-signature, short tenancies only
- **Download, sign, re-upload** — also the path for anything that cannot be e-signed

— together with the named additional fees and what each covers. One email to the
tenant.

The tenant sees the whole picture, including every fee, **before** signing anything.
They complete each document; the owner reviews and either accepts or rejects with a
reason; rejection returns it to the tenant with that reason attached. The loop runs
until every document is accepted, at which point the set is sealed into the private
document store with its hashes.

This is structurally the `VerificationCase` pipeline with a different reviewer. Copy
the shape — the `TryX()` transitions, the sparse review-queue index — rather than
reusing the entity.

Built: the entities and their transitions, the service, the API, both consumer
screens, and a read-only staff view carrying the signature trail.

**Not built — an in-app signature produces no stamped PDF.** The artefact today is
the source document plus a signature record (time, address, device, hash), which is
defensible evidence but is not what somebody expects to download and send to a bank.
Until that exists, prefer download-sign-reupload for anything that may need to leave
the platform.

**Outstanding before this reaches real users:** a Nigerian property lawyer has to
review the signing — not the agreement template, which we do not supply, but the
audit trail and the words shown to a tenant immediately before they sign.

### C — Payment *(≈3–4 weeks)*

Owner or agent onboards a payout account: they enter bank and account number, we
resolve the real account name with Paystack and **refuse if it does not match their
verified identity**, then create the subaccount. One screen, once.

The tenant pays rent, the owner's fees, our commission and the processing fee in a
single transaction, itemised. Paystack splits it. Both sides get a receipt. The owner
confirms handover.

### D — Tenancy management *(≈2 weeks)*

Rent due dates, renewal reminders, the owner's portfolio view. Cheap **because** C
means we can see the payment — before that we would be asking people to tell us about
money we cannot observe.

---

## Data model

New entities. Names chosen so that `Tenancy` is the aggregate everything else hangs
off.

### `Tenancy`

The spine. One per property-plus-candidate, and **at most one live at a time per
property** — selecting a second candidate while one is in progress is refused.

Carries a **snapshot of the rent at selection**, not a reference to the listing's
current price. The listing can be edited afterwards, and what was agreed must not
move underneath either party.

`LandlordCustomerId` is whoever listed the property — owner or agent — captured at
selection. Per the decision above there is exactly one counterparty, and it is this
one.

### `TenancyStatus`

Lifecycle values in order, terminal values in their own band so a new stage can be
added without renumbering. **Persisted — never reuse or renumber.**

```
CandidateSelected   = 1
DocumentsRequested  = 2
DocumentsAccepted   = 3
AwaitingPayment     = 4
Active              = 5
Ended               = 6

Withdrawn           = 10   // owner pulled out
DeclinedByCandidate = 11   // candidate is no longer interested
```

Both terminal states exist so a tenancy cannot get stuck. A candidate who has moved
on must be able to say so, or the owner waits on someone who will never respond.

### Later increments

`TenancyDocument` (B) — name, mode, status, rejection reason, file key, signature
record. `TenancyFee` (B) — name, amount in kobo, description. `Payment` (C) gains
`PaymentPurpose.Rent` and a subaccount reference.

---

## What this reuses

| Need | Already built |
|---|---|
| Private document store, presigned reads | `IFileStorageService` |
| Submit → review → accept/reject-with-reason | `VerificationCase` — same shape |
| Email and in-app notification | `IEmailService`, `Notification` |
| Paystack rail, webhooks, idempotent settle, refunds | `PaymentService` |
| Identity verification | `Customer.IsKycVerified` |
| Agent's right to let | `LetterOfAuthorityToLet`, `BusinessVerified` |
| Listing state while a deal is in progress | `PropertyAvailability.UnderOffer` |

---

## Still open

- **A Nigerian property lawyer must review the agreement handling before B ships.**
  Not the template — we are not supplying one in v1 — but the signing, the audit
  trail, and the copy that tells a tenant what they are signing.
- **Commission rate.** 5% was the number discussed. On a ₦5m annual rent that is
  ₦250,000 on top of what the tenant already pays, which is the kind of number that
  loses a deal at the last step. Worth modelling a cap before it ships.
- **Instalments.** Annual upfront is the Nigerian norm but six-monthly exists. v1
  assumes one payment; splitting it later changes C.
- **Stamping.** Tracked, not performed. Who does it and when needs an answer before
  any agreement is relied on.
