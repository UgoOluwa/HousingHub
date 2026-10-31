using HousingHub.Model.Entities;
using HousingHub.Service.Commons.Mappings;
using HousingHub.Service.Dtos.Inspection;
using Mapster;

namespace HousingHub.Test.Inspection;

/// <summary>
/// Mapping an inspection must not reach through its navigation properties.
/// </summary>
/// <remarks>
/// <c>Property</c> and <c>Customer</c> are <c>[DynamoDBIgnore]</c> stubs declared
/// non-nullable, so they are null on every row read back from Dynamo. Left to
/// itself Mapster flattens an unmatched destination member through them —
/// <c>PropertyOwnerId</c> becomes <c>Property.OwnerId</c> — and throws.
/// <para>
/// Which members flatten depends on what else is declared on either type, so this
/// breaks when a field is added at either end and nothing else fails to say so: the
/// service's catch swallows the exception and reports the inspection as not found.
/// </para>
/// </remarks>
public class InspectionMappingTests
{
    private static IMapper Mapper()
    {
        var config = new TypeAdapterConfig();
        new InspectionMapper().Register(config);
        return new ObjectMapper(config);
    }

    private static PropertyInspection Row() =>
        new(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), TimeSpan.FromHours(10), "note");

    [Fact]
    public void MappingARowWithNoNavigationProperties_DoesNotThrow()
    {
        var dto = Mapper().Map<InspectionDto>(Row());

        Assert.NotNull(dto);
    }

    /// <summary>
    /// The derived members are the query service's to fill in, and it only attaches
    /// what the caller is entitled to. A mapper quietly resolving them would hand
    /// them out on paths that never meant to.
    /// </summary>
    [Fact]
    public void TheDerivedMembers_ComeBackEmpty()
    {
        var dto = Mapper().Map<InspectionDto>(Row());

        Assert.Null(dto.PropertyOwnerId);
        Assert.Null(dto.PropertyImageUrl);
        Assert.Null(dto.CustomerName);
        Assert.Null(dto.CustomerLocation);
        Assert.Null(dto.PropertyOwnerName);
        Assert.Null(dto.AssignedStaffName);
    }

    [Fact]
    public void TheRowsOwnFields_AreCarriedAcross()
    {
        var row = Row();
        row.Status = HousingHub.Model.Enums.InspectionStatus.Confirmed;
        Assert.True(row.TryComplete("The gate was locked, I saw it from outside."));

        var dto = Mapper().Map<InspectionDto>(row);

        Assert.Equal(row.Id, dto.Id);
        Assert.Equal(row.CustomerId, dto.CustomerId);
        Assert.Equal(row.PropertyId, dto.PropertyId);
        Assert.Equal(row.Status, dto.Status);
        Assert.Equal("The gate was locked, I saw it from outside.", dto.CompletionComment);
        Assert.NotNull(dto.CompletedAt);
    }
}
