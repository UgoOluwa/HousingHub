using Amazon.DynamoDBv2.DataModel;
using HousingHub.Data.RepositoryInterfaces.Commands;
using HousingHub.Model.Entities;

namespace HousingHub.Repository.Commands;

public class PropertyAddressCommandRepository : GenericCommandRepository<PropertyAddress>, IPropertyAddressCommandRepository
{
    public PropertyAddressCommandRepository(IDynamoDBContext context)
        : base(context)
    {

    }
}
