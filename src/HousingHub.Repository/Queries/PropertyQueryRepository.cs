using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class PropertyQueryRepository : GenericQueryRepository<Property>, IPropertyQueryRepository
{
    public PropertyQueryRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        
    }
}
