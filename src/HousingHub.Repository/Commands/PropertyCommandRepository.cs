using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class PropertyCommandRepository : GenericCommandRepository<Property>, IPropertyCommandRepository
{
    public PropertyCommandRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        
    }
}
