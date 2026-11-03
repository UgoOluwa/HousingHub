# The letting flow, as built

Written 3 November 2026 from the code, not from memory. Every stage below names
the thing that actually enforces it, so a claim here can be checked.

---

## The chain

```
   register ──▶ browse ──▶ book a viewing ──▶ owner confirms
                                                    │
                        24h reminder ◀──────────────┤
                                                    ▼
                                         the viewing happens
                                                    │
                           renter marks it done ────┤──── or nobody does,
                           (+ optional note)        │     and it closes after
                                                    │     one chase + 7 days
                                                    ▼
                          owner sees everyone who viewed
                                   and picks one
                                        │
                                        ▼
                     rent is snapshotted · listing goes under offer
                                        │
                                        ▼
                 owner composes documents + fees ──▶ sends once
                                        │
                      ┌─────────────────┼─────────────────┐
                      ▼                 ▼                 ▼
                 tenant uploads    signs in app    prints, signs,
                                   (short lets)     re-uploads
                      └─────────────────┼─────────────────┘
                                        ▼
                        owner accepts each, or returns it
                             with a reason (loops)
                                        │
                                        ▼
                            every document accepted
                                        │
                                        ▼
                    tenant pays rent + fees + 5% + card fee
                     Paystack splits it · owner paid direct
                                        │
                                        ▼
                             ███ the let is running ███
```

Everything above this line exists and has been walked end to end against a real
Paystack transaction. Nothing below it does.

---

## Where identity verification actually bites

It is a real pipeline — documents, admin review, tiers, expiry — but it is
**enforced in exactly three places**, all of them on the owner's side:

| Gate | Where |
|---|---|
| Publishing a listing | `PropertyCommandService.SetPublishedAsync` |
| Adding a payout account | `PayoutAccountService.LoadVerifiedOwnerAsync` |
| Paying for business or title verification | `PaymentService` bundles the identity fee |

**A renter is never required to verify their identity.** Not to register, not to
book a viewing, not to be chosen, not to sign, not to pay. The owner is *shown*
whether a candidate is verified — `TenancyCandidateDto.IsIdentityVerified` — and
decides for themselves.

That may well be the right trade for getting renters through the door. It is worth
knowing it is a trade, because:

- **The FAQ says otherwise.** `FaqController` tells users *"Verification is required
  before you can book an inspection or list a property."* The second half is true.
  The first half is not enforced anywhere.
- An owner choosing an unverified candidate is agreeing to let property to somebody
  whose identity nobody checked.

## Where financial checks bite

**Nowhere. They do not exist.**

There is no affordability check, no income evidence, no credit reference, no
guarantor record, no employment verification. The nearest thing is that an owner
may *request* an employment letter as one of the tenancy documents — free text,
their wording, uploaded by the tenant, and read by nobody but them. We neither
verify it nor record what it said.

So the sequence "identity verification and financial checks, then documentation and
payment" describes two gates, one of which is partial and one of which is absent.

---

## What stops at "the let is running"

`TenancyStatus.Active` is the last state anything writes. `Ended` exists in the
enum and nothing sets it.

Not built, in rough order of how soon each is missed:

- **Rent after the first.** One payment, once. No schedule, no due dates, no
  reminders, no arrears.
- **Renewal.** No term end, so nothing can expire or be renewed.
- **Any management at all** — maintenance requests, inspections during the term,
  a landlord's portfolio view of several lets at once.
- **Ending a tenancy**, amicably or otherwise.

The payment rail is the part that makes the rest cheap: subsequent rent is the same
split against the same subaccount, so recurring collection is a scheduler and a
screen rather than a new integration.
