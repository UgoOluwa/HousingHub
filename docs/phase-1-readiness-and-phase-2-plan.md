# Where we are, and what Phase 2 should be

Re-assessment of all three repos after the security and performance work.
Supersedes Part 2 of `product-strategy.md`. August 2026.

---

## The short version

**Phase 1 as you've scoped it — a listing platform with manually verified
customer IDs — is the right first phase, and the code is close.** Closer than
the last assessment: seven of the eight original blockers are fixed.

You are not ready this week, but what's left is mostly *operational* rather than
engineering. Six of the eight remaining items are things only you can do in the
AWS console or in a terminal. Two are code, and both are the same kind of
problem: **the ID verification you're planning to do by hand isn't actually
wired into anything.** More on that below, because it's the finding that
matters.

---

## Part 1 — The original blockers

| # | Blocker | Status |
|---|---|---|
| 1 | Anyone can register as Admin | **Fixed** — validated at all three entry points |
| 2 | Any user can read everyone's PII and KYC documents | **Fixed** — admin-only, plus self-or-admin checks returning 404 |
| 3 | Any user can delete any account | **Fixed** |
| 4 | KYC documents in the public bucket | **Fixed in code** — private prefix, presigned URLs. Bucket policy is still yours to apply |
| 5 | Committed secrets | **Purged from the code. NOT ROTATED.** Still live in git history |
| 6 | FAQ falsely claims escrow | **Fixed** — now states plainly that Housing Hub does not process payments or hold funds |
| 7 | Admin UI inside the consumer app | **Fixed** — directory deleted, no orphaned routes or calls remain |
| 8 | No DynamoDB backups | **Unverified** — I can't see your AWS console |

And from the should-fix list: rate limiting is in, the register → verify-email
dead-end is fixed, search works, the property-type filter sends the right shape,
the hardcoded listing data is gone, and every link in the UI now resolves to a
route that exists.

Unchanged: SignalR is still disabled on Lambda so chat is 5–10s polling (works,
just costs more at concurrency), and there are still **no controller tests, no
authorization tests, and no frontend tests at all**. 505 backend unit tests, and
none of them exercise the layer where blockers 1–3 lived.

---

## Part 2 — Two code gaps that undercut Phase 1 specifically

These are new findings, and they matter more than anything else in this document
because they go directly at what Phase 1 *is*.

### 2.1 KYC is enforced in the browser, not on the server

`AddPropertyForm.tsx:71` redirects a user away from the add-property page if
`isKycVerified` is false. That's the only check.

`PropertyCommandService.CreateProperty` verifies the caller's `CustomerType` can
manage properties. It never looks at `IsKycVerified`. A direct
`POST /api/v1/Property` from anyone with an owner or agent account creates a live
listing with no identity verification whatsoever — no unusual tooling needed,
just the request the app itself makes.

Your entire Phase 1 proposition is "we check who these people are." Right now
that check is a redirect, and a redirect is a UX affordance, not a control. It
fails open.

**Fix:** an explicit `IsKycVerified` check in `CreateProperty`, alongside the
existing `CanManageProperties()` call. Small change — but decide first whether
the rule is "verified to *create*" or "verified to *publish*". Letting people
draft a listing while their ID is in review, and gating only the publish step, is
kinder to your supply side and gives you a queue of pending listings to work
through. I'd suggest gating publish.

The admin-on-behalf-of path needs the same treatment applied to the target owner,
not the acting admin.

### 2.2 You'd do the verification work and show nobody

`PropertyDto.IsVerified` is populated, returned by the API, and rendered by
precisely nothing. No component in the consumer frontend reads it. The same is
true of the owner's KYC status — a renter looking at a listing cannot tell
whether the person behind it has been verified.

So in Phase 1 as currently built, you'd manually review every ID, mark people
verified, and the renter — the person whose trust you're trying to earn — would
see no difference at all.

**Fix:** a verified badge on the listing card, the listing detail page, and the
owner's public profile. This is frontend work, it's not hard, and without it the
manual verification is unpaid admin.

One caution carried over from the earlier work: **get the wording reviewed before
it ships.** "ID Verified" is a defensible claim about a document you looked at.
"Verified Owner" is a claim about property rights you have not checked, and a
defrauded buyer will point at that badge. Phase 1 verifies a person's identity —
say exactly that and nothing more.

---

## Part 3 — Readiness verdict

**Ready for a closed, hand-supervised beta once the list below is clear. Not
ready for open signup.**

The distinction matters. With ~20 owners and ~100 renters you know by name, you
can absorb a bug. With open signup you cannot, and you have no monitoring to tell
you a bug happened.

### Must be done first — yours, not mine

1. **Rotate every secret** in `SECRET-ROTATION-REQUIRED.md`. They are in git
   history and remain compromised no matter what the current files say. This is
   the single most important item on this page.
2. **Audit the admin table** for accounts you don't recognise. The worker secret
   that gated SuperAdmin promotion shipped as a committed placeholder.
3. **Enable point-in-time recovery** on every DynamoDB table. One console
   setting. Without it, any data-loss incident is permanent.
4. **Apply the S3 bucket policy** denying anonymous `GetObject` on `private/*`
   (runbook in `s3-bucket-policy-runbook.md`). Until this is applied, KYC
   documents are private in name only.
5. **Backfill `Customer.IsActive`** — every existing row currently reads as
   suspended.
6. **Migrate legacy KYC documents** out of the public `kyc/` prefix.

### Must be done first — code

7. **Build and test.** `dotnet build && dotnet test`, `npm ci && npm run build`
   in both frontends. None of the recent work has been compiled — there is no
   dotnet in my environment. I caught two type errors by reading; I would not bet
   on having caught all of them.
8. **Smoke-test the CSP** with the browser console open. It's now enforcing, and
   an enforcing CSP fails as a dead button rather than an error.
9. **The two gaps in Part 2** — server-side KYC gate, and surfacing verification
   to renters.

### Strongly recommended before real users

10. **Error monitoring.** You asked me to leave this, and I did. It's now the
    largest operational gap. Sentry on both frontends and the two APIs is an
    afternoon. Without it, a beta tester hits an error, doesn't report it, quietly
    leaves, and you learn nothing. That's the failure mode a beta exists to
    prevent.
11. **Authorization tests.** Every blocker that made the original list was an
    authorization bug, and that layer still has zero coverage. A dozen tests
    asserting "customer A cannot touch customer B's property" would have caught
    most of them, and will catch the next one.

---

## Part 4 — Phase 2: verifying owners and agents

`verification-design.md` already has the regulatory research, the document lists
per party type, and a proposed data model. Don't re-derive it. What follows is
sequencing and the decisions I'd make differently now.

### The organising principle

**Build one document pipeline, not three.**

You will collect documents for business verification, then for title
verification, then — in Phase 5 — for financial verification. If each gets its
own bespoke upload form, review screen and status flag, you'll build the same
thing three times and end up with three inconsistent review queues.

Model it once: a **verification case** (who or what is being verified, what kind
of check, current state) holding **documents** (type, storage key, per-document
review state, reviewer, timestamp, rejection reason). Business verification is
the first consumer of that pipeline. Financial verification is the third. The
work you do in 2A is the work you don't repeat in Phase 5 — which is what makes
the eventual financial layer cheap instead of another ground-up build.

This is also why the current `IsKycVerified` boolean on `Customer` should stop
growing siblings. Adding `IsBusinessVerified`, `IsTitleVerified`,
`IsIncomeVerified` as flags will work for about six months and then become
unworkable, because none of them can express "submitted", "under review",
"rejected, here's why", "expired, re-verify" — all states you will need.

### 2A — Document intake and review (~2 weeks)

The generic pipeline, with no new verification types yet.

- `VerificationCase` and `VerificationDocument` entities, per the design doc's
  section 7. Everything private-prefixed in S3 with presigned access only, the
  pattern KYC already uses.
- Reuse `UploadedFileValidator` — magic-byte checking already exists and already
  handles the formats you'll get.
- One admin review queue that works for any case type: see the documents, approve
  or reject per document, approve or reject the case, always with a reason.
- Migrate the existing customer KYC onto it, so there's one queue rather than the
  KYC screen plus a new one.
- Email and in-app notification on every state change. Note that KYC decisions
  currently send email but create no in-app notification, and the email send
  isn't individually guarded — a Resend outage makes an already-saved approval
  report as a failure. Fix both while you're in there.

Ship this and Phase 1's manual ID verification runs on it too. That's the point.

### 2B — Business verification for agents and developers (~2–3 weeks)

- Document intake per `verification-design.md` §3: CAC certificate and status
  report, TIN, ESVARBON or NIESV registration for estate surveyors, LASRERA
  registration for anyone operating in Lagos.
- **CAC number lookup via API.** This is the one genuinely solved automation —
  Dojah, QoreID and Mono all offer it. Verify the RC number resolves to a real
  company and that the name matches what was submitted.
- Everything else is human review against the uploaded document. Nigerian
  registries are inconsistent enough that every automated check needs a manual
  override, and the reviewer's decision — not the API's — should be what's
  recorded.
- Directors named on the CAC record should be checkable against the account
  holder's already-verified ID. That link is where most impersonation gets caught,
  and it's the reason to do 2B after Phase 1's ID work rather than instead of it.

### 2C — Property title verification (~3–4 weeks)

Slower, and deliberately last of the three, because it is the highest-stakes
claim you will ever make and the least automatable.

- Documents per §4: Certificate of Occupancy, Deed of Assignment, Governor's
  Consent, survey plan.
- Neither the LASRERA register nor Lagos eGIS exposes an API. This is human
  review, and for anything high-value it should involve a lawyer, not an ops
  reviewer.
- The nuance from §101 of the design doc is the one to get right in the schema:
  **verification attaches to the property, not to the person**, and a person can
  legitimately list a property they don't own — an agent acting for an owner, or
  a tenant subletting. Model the *relationship* between person and property as
  its own verified thing. Getting this wrong means rebuilding.

### 2D — Tiers, not a badge (~1 week)

Ship graduated levels — ID verified / business verified / title verified — rather
than one binary badge. Supply can join at the lowest tier and climb, which means
verification becomes the thing that earns search placement rather than a wall in
front of listing at all. It also lets you say something precise and defensible at
each level instead of one vague claim that has to cover everything.

**Legal review of the wording for every tier before any of it ships.**

### What Phase 2 should deliberately not include

Financial verification. It's a Phase 5 concern, sequenced with lending, and it
only makes sense once there's a licensed partner who will actually underwrite
something. Building affordability checks with nobody to consume the output is
work with no user.

The pipeline you build in 2A is what makes it cheap when the time comes: bank
statements and income documents become another case type on machinery that
already exists, plus a Mono integration for the open-banking pull.

### Still true from the earlier strategy work

**Payments come before financing, and there is still no payment integration of
any kind.** That remains the true blocker on everything downstream — escrow, rent
collection, savings — and it's a smaller lift than any of the licensed products.
It doesn't compete with Phase 2; the two are independent and could run in
parallel if you have the capacity.

---

## Suggested order

1. The six operational items in Part 3 — mostly console work, do them this week
2. Build, test, smoke-test the CSP
3. Server-side KYC gate + verified badge (the two Part 2 gaps)
4. Sentry
5. A dozen authorization tests
6. **Closed beta opens** — 20 owners, 100 renters, Lagos, everything verified by
   hand
7. Phase 2A while the beta runs — the document pipeline
8. 2B business verification, 2C title verification, 2D tiers
9. Payments, in parallel if capacity allows

Items 1–5 are days, not weeks. The beta is genuinely close.
