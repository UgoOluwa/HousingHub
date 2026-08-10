using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class CustomerQueryRepository : GenericQueryRepository<Customer>, ICustomerQueryRepository
{
    private const string EmailIndex = "Email-index";
    private const string PhoneNumberIndex = "PhoneNumber-index";

    public CustomerQueryRepository(IDynamoDBContext context)
        : base(context)
    {

    }

    public Task<Customer?> GetByEmailAsync(string email) =>
        FindByIndexAsync(EmailIndex, email);

    public Task<Customer?> GetByPhoneNumberAsync(string phoneNumber) =>
        FindByIndexAsync(PhoneNumberIndex, phoneNumber);

    public async Task<Customer?> GetByEmailOrPhoneAsync(string emailOrPhone)
    {
        if (string.IsNullOrWhiteSpace(emailOrPhone)) return null;

        // Two indexed reads still cost far less than one full-table scan.
        return await GetByEmailAsync(emailOrPhone)
            ?? await GetByPhoneNumberAsync(emailOrPhone);
    }

    /// <summary>
    /// Queries the index for the key.
    /// </summary>
    /// <remarks>
    /// This used to follow up with a GetItem to hydrate the row, guarding against a
    /// KEYS_ONLY or INCLUDE projection omitting fields auth needs such as
    /// PasswordHash. Every GSI is created with <c>ProjectionType.ALL</c>
    /// (see <c>DynamoDbTableInitializer.CreateGsi</c>), so the query already returns
    /// the complete item and the second read was pure overhead — on the login path,
    /// which is the hottest read in the system.
    ///
    /// If a future index is ever created with a narrower projection, restore the
    /// hydrating load for it.
    /// </remarks>
    private async Task<Customer?> FindByIndexAsync(string indexName, string hashKeyValue)
    {
        if (string.IsNullOrWhiteSpace(hashKeyValue)) return null;

        var matches = await QueryByIndexAsync(indexName, hashKeyValue);
        return matches.FirstOrDefault();
    }
}
