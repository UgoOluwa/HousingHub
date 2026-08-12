# The full transaction lifecycle — assessment and plan

Your vision is the whole journey: list → verify identity → inspect → shortlist →
verify affordability → document and sign → pay → monitor the tenancy. This is an
assessment of that, and a sequence for building it.

August 2026. Builds on `product-strategy.md` and `verification-design.md`.

---

## The short version

**The shape of the journey is right, and it is a genuinely better product than
what exists in this market.** Selecting a candidate from completed inspections
and only then triggering an affordability check is the correct order — it is what
the UK referencing industry does, and it means you never ask a renter for their
financial life until someone is seriously interested in them.

Three parts of it are harder than they look, and none of the difficulty is
engineering:

1. **Charging for anything requires a payment rail you do not have.** Paid ID
   verification is the first feature that needs it. This is the true blocker.
2. **Holding rent for 48 hours means holding customer funds**, which under CBN
   rules requires a licence you do not have and should not pursue. It must run on
   a licensed partner's product.
3. **You cannot sign a land instrument electronically in Nigeria.** Short
   tenancies are fine. Sales and leases over three years are not — the law
   excludes them, and no amount of product design fixes that.

The rest is buildable, and the order below is designed so each phase earns
something rather than just adding surface.

---

## Part 1 — What the research says

### 1.1 The UK referencing model transfers well

Goodlord, HomeLet and Canopy all run the same shape: credit check, affordability
assessment, employment verification, and a previous-landlord reference. The
affordability test is a formula — rent as a proportion of gross income, commonly
**30–40%**.

Two things about their reports matter for your design.

**The landlord gets a verdict, not the raw data.** The output is essentially
pass / fail against a threshold, with supporting summary. The landlord does not
receive the applicant's bank statements. That is both a privacy decision and a
product one: raw statements invite the landlord to make judgements they are not
qualified to make and expose you to discrimination claims.

**The threshold is the landlord's to set, within limits.** Different landlords
accept different ratios. Making it configurable per listing is more useful than
one platform-wide rule — and it lets an owner who is relaxed about affordability
accept a self-employed applicant a rigid formula would reject.

### 1.2 Nigeria has the data, through intermediaries

The affordability check is feasible here. What exists:

- **Open banking.** Mono connects to 200+ Nigerian financial institutions and
  sells a Creditworthiness API that combines bureau data with live bank data and
  income-pattern analysis. Okra, Indicina, Periculum and Lendsqr occupy adjacent
  space. This is the realistic route to verified income.
- **Credit bureaus.** Three CBN-licensed: CRC (widest coverage, most commonly
  integrated), FirstCentral, and CreditRegistry. A bureau check tells you about
  existing debt obligations, which is the other half of affordability.

**The constraint is coverage, not access.** A large share of Nigerian renters are
informally employed and thinly banked. An affordability engine tuned to salaried
applicants will reject people who can genuinely pay, in a market where that is
the norm rather than the exception. Design for it: the check should produce a
graded picture, and the owner should be able to accept an applicant the formula
scores poorly. If your product's answer to a trader with strong cash flow and no
payslip is "declined", you have rebuilt the exclusion you are trying to solve.

### 1.3 The documentation phase has a hard legal ceiling

This is the finding that should most change your plan.

**Electronic signatures are valid in Nigeria in general.** Section 93 of the
Evidence Act 2011 recognises them.

**They are explicitly not valid for instruments affecting land under the Land Use
Act.** Deeds, assignments, and anything registrable are excluded and require wet
ink.

Where that lands:

| Transaction | In-app signing? | Why |
|---|---|---|
| Residential tenancy, **≤ 3 years** | Yes | A contract, not a land instrument |
| Lease **> 3 years** | **No** | Becomes a lease requiring Governor's Consent and registration |
| Outright **sale / purchase** | **No** | Deed of Assignment — wet ink, consent, registration |

Two further requirements even for the short tenancies you *can* sign in app:

- **Stamping.** A tenancy agreement is not enforceable evidence until stamped
  with the revenue service. An unstamped agreement is inadmissible if there is
  ever a dispute — which is precisely when your users will need it.
- **Lagos Tenancy Law 2011** governs rent increases, notice periods and eviction.
  Your generated agreements must comply, and a template that quietly contradicts
  it is worse than no template.

**Implication for the product:** in-app execution works for the residential
rental journey, which is most of your volume. For sales, the app can prepare
documents, route them, track status and store the executed copies — but the
signing itself happens offline with a lawyer. Build the workflow so an offline
execution step is a first-class state, not an exception.

### 1.4 Money is still the binding constraint

Unchanged from the earlier strategy work, and it now blocks more than it did.

- **Holding first rent for 48 hours is escrow.** Escrow is holding customer
  funds. Under CBN rules that is Mobile Money Operator territory. Do not build
  it on your own balance sheet — run it on a licensed PSP's escrow product and
  earn a fee.
- **Charging for verification is simpler** — it is a payment *to you* for a
  service, not custody of someone else's money. Paystack or Flutterwave covers
  it. But it still requires the integration that does not exist yet.

---

## Part 2 — What I would change about the plan

Four amendments. The rest of your sequence I would keep.

### 2.1 Payments come before paid ID verification

You cannot charge for ID verification without a payment rail, and paid ID
verification is the first thing in your plan that charges anyone. So payments is
not a later phase — it is the next one.

It is also the cheapest thing on the list and unblocks everything downstream:
verification fees, then agency fees, then eventually the protected first-rent
payment. Do it first and every subsequent phase gets easier.

### 2.2 Reconsider charging for ID verification at all

Worth thinking about rather than assuming.

Charging for it is defensible — it is a real cost, and a fee filters out
low-intent signups. But in Phase 1 you are verifying manually, so your marginal
cost is your own time, and a fee on the **supply** side is friction where you can
least afford it. Twenty owners is a small number to lose anyone from.

I would suggest: **free for owners, agents and developers; charged for renters at
the point they are shortlisted**, bundled with the affordability check rather than
billed separately. That way the money arrives at the moment the renter has the
strongest reason to pay — someone has picked them — and your supply side never
hits a paywall.

If you do charge everyone, keep it genuinely one-off and make the badge
permanent, exactly as you described.

### 2.3 The affordability report should be a verdict, not a data dump

The single most important design decision in this whole phase.

**Do not show the landlord the applicant's bank statements.** Under NDPA,
financial data is sensitive personal data requiring explicit consent and strict
safeguards, and "the landlord wanted to see it" is not a lawful basis for handing
over more than the decision requires.

What the owner should see:

- **Affordability verdict** against the rent for *this* property — pass, marginal,
  or fail, with the ratio used
- **Verified income band**, not exact figures
- **Income stability** — how consistent, over how many months
- **Existing obligations** summary from the bureau — count and severity, not
  itemised
- **Any adverse credit flags**
- **Date and method** of the check, so the owner knows how fresh it is

What they should never see: transaction history, account balances, employer name,
or the raw bureau file.

This is not only compliance. It is also what makes the product defensible — you
are selling a *decision*, and a decision is worth more than a PDF the owner has to
interpret.

### 2.4 Owners and businesses need genuinely different flows

You are right that owners are not businesses, and the design should reflect it
rather than making owners fill in a business form with blanks.

- **Agents and developers** → business verification. CAC certificate, status
  report, TIN, LASRERA registration for Lagos, ESVARBON or NIESV where they are
  practising estate surveyors. Already scoped in `verification-design.md` §3.
- **Owners** → property verification only. They prove they own the specific
  property; there is no company to verify. Already scoped in §4.
- **The managed-owner path you described already exists in the codebase.**
  `Customer.IsManagedByHousingHub` and the admin post-on-behalf-of flow are built.
  Owners who want Housing Hub to manage the listing use that route.

---

## Part 3 — The proposed sequence

Each phase ships something usable on its own. Nothing here depends on a licence
you do not hold.

### Phase 2A — Document pipeline ✅ *done*

Built and committed. Business and title verification run on it; identity and
financial verification will reuse it rather than needing their own.

### Phase 2B — Business verification *(~2–3 weeks)*

Agents and developers. CAC lookup via a provider (Dojah, QoreID or Mono — the
interface is already provider-agnostic, so this is one class). Everything else is
human review with an override, because Nigerian registries are inconsistent
enough that a failed lookup is often a bad record rather than a bad applicant.

Directors named on the CAC record get checked against the account holder's
verified identity — that link is where most impersonation is caught, and it is
why identity verification has to come first.

### Phase 2C — Owner property verification *(~3–4 weeks)*

Title documents per `verification-design.md` §4. Human review, and for anything
high value a lawyer rather than an ops reviewer.

The nuance to get right in the schema: **verification attaches to the property,
not the person**, and a person can legitimately list a property they do not own —
an agent acting for an owner, a tenant subletting. Model the *relationship*
between person and property as its own verified thing. Getting this wrong means
rebuilding.

### Phase 3 — Payments *(~4–6 weeks)* ← **the unblocker**

Paystack or Flutterwave. Card and bank transfer. Start with fees owed **to you**:
verification fees, later agency commission. No custody of rent yet.

Also: the tiered badge system, now that there is something to charge for. Tiers
rather than one badge, so each level makes a claim that is precisely true.

### Phase 4 — Shortlisting and affordability *(~6–8 weeks)*

The flow you described, which I think is the strongest idea in your plan:

1. Owner sees completed inspections for a property
2. Owner shortlists a candidate → candidate is notified, in-app and by email
3. Candidate consents and connects their bank via Mono, or uploads statements
4. Payment taken — **renter pays by default, owner can choose to cover it**, with
   your margin on top and disclosed
5. Check runs against **this property's rent**, not in general
6. Owner sees the verdict report from §2.3
7. Owner accepts or declines
8. On accept: listing marked unavailable, candidate notified with next steps
9. On decline: candidate told clearly and kindly that the owner did not proceed
   on affordability grounds, in-app and by email; owner returns to the shortlist

Two things to be careful about in step 9. Be precise about *what* was declined —
"the owner did not proceed" rather than "you failed" — because the same person
may be perfect for the next property. And do not reveal the owner's threshold, or
applicants will reverse-engineer it.

One design point worth stating: the check is **per property**, so a candidate
shortlisted for three properties pays three times. That is defensible because the
answer genuinely differs per rent — but consider a discounted re-check within, say,
30 days, reusing the same bank connection. It costs you almost nothing and removes
the obvious complaint.

### Phase 5 — Documentation and signing *(~6–8 weeks)*

Templates compliant with Lagos Tenancy Law 2011, owner-uploaded variations,
in-app back-and-forth, e-signature for tenancies **≤ 3 years**.

Explicitly modelled: an **offline execution** path for sales and long leases,
where the app prepares and tracks but the signing happens with a lawyer. And a
**stamping** step, because an unstamped agreement is inadmissible exactly when it
matters.

**A Nigerian property lawyer must review the templates before they ship.** Not
optional — a defective agreement generated by your platform is your liability.

### Phase 6 — Protected payment *(~6–8 weeks, licence-dependent)*

The 48-hour hold. **On a licensed partner's escrow product**, not your balance
sheet. Confirm the structure with a fintech lawyer before building.

Both sides need to understand it up front: the renter needs to know they can
report a mismatch, and the owner needs to know their money is held for two days.
Surprising the owner here loses supply.

### Phase 7 — Tenancy management *(later)*

Rent monitoring, renewal reminders, payment tracking. Only meaningful once
Phase 6 means rent flows through the platform — before that, you are asking people
to tell you about payments you cannot see.

---

## Part 4 — The things I would flag hardest

**One.** Payments blocks paid ID verification, affordability fees, and everything
after. It is the smallest phase on this list and the highest leverage. Do it next.

**Two.** Do not build escrow yourself. The capital requirement is serious, 2026 is
an enforcement year, and APP fraud liability can now fall on the platform even
when the user authorised the payment. Partner into it.

**Three.** Get a lawyer twice: once on the tenancy templates, once on the
verification badge wording. Both are places where your platform makes a statement
a user relies on, and both are cheap to get right in advance and expensive to get
wrong.

**Four.** The affordability check is where you can accidentally rebuild financial
exclusion. Most of your market does not have payslips. If the formula is the
decision, you will decline people who can pay. Make the owner the decision-maker
and the formula the input.

**Five.** Beta first. Everything above assumes you have run a closed beta and know
where people actually drop off. The outstanding items from
`phase-1-readiness-and-phase-2-plan.md` — secret rotation, PITR, the S3 bucket
policy, the two backfills — are still open, and they gate that.

---

## Sources

- [Goodlord — tenant referencing](https://www.goodlord.com/letting-agent-solutions/tenant-referencing)
- [Tenant affordability checks: UK landlord guide — Latch](https://www.uselatch.co.uk/blog/tenant-affordability-checks-landlord-guide-uk)
- [Mono — Creditworthiness API](https://mono.co/products/creditworthiness)
- [FirstCentral Credit Bureau](https://firstcentralcreditbureau.com/)
- [How to draft a legally binding lease agreement in Nigeria — Mondaq](https://www.mondaq.com/nigeria/landlord-tenant-leases/1524478/how-to-draft-a-legally-binding-lease-agreement-in-nigeria)
- [Tenancy Law No. 14, 2011, Lagos State](https://nnamdiebolegal.wordpress.com/wp-content/uploads/2017/10/tenancy-law-no-14-2011-laws-of-lagos-state.pdf)
- [Validity and limitation of electronic signatures under Nigerian law — Omaplex](https://omaplex.com.ng/the-validity-and-limitation-of-electronic-signatures-under-the-nigerian-law/)
- [eSignature legality in Nigeria — Docusign](https://www.docusign.com/products/electronic-signature/legality/nigeria)
- [Nigeria Data Protection Act 2023 overview — KPMG](https://kpmg.com/ng/en/home/insights/2023/09/the-nigeria-data-protection-act--2023.html)
