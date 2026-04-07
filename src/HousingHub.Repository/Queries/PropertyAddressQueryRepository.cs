using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Queries;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Queries;

public class PropertyAddressQueryRepository : GenericQueryRepository<PropertyAddress>, IPropertyAddressQueryRepository
{
    public PropertyAddressQueryRepository(IDynamoDBContext context)
        : base(context)
    {

    }
}
