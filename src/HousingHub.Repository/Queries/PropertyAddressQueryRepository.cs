using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class PropertyAddressQueryRepository : GenericQueryRepository<PropertyAddress>, IPropertyAddressQueryRepository
{
    public PropertyAddressQueryRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        
    }
}
