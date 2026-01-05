using HousingHub.Data.Contexts;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class PropertyInterestCommandRepository : GenericCommandRepository<PropertyInterest>, IPropertyInterestCommandRepository
{
    public PropertyInterestCommandRepository(AppDbContext dbContext)
        : base(dbContext)
    {
        
    }
}
