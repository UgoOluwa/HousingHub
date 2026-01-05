using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class PropertyInterestQueryRepository : GenericQueryRepository<PropertyInterest>, IPropertyInterestQueryRepository
{
    public PropertyInterestQueryRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        
    }
}
