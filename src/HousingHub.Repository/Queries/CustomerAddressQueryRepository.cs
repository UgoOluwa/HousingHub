using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class CustomerAddressQueryRepository : GenericQueryRepository<CustomerAddress>, ICustomerAddressQueryRepository
{
    public CustomerAddressQueryRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        
    }
}
