using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HousingHub.API.Controllers.V1;

// Public FAQ content — no auth required.
[AllowAnonymous]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[Controller]")]
public class FaqController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetFaqs()
    {
        var faqs = new[]
        {
            new
            {
                Id = 1,
                Question = "How do I schedule a property inspection on Housing Hub?",
                Answer = "Once you find a property you are interested in, click the 'Schedule Inspection' button on the listing page. Choose your preferred date and time from the available slots, and you will receive a confirmation notification via email and SMS. The property agent or owner will also be notified and will reach out to confirm.",
                Category = "Inspections"
            },
            new
            {
                Id = 2,
                Question = "What should I expect during a property inspection?",
                Answer = "During an inspection, you will be guided through the property by the verified agent or landlord. Check the state of fixtures, water supply, electricity (PHCN availability and prepaid meter reading), security features, and the overall condition of the apartment. It is advisable to take photos and ask about service charges, waste disposal, and parking.",
                Category = "Inspections"
            },
            new
            {
                Id = 3,
                Question = "Can I cancel or reschedule a property inspection?",
                Answer = "You can cancel or reschedule an inspection up to 24 hours before the scheduled time without any penalty. Log into your account, navigate to 'My Inspections', and select the inspection you wish to modify. Repeated last-minute cancellations may affect your credibility score on the platform.",
                Category = "Inspections"
            },
            new
            {
                Id = 4,
                Question = "What happens after my inspection is confirmed?",
                Answer = "After your inspection is confirmed, you will receive a reminder notification 2 hours before the appointment. Once the inspection is completed, you will be prompted to leave a review of the property and the agent. If you are satisfied and wish to proceed, you can initiate the rental agreement process directly from your dashboard.",
                Category = "Inspections"
            },
            new
            {
                Id = 5,
                Question = "What documents do I need for KYC verification?",
                Answer = "To complete KYC on Housing Hub, you will need a valid government-issued ID (National ID card, International passport, Driver's licence, or Voter's card), a selfie for facial verification, and proof of address (utility bill, bank statement, or tenancy agreement not older than 3 months). Verification is required before you can book an inspection or list a property.",
                Category = "KYC & Verification"
            },
            new
            {
                Id = 6,
                Question = "How long does KYC verification take?",
                Answer = "KYC verification is typically completed within a few minutes for automated checks. In some cases, manual review may take up to 24 hours. You will receive an email and in-app notification once your verification is complete. Ensure your documents are clear and not expired to avoid delays.",
                Category = "KYC & Verification"
            },
            new
            {
                Id = 7,
                Question = "How do I verify that a property owner or agent is legitimate?",
                Answer = "All landlords and agents on Housing Hub undergo identity verification and document checks before their listings go live. You can check the verified badge on a listing, view the agent's profile history, and read reviews from previous tenants. Never pay money outside the Housing Hub platform, and always confirm all transactions through official channels.",
                Category = "KYC & Verification"
            },
            new
            {
                Id = 8,
                Question = "How do I list my property on Housing Hub?",
                Answer = "To list your property, create a landlord or agent account and complete your KYC verification. Then click 'Add Listing', fill in the property details (type, location, size, amenities), upload high-quality photos, set your asking price, and specify available inspection slots. Our team reviews listings within 24–48 hours before they go live.",
                Category = "Listings"
            },
            new
            {
                Id = 9,
                Question = "What is the difference between a short-let and a long-term lease?",
                Answer = "A short-let is a furnished apartment rented on a daily, weekly, or monthly basis — ideal for business trips, relocations, or temporary stays. A long-term lease typically runs for 6 months to 2 years and is suited for tenants seeking a permanent residence. Housing Hub supports both lease types, each with its own pricing and agreement structure.",
                Category = "Listings"
            },
            new
            {
                Id = 10,
                Question = "How does Housing Hub protect me from rental fraud?",
                Answer = "Housing Hub protects users through mandatory KYC for all landlords, agents, and tenants; secure in-platform payment processing; a verified badge system; and an inspection protocol that prevents you from paying rent before physically viewing the property. We never request payment via bank transfer to personal accounts. If anything feels suspicious, report it immediately through the app.",
                Category = "Safety & Trust"
            },
            new
            {
                Id = 11,
                Question = "How do I report a suspicious or fraudulent property listing?",
                Answer = "If you suspect a listing is fraudulent, click the 'Report' button on the property listing page and provide details about your concern. Our trust and safety team will review the report within 24 hours, remove the listing if confirmed fraudulent, and may escalate to law enforcement where necessary. You can also reach us at info@housinghub.ng.",
                Category = "Safety & Trust"
            },
            new
            {
                Id = 12,
                Question = "How does payment work on Housing Hub?",
                Answer = "All payments on Housing Hub are made securely through our in-app payment gateway. After agreeing to a rental, you will be presented with a payment breakdown covering rent, agency fee, caution deposit, and service charge where applicable. Funds are held in escrow until the tenancy agreement is signed, protecting both landlord and tenant. Receipts are automatically generated for every transaction.",
                Category = "Payments"
            },
        };

        return Ok(new { data = faqs, isSuccessful = true });
    }
}
