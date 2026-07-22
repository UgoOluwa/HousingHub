using HousingHub.Model.Entities;

namespace HousingHub.Data.RepositoryInterfaces.Queries;

public interface ICustomerQueryRepository : IGenericQueryRepository<Customer>
{
    /// <summary>Index-backed lookup returning the complete customer record.</summary>
    Task<Customer?> GetByEmailAsync(string email);

    /// <summary>Index-backed lookup returning the complete customer record.</summary>
    Task<Customer?> GetByPhoneNumberAsync(string phoneNumber);

    /// <summary>Matches either identifier, as used by the login form.</summary>
    Task<Customer?> GetByEmailOrPhoneAsync(string emailOrPhone);
}
