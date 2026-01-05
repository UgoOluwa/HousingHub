using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class CustomerAddressCommandRepository : GenericCommandRepository<CustomerAddress>, ICustomerAddressCommandRepository
{
    public CustomerAddressCommandRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        
    }
}
