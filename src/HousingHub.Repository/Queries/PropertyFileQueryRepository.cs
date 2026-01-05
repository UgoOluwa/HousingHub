using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class PropertyFileQueryRepository : GenericQueryRepository<PropertyFile>, IPropertyFileQueryRepository
{
    public PropertyFileQueryRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        
    }
}
