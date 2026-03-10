using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class PropertyInspectionCommandRepository : GenericCommandRepository<PropertyInspection>, IPropertyInspectionCommandRepository
{
    public PropertyInspectionCommandRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        
    }
}
