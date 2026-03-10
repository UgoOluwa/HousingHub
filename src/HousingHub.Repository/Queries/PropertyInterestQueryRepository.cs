using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class PropertyInspectionQueryRepository : GenericQueryRepository<PropertyInspection>, IPropertyInspectionQueryRepository
{
    public PropertyInspectionQueryRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        
    }
}
