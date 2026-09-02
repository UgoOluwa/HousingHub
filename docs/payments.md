# Payments — Phase 3

Phase 3 of `transaction-lifecycle-plan.md`. **The backend rail is built and
tested; nothing is charged yet.** `Payments:Enabled` is `false`, and while it is
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
5. **State the refund position in the copy before payment.** The provider bills us
   whether the applicant passes or not, and UK referencing companies treat fees as
   non-refundable because the work was performed. Whatever is chosen, it has to be
   said in plain words up front — chargebacks on a young merchant account cost out
   of all proportion to the amount disputed.
6. **Set `Payments__Enabled=true`.** Dev first. Walk a full case end to end against
   test keys before production.

### The new table

`Payments` is added to `DynamoDbTableInitializer`, so it is created on next start
with three indexes — `Reference-index` (a webhook arrives knowing nothing else),
`CustomerId-index`, and `SubjectId-index` (asked on every submission, so it must
not be a scan).

**Enable PITR and deletion protection on `prod_Payments` once it exists.** The
other sixteen production tables have both; a new one does not inherit them. This
is the table where "we lost a row" means "somebody paid and got nothing".

---

## Not built

- **Front-end checkout.** No UI calls any of this yet. Next piece of work.
- **Notification on settlement.** A payer gets no email confirming payment. Worth
  having before beta; the receipt data is all on the payment row.
- **Refunds.** No endpoint. Paystack's dashboard can refund manually, which is the
  right first answer at this volume.
- **Admin visibility.** No admin endpoint lists payments or shows flagged ones. A
  `Flagged` payment currently surfaces only as a logged error — it needs somewhere
  a person will actually look before charging starts.
- **Paystack IP allowlisting.** Paystack publishes the addresses its webhooks come
  from. The signature check is the real control, but the allowlist is cheap
  defence in depth.
- **Affordability and the renter-side bundle.** Phase 4.
