using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class PropertyFileCommandRepository : GenericCommandRepository<PropertyFile>, IPropertyFileCommandRepository
{
    public PropertyFileCommandRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        
    }
}
