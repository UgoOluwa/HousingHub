# Payments — Phase 3

Phase 3 of `transaction-lifecycle-plan.md`. **Built and tested end to end — rail,
checkout, receipts, refunds and the admin view. Nothing is charged yet.** `Payments:Enabled` is `false`, and while it is
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
| A refund needs a real reason, and refuses "n/a" | Six months on, the question is why money left the account. |
| A refund sends back what **arrived**, not what was asked | On a flagged payment those differ, which is the main reason to refund. |
| The payment is claimed before the provider is called | Two admins clicking at once would otherwise send two refunds. |
| A refused refund releases the claim | Otherwise the payment sits forever pending a refund nobody accepted. |
| A refunded payment stops satisfying the submission gate | Money returned is not money paid. |
| A refund webhook resolves by `transaction_reference` | Reading `reference` would find the refund's own id and silently drop the event. |
| A flagged payment sends no receipt | Nothing was handed over; a receipt would say otherwise. |

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
5. **Finish the operator details.** The terms and privacy policy have been
   rewritten for a platform that charges — section 3 covers fees, non-refundability
   and refunds, and both pages now name the operating company as the party you
   contract with and the data controller. What is still missing is
   `Housing-Hub-FE/src/lib/operator.ts`: **the CAC registration number and
   registered address are blank.** Both render only when set, so nothing shows a
   placeholder to users, but a published agreement should carry them.

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

## Receipts

Sent on settlement, after the payment is saved and never before it. The payment is
the record; the receipt is a courtesy, and a failing mail provider must not cost
somebody the thing they paid for. `SendAsync` swallows its own failures so this
cannot throw into the webhook path.

A flagged payment sends **no** receipt — nothing was handed over, so a receipt
saying the request is with the review team would be false.

The identity line appears only when it was charged, which makes the receipt the
answer to "why was this more than the advertised price".

## Refunds

The only action in the system that moves money out, and the only write staff have
over a payment. There is deliberately no endpoint to mark one successful, unflag
one, or edit an amount — each would grant a paid service with no money moving, and
the row would be indistinguishable from the genuine thing.

**SuperAdmin only** (`SuperAdminOnly` policy), reason required, minimum ten
characters. The reason is recorded against the payment with the admin's id and is
sent both to the payer and to Paystack's own refund record, so all three agree
about why the money went back.

Three things it is careful about:

- **The amount is the provider's, not ours.** The service re-verifies the charge and
  refunds what actually arrived. On a flagged payment those differ by definition —
  that is what "flagged" means — and refunding our own figure would send back an
  amount nobody paid. An admin cannot choose the figure; partial refunds go through
  Paystack's dashboard.
- **The payment is claimed before the provider is contacted.** Two admins clicking
  at once would otherwise each see a refundable payment and each send a refund. The
  cost is the opposite failure — a payment left pending a refund that was never
  accepted — which `TryAbandonRefund` recovers, and which is the safer of the two
  because no money moved twice.
- **A refunded payment is no longer settled.** `IsSettled` is true only for
  `Successful`, so a refunded verification fee stops satisfying the submission gate
  without the gate needing to know refunds exist.

### When a refund fails after being accepted

The worst state this system can reach quietly: somebody was told their money was
coming back, and hours later — long after the admin who pressed the button stopped
watching — it did not arrive.

It is therefore **flagged**, not quietly restored. The payment goes to `Flagged`
with a note naming the amount still owed, which puts it straight into the admin
queue through the sparse index, and it stays refundable so retrying is the obvious
next action. The attempt is deliberately kept — who asked, why, when, for how much —
because clearing it would leave a flagged payment with no explanation of how it got
that way.

It is also still logged at error level, which is what reaches Sentry
(`MinimumEventLevel = LogLevel.Error`). The queue tells whoever handles money; the
log tells whoever handles the system. They are not the same person and both need to
know.

A **synchronous** refusal is different and does not flag: no money moved, and the
admin who asked is looking at the error. Filling the queue with those would make it
noise.

`refund.processed` and `refund.failed` webhooks finish the job. A refund issued
directly in Paystack's dashboard is handled too — it arrives having never passed
through this application, which is why `TryCompleteRefund` accepts a merely
successful payment. A **failed** refund logs at error level and says the payer is
still owed the money; a flagged payment returns to the admin queue, but one that was
merely successful does not, so that log line is the only signal. Worth an alert.

Refund events are taken at their signed word rather than re-verified, unlike
charges. The risk runs the other way: accepting a forged charge would hand over a
paid service, whereas accepting a forged refund would tell somebody their money is
coming back when it is not. The signature stops both, and there is no entitlement
being granted to re-verify against.

## Consumer payment history

`/payments`, in the account sidebar. Shown to an owner, who can be charged today,
and to anyone who has actually paid — so a renter who paid once can still find the
receipt without every renter carrying a permanently empty section.

A pending attempt keeps its gateway link, so somebody who closed the tab can finish
rather than start again — the server hands back the same attempt, so it is not a
second charge.

## Not built

- **Partial refunds.** Deliberate — Paystack's dashboard does them, recorded
  against the transaction. Building it here would mean an admin choosing an amount,
  which is the one thing this design keeps out of their hands.
- **Paystack IP allowlisting.** The signature check is the real control; the
  allowlist is cheap defence in depth.
- **Affordability and the renter-side bundle.** Phase 4.
