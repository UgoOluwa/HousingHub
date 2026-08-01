using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class RefreshTokenQueryRepository : GenericQueryRepository<RefreshToken>, IRefreshTokenQueryRepository
{
    private const string TokenHashIndex = "TokenHash-index";
    private const string CustomerIdIndex = "CustomerId-index";

    public RefreshTokenQueryRepository(IDynamoDBContext context)
        : base(context)
    {
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash)
    {
        if (string.IsNullOrWhiteSpace(tokenHash)) return null;

        var matches = await QueryByIndexAsync(TokenHashIndex, tokenHash);
        return matches.FirstOrDefault();
    }

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByCustomerIdAsync(Guid customerId)
    {
        var matches = await QueryByIndexAsync(CustomerIdIndex, customerId);
        return matches.Where(t => !t.IsRevoked).ToList();
    }
}
