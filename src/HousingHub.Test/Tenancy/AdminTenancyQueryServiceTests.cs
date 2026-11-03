using System.Linq.Expressions;
using HousingHub.Data.RepositoryInterfaces.Common;
using HousingHub.Model.Entities;
using HousingHub.Model.Enums;
using HousingHub.Service.Commons.FileStorage;
using HousingHub.Service.TenancyService;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HousingHub.Test.Tenancies;

/// <summary>
/// What staff can see about a tenancy, and what they cannot.
/// </summary>
public class AdminTenancyQueryServiceTests
{
    private const string TenancyIndex = "TenancyId-index";
    private const string Hash = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

    private readonly Mock<IUnitOfWOrk> _unitOfWork = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<IFileStorageService> _fileStorage = new();
    private readonly AdminTenancyQueryService _sut;

    public AdminTenancyQueryServiceTests()
    {
        _sut = new AdminTenancyQueryService(
            _unitOfWork.Object, _fileStorage.Object, NullLogger<AdminTenancyQueryService>.Instance);
    }

    private static Customer Person(string first, string last, string email) =>
        new(first, last, email, "08000000000", CustomerType.Customer, "hash") { Id = Guid.NewGuid() };

    private static Tenancy MakeTenancy(
        Guid landlordId, Guid tenantId, Guid propertyId,
        TenancyStatus status = TenancyStatus.DocumentsRequested,
        long rentKobo = 150_000_00) =>
        new(propertyId, landlordId, tenantId, Guid.NewGuid(), rentKobo, PropertyLeaseType.Rent)
        {
            Id = Guid.NewGuid(),
            Status = status,
            DateCreated = DateTime.UtcNow,
        };

    private void Setup(
        IEnumerable<Tenancy> tenancies,
        IEnumerable<Customer>? customers = null,
        IEnumerable<Property>? properties = null,
        IEnumerable<TenancyDocument>? documents = null,
        IEnumerable<TenancyFee>? fees = null)
    {
        var tenancyList = tenancies.ToList();

        _unitOfWork.Setup(u => u.TenancyQueries.GetAllAsync()).ReturnsAsync(tenancyList);

        foreach (var tenancy in tenancyList)
            _unitOfWork.Setup(u => u.TenancyQueries.GetByIdAsync(tenancy.Id)).ReturnsAsync(tenancy);

        _unitOfWork
            .Setup(u => u.CustomerQueries.GetManyByAsync(
                It.IsAny<Expression<Func<Customer, Guid>>>(), It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync((customers ?? []).ToList());

        _unitOfWork
            .Setup(u => u.PropertyQueries.GetManyByAsync(
                It.IsAny<Expression<Func<Property, Guid>>>(), It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync((properties ?? []).ToList());

        _unitOfWork
            .Setup(u => u.TenancyDocumentQueries.QueryByIndexAsync(TenancyIndex, It.IsAny<object>()))
            .ReturnsAsync((documents ?? []).ToList());

        _unitOfWork
            .Setup(u => u.TenancyFeeQueries.QueryByIndexAsync(TenancyIndex, It.IsAny<object>()))
            .ReturnsAsync((fees ?? []).ToList());
    }

    // ── the listing ──────────────────────────────────────────────

    /// <summary>
    /// A row showing two customer ids is a row somebody has to go and look up before
    /// they can do anything with it.
    /// </summary>
    [Fact]
    public async Task Listing_NamesBothParties()
    {
        var landlord = Person("Ada", "Obi", "ada@example.com");
        var tenant = Person("Bayo", "Eze", "bayo@example.com");
        var property = new Property { Id = Guid.NewGuid(), Title = "2 bed in Yaba" };

        Setup(
            [MakeTenancy(landlord.Id, tenant.Id, property.Id)],
            [landlord, tenant],
            [property]);

        var result = await _sut.GetTenanciesAsync(1, 20);
        var row = Assert.Single(result.Data.Items);

        Assert.Equal("Ada Obi", row.LandlordName);
        Assert.Equal("ada@example.com", row.LandlordEmail);
        Assert.Equal("Bayo Eze", row.TenantName);
        Assert.Equal("bayo@example.com", row.TenantEmail);
        Assert.Equal("2 bed in Yaba", row.PropertyTitle);
    }

    /// <summary>The total is the figure the tenant was shown — rent plus every fee.</summary>
    [Fact]
    public async Task Listing_TotalsRentAndFees()
    {
        var tenancy = MakeTenancy(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), rentKobo: 150_000_00);

        Setup([tenancy], fees:
        [
            new TenancyFee(tenancy.Id, "Agency fee", "10% of the annual rent", 15_000_00),
            new TenancyFee(tenancy.Id, "Caution deposit", "Refundable at the end", 50_000_00),
        ]);

        var result = await _sut.GetTenanciesAsync(1, 20);
        var row = Assert.Single(result.Data.Items);

        Assert.Equal(150_000_00, row.AgreedRentKobo);
        Assert.Equal(65_000_00, row.FeesKobo);
        Assert.Equal(215_000_00, row.TotalKobo);
    }

    /// <summary>
    /// The question staff actually have is which side the thing is stuck on, so the
    /// counts answer it directly rather than handing over a list to be totalled.
    /// </summary>
    [Fact]
    public async Task Listing_SaysWhichSideIsHoldingThingsUp()
    {
        var tenancy = MakeTenancy(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var waitingOnTenant = new TenancyDocument(
            tenancy.Id, "Employment letter", TenancyDocumentMode.Upload, false, null, null, null);

        var returnedToTenant = new TenancyDocument(
            tenancy.Id, "Reference", TenancyDocumentMode.Upload, false, null, null, null);
        returnedToTenant.TrySubmitFile("a.pdf");
        returnedToTenant.TryReject("Page 2 is missing");

        var waitingOnOwner = new TenancyDocument(
            tenancy.Id, "Tenancy agreement", TenancyDocumentMode.SignOffline, true, null, "source.pdf", Hash);
        waitingOnOwner.TrySubmitFile("signed.pdf");

        var done = new TenancyDocument(
            tenancy.Id, "Guarantor form", TenancyDocumentMode.Upload, false, null, null, null);
        done.TrySubmitFile("guarantor.pdf");
        done.TryAccept();

        Setup([tenancy], documents: [waitingOnTenant, returnedToTenant, waitingOnOwner, done]);

        var result = await _sut.GetTenanciesAsync(1, 20);
        var row = Assert.Single(result.Data.Items);

        Assert.Equal(4, row.DocumentCount);
        Assert.Equal(1, row.AcceptedDocumentCount);
        Assert.Equal(2, row.AwaitingTenantCount);
        Assert.Equal(1, row.AwaitingOwnerCount);
    }

    [Fact]
    public async Task Listing_FiltersByStatus()
    {
        Setup(
        [
            MakeTenancy(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenancyStatus.DocumentsRequested),
            MakeTenancy(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenancyStatus.Withdrawn),
            MakeTenancy(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TenancyStatus.DocumentsRequested),
        ]);

        var result = await _sut.GetTenanciesAsync(1, 20, TenancyStatus.DocumentsRequested);

        Assert.Equal(2, result.Data.Items.Count);
        Assert.All(result.Data.Items, t => Assert.Equal(TenancyStatus.DocumentsRequested, t.Status));
    }

    /// <summary>
    /// The count is of everything matching, not of the page. A pager that reports the
    /// page size has no second page to offer.
    /// </summary>
    [Fact]
    public async Task Listing_CountsEverythingMatching_NotJustThePage()
    {
        Setup(Enumerable.Range(0, 7)
            .Select(_ => MakeTenancy(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()))
            .ToList());

        var result = await _sut.GetTenanciesAsync(1, 3);

        Assert.Equal(3, result.Data.Items.Count);
        Assert.Equal(7, result.Data.TotalCount);
    }

    // ── the detail ───────────────────────────────────────────────

    /// <summary>
    /// If a tenant later denies signing, this is what answers it — so all of it has
    /// to be readable without a database console.
    /// </summary>
    [Fact]
    public async Task Detail_CarriesTheSignatureTrail()
    {
        var tenancy = MakeTenancy(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var agreement = new TenancyDocument(
            tenancy.Id, "Tenancy agreement", TenancyDocumentMode.SignInApp, true, null, "source.pdf", Hash);
        agreement.TrySign(Hash, "102.89.3.11", "Mozilla/5.0 (iPhone)");

        Setup([tenancy], documents: [agreement]);

        var result = await _sut.GetAsync(tenancy.Id);
        var document = Assert.Single(result.Data.Documents);

        Assert.NotNull(document.SignedAt);
        Assert.Equal("102.89.3.11", document.SignerIpAddress);
        Assert.Equal("Mozilla/5.0 (iPhone)", document.SignerUserAgent);
        Assert.Equal(Hash, document.SourceFileHash);
        Assert.Equal(Hash, document.SignedDocumentHash);
    }

    [Fact]
    public async Task Detail_ForSomethingThatDoesNotExist_Fails()
    {
        Setup([]);
        _unitOfWork.Setup(u => u.TenancyQueries.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Tenancy?)null);

        var result = await _sut.GetAsync(Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Data);
    }

    // ── document links ───────────────────────────────────────────

    [Fact]
    public async Task DocumentUrl_ForASubmittedFile_IsShortLived()
    {
        var tenancy = MakeTenancy(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var document = new TenancyDocument(
            tenancy.Id, "Employment letter", TenancyDocumentMode.Upload, false, null, null, null);
        document.TrySubmitFile("private/tenancies/x/submitted/letter.pdf");

        Setup([tenancy], documents: [document]);
        _fileStorage
            .Setup(f => f.GetPresignedUrlAsync("private/tenancies/x/submitted/letter.pdf", It.IsAny<TimeSpan>()))
            .ReturnsAsync("https://signed.example/letter.pdf");

        var result = await _sut.GetDocumentUrlAsync(tenancy.Id, document.Id, TenancyDocumentFile.Submitted);

        Assert.True(result.IsSuccessful);
        Assert.Equal("https://signed.example/letter.pdf", result.Data);
        _fileStorage.Verify(
            f => f.GetPresignedUrlAsync(It.IsAny<string>(), It.Is<TimeSpan>(t => t <= TimeSpan.FromMinutes(15))),
            Times.Once);
    }

    /// <summary>
    /// Nothing has been uploaded yet, so there is no file. Fails rather than handing
    /// back a link to nowhere that reads as a storage error when it is opened.
    /// </summary>
    [Fact]
    public async Task DocumentUrl_WhenNothingHasBeenSubmitted_Fails()
    {
        var tenancy = MakeTenancy(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var document = new TenancyDocument(
            tenancy.Id, "Employment letter", TenancyDocumentMode.Upload, false, null, null, null);

        Setup([tenancy], documents: [document]);

        var result = await _sut.GetDocumentUrlAsync(tenancy.Id, document.Id, TenancyDocumentFile.Submitted);

        Assert.False(result.IsSuccessful);
        _fileStorage.Verify(
            f => f.GetPresignedUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
    }
}
