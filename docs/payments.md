# Payments — Phase 3

Phase 3 of `transaction-lifecycle-plan.md`. **The rail, the checkout and the admin
view are built and tested; nothing is charged yet.** `Payments:Enabled` is `false`, and while it is
false every existing flow behaves exactly as it did.

Provider is **Paystack**, chosen over Flutterwave for one reason that matters more
than features: Paystack signs each webhook with an HMAC over the request body, so
a receiver can prove the body is untampered. Flutterwave sends a static shared
secret in a header, which proves the sender knows a secret and says nothing about
what they sent.

---

## Scope, and the hard line

**Fees owed to Housing Hub. Never custody of anybody else's money.**

Holding a renter's first rent — even for 48 hours — is escrow, which under CBN
rules is Mobile Money Operator territory. It must run on a licensed partner's
product and must never touch the `Payments` table. See part 1.4 of the lifecycle
plan. Phase 6 is where that arrives, licence-dependent, and it is a different
system.

What can be charged for today is verification, because that is what exists. The
renter side — affordability at shortlist — is Phase 4.

---

## The bundling rule

From the lifecycle plan, and implemented in `PaymentService.TryPrice`:

> **Identity is bundled into the first paid verification you need, and never
> charged again.**

- Agent or developer → bundled with business verification
- Owner → bundled with property verification
- Renter → bundled with the affordability check *(Phase 4, not built)*

Someone verified as a renter who later lists a property pays only for the property
verification. The identity line is kept as its own figure on the payment rather
than folded into the total, so a receipt can say what was actually bought and a
payer who has been charged once can see they were not charged twice.

`PaymentPurpose.IdentityVerification` also exists as a purpose in its own right,
because the plan requires that buying it directly costs the same as buying it
bundled.

---

## The flow

1. Client asks for a quote — `GET /api/v1/Payment/verification-cases/{id}/quote`.
   The price is shown **before** anyone is asked to pay.
2. Client initialises — `POST /api/v1/Payment/verification-cases/{id}`. The server
   prices it, writes a `Payment` row, calls Paystack, returns an authorisation URL.
3. Payer pays on Paystack.
4. Paystack posts to `POST /api/payments/webhook`. Signature checked, transaction
   re-read from Paystack, amount compared, payment settled.
5. Client calls the existing `POST /api/v1/Verification/cases/{id}/submit`, which
   now finds a settled payment and lets the case through.

**Step 5 is the client's call, not a side effect of step 4.** That keeps one code
path moving a case out of Draft, and avoids a circular dependency between the
payment and verification services. The consequence: a payer who closes the tab
after paying leaves a paid draft. It is self-healing — they come back, click
submit, and it goes through, because the gate only asks whether a settled payment
exists.

**A paid case that was never submitted is discoverable**, not lost: a settled
payment whose subject is still a Draft. Worth a report before beta.

---

## What the tests cover, and why they exist

61 tests. Every one is a way a payment integration loses money or gives something
away, and most cannot be arranged against a provider sandbox — you cannot ask
Paystack to confirm the wrong amount or to redeliver a webhook on demand.

| Guarantee | Why it matters |
|---|---|
| A forged or wrongly keyed signature is rejected | The webhook endpoint is anonymous by necessity. This check *is* the authentication. |
| A genuine signature over an **edited** body is rejected | The shape of "same webhook, bigger amount". |
| A truncated but correctly prefixing signature is rejected | The shape of a length-confusion bug. |
| No secret configured → everything rejected | A misconfigured environment must not settle whatever it is sent. |
| Comparison is fixed-time | An early-returning comparison leaks, through timing, how much of a guessed signature was right. |
| A redelivered webhook settles **once** | Providers retry by design, and this is also what makes a captured-and-replayed body inert. |
| A confirmed payment for the **wrong amount** is flagged, not settled | Settling on the gateway's figure lets a payer choose the price. |
| A gateway that cannot be reached asks for redelivery | Returning success would drop a real payment on the floor. |
| A missing price refuses rather than charging zero | Otherwise a missing config entry gives the item away, silently, while health checks stay green. |
| A double-clicked initialise reuses the attempt in flight | Otherwise it is a double charge. |
| A callback URL on an untrusted origin is dropped | Unchecked, it is an open redirect at the moment the payer expects a receipt. |
| A settled payment stops offering its gateway link | Otherwise it invites paying twice. |

The callback URL is validated against `Cors:AllowedOrigins` — that list already
answers "is this one of our front ends", it is required in production, and a
second list would drift from it. Scheme, host **and** port must match; a prefix
comparison would admit `housinghub.example.attacker.example`.

---

## Turning it on

Nothing here can be done from the codebase.

1. **Open a Paystack account** and complete business verification on it. Test keys
   work immediately; live keys need the account approved.
2. **Set the secret key** — `Payments__Paystack__SecretKey` on the consumer Lambda,
   per environment. It is both the API credential and the webhook signing key, so
   leaking it means both "someone can charge as us" and "someone can forge a
   settlement". Never commit it; never log it.
3. **Set the webhook URL** in the Paystack dashboard to
   `https://<api-host>/api/payments/webhook`. Deliberately unversioned — a version
   in that URL would mean shipping v2 silently stops settling payments, with the
   only symptom being that people who paid never get what they bought.
4. **Agree the prices** and set them as **whole numbers of kobo**:
   `Payments__Fees__IdentityVerification`, `__BusinessVerification`,
   `__PropertyVerification`. `500000` is five thousand naira. A price written as
   naira with a decimal point is a rounding bug waiting to happen.
5. **Rewrite the terms first.** `Housing-Hub-FE/src/app/terms/page.tsx` currently
   says Housing Hub *"is not a payment processor. We do not currently process
   payments or hold funds"* — and then tells users to **treat any request to pay
   Housing Hub as fraudulent and report it.** That is correct today and exactly
   backwards the moment this flag flips. It also needs to name the legal entity
   that is being paid; the page currently contracts as "Housing Hub", which is not
   a legal person. **Do this before step 6, not after.**

6. **State the refund position in the copy before payment.** The provider bills us
   whether the applicant passes or not, and UK referencing companies treat fees as
   non-refundable because the work was performed. Whatever is chosen, it has to be
   said in plain words up front — chargebacks on a young merchant account cost out
   of all proportion to the amount disputed.
7. **Set `Payments__Enabled=true`.** Dev first. Walk a full case end to end against
   test keys before production.

### The new table

`Payments` is added to `DynamoDbTableInitializer`, so it is created on next start
with four indexes — `Reference-index` (a webhook arrives knowing nothing else),
`CustomerId-index`, `SubjectId-index` (asked on every submission, so it must not be
a scan), and `FlagWatch-index`, which is sparse: only flagged payments carry the
attribute, so the admin queue reads exactly the rows needing a person and does not
grow with successful ones.

**Enable PITR and deletion protection on `prod_Payments` once it exists.** The
other sixteen production tables have both; a new one does not inherit them. This
is the table where "we lost a row" means "somebody paid and got nothing".

---

## Front end

### Consumer — checkout on the verification request

`VerificationCheckout` is the primary action on a draft request, and **which
action it is comes from the server, not from a build-time flag.** The quote
reports whether charging is on at all and whether this case is already paid for,
so the same component renders a free environment and a paid one.

The price is broken down before payment, with the identity check as its own line —
shown only when it is actually being charged, so somebody who was verified last
year sees no identity line rather than a total they cannot account for.

**Returning from the gateway is treated as a hint, not as proof.** Paystack
appends `?reference=` to the callback URL, and the payer controls that redirect, so
the component asks the server about the payment and polls until the server says the
signed webhook has settled it. Polling stops on any settled state and gives up
after two minutes with a message that says what is true — the payment may still
complete — rather than an error implying it failed.

Once settled it submits the case automatically, guarded by a ref so a poll tick
cannot fire a second submission.

`Flagged` is deliberately **not** worded as a failure. Money may have left the
payer's account, and telling them it failed would be wrong in the direction that
loses trust, at the worst possible moment.

### Admin — the flagged queue

`/admin/payments`. Read-only, matching the API: an admin who could mark a payment
successful could grant paid services with no money moving, and the row would be
indistinguishable from a real settlement.

**"Needs checking" is the first tab, and the count is in the navbar**, polled every
minute. That is the whole reason this screen exists — before it, a flagged payment
appeared only in a log line, which is nowhere anybody looks. It is also the only
queue here where nobody noticing costs a *customer* money rather than us.

Each flagged row shows the reason in full on its own line, and both references —
ours, which appears in our logs and the payer's receipt, and Paystack's, which is
what their dashboard search accepts. Reconciling means looking it up there, so both
are one click to copy.

The general list reads the whole table and pages in memory; the flagged queue reads
a sparse index. Fine at this volume — past a few thousand payments the general list
wants a date-bucketed index and a cursor API.

---

## Not built

- **Consumer payment history.** `GET /api/v1/Payment/mine` exists and the hook is
  written, but no page lists a payer's receipts. Small, and worth having before
  charging starts — a payer who cannot find what they paid emails support.
- **Settlement email.** A payer gets no confirmation. The receipt data is all on
  the payment row.
- **Refunds.** No endpoint. Paystack's dashboard refunds manually, which is the
  right answer at this volume, and it records against the transaction that exists.
- **Paystack IP allowlisting.** The signature check is the real control; the
  allowlist is cheap defence in depth.
- **Affordability and the renter-side bundle.** Phase 4.
