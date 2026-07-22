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
    /// Queries the index for the key, then loads the item by its primary key.
    ///
    /// The second read is deliberate: a GSI only returns the attributes it
    /// projects, and auth needs fields such as PasswordHash that a KEYS_ONLY or
    /// INCLUDE projection would omit. Hydrating keeps this correct whatever the
    /// index projection is, and is still vastly cheaper than scanning. If the
    /// indexes are confirmed as projecting ALL, the load can be dropped.
    /// </summary>
    private async Task<Customer?> FindByIndexAsync(string indexName, string hashKeyValue)
    {
        if (string.IsNullOrWhiteSpace(hashKeyValue)) return null;

        var matches = await QueryByIndexAsync(indexName, hashKeyValue);
        var match = matches.FirstOrDefault();

        return match == null ? null : await GetByIdAsync(match.Id);
    }
}
