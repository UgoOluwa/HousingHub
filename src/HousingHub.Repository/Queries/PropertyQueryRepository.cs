using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class PropertyQueryRepository : GenericQueryRepository<Property>, IPropertyQueryRepository
{
    public PropertyQueryRepository(IDynamoDBContext context)
        : base(context)
    {

    }
}
