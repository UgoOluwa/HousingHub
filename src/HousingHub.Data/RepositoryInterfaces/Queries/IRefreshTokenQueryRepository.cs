using HousingHub.Model.Entities;

namespace HousingHub.Data.RepositoryInterfaces.Queries;

public interface IRefreshTokenQueryRepository : IGenericQueryRepository<RefreshToken>
{
    /// <summary>Index-backed lookup by the token's SHA-256 hash.</summary>
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash);

    /// <summary>All non-revoked tokens for a customer, used to kill every session on theft detection.</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveByCustomerIdAsync(Guid customerId);
}
