# Business & Property Ownership Verification — Design

Research and integration plan for verifying (a) agents and developers as legitimate
businesses, and (b) that a home owner actually owns the home they are listing.

Written August 2026. Nigerian regulatory context.

---

## 1. Why this matters commercially

The EFCC reports that **over 70% of Nigerian real estate fraud cases involve
problems with property titles** — not fake photos, not fake agents, but title.
Housing Hub's stated differentiator is fraud protection. Right now the platform
verifies a *person's identity* (NIN, ID document) but never verifies that the
person has any right to the property they list. That is the gap this closes.

Verifying identity answers "is this a real human?" It does not answer
"does this human own this house?" Those are different questions and the second
one is where the money is lost.

---

## 2. The regulatory landscape

Three bodies claim authority over real estate practitioners, and they overlap.
This confuses even practitioners, so the product must not force users to
self-classify incorrectly.

| Body | Level | Who it actually regulates | Relevance to us |
|---|---|---|---|
| **LASRERA** | Lagos State | *Every* agent, developer, property manager operating in Lagos | **Primary.** Broadest net, has a public register |
| **ESVARBON** | Federal | Only trained Estate Surveyors & Valuers (the "ESV" title) | Secondary — a minority of our agents |
| **NIESV** | Professional body | Members only; **issues no licence** | Informational only |

Key facts established by research:

- LASRERA registration is **mandatory** for anyone doing agency, sales, rentals
  or development in Lagos. Non-registration attracts a fine of **₦250,000
  (individual)** or **₦1,000,000 (organisation)**.
- LASRERA permits are **valid one year and must be renewed annually** — so
  verification must expire, not be permanent.
- ESVARBON and NIESV sued Lagos State arguing LASRERA was unconstitutional.
  The Federal High Court **ruled for Lagos State**; ESVARBON is appealing.
  Treat LASRERA as authoritative but keep the model flexible.
- The **RERCON Bill** (a national real estate regulator) passed the Senate in
  2021 and the House in 2023 but **is not yet signed into law**. If it passes,
  a national registration number becomes relevant — design for that.
- **Outside Lagos there is largely no state-level regulator.** Abuja, Port
  Harcourt etc. have no LASRERA equivalent. Verification must degrade
  gracefully rather than block those markets.

---

## 3. Documents required — Business verification (Agents & Developers)

### 3.1 Agent / Agency

| # | Document | Mandatory | Source of truth | Auto-verifiable |
|---|---|---|---|---|
| 1 | **CAC certificate** (RC or BN number) | Yes | Corporate Affairs Commission | **Yes — API** |
| 2 | **LASRERA permit** (Lagos only) | Yes in Lagos | LASRERA register | Manual (public web register) |
| 3 | **Means of ID** — NIN / passport / DL | Yes | NIMC | **Yes — API** |
| 4 | **Operational office address** | Yes | Self-declared + utility bill | Manual |
| 5 | **Tax clearance** — 3 years | Corporates | FIRS / LIRS | Manual |
| 6 | **ESVARBON licence** | Only if claiming "ESV" | ESVARBON register | Manual |
| 7 | **Proof of address** — utility bill ≤3 months | Yes | — | Manual |

Minimum qualification under LASRERA guidelines is SSCE, but requesting
certificates is disproportionate for a listings platform. Recommend skipping.

### 3.2 Developer

Everything above, plus per-project evidence:

| # | Document | Mandatory | Issuing body |
|---|---|---|---|
| 8 | **Planning Permit** | Yes | LASPPPA (Lagos) / state planning authority |
| 9 | **Letter of Authorisation to Build** | Yes | LASBCA |
| 10 | **Certificate of Completion & Fitness for Habitation** | For completed builds | LASBCA |
| 11 | **Title document for the development land** | Yes | See §4 |

A developer selling off-plan without a planning permit is the single highest-risk
actor on a platform like this. Item 8 should be non-negotiable for off-plan listings.

---

## 4. Documents required — Property ownership (Home Owners)

Nigerian title flows: **C of O → Deed of Assignment → Governor's Consent.**

| # | Document | What it proves | Notes |
|---|---|---|---|
| 1 | **Certificate of Occupancy (C of O)** | State granted right to occupy, ~99 years | The strongest single document. Absence may mean the land is under government acquisition |
| 2 | **Deed of Assignment** | Transfer from previous owner to this owner | Needed when the C of O is in someone else's name |
| 3 | **Governor's Consent** | State approved the transfer | Under the Land Use Act 1978, a transfer **without** it is void. Takes 3–12 months to obtain |
| 4 | **Survey Plan** | Exact boundaries and dimensions | Cross-checkable against the state land ministry |
| 5 | **Receipt of Purchase** | The current owner actually paid | Weak alone, useful corroboration |
| 6 | **Land Registry search result** | No mortgage, lien, litigation or acquisition | ₦15,000–20,000 in Lagos, 5–14 days |

### Critical nuance for the data model

A C of O in a name that is **not** the lister's name is the classic fraud
pattern. The system must capture **the name on the title document** as a
separate field and compare it to the verified KYC legal name. A mismatch
without a Deed of Assignment bridging the two names is an automatic escalation.

### Rented vs owned

Many legitimate listers are not owners — they are agents acting for an owner,
or licensed property managers. For those, the right artefact is a
**Letter of Authority to Let/Sell** signed by the owner, plus the owner's title
document. Forcing everyone down the "prove you own it" path will block
legitimate supply.

---

## 5. What can be automated today

### Available now — commercial APIs

**CAC company lookup** is a solved problem. Multiple Nigerian providers offer
RC/BN verification returning company status, registration number, type, and
registered address:

- **Mono** — CAC Lookup v3, database of 3.1M+ registered businesses
- **Dojah** — CAC lookup by RC number or company name
- **QoreID** — CAC Basic and CAC Premium tiers
- **VerifyMe** — limited companies, business names, incorporated trustees
- **Korapay**, **Zeeh Africa**, **MetaMap** — equivalent offerings

Most of these same providers also do **NIN and BVN verification**, so one
vendor can likely cover both identity and business checks. Worth negotiating a
single contract.

### Available but not as a clean API

**Lagos eGIS / landonline.lagosstate.gov.ng.** Lagos launched an electronic
GIS platform where titles can be searched and verified online, and the Land
Registry has scanned 10.5M+ pages of title documents since 2005. There is a
portal but **no documented public API** — it requires an authenticated user
account and manual transaction initiation.

Practical read: this is a **human-in-the-loop** step for now. An admin opens
eGIS, runs the search, attaches the result. Do not promise automated title
verification in the UI.

### Not automatable

LASRERA and ESVARBON have public registers but **no APIs**. Verification means
an admin checking the register by hand. Budget for that operationally.

---

## 6. Current state of the codebase

What exists today:

```
Customer entity          → NationalIdNumber, IdType, IdDocumentUrl,
                           KycSubmittedAt, IsKycVerified, KycRejectionReason
IDType enum              → NIN, DL, Passport, VoterCard, Other
CustomerType (flags)     → Unset, HouseOwner, Agent, Customer, Admin, Developer
Property entity          → IsPublished, IsVerified, VerifiedAt
IFileStorageService      → UploadFileAsync(file, subDirectory), DeleteFileAsync
Admin endpoint           → PUT verify-kyc?approve={bool}&reason={string}
```

The bones are right. Three structural gaps:

1. **KYC is single-document.** One `IdDocumentUrl` on `Customer`. Business and
   property verification need *many* documents each, with per-document status.
2. **`Property.IsVerified` is a bare bool** with no evidence trail — nothing
   records *why* it was verified or by whom.
3. **No expiry anywhere.** LASRERA permits lapse annually. `IsKycVerified` is
   permanent once set.

---

## 7. Proposed data model

Two new entities, deliberately kept generic so one review pipeline serves both.

### 7.1 `VerificationDocument`

```csharp
public class VerificationDocument : BaseEntity
{
    public Guid OwnerId { get; set; }              // Customer.Id or Property.Id
    public VerificationSubjectType SubjectType { get; set; }  // Business | Property
    public VerificationDocumentType DocumentType { get; set; }
    public string FileUrl { get; set; } = null!;

    // Extracted / declared metadata
    public string? DocumentNumber { get; set; }    // RC number, C of O number
    public string? NameOnDocument { get; set; }    // ← the anti-fraud field
    public string? IssuingAuthority { get; set; }  // "Lagos State", "CAC"
    public DateTime? IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }       // LASRERA permits: +1 year

    public DocumentReviewStatus Status { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? ReviewedByAdminId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Automated check result, where a provider exists
    public bool? AutoCheckPassed { get; set; }
    public string? AutoCheckProvider { get; set; }  // "Mono", "Dojah"
    public string? AutoCheckRawResponse { get; set; }
}
```

### 7.2 `VerificationCase`

Groups documents into one reviewable unit so an admin approves a *case*, not
seven loose files.

```csharp
public class VerificationCase : BaseEntity
{
    public Guid SubjectId { get; set; }
    public VerificationSubjectType SubjectType { get; set; }
    public VerificationTier RequestedTier { get; set; }
    public VerificationCaseStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedByAdminId { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime? ExpiresAt { get; set; }        // earliest document expiry
}
```

### 7.3 New enums

```csharp
public enum VerificationSubjectType { Business = 1, Property = 2 }

public enum VerificationDocumentType
{
    // Business
    CacCertificate = 1,
    LasreraPermit = 2,
    EsvarbonLicence = 3,
    TaxClearance = 4,
    ProofOfAddress = 5,
    // Property — title
    CertificateOfOccupancy = 20,
    DeedOfAssignment = 21,
    GovernorsConsent = 22,
    SurveyPlan = 23,
    PurchaseReceipt = 24,
    LandRegistrySearch = 25,
    LetterOfAuthorityToLet = 26,
    // Developer — build
    PlanningPermit = 40,
    AuthorisationToBuild = 41,
    CertificateOfCompletion = 42,
}

public enum DocumentReviewStatus { Pending = 1, Approved = 2, Rejected = 3, Expired = 4 }

public enum VerificationCaseStatus
{
    Draft = 1, Submitted = 2, UnderReview = 3,
    Approved = 4, Rejected = 5, Expired = 6, EscalatedNameMismatch = 7
}

public enum VerificationTier { Unverified = 0, IdentityVerified = 1, BusinessVerified = 2, TitleVerified = 3 }
```

### 7.4 Entity changes

```csharp
// Customer
public VerificationTier BusinessVerificationTier { get; set; }
public DateTime? BusinessVerifiedAt { get; set; }
public DateTime? BusinessVerificationExpiresAt { get; set; }
public string? CacNumber { get; set; }
public string? LasreraPermitNumber { get; set; }

// Property
public VerificationTier TitleVerificationTier { get; set; }
public string? TitleHolderName { get; set; }   // name on the C of O
public bool ListerIsTitleHolder { get; set; }  // false ⇒ needs Letter of Authority
public DateTime? TitleVerifiedAt { get; set; }
```

Keep the existing `Property.IsVerified` as a computed convenience over
`TitleVerificationTier >= TitleVerified` so nothing downstream breaks.

---

## 8. Verification tiers — what the user sees

Tiers are better than a binary badge: they let supply onto the platform early
while still telling buyers exactly how much diligence has been done.

| Tier | Badge | Requires | Can do |
|---|---|---|---|
| 0 | *(none)* | — | Browse only |
| 1 | **ID Verified** | NIN + ID document *(exists today)* | Book inspections |
| 2 | **Business Verified** | Tier 1 + CAC + LASRERA (if Lagos) | List properties |
| 3 | **Title Verified** | Tier 2 + C of O / Deed + registry search | Carries the gold trust badge |

Buyer-facing copy should state what was checked, not just show a tick:
*"Title verified — C of O confirmed against the Lagos Land Registry, 12 Aug 2026."*
A vague badge that turns out to be shallow is worse than no badge, because it
transfers the buyer's trust to you.

---

## 9. Phased rollout

**Phase 1 — Foundation (~1 sprint).** Entities, enums, migrations. Generalise
`IFileStorageService` usage into a document service. Extend the admin review UI
from the existing single-document KYC screen to a case view. No user-facing
change yet.

**Phase 2 — Business verification (~1–2 sprints).** CAC upload + automated
lookup via one provider (recommend evaluating Mono and QoreID first). LASRERA
permit upload with manual admin check against the public register. Annual
expiry with a reminder email at T-30 days — the email infrastructure is
already there and template-driven.

**Phase 3 — Property title (~2 sprints).** Title document upload against a
property. Capture `NameOnDocument` and auto-flag mismatches against the lister's
verified KYC name. Admin runs the eGIS search manually and attaches the result.
Letter-of-Authority path for agents listing on an owner's behalf.

**Phase 4 — Trust surface (~1 sprint).** Tier badges on listing cards and detail
pages. Filter for "Title Verified only". Verification status in the owner
dashboard with a checklist of what is outstanding.

---

## 10. Risks and judgement calls

**Verification is a liability transfer.** The moment you display "Title
Verified", a defrauded buyer will point at that badge in court. Get a lawyer to
review the badge wording and your T&Cs before Phase 4 ships. This is the single
most important item in this document.

**Manual review will not scale, and that is acceptable at first.** LASRERA and
eGIS both need a human. At low volume that is fine. Model the cost: if a
reviewer handles ~20 cases a day, work out the headcount at your target listing
volume before committing to a turnaround-time promise in the UI.

**Do not gate supply too early.** Requiring Tier 3 to list will empty the
platform. Let owners list at Tier 1 with a visible "Unverified" state and make
verification the thing that earns them placement and inspection requests.

**Lagos-first, by necessity.** LASRERA and eGIS are Lagos-only. Abuja and Port
Harcourt have no equivalent. Either scope verification to Lagos initially or
accept a weaker check elsewhere — but say which, in the UI, honestly.

**Document forgery is real.** Uploaded PDFs of C of Os can be fabricated. The
registry search is what actually catches this, not the uploaded document.
Treat uploads as claims and the registry search as evidence.

**Data protection.** These documents are sensitive personal data under the
NDPA. Encrypt at rest, restrict admin access by role, set a retention policy,
and update the privacy policy — which currently says nothing about title
documents.

---

## Sources

- [Mandatory Registration Requirements For Real Estate Practitioners In Lagos State — Famsville Solicitors / Mondaq](https://www.mondaq.com/nigeria/landlord-tenant--leases/961292/mandatory-registration-requirements-for-real-estate-practitioners-in-lagos-state)
- [LASRERA vs. ESVARBON vs. NIESV — EdenBrooks Homes, March 2026](https://edenbrooks.com.ng/lasrera-vs-esvarbon-vs-niesv-which-body-actually-regulates-you-as-a-property-sales-agent-in-lagos-nigeria/)
- [How to Verify Property Titles in Nigeria Before Purchase — The Trusted Advisors](https://trustedadvisorslaw.com/insights/how-to-verify-property-titles-in-nigeria)
- [LASRERA official portal](https://lasrera.lagosstate.gov.ng/)
- [Lagos eGIS land administration portal](https://landonline.lagosstate.gov.ng/index.html)
- [Lagos launches e-GIS platform to apply and verify land titles online — Nairametrics](https://nairametrics.com/2024/01/27/lagos-state-govt-launches-e-gis-platform-to-apply-and-verify-land-titles-online/)
- [ESVARBON official site](https://www.esvarbon.gov.ng/)
- [All The Permits You Need As A Real Estate Developer In Lagos State — Mondaq](https://www.mondaq.com/nigeria/real-estate/1737158/all-the-permits-you-need-as-a-real-estate-developer-in-lagos-state)
- [Mono CAC Lookup v3](https://mono.co/blog/mono-cac-lookup-v3-is-live)
- [Dojah CAC Lookup docs](https://docs.dojah.io/overview/business-verification/cac)
- [QoreID CAC Premium docs](https://docs.qoreid.com/docs/cac-premium)
- [VerifyMe CAC API docs](https://docs.verifyme.ng/identity-verifications/corporate-affairs-commission)
