using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class PropertyAddressCommandRepository : GenericCommandRepository<PropertyAddress>, IPropertyAddressCommandRepository
{
    public PropertyAddressCommandRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        
    }
}
