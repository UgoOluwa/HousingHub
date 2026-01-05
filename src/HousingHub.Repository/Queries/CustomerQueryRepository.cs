using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class CustomerQueryRepository : GenericQueryRepository<Customer>, ICustomerQueryRepository
{
    public CustomerQueryRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        
    }
}
