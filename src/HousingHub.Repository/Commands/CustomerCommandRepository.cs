using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class CustomerCommandRepository : GenericCommandRepository<Customer>, ICustomerCommandRepository
{
    public CustomerCommandRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        
    }
}
