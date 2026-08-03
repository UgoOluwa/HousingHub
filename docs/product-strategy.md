# Housing Hub — Phasing, Beta Readiness & Strategic Assessment

Assessment of the proptech→fintech vision, benchmarked against HousingAnywhere
and Nigerian comparables, with a beta-readiness verdict on the current build.

August 2026.

---

## Part 1 — Does the vision fly?

**Short answer: the vision is sound, the sequencing needs work, and the current
build cannot go to beta this week.**

The thesis — that Nigerian property transactions fail on *trust*, and that a
platform owning verification end-to-end can capture the transaction — is
correct and well-evidenced. The EFCC attributes over 70% of Nigerian real
estate fraud to title problems. That is a real, expensive, unsolved problem.

Three things in the plan need challenging.

### 1.1 The fintech layer is a licensing problem, not an engineering problem

This is the single most important finding, and it reorders the roadmap.

Under CBN rules, **Mobile Money Operator is the only licence category permitted
to hold customer funds**. Escrow, savings-toward-rent, and wallet balances all
constitute holding customer funds.

Reported capital requirements conflict across sources — one puts MMO at ₦100m
paid-up plus ₦100m refundable escrow; another says it matches Switching and
Processing at ₦2bn. **Verify this with a fintech lawyer before planning around
either number.** Even at the low end it is a serious raise, and the escrow
deposit is separate money from the paid-up capital.

Lending is a second, distinct regime: a state Money Lender's licence plus FCCPC
registration for consumer protection.

2026 is also an enforcement year, not a grace period. Real-time AML monitoring
is mandatory. APP fraud liability can now fall on the platform *even when the
user authorised the payment*. NDPA/GAID data protection is being enforced. One
industry survey reports 87.5% of Nigerian fintechs say compliance cost is
actively limiting their ability to innovate.

**Implication:** you do not build the fintech layer. You *partner* into it.
Escrow via a licensed PSP's escrow product; rent financing via a licensed
lender who takes the credit risk; open banking data via Mono, which already
holds the licence. Housing Hub stays the origination and trust layer and earns
fees. That is achievable in months. Becoming a licensed deposit-holder is a
multi-year, capital-intensive detour that would consume the company.

### 1.2 HousingAnywhere is a useful model, but a partial one

Worth being precise about what transfers and what doesn't.

**What transfers — their payment protection mechanic.** The tenant pays the
first month's rent to HousingAnywhere. The platform holds it and releases to
the landlord **48 hours after move-in**, unless the tenant reports the place
doesn't match the listing. That is escrow's trust benefit with a very short
custody window. It is the single most copyable idea they have, and it maps
almost exactly onto your fraud problem.

**What transfers — their revenue model.** A one-time Tenant Protection fee of
**25–40% of the first month's rent (min €175)**, plus a landlord commission on
successful booking, plus a 2.5% service fee on subsequent payments. Note what
this is *not*: they make money on transaction facilitation, not on credit. They
explicitly **do not** guarantee monthly rent and **do not** offer damage
insurance. A mature player in a far better-capitalised market deliberately
stayed out of the credit business. Worth sitting with.

**What does not transfer — the core premise.** HousingAnywhere's FAQ says:
*"Can I visit the place before I rent? No, and you won't need to."* Their user
is a student or expat moving cities who *cannot* view. They are solving
distance.

You are solving *fraud*, for users who are local and for whom physical
inspection is the trust anchor. Your inspection flow is the right instinct and
is the opposite of theirs. Do not import their no-viewing model.

### 1.3 The affordability trap is the real strategic risk

Nigerian proptech has a documented pattern: platforms concentrate on high-end
properties because that's where the margin is, which means the affordability
problem they claim to solve goes unaddressed for most renters. Founders in the
space have said openly they won't list cheaper inventory because it doesn't pay.

Spleet, Kwaba and Rent Small Small have all attacked rent-instalments. Spleet
raised $2.6m in 2022 and offers Rent Now Pay Later up to ₦3m at ~3.5% monthly.
That is roughly 51% APR — which tells you what the credit risk in this market
actually costs.

So the fintech layer is *occupied*, and the economics are punishing. The
verification layer is comparatively *empty*. That asymmetry should drive your
sequencing: **verification is the differentiator; financing is a partnership.**

### 1.4 What I'd change about the plan

Your stated order is listing → verification → fintech. Two amendments:

- **Split verification.** Property/title verification is your moat and should
  come first. Tenant *financial* verification is really a prerequisite for
  lending — sequence it with the fintech phase, not ahead of it.
- **Insert payments before financing.** You cannot do escrow, rent collection
  or savings without a payment rail. There is currently no payment integration
  of any kind in the codebase. That is the true blocker on everything
  downstream, and it's a smaller lift than any of the licensed products.

---

## Part 2 — Beta readiness

**Verdict: not ready. Do not put real users on it yet. But the gap is days of
work, not months — the problems are concentrated, not diffuse.**

The build is further along than most pre-beta products: ~392 backend unit
tests, both frontends typecheck clean with zero errors, and the core journeys
are genuinely wired to real endpoints. The inspection flow in particular works
end to end. That is real work.

It is blocked by a small number of severe security defects.

### 2.1 Blockers — must fix before any real user

| # | Issue | Where |
|---|---|---|
| 1 | **Anyone can register as Admin.** `CustomerType` is taken from the request body and never validated. Register with `customerType: 8` → get an admin JWT → self-approve KYC. Defeats the entire trust model. | `AuthController.cs:42`, `RegisterAuthCommandValidator.cs:48-55` |
| 2 | **Any logged-in user can read every user's PII and KYC ID documents.** `GET /Customer/all` and `GET /Customer/{id}` have bare `[Authorize]` and return National ID numbers and ID document URLs. | `CustomerController.cs:46-56` |
| 3 | **Any logged-in user can delete any account.** No ownership check. | `CustomerController.cs:64-65` |
| 4 | **KYC ID documents in the same bucket as public property photos**, served as plain URLs, not presigned. No file-type or size validation on the KYC path. | `S3FileStorageService.cs:44`, `UploadKycDocumentCommandHandler.cs:31` |
| 5 | **Committed secrets**: a JWT signing key, the admin seed key, a worker secret that gates SuperAdmin promotion, plus seeder files containing live bearer tokens and account passwords. Must be **rotated**, not just deleted. | `appsettings.Development.json:9`, `Housing-Hub-Admin/src/utils/` |
| 6 | **The FAQ tells users their money is held in escrow. There is no payment integration at all.** A false consumer claim about custody of money. | `FaqController.cs:97-99` |
| 7 | **Admin UI inside the consumer app** with no role check — any logged-in customer can open `/admin` and reach live PII endpoints. | `Housing-Hub-FE/src/app/admin/` |
| 8 | **No DynamoDB backups or point-in-time recovery.** Combined with #3, a malicious delete is unrecoverable. | `AppDbContext.cs:117-125` |

Fix order, per the audit: validate `CustomerType` on register (one call to the
`IsSelectableAtOnboarding()` helper that already exists) → lock down the three
Customer endpoints → rotate secrets and purge seeders → move KYC to a private
bucket with presigned URLs → delete the consumer-app admin directory → rewrite
the payment copy → enable PITR.

The Admin API already implements the correct pattern: a `FallbackPolicy` that
denies by default with explicit `[AllowAnonymous]` opt-outs. Applying that same
pattern to the consumer API removes blockers 1–3 structurally.

### 2.2 Should-fix during beta

- **No error monitoring** anywhere — no Sentry, no APM. You'll be blind.
- **No rate limiting** on `/login`, `/register`, `/forgot-password` — open to
  credential stuffing and email-bombing your Resend quota.
- **Register → verify-email dead-ends.** The redirect omits `?email=`, so the
  resend button is permanently disabled. A user whose email doesn't arrive has
  no recovery path. This will be your most common support ticket.
- **Free-text search doesn't exist** — the UI reads `?q=` but nothing sets it.
- **Property-type filter silently returns nothing** — the FE sends the enum
  name, the API expects the integer.
- **Hardcoded data on the public property page**: every listing shows Property
  ID `SPH-12024`, "Listed Dec 1, 2024", and `4 bedrooms / 3 bathrooms`
  regardless of the actual property. Beta testers will notice immediately.
- Dead routes: `/kyc`, `/switch-account`, `/forgot-password` all 404 from links
  that exist in the UI.
- **SignalR is disabled on Lambda**; chat falls back to 5–10s polling. Works,
  but "real-time" is polling in production and costs scale with concurrency.
- No frontend tests, no controller or authorization tests on the backend —
  which is precisely the layer where blockers 1–3 live.

### 2.3 What genuinely works

Clean CQRS/MediatR architecture. Refresh-token rotation with single-flight
handling and replay detection — better than most production apps. The full
inspection lifecycle. Owner add → publish → listing. Real messaging and
notifications. CORS correctly restricted. ~392 meaningful unit tests with a CI
gate.

---

## Part 3 — Recommended phasing

### Phase 0 — Beta hardening *(1–2 weeks)*

The eight blockers above, plus Sentry, rate limiting, and the register→resend
dead-end. Nothing new gets built. **This is the only thing standing between you
and a closed beta.**

### Phase 1 — Closed beta *(4–6 weeks, runs concurrently with Phase 2)*

Lagos only. Target ~20 owners/agents and ~100 renters — enough for signal,
small enough to support by hand. Manually verify every listing yourself; it
doesn't scale, and at this volume it doesn't need to. Instrument everything:
where people drop off, what they search for, which listings get inspection
requests. Ship the "should-fix" list as you learn.

### Phase 2 — Verification *(6–8 weeks)*

Per the separate `verification-design.md`. Business verification first (CAC
lookup is a solved API problem — Mono, Dojah, QoreID all offer it), then title
verification with a human in the loop for the LASRERA register and Lagos eGIS,
neither of which exposes an API.

Ship verification *tiers*, not a binary badge, so supply can join at Tier 1
while verification becomes the thing that earns placement.

**Get a lawyer over the badge wording before it ships.** The moment a listing
says "Title Verified", a defrauded buyer will point at that badge in court.

### Phase 3 — Payments *(4–6 weeks)*

Paystack or Flutterwave for card and transfer. Inspection fees and agency
commission first — small, low-risk transactions that prove the rail. **No
custody of rent yet.** This unblocks everything downstream and is the cheapest
step in the whole plan.

### Phase 4 — Payment protection *(6–8 weeks)*

The HousingAnywhere mechanic, adapted: renter pays first rent to the platform,
held until 48 hours after move-in, released unless the renter reports a
mismatch. **This must run on a licensed partner's escrow product, not your own
balance sheet.** Confirm the structure with a fintech lawyer first.

This is where the business model appears. HousingAnywhere charges 25–40% of
first month's rent for exactly this.

### Phase 5 — Financial verification + rent financing *(3–6 months)*

Open banking affordability checks via Mono. Rent financing **originated by
Housing Hub, underwritten and funded by a licensed lender who carries the
credit risk.** You earn origination fees.

Spleet's ~3.5% monthly pricing is the market signal for what this risk costs.
Do not take that risk onto your own book without a licence, capital, and a
collections capability you don't currently have.

### Phase 6 — Savings *(licence-dependent)*

Savings-toward-rent means holding customer deposits, which means MMO or a bank
partnership. Treat as a separate strategic decision with its own business case,
not a feature. Realistically 12+ months out.

---

## Part 4 — The three workstreams

1. **This chat** — bug fixes and Phase 0 hardening.
2. **Content** — LinkedIn/Instagram content to build the audience.
3. **Marketing** — beta recruitment strategy: how to get the first 20 owners
   and 100 renters.

Kickoff briefs for 2 and 3 are in `Housing-Hub-FE/docs/chat-kickoff-briefs.md`.
Run marketing before content — positioning should drive the content pillars,
not the other way round.

---

## Sources

- [HousingAnywhere — Tenant Protection & pricing](https://housinganywhere.com/pricing/landlords)
- [HousingAnywhere — how it works](https://housanywhere.com/renting)
- [CBN fintech regulations 2026: licensing & compliance — Techpoint Africa, May 2026](https://techpoint.africa/guide/cbn-fintech-regulations/)
- [Fintech Licence Requirements in Nigeria 2026: capital, fees, CBN process — Lawzana](https://lawzana.com/articles/nigeria/fintech-licence-requirements-in-nigeria-2026-capital-fees-cbn-process-960)
- [CBN licences in Nigeria: types, costs — TechCabal](https://techcabal.com/2025/05/13/cbn-licences-in-nigeria/)
- [Nigerian proptech Spleet raises $2.6M — TechCrunch](https://techcrunch.com/2022/10/04/nigerian-proptech-spleet-gets-2-8m-led-by-mac-vc-to-scale-its-property-management-products/)
- [Why solving Nigeria's housing problem is beyond proptech startups for now — Techpoint Africa](https://techpoint.africa/insight/nigeria-housing-problem-proptech-startups/)
- [How Kwaba is solving Nigeria's rent complexities — Estate Intel](https://estateintel.com/insights/how-kwaba-is-solving-nigerias-rent-complexities)
- [How to Verify Property Titles in Nigeria — The Trusted Advisors](https://trustedadvisorslaw.com/insights/how-to-verify-property-titles-in-nigeria)
