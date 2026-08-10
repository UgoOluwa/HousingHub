using HousingHub.Model.Entities;
using HousingHub.Repository.Queries;
// Several sibling namespaces under HousingHub.Test share a simple name with an
// entity class (HousingHub.Test.PropertyFile, .PropertyAddress, .CustomerAddress,
// .Admin) — C#'s sibling-namespace lookup resolves the bare identifier to the
// namespace instead of the HousingHub.Model.Entities type, so these need aliasing.
using PropertyFileEntity = HousingHub.Model.Entities.PropertyFile;
using PropertyAddressEntity = HousingHub.Model.Entities.PropertyAddress;
using CustomerAddressEntity = HousingHub.Model.Entities.CustomerAddress;
using AdminEntity = HousingHub.Model.Entities.Admin;

namespace HousingHub.Test.Repository;

/// <summary>
/// Locks down how the repository decides an entity's primary key and indexes.
/// </summary>
/// <remarks>
/// These exist because of a specific bug. <c>DynamoDBGlobalSecondaryIndexHashKeyAttribute</c>
/// derives from <c>DynamoDBHashKeyAttribute</c>, so a naive
/// <c>GetCustomAttribute&lt;DynamoDBHashKeyAttribute&gt;()</c> matches GSI properties
/// as well. Because the real key (<c>Id</c>) is inherited from <c>BaseEntity</c> and
/// GSI properties are declared on the derived type — which reflection returns first —
/// every entity resolved its "hash key" to the wrong property.
///
/// It failed silently rather than loudly: a Guid-typed GSI property produced a
/// GetItem against the wrong key and returned nothing, which would have emptied
/// notifications, chat history, property files and addresses across the app.
/// </remarks>
public class DynamoIndexMapTests
{
    // Deliberately covers entities that declare a Guid-typed GSI (the silent-failure
    // shape) and a string-typed one (the throwing shape).

    [Fact]
    public void HashKey_IsAlwaysId_NotAGlobalSecondaryIndexProperty()
    {
        Assert.Equal("Id", DynamoIndexMap<Customer>.HashKey?.Name);
        Assert.Equal("Id", DynamoIndexMap<Property>.HashKey?.Name);
        Assert.Equal("Id", DynamoIndexMap<Notification>.HashKey?.Name);
        Assert.Equal("Id", DynamoIndexMap<ChatMessage>.HashKey?.Name);
        Assert.Equal("Id", DynamoIndexMap<PropertyFileEntity>.HashKey?.Name);
        Assert.Equal("Id", DynamoIndexMap<PropertyAddressEntity>.HashKey?.Name);
        Assert.Equal("Id", DynamoIndexMap<CustomerAddressEntity>.HashKey?.Name);
        Assert.Equal("Id", DynamoIndexMap<RefreshToken>.HashKey?.Name);
        Assert.Equal("Id", DynamoIndexMap<AdminEntity>.HashKey?.Name);
    }

    [Fact]
    public void HashKey_IsGuidTyped_SoGetItemCannotBeIssuedWithTheWrongKeyType()
    {
        // The string-typed GSI properties (Email, TokenHash) would throw on LoadAsync;
        // the Guid-typed ones would silently return nothing. Both are excluded by
        // asserting the resolved key is the Guid Id.
        Assert.Equal(typeof(Guid), DynamoIndexMap<Customer>.HashKey?.PropertyType);
        Assert.Equal(typeof(Guid), DynamoIndexMap<Notification>.HashKey?.PropertyType);
    }

    [Fact]
    public void GlobalIndexes_AreDiscoveredFromTheEntityAttributes()
    {
        Assert.Equal("Email-index", DynamoIndexMap<Customer>.GlobalIndexes["Email"]);
        Assert.Equal("PhoneNumber-index", DynamoIndexMap<Customer>.GlobalIndexes["PhoneNumber"]);
        Assert.Equal("OwnerId-index", DynamoIndexMap<Property>.GlobalIndexes["OwnerId"]);
        Assert.Equal("RecipientId-index", DynamoIndexMap<Notification>.GlobalIndexes["RecipientId"]);
        Assert.Equal("ConversationId-index", DynamoIndexMap<ChatMessage>.GlobalIndexes["ConversationId"]);
    }

    [Fact]
    public void GlobalIndexes_DoesNotContainThePrimaryKey()
    {
        // Id is the table key, not a GSI. Listing it here would send a Query to a
        // non-existent "Id-index".
        Assert.False(DynamoIndexMap<Customer>.GlobalIndexes.ContainsKey("Id"));
        Assert.False(DynamoIndexMap<Property>.GlobalIndexes.ContainsKey("Id"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsQueryableKeyValue_RejectsAbsentStrings(string? value)
    {
        // Querying on these either errors or quietly matches nothing, so the caller
        // must fall through to a scan instead.
        Assert.False(DynamoIndexMap<Customer>.IsQueryableKeyValue(value));
    }

    [Fact]
    public void IsQueryableKeyValue_RejectsEmptyGuidAndNonKeyTypes()
    {
        Assert.False(DynamoIndexMap<Customer>.IsQueryableKeyValue(Guid.Empty));
        Assert.False(DynamoIndexMap<Customer>.IsQueryableKeyValue(true));
        Assert.False(DynamoIndexMap<Customer>.IsQueryableKeyValue(DateTime.UtcNow));
    }

    [Fact]
    public void IsQueryableKeyValue_AcceptsRealKeys()
    {
        Assert.True(DynamoIndexMap<Customer>.IsQueryableKeyValue(Guid.NewGuid()));
        Assert.True(DynamoIndexMap<Customer>.IsQueryableKeyValue("john@test.com"));
    }
}
