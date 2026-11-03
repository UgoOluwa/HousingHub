# Go to market

Written after the conversation with Desmond Audu on 3 November 2026. He has five
years in Nigerian proptech and built Quicklet, which pivoted three times — rental
verification, then full rentals, then co-living. The notes are his domain knowledge;
the reading of them is mine, and the places I am unsure are marked.

Complements `product-strategy.md` (August 2026), which covers licensing and
phasing. This is about who to sell to and how.

---

## 1. The thesis has to change

Housing Hub was conceived to **remove agents from letting**. On the evidence, that
is not available.

Agents run WhatsApp groups of 500 to 1,000+ members. That is the distribution, and
it is theirs. They see themselves as the main character of the transaction, and
they read any app as either diminishing their authority or preparing to replicate
them. A product whose premise is their removal will be refused by the people who
control access to both supply and demand — and refused loudly, in the groups.

**The reframe: do not remove the agent, make the good one provable.**

A renter's actual complaint is not that an agent exists. It is that they cannot
tell a real agent from a fraud, pay inspection fees to view properties that do not
exist, and have no recourse. A landlord's complaint is not that an agent exists
either. It is that they cannot see what the agent is doing with their property or
their money.

Neither problem is solved by removing agents. Both are solved by making them
legible — which is a thing a good agent *wants*, because it is how they stop
competing with fraudsters on price.

This is not a concession. It is a better business: the agents become distribution
rather than opposition, and distribution is what Desmond says decides survival.

## 2. What we already built for this, without knowing it

Worth noticing before deciding anything. The verification pipeline already handles
**LASRERA registration, ESVARBON licences, NIESV membership, CAC certificates and
tax clearance**. Those are not renter credentials. They are the professional
credentials of estate agents and property businesses.

We also already support an agent as the managing party on a tenancy.

So the product built over the last months is closer to *agent infrastructure* than
to agent replacement. The thesis was wrong about its own codebase.

## 3. The sharpest wedge: the diaspora landlord

Desmond's first strategy is to identify one specific user character. I think it is
the landlord abroad, and I think it is the strongest card in the deck.

**Why them:**

- **The pain is acute and expensive.** They cannot see their property. Rent reaches
  them when an agent chooses to remit it, minus whatever the agent says it was.
  The usual mitigation is trusting a relative, which costs relationships.
- **They can pay.** Foreign-currency income against naira-denominated service, and
  a fee that is trivial next to one year's rent they failed to collect.
- **They are reachable without street distribution.** Diaspora communities,
  referral, and Nigerian professional networks abroad — not the agent-controlled
  WhatsApp groups where we have no standing.
- **Agents stop being an obstacle.** A landlord abroad *needs* somebody on the
  ground. We are not competing with the agent; we are the accountability layer over
  them, which is what the landlord is missing and the honest agent does not mind.

**And it is what we built.** Every surface from the last month is worth more to a
remote landlord than to a renter:

| What exists | What it is worth to a landlord abroad |
|---|---|
| Candidate list from completed viewings | Sees who actually viewed, and chooses — rather than being told |
| Document pack with signature trail | A tenancy agreement they can hold, with who signed it and when |
| **Split payment** | Rent goes to *their* account directly; the agent's cut is paid automatically |
| Admin finance report | What was collected and what it cost, per property |

The split payment is the one that matters most. Today the single largest failure in
diaspora letting is that rent passes through an agent's hands. Paystack's split
removes that without removing the agent — the landlord is paid first and directly,
and the agent is paid their share in the same transaction, automatically, which is
better for an honest agent than chasing it.

**The product gap this exposes.** Our tenancy has exactly one counterparty to the
tenant, taken from whoever listed the property; where an agent manages for a
landlord, the agent *is* that party and the landlord is not represented at all. For
this strategy that is backwards. We would need owner and manager as distinct
parties: the manager does the work and sees the screens, the owner is paid and sees
everything. That is a real piece of work and it is the first thing I would build
if this is the direction.

## 4. The housing number: right idea, wrong order

The BVN-for-housing concept is good and Desmond validated it against real guarantor
pain. His warning is the important half: **Laser Pay raised $1.1m in 2020 and shut
down because adoption literacy was not there.** A platform has to meet people where
they are before introducing something that asks them to think in a new primitive.

The resolution is not to build it later. It is to notice that **it is a by-product,
not a product.**

Every completed viewing, signed tenancy and settled rent payment is already a
record of housing history — we built that infrastructure this week without calling
it that. Run the letting rail for two years and the database exists, assembled from
transactions people had their own reasons to complete. Nobody is asked to adopt a
new primitive; they are asked to rent a flat.

Sell the housing number on day one and you are Laser Pay. Accumulate it while
selling something people already want, and you arrive at the same asset with
customers attached.

The same is true of the **data gap** he describes — investors from Brazil and Saudi
speculating about Nigerian rental behaviour because nobody has structured data on
buying timelines, renting demographics or monthly volumes. Asking rent versus agreed
rent, listing-to-let time, inspection-to-selection conversion, fee structures by
area: all of it falls out of running the rail. It is a second by-product and
possibly a second business, but it is not a thing to go and build.

## 5. The hardest thing in the notes is about us

> *"Focus on a single core problem before expanding. Early attempts at
> multi-feature rollouts fail without initial market validation."*

Housing Hub currently has: listings, search, inspections with rescheduling and
handoff, in-app chat, identity verification, business verification, title
verification, a payment rail, tenancy documents with three signing modes,
e-signature with an audit trail, split payments, a finance report, and an admin
dashboard over all of it.

**It has no users.**

Quicklet pivoted three times. We have built as though we already knew the answer,
and the honest reading of Desmond's advice is that most of this surface is a bet
placed before the market was asked anything. Some of it will turn out to be wrong,
and the cost is not the code — it is that every screen is now a thing to maintain,
explain and keep correct while we find out.

I do not think the answer is to delete anything. I think it is to **stop building
and start selling**, and to accept that the next real information comes from twenty
landlords rather than from another increment.

## 6. What I would actually do

1. **Pick the diaspora landlord and say so out loud.** One character, per his own
   framework. Everything else becomes "not now" rather than "no".
2. **Split owner from manager in the tenancy model.** The strategy does not work
   without it, and it is the only build I would prioritise over selling.
3. **Recruit ten agents, not a hundred.** Verified, LASRERA-registered, given a
   badge that is worth something because it is scarce. They are the ground team for
   the landlords we sign.
4. **Close the two gaps in the flow before anybody real uses it** — see
   `the-letting-flow.md`. A renter is never identity-checked and no financial check
   exists anywhere, and the second one matters enormously to a landlord abroad
   choosing between strangers.
5. **Leave the housing number and the data product alone** until the rail has run
   for a year.

## 7. On the regulatory lever

Desmond's third strategy — leverage government policy, as the cashless period
forced adoption — I am least able to assess, and I would not plan around it.

What I can say is that the ingredients are already in the product: LASRERA
registration is a verification document type, and a platform that can prove which
agents are registered is useful to a regulator that wants registration enforced.
Whether that becomes a lever depends on relationships and timing that neither of us
has visibility into, and it should be treated as upside rather than as a plan.

## 8. One thing worth weighing about the partnership

Not my call, and I only have one side of it. But the notes make the shape clear:
he brings distribution, agent relationships and five years of knowing which pivots
failed; the gap in Housing Hub is not engineering. The product is further along than
the go-to-market by a wide margin, and that asymmetry is the argument.

The thing worth testing before Friday is whether he agrees the diaspora landlord is
the wedge — because if his instinct is co-living or renters, that is a different
company and better known now.

---

### Market context, from the notes

Recorded as given, not independently verified.

- Nigeria's housing deficit is ~14 million units, against ~1 million in the US and
  ~4 million in the UK.
- Households average three to four people; self-contained units at ₦500–600k are
  increasingly shared through co-living.
- The upper class is under 20%; the middle class is being squeezed by inflation, and
  many people in their thirties live with parents.
- Total African proptech investment is ~$60m, of which $55m+ is Egypt's Nawi and
  ~$5.5m a Moroccan company. **Sub-Saharan proptech has raised almost nothing** —
  which means little competition and little proof the model works.
